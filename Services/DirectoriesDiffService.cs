using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace IDABatchToolWinUI.Services;

/// <summary>Модуль модульного сравнения: подпапка OLD/NEW (одна из сторон может отсутствовать -
/// тогда модуль «удалён целиком» / «новый»). SizeBytes - оценка объёма работы (байты файлов).</summary>
public sealed record ModuleInfo(string Name, string? OldPath, string? NewPath, long SizeBytes);

/// <summary>Итог сравнения одного модуля. Kind: modified | added | deleted | unchanged | error.
/// Для unchanged папка модуля не создаётся, но Stats сохраняются (в т.ч. счётчик двоичных).</summary>
public sealed record ModuleOutcome(string Module, string Kind, DirsDiffStats? Stats, string? Error);

/// <summary>Итог модульного прогона: результаты по модулям (отсортированы по имени) и параметры.</summary>
public sealed record ModuleRunResult(IReadOnlyList<ModuleOutcome> Outcomes, int Workers, int TotalModules);

/// <summary>Итог текстового диффа двух директорий.</summary>
public sealed record DirsDiffStats(
    int Modified, int Added, int Deleted, int Binary, int Errors, int Unchanged,
    int TotalFiles, string OutPath, string LogPath, long SizeBytes,
    IReadOnlyList<string> ErrorDetails, IReadOnlyList<string> BinaryFiles)
{
    public bool HasErrors => Errors > 0;
}

/// <summary>
/// Текстовый дифф двух папок с исходниками - нативный порт diff_gui.py
/// (исполнение 1): рекурсивный обход с glob-исключениями, sniff бинарности
/// по нулевому байту, нормализация EOL и единый unified diff в стиле
/// «git diff --no-index --no-prefix» в один файл + журнал &lt;файл&gt;.log.
/// Внутренний алгоритм - Myers O(ND): расстояние редактирования выше
/// MaxEditDistance (почти полностью переписанный огромный файл) отдаётся
/// одним блоком замены - вывод остаётся корректным unified diff.
/// </summary>
public static class DirectoriesDiffService
{
    public const string DefaultExcludes =
        ".git, .svn, __pycache__, *.pyc, .venv, venv, node_modules, .idea, .vscode, dist, build, *.log";

    /// <summary>Контекст по умолчанию: 3 неизменённые строки вокруг изменений (unified diff).</summary>
    public const int DefaultContext = 3;

    /// <summary>
    /// «Полный дифф»: контекст, заведомо накрывающий файл целиком - в дифф
    /// пишутся все строки. Верхний предел подобран так, чтобы 2*n в AppendHunks
    /// не переполнялся; реальных файлов длиннее не бывает.
    /// </summary>
    public const int FullContext = int.MaxValue / 4;

    private const int BinarySniffBytes = 8192;
    private const int MaxEditDistance = 2048;

    private static readonly ConcurrentDictionary<string, System.Text.RegularExpressions.Regex>
        GlobCache = new();

