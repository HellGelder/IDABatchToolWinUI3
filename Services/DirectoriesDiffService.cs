using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace IDABatchToolWinUI.Services;

/// <summary>Итог текстового диффа двух директорий.</summary>
public sealed record DirsDiffStats(
    int Modified, int Added, int Deleted, int Binary, int Errors, int Unchanged,
    int TotalFiles, string OutPath, string LogPath, long SizeBytes,
    IReadOnlyList<string> ErrorDetails, IReadOnlyList<string> BinaryFiles)
{
    public bool HasErrors => Errors > 0;
}

/// <summary>
/// Текстовый дифф двух папок с исходниками — нативный порт diff_gui.py
/// (исполнение 1): рекурсивный обход с glob-исключениями, sniff бинарности
/// по нулевому байту, нормализация EOL и единый unified diff в стиле
/// «git diff --no-index --no-prefix» в один файл + журнал &lt;файл&gt;.log.
/// Внутренний алгоритм — Myers O(ND): расстояние редактирования выше
/// MaxEditDistance (почти полностью переписанный огромный файл) отдаётся
/// одним блоком замены — вывод остаётся корректным unified diff.
/// </summary>
public static class DirectoriesDiffService
{
    public const string DefaultExcludes =
        ".git, .svn, __pycache__, *.pyc, .venv, venv, node_modules, .idea, .vscode, dist, build, *.log";

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
    /// строки (они же пишутся в &lt;outPath&gt;.log), прогресс — (cur, total, rel).
    /// </summary>
    public static DirsDiffStats Run(
        string oldDir, string newDir, string outPath, int context,
        IReadOnlyList<string> patterns,
        Action<string>? log = null,
        Action<int, int, string>? progress = null,
        CancellationToken ct = default)
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
        L("INFO", $"Строк контекста: {context}");

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
        L("INFO", $"Сравнение {total} файлов (контекст: {context} строк)");

        var chunks = new List<string>();
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
                if (chunk != null) chunks.Add(chunk);
                L("INFO", $"{prefix} {rel} — {KindLabel(kind)}");
            }
            catch (Exception exc)
            {
                L("ERROR", $"{prefix} {rel} — ОШИБКА ОБРАБОТКИ: {exc.Message}");
                errors.Add($"{rel}: {exc.Message}");
            }
            progress?.Invoke(idx + 1, total, rel);
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
            $"Двоичных (без содержимого): {s.Binary}",
            $"Ошибок:            {s.Errors}",
            $"Файл диффа: {s.OutPath} ({s.SizeBytes / 1024f:F1} КБ)",
            $"Журнал: {s.LogPath}",
        };
        if (s.BinaryFiles.Count > 0)
        {
            var shown = string.Join(", ", s.BinaryFiles.Take(10));
            var more = s.BinaryFiles.Count > 10 ? $" ...и ещё {s.BinaryFiles.Count - 10}" : "";
            lines.Add($"Двоичные файлы: {shown}{more}");
        }
        if (s.ErrorDetails.Count > 0)
        {
            lines.Add("Ошибки (подробности — в журнале):");
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
                // Для eq значимы оба индекса; у del не читается B, у insert — A.
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
}