    /// <summary>Список исключений «a, *.b, c» → очищенные шаблоны в нижнем регистре.</summary>
    public static List<string> ParsePatterns(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .Select(p => p.ToLowerInvariant())
           .ToList();

    /// <summary>
    /// Сравнивает две директории и пишет единый дифф. Бросает
    /// OperationCanceledException при отмене. Лог-колбэк получает готовые
    /// строки (они же пишутся в &lt;outPath&gt;.log), прогресс - (cur, total, rel).
    /// При skipBinary файлы, распознанные как двоичные, не попадают в дифф
    /// (учитываются только в статистике и журнале) - остаются текстовые файлы.
    /// </summary>
    public static DirsDiffStats Run(
        string oldDir, string newDir, string outPath, int context,
        IReadOnlyList<string> patterns,
        Action<string>? log = null,
        Action<int, int, string>? progress = null,
        CancellationToken ct = default,
        bool skipBinary = false)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var logLines = new List<string>();

        void L(string level, string msg)
        {
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {msg}";
            logLines.Add(line);
            log?.Invoke(line);
        }

        L("INFO", "Старт генерации диффа");
        L("INFO", $"OLD: {oldDir}");
        L("INFO", $"NEW: {newDir}");
        L("INFO", $"Результат: {outPath}");
        L("INFO", "Исключения: " + (patterns.Count > 0 ? string.Join(", ", patterns) : "(нет)"));
        L("INFO", IsFullContext(context) ? "Полный дифф (все строки файлов)" : $"Строк контекста: {context}");

        // Предварительная проверка: если папки совпадают побайтово, пофайловое
        // сравнение не запускается - все файлы сразу помечаются без изменений
        L("INFO", "Проверка контрольных сумм OLD/NEW...");
        var identical = FoldersIdentical(oldDir, newDir, patterns, L, ct);
        if (identical)
            L("INFO", "Контрольные суммы совпали - содержимое идентично, пофайловое сравнение пропущено");

        L("INFO", $"Обход папки OLD: {oldDir}");
        var (oldFiles, oldExcluded) = Collect(oldDir, patterns, L, ct);
        L("INFO", $"OLD: найдено файлов {oldFiles.Count}, пропущено по исключениям {oldExcluded}");

        L("INFO", $"Обход папки NEW: {newDir}");
        var (newFiles, newExcluded) = Collect(newDir, patterns, L, ct);
        L("INFO", $"NEW: найдено файлов {newFiles.Count}, пропущено по исключениям {newExcluded}");

        var modified = new List<string>();
        var added = new List<string>();
        var deleted = new List<string>();
        var binary = new List<string>();
        var errors = new List<string>();
        int unchanged = 0;

        var rels = oldFiles.Keys.Union(newFiles.Keys).OrderBy(k => k, StringComparer.Ordinal).ToList();
        int total = rels.Count;
        if (total == 0) L("WARNING", "В папках не найдено ни одного файла для сравнения!");
        L("INFO", identical
            ? $"Построчное сравнение {total} файлов не требуется (все без изменений)"
            : IsFullContext(context)
                ? $"Сравнение {total} файлов (все строки файлов)"
                : $"Сравнение {total} файлов (контекст: {context} строк)");

        var chunks = new List<string>();
        if (identical)
        {
            unchanged = total;
        }
        else
        {
            for (int idx = 0; idx < total; idx++)
            {
                ct.ThrowIfCancellationRequested();
                var rel = rels[idx];
                var prefix = $"[{idx + 1}/{total}]";
                try
                {
                    var (kind, chunk) = ComparePair(rel,
                        oldFiles.GetValueOrDefault(rel), newFiles.GetValueOrDefault(rel), context);
                    if (kind == "same") { unchanged++; continue; }
                    switch (kind)
                    {
                        case "modified": modified.Add(rel); break;
                        case "added": added.Add(rel); break;
                        case "deleted": deleted.Add(rel); break;
                        case "binary": binary.Add(rel); break;
                    }
                    // Двоичные файлы не имеют построчного содержимого: при skipBinary
                    // в дифф они не пишутся (информация остаётся в статистике и журнале)
                    var omitted = kind == "binary" && skipBinary;
                    if (chunk != null && !omitted) chunks.Add(chunk);
                    L("INFO", $"{prefix} {rel} - {KindLabel(kind)}{(omitted ? " (пропущен: только исходные коды)" : "")}");
                }
                catch (Exception exc)
                {
                    L("ERROR", $"{prefix} {rel} - ОШИБКА ОБРАБОТКИ: {exc.Message}");
                    errors.Add($"{rel}: {exc.Message}");
                }
                progress?.Invoke(idx + 1, total, rel);
            }
        }

        var sb = new StringBuilder();
        sb.Append(string.Join("\n\n", chunks));
        if (chunks.Count > 0) sb.Append('\n');
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));

        var logPath = outPath + ".log";
        L("INFO", $"Дифф записан: {outPath} ({new FileInfo(outPath).Length / 1024f:F1} КБ), " +
                  $"время: {started.Elapsed.TotalSeconds:F1} с");
        File.WriteAllLines(logPath, logLines, new UTF8Encoding(false));

        return new DirsDiffStats(
            modified.Count, added.Count, deleted.Count, binary.Count, errors.Count, unchanged,
            total, outPath, logPath, new FileInfo(outPath).Length, errors, binary);
    }

    /// <summary>Итоговая сводка для диалога и журнала (аналог summarize()).</summary>
    public static string[] Summarize(DirsDiffStats s)
    {
        var lines = new List<string>
        {
            $"Изменённых файлов: {s.Modified}",
            $"Новых файлов:      {s.Added}",
            $"Удалённых файлов:  {s.Deleted}",
            $"Ошибок:            {s.Errors}",
            $"Файл диффа: {s.OutPath} ({s.SizeBytes / 1024f:F1} КБ)",
            $"Журнал: {s.LogPath}",
        };
        if (s.ErrorDetails.Count > 0)
        {
            lines.Add("Ошибки (подробности - в журнале):");
            lines.AddRange(s.ErrorDetails.Take(10).Select(e => "  - " + e));
        }
        return lines.ToArray();
    }

    private static string KindLabel(string kind) => kind switch
    {
        "modified" => "изменён",
        "added" => "новый файл",
        "deleted" => "удалён",
        "binary" => "двоичный",
        _ => kind,
    };

    // ───────────────────── обход директорий ─────────────────────

    private static (Dictionary<string, string> files, int excluded) Collect(
        string root, IReadOnlyList<string> patterns, Action<string, string> log, CancellationToken ct)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        int excluded = 0;

        void Walk(string dir, string relPrefix)
        {
            ct.ThrowIfCancellationRequested();
            string[] subDirs;
            try { subDirs = Directory.GetDirectories(dir); }
            catch (Exception e)
            {
                log("WARNING", $"Обход: нет доступа к «{dir}»: {e.Message}");
                return;
            }
            foreach (var d in subDirs)
            {
                var name = Path.GetFileName(d);
                var rel = relPrefix.Length == 0 ? name : relPrefix + "/" + name;
                if (IsExcluded(rel, patterns)) { excluded++; continue; }
                Walk(d, rel);
            }

            string[] fs;
            try { fs = Directory.GetFiles(dir); }
            catch (Exception e)
            {
                log("WARNING", $"Обход: нет доступа к «{dir}»: {e.Message}");
                return;
            }
            foreach (var f in fs)
            {
                var name = Path.GetFileName(f);
                var rel = relPrefix.Length == 0 ? name : relPrefix + "/" + name;
                if (IsExcluded(rel, patterns)) { excluded++; continue; }
                files[rel] = f;
            }
        }

        Walk(root, "");
        return (files, excluded);
    }

    /// <summary>
    /// Предварительная проверка «папки идентичны»: сопоставляются наборы
    /// относительных путей (с учётом исключений), затем размеры и содержимое
    /// файлов. Совпадение гарантирует пустой результат диффа - пофайловое
    /// сравнение можно пропустить; расхождение - обычный путь. Папка, отсутствующая
    /// с обеих сторон, считается совпадением (сравнивать нечего); только с одной -
    /// расхождением (без лишних предупреждений от Collect о недоступных путях).
    /// </summary>
    private static bool FoldersIdentical(
        string oldDir, string newDir, IReadOnlyList<string> patterns,
        Action<string, string> log, CancellationToken ct)
    {
        bool hasOld = Directory.Exists(oldDir), hasNew = Directory.Exists(newDir);
        if (hasOld != hasNew) return false;
        if (!hasOld) return true;

        var (oldFiles, _) = Collect(oldDir, patterns, log, ct);
        var (newFiles, _) = Collect(newDir, patterns, log, ct);
        if (oldFiles.Count != newFiles.Count ||
            !oldFiles.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(newFiles.Keys))
            return false;

        var rels = oldFiles.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        foreach (var rel in rels)
        {
            ct.ThrowIfCancellationRequested();
            if (new FileInfo(oldFiles[rel]).Length != new FileInfo(newFiles[rel]).Length)
                return false;
        }
        foreach (var rel in rels)
        {
            ct.ThrowIfCancellationRequested();
            if (!FileEquals(oldFiles[rel], newFiles[rel])) return false;
        }
        return true;
    }

    /// <summary>Побайтовое равенство двух файлов (потоковое, без загрузки в память целиком).</summary>
    private static bool FileEquals(string pathA, string pathB)
    {
        const int BufSize = 1 << 16;
        using var fa = File.OpenRead(pathA);
        using var fb = File.OpenRead(pathB);
        if (fa.Length != fb.Length) return false;
        var bufA = new byte[BufSize];
        var bufB = new byte[BufSize];
        int readA;
        while ((readA = ReadFull(fa, bufA)) > 0)
        {
            int readB = ReadFull(fb, bufB);
            if (readA != readB || !bufA.AsSpan(0, readA).SequenceEqual(bufB.AsSpan(0, readB)))
                return false;
        }
        return true;
    }

    /// <summary>Читает в буфер до его заполнения или конца потока (частичные Read склеиваются).</summary>
    private static int ReadFull(FileStream stream, byte[] buffer)
    {
        int total = 0, read;
        while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
            total += read;
        return total;
    }

    /// <summary>Шаблон совпадает с любым компонентом пути или с путём целиком (fnmatch).</summary>
    private static bool IsExcluded(string rel, IReadOnlyList<string> patterns)
    {
        if (patterns.Count == 0) return false;
        var lower = rel.ToLowerInvariant();
        var parts = lower.Split('/');
        foreach (var pat in patterns)
        {
            if (GlobMatch(pat, lower)) return true;
            foreach (var part in parts)
                if (GlobMatch(pat, part)) return true;
        }
        return false;
    }

    private static bool GlobMatch(string pattern, string text) =>
        GlobCache.GetOrAdd(pattern, p => new System.Text.RegularExpressions.Regex(
            "^" + System.Text.RegularExpressions.Regex.Escape(p)
                .Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
            System.Text.RegularExpressions.RegexOptions.Compiled))
        .IsMatch(text);

    // ───────────────────── сравнение пары файлов ─────────────────────

    private static (string kind, string? chunk) ComparePair(
        string rel, string? oldPath, string? newPath, int context)
    {
        var oldLabel = "old/" + rel;
        var newLabel = "new/" + rel;
        var head = $"diff --git {oldLabel} {newLabel}";

        if (newPath == null)
        {
            var (_, lines) = LoadFile(oldPath!);
            var body = UnifiedDiff(lines, Array.Empty<string>(), oldLabel, "/dev/null", context);
            return ("deleted", string.Join("\n",
                new[] { head, "deleted file mode 100644" }.Concat(body)));
        }

        if (oldPath == null)
        {
            var (isBin, lines) = LoadFile(newPath);
            if (isBin)
                return ("binary", $"{head}\nBinary files {oldLabel} and {newLabel} differ");
            var body = UnifiedDiff(Array.Empty<string>(), lines, "/dev/null", newLabel, context);
            return ("added", string.Join("\n",
                new[] { head, "new file mode 100644" }.Concat(body)));
        }

        var (binA, linesA) = LoadFile(oldPath);
        var (binB, linesB) = LoadFile(newPath);
        if (binA || binB)
            return ("binary", $"{head}\nBinary files {oldLabel} and {newLabel} differ");
        if (linesA.SequenceEqual(linesB))
            return ("same", null);
        var body2 = UnifiedDiff(linesA, linesB, oldLabel, newLabel, context);
        return ("modified", string.Join("\n", new[] { head }.Concat(body2)));
    }

    private static (bool isBinary, string[] lines) LoadFile(string path)
    {
        var raw = File.ReadAllBytes(path);
        var sniff = Math.Min(raw.Length, BinarySniffBytes);
        for (int i = 0; i < sniff; i++)
            if (raw[i] == 0) return (true, Array.Empty<string>());

        // utf-8-sig: BOM съедается; невалидные байты → U+FFFD (как errors="replace")
        var start = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF ? 3 : 0;
        var text = Encoding.UTF8.GetString(raw, start, raw.Length - start);
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        return (false, SplitLines(text));
    }

    private static string[] SplitLines(string text)
    {
        if (text.Length == 0) return Array.Empty<string>();
        var parts = text.Split('\n');
        if (parts[^1].Length == 0) return parts[..^1]; // как str.splitlines()
        return parts;
    }

    // ───────────────────── unified diff ─────────────────────

    /// <summary>
    /// Unified diff двух последовательностей строк: «--- a» / «+++ b» + ханки.
    /// Пустой список, если последовательности равны (как difflib.unified_diff).
    /// </summary>
    private static List<string> UnifiedDiff(string[] a, string[] b, string aLabel, string bLabel, int n)
    {
        var ops = BuildOps(a, b);
        var result = new List<string>();
        if (!ops.Any(o => o.Kind != OpEqual)) return result;
        result.Add("--- " + aLabel);
        result.Add("+++ " + bLabel);
        AppendHunks(result, ops, a, b, n);
        return result;
    }

    private const byte OpEqual = 0, OpDelete = 1, OpInsert = 2;

    /// <summary>Операции над выровненными строками: общие префикс/суффикс + Myers в середине.</summary>
    private static List<(byte Kind, int A, int B)> BuildOps(string[] a, string[] b)
    {
        var ops = new List<(byte, int, int)>(a.Length + b.Length);

        int p = 0;
        while (p < a.Length && p < b.Length && a[p] == b[p]) p++;
        int tailA = a.Length, tailB = b.Length;
        while (tailA > p && tailB > p && a[tailA - 1] == b[tailB - 1]) { tailA--; tailB--; }

        for (int i = 0; i < p; i++) ops.Add((OpEqual, i, i));

        int midA = tailA - p, midB = tailB - p;
        if (midA > 0 || midB > 0)
        {
            // Интернирование строк середины → int-массивы для Myers.
            var map = new Dictionary<string, int>(midA + midB);
            int[] ia = MapSlice(a, p, midA), ib = MapSlice(b, p, midB);

            int[] MapSlice(string[] src, int off, int len)
            {
                var res = new int[len];
                for (int i = 0; i < len; i++)
                {
                    if (!map.TryGetValue(src[off + i], out var id)) map[src[off + i]] = id = map.Count;
                    res[i] = id;
                }
                return res;
            }

            var middle = MyersDiff(ia, ib, MaxEditDistance);
            if (middle != null)
            {
                // Для eq значимы оба индекса; у del не читается B, у insert - A.
                foreach (var (kind, ai, bi) in middle)
                    ops.Add((kind, p + ai, p + bi));
            }
            else
            {
                // Расстояние выше лимита: середина целиком как блок замены.
                for (int i = 0; i < midA; i++) ops.Add((OpDelete, p + i, p));
                for (int i = 0; i < midB; i++) ops.Add((OpInsert, tailA, p + i));
            }
        }

        for (int i = tailA; i < a.Length; i++) ops.Add((OpEqual, i, tailB + i - tailA));
        return ops;
    }

    /// <summary>
    /// Myers O(ND) с трассировкой. Возвращает операции по индексам середины
    /// (a-индекс для eq/del, b-индекс для insert) или null, если расстояние
    /// редактирования превысило maxD.
    /// </summary>
    private static List<(byte Kind, int A, int B)>? MyersDiff(int[] a, int[] b, int maxD)
    {
        int n = a.Length, m = b.Length, off = maxD;
        var v = new int[2 * maxD + 1];
        var trace = new List<int[]>(maxD + 1) { (int[])v.Clone() };
        v[off + 1] = 0;

        for (int d = 0; d <= maxD; d++)
        {
            for (int k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && v[off + k - 1] < v[off + k + 1]))
                    x = v[off + k + 1];          // вниз: вставка из b
                else
                    x = v[off + k - 1] + 1;      // вправо: удаление из a
                int y = x - k;
                while (x < n && y < m && a[x] == b[y]) { x++; y++; }
                v[off + k] = x;
                if (x >= n && y >= m)
                {
                    trace.Add((int[])v.Clone());
                    return Backtrack(trace, a, b, off, d);
                }
            }
            trace.Add((int[])v.Clone());
        }
        return null;
    }

    private static List<(byte Kind, int A, int B)> Backtrack(
        List<int[]> trace, int[] a, int[] b, int off, int finalD)
    {
        int x = a.Length, y = b.Length;
        var ops = new List<(byte, int, int)>();

        for (int d = finalD; d >= 0; d--)
        {
            var v = trace[d];
            int k = x - y;
            int prevK = k == -d || (k != d && v[off + k - 1] < v[off + k + 1]) ? k + 1 : k - 1;
            int prevX = v[off + prevK];
            int prevY = prevX - prevK;

            while (x > prevX && y > prevY) { x--; y--; ops.Add((OpEqual, x, y)); }
            if (d > 0)
            {
                if (x == prevX) { y--; ops.Add((OpInsert, x, y)); }  // из (prevX, prevY) вниз
                else { x--; ops.Add((OpDelete, x, y)); }             // из (prevX, prevY) вправо
            }
        }
        ops.Reverse();
        return ops;
    }

    /// <summary>Задан ли полный дифф (контекст накрывает файл целиком - все строки).</summary>
    private static bool IsFullContext(int context) => context >= FullContext;

    /// <summary>Группировка операций в ханки с контекстом n (семантика unified diff).</summary>
    private static void AppendHunks(
        List<string> output, List<(byte Kind, int A, int B)> ops, string[] a, string[] b, int n)
    {
        int count = ops.Count, i = 0;
        while (i < count)
        {
            while (i < count && ops[i].Kind == OpEqual) i++;
            if (i >= count) break;

            int first = i, last = i, j = i;
            while (j < count)
            {
                if (ops[j].Kind != OpEqual) { last = j; j++; continue; }
                int gapEnd = j;
                while (gapEnd < count && ops[gapEnd].Kind == OpEqual) gapEnd++;
                if (gapEnd < count && gapEnd - j <= 2 * n) { last = gapEnd; j = gapEnd; continue; }
                break;
            }

            int start = Math.Max(0, first - n);
            int end = Math.Min(count - 1, last + n);

            int prevA = -1, prevB = -1;
            for (int t = 0; t < start; t++)
            {
                if (ops[t].Kind != OpInsert) prevA = ops[t].A;
                if (ops[t].Kind != OpDelete) prevB = ops[t].B;
            }

            bool aSet = false, bSet = false;
            int aStart = 0, aCount = 0, bStart = 0, bCount = 0;
            for (int t = start; t <= end; t++)
            {
                var (kind, ai, bi) = ops[t];
                if (kind != OpInsert) { aCount++; if (!aSet) { aStart = ai + 1; aSet = true; } prevA = ai; }
                if (kind != OpDelete) { bCount++; if (!bSet) { bStart = bi + 1; bSet = true; } prevB = bi; }
            }
            if (!aSet) aStart = prevA + 2; // 0 строк: позиция вставки, 1-based = prevA+1 → формат вернёт prevA+1
            if (!bSet) bStart = prevB + 2;

            output.Add($"@@ -{FormatRange(aStart, aCount)} +{FormatRange(bStart, bCount)} @@");
            for (int t = start; t <= end; t++)
            {
                var (kind, ai, bi) = ops[t];
                output.Add((kind switch
                {
                    OpEqual => " " + a[ai],
                    OpDelete => "-" + a[ai],
                    _ => "+" + b[bi],
                }));
            }
            i = end + 1;
        }
    }

    /// <summary>«начало,длина»: длина 1 опускается, при 0 начало сдвигается (как difflib).</summary>
    private static string FormatRange(int start1Based, int length)
    {
        if (length == 1) return start1Based.ToString(CultureInfo.InvariantCulture);
        if (length == 0) return (start1Based - 1).ToString(CultureInfo.InvariantCulture) + ",0";
        return start1Based.ToString(CultureInfo.InvariantCulture) + "," + length.ToString(CultureInfo.InvariantCulture);
    }

    // ───────────────────── модульное сравнение ─────────────────────

    /// <summary>Есть ли в каталоге подпапки (для авто-режима «Модульного сравнения»).</summary>
    public static bool HasSubDirectories(string dir)
    {
        try { return Directory.EnumerateDirectories(dir).Any(); }
        catch { return false; }
    }

    /// <summary>
    /// Модули = подпапки первого уровня OLD/NEW, пары по имени. Модуль, существующий
    /// только с одной стороны, - полноценный участник («удалён целиком»/«новый»).
    /// Размер для балансировки - суммарный объём файлов (макс. из двух сторон).
    /// </summary>
    public static List<ModuleInfo> DiscoverModules(string oldDir, string newDir)
    {
        var names = SafeSubDirNames(oldDir)
            .Union(SafeSubDirNames(newDir), StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal);

        var list = new List<ModuleInfo>();
        foreach (var name in names)
        {
            var oldPath = Path.Combine(oldDir, name);
            var newPath = Path.Combine(newDir, name);
            var hasOld = Directory.Exists(oldPath);
            var hasNew = Directory.Exists(newPath);
            var size = Math.Max(hasOld ? SafeDirSize(oldPath) : 0, hasNew ? SafeDirSize(newPath) : 0);
            list.Add(new ModuleInfo(name, hasOld ? oldPath : null, hasNew ? newPath : null, size));
        }
        return list;
    }

    private static IEnumerable<string> SafeSubDirNames(string dir)
    {
        string[] dirs;
        try { dirs = Directory.GetDirectories(dir); }
        catch { yield break; }
        foreach (var d in dirs) yield return Path.GetFileName(d);
    }

    /// <summary>Суммарный размер файлов каталога (рекурсивно, без чтения содержимого);
    /// недоступные подкаталоги пропускаются.</summary>
    public static long SafeDirSize(string root)
    {
        long total = 0;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch { continue; }
            foreach (var f in files)
            {
                try { total += new FileInfo(f).Length; } catch { }
            }
            string[] sub;
            try { sub = Directory.GetDirectories(dir); }
            catch { continue; }
            foreach (var d in sub) pending.Push(d);
        }
        return total;
    }

    private static int SafeFileCount(string dir)
    {
        try { return Directory.GetFiles(dir).Length; }
        catch { return 0; }
    }

    private static string SizeText(long bytes) =>
        bytes < 1024 * 1024 ? $"{bytes / 1024.0:F0} КБ" : $"{bytes / 1024.0 / 1024.0:F1} МБ";

    /// <summary>
    /// Аккуратное разбиение модулей на пулы: модули по убыванию размера жадно
    /// кладутся в наименее загруженный пул - внутри пула порядок тоже «крупные вперёд».
    /// </summary>
    public static List<Queue<ModuleInfo>> BuildPools(IReadOnlyList<ModuleInfo> modules, int workerCount)
    {
        var pools = Enumerable.Range(0, workerCount).Select(_ => new Queue<ModuleInfo>()).ToList();
        var totals = new long[workerCount];
        foreach (var m in modules.OrderByDescending(m => m.SizeBytes).ThenBy(m => m.Name, StringComparer.Ordinal))
        {
            int min = 0;
            for (int k = 1; k < workerCount; k++)
                if (totals[k] < totals[min]) min = k;
            pools[min].Enqueue(m);
            totals[min] += m.SizeBytes;
        }
        return pools;
    }

    /// <summary>
    /// Модульное сравнение: min(4, модулей) воркеров, у каждого свой пул (см. BuildPools).
    /// Воркер обрабатывает только свой пул; когда пул опустел, под замком атомарно
    /// забирает следующий непроизведённый модуль из любого пула - освободившиеся
    /// потоки делят остаток, модуль не обрабатывается дважды. Модуль, изменившийся
    /// только двоичными файлами при skipBinary, считается «без изменений».
    /// </summary>
    /// <param name="log">Готовая строка для журнала страницы (префиксы [T№][модуль] ставятся здесь).</param>
    /// <param name="progress">(поток, модуль, cur, total) - прогресс по файлам для карточки потока.</param>
    /// <param name="threadModule">(поток, модуль | null) - карточка: текущий модуль / поток свободен.</param>
    /// <param name="moduleDone">(готово, всего) - счётчик завершённых модулей.</param>
    public static async Task<ModuleRunResult> RunModulesAsync(
        IReadOnlyList<ModuleInfo> modules, string oldDir, string newDir, string outDir,
        int context, IReadOnlyList<string> patterns, bool skipBinary,
        Action<string> log,
        Action<int, string, int, int> progress,
        Action<int, string?> threadModule,
        Action<int, int> moduleDone,
        CancellationToken ct)
    {
        if (modules.Count == 0)
            return new ModuleRunResult(Array.Empty<ModuleOutcome>(), 0, 0);

        int workers = Math.Min(4, modules.Count);
        int total = modules.Count;
        var started = System.Diagnostics.Stopwatch.StartNew();

        var rootFiles = SafeFileCount(oldDir) + SafeFileCount(newDir);
        if (rootFiles > 0)
            log($"Файлов в корне OLD/NEW (вне подпапок-модулей): {rootFiles} - не сравниваются");

        var pools = BuildPools(modules, workers);
        log($"Потоков: {workers}; распределение: " +
            string.Join(", ", pools.Select((p, i) => $"T{i + 1} - {p.Count} мод. / {SizeText(p.Sum(m => m.SizeBytes))}")));

        var queues = pools.Select(p => new ConcurrentQueue<ModuleInfo>(p)).ToArray();
        var outcomes = new ConcurrentBag<ModuleOutcome>();
        var gate = new object();
        int done = 0;

        void Worker(int t)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                ModuleInfo? mod = null;
                if (!queues[t].TryDequeue(out mod))
                {
                    // Свой пул пуст: под замком забираем первый непроизведённый
                    // модуль из любого пула - атомарно, без дублей
                    lock (gate)
                    {
                        foreach (var q in queues)
                            if (q.TryDequeue(out mod)) break;
                    }
                }
                if (mod == null) break; // все модули распределены и обработаны

                threadModule(t, mod.Name);
                log($"[T{t + 1}][{mod.Name}] старт, размер {SizeText(mod.SizeBytes)}");

                var moduleOutDir = Path.Combine(outDir, mod.Name);
                Directory.CreateDirectory(moduleOutDir);
                var diffPath = Path.Combine(moduleOutDir, "changes.diff");
                try
                {
                    var stats = Run(
                        mod.OldPath ?? Path.Combine(oldDir, mod.Name),
                        mod.NewPath ?? Path.Combine(newDir, mod.Name),
                        diffPath, context, patterns,
                        // В UI-журнал - только предупреждения и ошибки: полный журнал
                        // по модулю Run пишет сам в changes.diff.log, а пофайловый INFO
                        // в UI тоннами строк вешает интерфейс (см. FlushGitUiUpdates)
                        line =>
                        {
                            if (line.Contains("[WARNING]") || line.Contains("[ERROR]"))
                                log($"[T{t + 1}][{mod.Name}] {line}");
                        },
                        (cur, tot, _) => progress(t, mod.Name, cur, tot),
                        ct, skipBinary);

                    // Текстовые изменения определяют «есть ли что показать»:
                    // модуль, изменившийся только пропущенными двоичными, - без изменений
                    var textChanges = stats.Modified + stats.Added + stats.Deleted;
                    string kind = mod.OldPath == null ? "added"
                        : mod.NewPath == null ? "deleted"
                        : textChanges == 0 ? "unchanged"
                        : "modified";
                    if (kind == "unchanged")
                    {
                        TryDeleteDir(moduleOutDir);
                        log($"[T{t + 1}][{mod.Name}] без изменений");
                    }
                    else
                    {
                        log($"[T{t + 1}][{mod.Name}] готово: измен. {stats.Modified}, новых {stats.Added}, " +
                            $"удал. {stats.Deleted}, двоич. {stats.Binary}" +
                            (stats.HasErrors ? $", ОШИБОК: {stats.Errors}" : ""));
                    }
                    outcomes.Add(new ModuleOutcome(mod.Name, kind, stats, null));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    log($"[T{t + 1}][{mod.Name}] ОШИБКА: {ex.GetType().Name}: {ex.Message} (HResult 0x{ex.HResult:X8})");
                    outcomes.Add(new ModuleOutcome(mod.Name, "error", null, ex.Message));
                }
                moduleDone(Interlocked.Increment(ref done), total);
            }
            threadModule(t, null);
        }

        var tasks = Enumerable.Range(0, workers).Select(t => Task.Run(() => Worker(t))).ToArray();
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        started.Stop();
        log($"Модульное сравнение завершено за {started.Elapsed.TotalSeconds:F1} с: модулей {total}, потоков {workers}");
        return new ModuleRunResult(
            outcomes.OrderBy(o => o.Module, StringComparer.Ordinal).ToList(), workers, total);
    }

    private static void TryDeleteDir(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { }
    }
}
