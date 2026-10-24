using System.Diagnostics;
using System.Text;
using IDABatchToolWinUI.Diff;
using static IDABatchToolWinUI.Diff.DiffJson;
using IDABatchToolWinUI.Models;
using IDABatchToolWinUI.Services;

namespace IDABatchToolWinUI.Workers;

/// <summary>
/// Параллельное сравнение пар с помощью BinDiff + Diaphora — перенос
/// diff_worker.py на C#: export BinExport и JSON, BinDiff CLI, Diaphora, пост-обработка
/// (hexdump diff, enrich, unmatched), HTML-отчёты через Python-bridge.
/// </summary>
public sealed class DiffWorker : IDisposable
{
    private const string ExportDataScript = "export_data.py";
    private const string DiaphoraDir = "diaphora";
    private const string DiaphoraScript = "diaphora.py";

    public event Action<string, string>? StageChanged;        // (stage, "started" | "done")
    public event Action<string, int, int, string>? StageFile; // (stage, current, total, fileName)
    public event Action<string, string, string>? PairStatus; // (relKey, engine, status)
    public event Action<string, int>? PairProcessStarted;    // (relKey, PID последнего запущенного процесса пары)
    public event Action<string>? ErrorOccurred;
    public event Action<int, int>? Finished;                 // (success, total)

    private CancellationTokenSource? _cts;
    private Task? _task;
    private OutcomeTracker? _outcomes;

    public IReadOnlyList<DiffPair> Pairs { get; }
    public string IdatPath { get; }
    public string BindiffPath { get; }
    public string OutputDir { get; }
    public string Engine { get; }         // bindiff | diaphora | both
    public string LeftDir { get; }
    public string RightDir { get; }
    public string? AddOutputDir { get; }
    public int MaxWorkers { get; }

    private const long LargeFileThreshold = 100L * 1024 * 1024;
    private static readonly TimeSpan PulseInterval = TimeSpan.FromSeconds(30);
    /// <summary>Ни ОЗУ, ни БД диафоры не менялись дольше этого срока — процесс завис, убиваем.</summary>
    private static readonly TimeSpan StallTimeout = TimeSpan.FromHours(4);

    /// <summary>Прогресс экспорта Diaphora, разобранный из stdout («Exported N out of M total»).</summary>
    private sealed class ExportProgress
    {
        public int Done;            // экспортировано функций
        public int Total;           // всего функций
        public TimeSpan? Remaining; // оценка Diaphora «remaining time»
    }

    private static readonly System.Text.RegularExpressions.Regex ExportedRe = new(
        @"Exported (\d+) function\(s\) out of (\d+) total",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex RemainingRe = new(
        @"remaining time ~(\d+):(\d+):(\d+)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    public DiffWorker(
        IReadOnlyList<DiffPair> pairs,
        string idatPath,
        string bindiffPath,
        string outputDir,
        string engine,
        string leftDir,
        string rightDir,
        string? addOutputDir,
        int? maxWorkers = null)
    {
        Pairs = pairs;
        IdatPath = idatPath;
        BindiffPath = bindiffPath;
        OutputDir = outputDir;
        Engine = engine;
        LeftDir = leftDir;
        RightDir = rightDir;
        AddOutputDir = addOutputDir;
        MaxWorkers = Math.Clamp(maxWorkers ?? 4, 1, 32);
    }

    public void Start()
    {
        if (_task != null) return;
        _cts = new CancellationTokenSource();
        _task = Task.Run(() => RunCore(_cts.Token));
    }

    public void Cancel() => _cts?.Cancel();

    private string ScriptsDir => AppConstants.ScriptsDir;

    private string ExportScript => Path.Combine(AppConstants.ScriptsDir, ExportDataScript);
    private string DiaphoraPath => Path.Combine(AppConstants.ScriptsDir, DiaphoraDir, DiaphoraScript);

    private async Task RunCore(CancellationToken ct)
    {
        // Страховка: любое исключение из фаз не должно терять Finished —
        // иначе страница навсегда остаётся в состоянии «выполняется».
        try
        {
            await RunCoreInner(ct);
        }
        catch (OperationCanceledException)
        {
            Abort();
        }
        catch (Exception e)
        {
            ErrorOccurred?.Invoke($"Критическая ошибка сравнения: {e.Message}");
            Abort();
        }
    }

    private async Task RunCoreInner(CancellationToken ct)
    {
        var total = Pairs.Count;
        if (total == 0) { Finished?.Invoke(0, 0); return; }

        // Дальше работаем только с выбранными парами (уже отфильтрованы страницей).
        var all = Pairs
            .OrderByDescending(p => File.Exists(p.Primary) ? new FileInfo(p.Primary).Length : 0)
            .ToList();

        var useBindiff = Engine is "bindiff" or "both";
        var useDiaphora = Engine is "diaphora" or "both";

        // Пары с идентичными бинарниками (равные SHA-256) не анализируем вовсе:
        // их сходство полная и по определению — BinDiff/Diaphora там не нужны.
        var identical = new HashSet<string>();
        foreach (var p in all)
        {
            var orig1 = FindOriginalBinary(p.Primary);
            var orig2 = FindOriginalBinary(p.Secondary);
            if (orig1 == null || orig2 == null) continue;
            if (FilesEqual(orig1, orig2)) identical.Add(p.Stem);
        }
        if (identical.Count > 0)
            ErrorOccurred?.Invoke($"Идентичных пар (побайтно): {identical.Count} — анализ не запускается, отчёт отмечен «Модули идентичны».");

        var work = all.Where(p => !identical.Contains(p.Stem)).ToList();
        _outcomes = new OutcomeTracker(all.Select(p => p.Stem));

        // Этапы выполнения: счётчик «шагов» не ведём — GUI показывает этапы
        // и статусы обработки каждого файла.
        var phases = new List<(string stage, string statusText, List<DiffPair> pairs, Func<DiffPair, Task<bool>> process)>();
        if (useBindiff)
            phases.Add(("BinDiff", "Экспорт из БД", work, p => ProcessBindiffPairAsync(p, ct)));
        if (useDiaphora)
            phases.Add(("Diaphora", "Экспорт из БД", work, p => ProcessDiaphoraPairAsync(p, ct)));
        phases.Add(("Пост-анализ", "Пост-анализ", work, p => ProcessPostPairAsync(p, ct)));

        foreach (var (stage, statusText, pairs, process) in phases)
        {
            StageChanged?.Invoke(stage, "started");
            await RunPass(stage, pairs, pairs.Count, null, statusText, process, ct);
            if (ct.IsCancellationRequested) { Abort(); return; }
            StageChanged?.Invoke(stage, "done");
        }

        // Идентичным парам — заглушка без запуска анализаторов.
        foreach (var p in all.Where(p => identical.Contains(p.Stem)))
        {
            WriteIdenticalJson(p);
            PairStatus?.Invoke(p.RelKey, "bindiff", "identical");
            PairStatus?.Invoke(p.RelKey, "diaphora", "identical");

            var identicalJson = Path.Combine(OutputDir, $"{p.Stem}.diff.json");
            if (!ArtifactValidator.IsValidDiffJson(identicalJson, out var reason))
            {
                _outcomes?.Fail(p.Stem);
                ErrorOccurred?.Invoke($"Идентичная пара {p.RelKey}: {reason}");
            }
        }

        // Генерация HTML-отчётов (одна фаза)
        StageChanged?.Invoke("Генерация HTML", "started");
        await GenerateReportsAsync(ct);
        if (ct.IsCancellationRequested) { Abort(); return; }
        StageChanged?.Invoke("Генерация HTML", "done");

        // Доанализ для engine=bindiff — только при наличии пар <99%, ещё не прошедших доанализ
        if (Engine == "bindiff" && !string.IsNullOrEmpty(AddOutputDir))
        {
            if (CountLowSimilarityPairs(all) > 0)
            {
                await RunAddAnalysisAsync(all, ct);
                if (ct.IsCancellationRequested) { Abort(); return; }
            }
            else
            {
                ErrorOccurred?.Invoke(
                    "Доанализ не запускался: нет пар, требующих доанализа " +
                    "(similarity ≥ 99% или доанализ уже успешно завершён).");
            }
        }

        Finished?.Invoke(_outcomes?.Succeeded ?? 0, all.Count);
    }

    /// <summary>
    /// Доанализ пары уже завершён успешно: в add-JSON стоит маркер add_analysis_done
    /// (ставится после реального слияния результатов Diaphora). Факт существования файла
    /// маркером не является — там может лежать необработанная копия исходного diff.json.
    /// </summary>
    private bool IsAddAnalysisDone(string stem)
    {
        if (string.IsNullOrEmpty(AddOutputDir)) return false;
        var path = Path.Combine(AddOutputDir, $"{stem}.diff.json");
        if (!File.Exists(path)) return false;
        var d = ReadJson(path);
        return d != null && d.TryGetValue("add_analysis_done", out var v) && v is true;
    }

    private int CountLowSimilarityPairs(List<DiffPair> pairs)
    {
        int low = 0;
        foreach (var p in pairs)
        {
            var d = Path.Combine(OutputDir, $"{p.Stem}.diff.json");
            if (!File.Exists(d)) continue;
            if (IsAddAnalysisDone(p.Stem)) continue;
            if (IsIdenticalOrFullHexdump(d)) continue;
            try
            {
                var data = ReadJson(d);
                var sim = data != null && data.TryGetValue("similarity", out var v) && v != null
                    ? Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture) : 0.0;
                if (sim < 0.99) low++;
            }
            catch { /* пропускаем */ }
        }
        return low;
    }

    /// <summary>Пара не нуждается в доанализе независимо от similarity:
    /// побайтно идентична (маркер identical) или BinDiff не дал функций при
    /// 100% hexdump-схожести (анализаторы здесь бессильны, файлы одинаковые).</summary>
    private static bool IsIdenticalOrFullHexdump(string diffJsonPath)
    {
        try
        {
            var data = ReadJson(diffJsonPath);
            if (data == null) return false;
            if (data.TryGetValue("identical", out var id) && id is true) return true;
            var hd = data.TryGetValue("hexdump_similarity", out var hv) && hv != null
                ? Convert.ToDouble(hv, System.Globalization.CultureInfo.InvariantCulture) : 0.0;
            if (hd < 1.0) return false;
            var matched = data.TryGetValue("matched_functions", out var mf) && mf is List<object?> ml ? ml.Count : 0;
            var total1 = data.TryGetValue("total_functions1", out var t1) && t1 != null ? Convert.ToInt64(t1) : 0;
            return matched == 0 && total1 == 0;
        }
        catch { return false; }
    }

    private void Abort()
    {
        Finished?.Invoke(_outcomes?.Succeeded ?? 0, Pairs.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Проходы
    // ─────────────────────────────────────────────────────────────────

    private async Task RunPass(
        string stage, List<DiffPair> pairs, int total, string? engine,
        string statusText,
        Func<DiffPair, Task<bool>> process, CancellationToken ct,
        bool parallelLarge = false, bool trackOutcome = true)
    {
        if (pairs.Count == 0) return;

        // Начало фазы: статус этапа для задействованных колонок
        // (не «waiting» — колонки других движков уже содержат их результат).
        foreach (var p in pairs)
            if (engine == null)
            {
                if (Engine is "bindiff" or "both") PairStatus?.Invoke(p.RelKey, "bindiff", statusText);
                if (Engine is "diaphora" or "both") PairStatus?.Invoke(p.RelKey, "diaphora", statusText);
            }
            else PairStatus?.Invoke(p.RelKey, engine, statusText);

        StageFile?.Invoke(stage, 0, total, "");

        // parallelLarge=true — крупные файлы тоже идут через общий семафор
        // (доанализ Diaphora должен быть многопоточным; в исполнении 1 крупные
        // файлы всегда последовательны — для основных фаз сохраняем это поведение).
        var small = parallelLarge ? pairs : pairs.Where(p => !IsLarge(p.Primary)).ToList();
        var large = parallelLarge ? new List<DiffPair>() : pairs.Where(p => IsLarge(p.Primary)).ToList();
        var completed = 0;

        async Task RunOne(DiffPair p)
        {
            // Ставим «анализ» для задействованного движка (engine == null -> оба)
            if (engine == null)
            {
                if (Engine is "bindiff" or "both") PairStatus?.Invoke(p.RelKey, "bindiff", "analysis");
                if (Engine is "diaphora" or "both") PairStatus?.Invoke(p.RelKey, "diaphora", "analysis");
            }
            else PairStatus?.Invoke(p.RelKey, engine, "analysis");

            bool ok;
            try { ok = await process(p); }
            catch (Exception e)
            {
                ErrorOccurred?.Invoke($"Ошибка {p.RelKey} в фазе {stage}: {e.Message}");
                ok = false;
            }
            var status = ok ? "done" : "error";
            if (!ok && trackOutcome) _outcomes?.Fail(p.Stem);
            if (engine == null)
            {
                if (Engine is "bindiff" or "both") PairStatus?.Invoke(p.RelKey, "bindiff", status);
                if (Engine is "diaphora" or "both") PairStatus?.Invoke(p.RelKey, "diaphora", status);
            }
            else PairStatus?.Invoke(p.RelKey, engine, status);

            var done = Interlocked.Increment(ref completed);
            StageFile?.Invoke(stage, done, total, Path.GetFileName(p.Primary));
        }

        using var sem = new SemaphoreSlim(MaxWorkers);
        var tasks = small.Select(async p =>
        {
            if (ct.IsCancellationRequested) return;
            await sem.WaitAsync(ct);
            try { await RunOne(p); } finally { sem.Release(); }
        }).ToList();

        await Task.WhenAll(tasks);

        // Крупные файлы — последовательно
        foreach (var p in large)
        {
            if (ct.IsCancellationRequested) break;
            await RunOne(p);
        }
    }

    private static bool IsLarge(string path)
        => File.Exists(path) && new FileInfo(path).Length >= LargeFileThreshold;

    // ─────────────────────────────────────────────────────────────────
    //  BinDiff
    // ─────────────────────────────────────────────────────────────────

    private async Task<bool> ProcessBindiffPairAsync(DiffPair p, CancellationToken ct)
    {
        var stem = p.Stem;
        var jsonOutput = Path.Combine(OutputDir, $"{stem}.diff.json");
        var binexportP = Path.Combine(OutputDir, $"{stem}_primary.BinExport");
        var binexportS = Path.Combine(OutputDir, $"{stem}_secondary.BinExport");

        if (!await ExportBinExportAsync(p.Primary, binexportP, p.RelKey, ct)) return false;
        if (!await ExportBinExportAsync(p.Secondary, binexportS, p.RelKey, ct)) return false;

        var diffOutput = Path.Combine(OutputDir, $"{stem}.BinDiff");
        if (!await RunBindiffAsync(binexportP, binexportS, diffOutput, p.RelKey, ct))
        {
            ErrorOccurred?.Invoke($"BinDiff: {stem}");
            return false;
        }
        ParseBindiffResult(diffOutput, p.Primary, p.Secondary, jsonOutput);
        if (!ArtifactValidator.IsValidDiffJson(jsonOutput, out var reason))
        {
            ErrorOccurred?.Invoke($"BinDiff {stem}: {reason}");
            return false;
        }
        return true;
    }

    private async Task<bool> ExportBinExportAsync(string i64Path, string outputFile, string relKey, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return false;
        if (File.Exists(outputFile)) File.Delete(outputFile);

        var psi = NewIdatPsi();
        psi.ArgumentList.Add("-A");
        psi.ArgumentList.Add("-OBinExportAutoAction:BinExportBinary");
        psi.ArgumentList.Add($"-OBinExportModule:{outputFile}");
        psi.ArgumentList.Add(i64Path);
        var (rc, _, _) = await RunProcAsync(psi, relKey, ct);
        return rc == 0 && File.Exists(outputFile);
    }

    private async Task<bool> RunBindiffAsync(string primary, string secondary, string output, string relKey, CancellationToken ct)
    {
        var tmpDir = Path.Combine(Path.GetDirectoryName(output)!, Path.GetFileNameWithoutExtension(output) + "_tmp");
        if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
        Directory.CreateDirectory(tmpDir);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = BindiffPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--primary"); psi.ArgumentList.Add(primary);
            psi.ArgumentList.Add("--secondary"); psi.ArgumentList.Add(secondary);
            psi.ArgumentList.Add("--output_dir"); psi.ArgumentList.Add(tmpDir);
            var (rc, _, _) = await RunProcAsync(psi, relKey, ct);
            if (rc != 0) return false;

            var diffFiles = Directory.GetFiles(tmpDir, "*.BinDiff");
            if (diffFiles.Length == 0) return false;
            if (File.Exists(output)) File.Delete(output);
            File.Move(diffFiles[0], output);
            return true;
        }
        catch (Exception e)
        {
            ErrorOccurred?.Invoke($"BinDiff: {e.Message}");
            return false;
        }
        finally
        {
            try { Directory.Delete(tmpDir, recursive: true); } catch { /* ignore */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Diaphora
    // ─────────────────────────────────────────────────────────────────

    private async Task<bool> ProcessDiaphoraPairAsync(DiffPair p, CancellationToken ct)
    {
        var stem = p.Stem;
        var jsonOutput = Path.Combine(OutputDir, $"{stem}.diff.json");
        var dbPri = Path.Combine(OutputDir, $"{stem}_primary.diaphora.sqlite");
        var dbSec = Path.Combine(OutputDir, $"{stem}_secondary.diaphora.sqlite");
        var result = Path.Combine(OutputDir, $"{stem}_diaphora_result.sqlite");

        if (!File.Exists(DiaphoraPath))
        {
            ErrorOccurred?.Invoke($"Diaphora не найден: {DiaphoraPath}");
            return false;
        }

        var (priOk, priReason) = await RunDiaphoraExportAsync(p.Primary, dbPri, p.RelKey, ct);
        if (!priOk)
        {
            ErrorOccurred?.Invoke($"Diaphora экспорт primary {stem}: {priReason}");
            Cleanup(dbPri, dbSec, result);
            return false;
        }
        var (secOk, secReason) = await RunDiaphoraExportAsync(p.Secondary, dbSec, p.RelKey, ct);
        if (!secOk)
        {
            ErrorOccurred?.Invoke($"Diaphora экспорт secondary {stem}: {secReason}");
            Cleanup(dbPri, dbSec, result);
            return false;
        }

        if (!File.Exists(dbPri) || !File.Exists(dbSec))
        {
            ErrorOccurred?.Invoke($"Diaphora диф {stem}: базы не созданы");
            Cleanup(dbPri, dbSec, result);
            return false;
        }

        var (diffOk, diffReason) = await RunDiaphoraDiffAsync(dbPri, dbSec, result, p.RelKey, ct);
        if (diffOk)
        {
            MergeDiaphoraIntoJson(jsonOutput, result, stem);
        }
        else
        {
            ErrorOccurred?.Invoke($"Diaphora диф {stem}: {diffReason}");
        }

        Cleanup(dbPri, dbSec, result);
        if (!diffOk) return false;

        if (!ArtifactValidator.IsValidDiffJson(jsonOutput, out var reason))
        {
            ErrorOccurred?.Invoke($"Diaphora {stem}: {reason}");
            return false;
        }
        return true;
    }

    private async Task<(bool Ok, string? Reason)> RunDiaphoraExportAsync(string i64Path, string outSqlite, string relKey, CancellationToken ct)
    {
        var psi = NewIdatPsi();
        var env = psi.Environment;
        env["DIAPHORA_AUTO"] = "1";
        env["DIAPHORA_EXPORT_FILE"] = outSqlite;
        // Облегчённый профиль экспорта (перенос из исполнения 1): псевдокод-эвристики
        // работают, микрокод off — Hex-Rays вызывается один раз на функцию вместо двух.
        // Работает только с патчем get_value_for (иначе '0' остаётся строкой и не
        // отключает флаг) и патчем ключа export_microcode в diaphora_ida.py.
        env["DIAPHORA_USE_DECOMPILER"] = "1";
        env["DIAPHORA_EXPORT_MICROCODE"] = "0";
        env["DIAPHORA_FUNCTION_SUMMARIES_ONLY"] = "0";

        // Для крупных файлов — как в исполнении 1: увеличенная виртуальная память,
        // отключение undo, отдельный лог IDA.
        if (IsLarge(i64Path))
        {
            psi.ArgumentList.Add("-dVPAGESIZE=16384");
            psi.ArgumentList.Add("-dUNDO_MAXSIZE=0");
            var idaLog = Path.Combine(Path.GetDirectoryName(outSqlite) ?? ".", Path.GetFileNameWithoutExtension(outSqlite) + ".ida.log");
            psi.ArgumentList.Add($"-L{idaLog}");
        }

        psi.ArgumentList.Add("-A");
        // Важно: без кавычек внутри аргумента — ProcessStartInfo.ArgumentList сам
        // экранирует путь; кавычки в значении ломали нахождение скрипта IDA.
        psi.ArgumentList.Add($"-S{DiaphoraPath}");
        psi.ArgumentList.Add(i64Path);

        var (rc, stdout, stderr) = await RunProcWithPulseAsync(psi, relKey, "экспорт", outSqlite, ct);
        if (rc == 0 && File.Exists(outSqlite)) return (true, null);
        return (false, DescribeProcFailure(rc, stdout, stderr, $"экспорт {Path.GetFileName(i64Path)}"));
    }

    /// <summary>Короткая причина неудачи запуска Diaphora для заметки в отчёте:
    /// код выхода и хвост stderr/stdout (последние непустые строки).</summary>
    private static string DescribeProcFailure(int rc, string stdout, string stderr, string phase)
    {
        var tail = LastNonEmptyLines(stderr is "" or null ? stdout : stderr, 3);
        if (tail == "") tail = LastNonEmptyLines(stdout, 3);
        var baseMsg = $"код выхода {rc}, фаза {phase}";
        return tail == "" ? baseMsg : $"{baseMsg}: {tail}";
    }

    /// <summary>Последние непустые строки журнала (без ANSI-прочих артефактов), ≤ 500 символов.</summary>
    private static string LastNonEmptyLines(string text, int count)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var lines = text.Replace("\r", "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0)
            .ToArray();
        var picked = lines.Length <= count ? lines : lines[^count..];
        var joined = string.Join(" | ", picked);
        return joined.Length <= 500 ? joined : "…" + joined[^500..];
    }

    private async Task<(bool Ok, string? Reason)> RunDiaphoraDiffAsync(string db1, string db2, string outSqlite, string relKey, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = AppConstants.PythonwPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        // Быстрый профиль дифа (перенос из исполнения 1): медленные и экспериментальные
        // эвристики избыточны, таймаут эвристики снижен, чтобы одна тяжёлая эвристика
        // не растягивала диф на часы. Нужен патч get_value_for в diaphora.py —
        // без него env-строки не приводятся к bool/int и флаги не работают.
        psi.Environment["DIAPHORA_SLOW_HEURISTICS"] = "0";
        psi.Environment["DIAPHORA_EXPERIMENTAL"] = "0";
        psi.Environment["DIAPHORA_UNRELIABLE"] = "0";
        psi.Environment["DIAPHORA_SQL_TIMEOUT_LIMIT"] = "60";

        psi.ArgumentList.Add(DiaphoraPath);
        psi.ArgumentList.Add(db1);
        psi.ArgumentList.Add(db2);
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(outSqlite);
        var (rc, stdout, stderr) = await RunProcWithPulseAsync(psi, relKey, "диф", outSqlite, ct);
        if (rc == 0 && File.Exists(outSqlite)) return (true, null);
        return (false, DescribeProcFailure(rc, stdout, stderr, "сравнение БД"));
    }

    // ─────────────────────────────────────────────────────────────────
    //  Пост-анализ
    // ─────────────────────────────────────────────────────────────────

    private async Task<bool> ProcessPostPairAsync(DiffPair p, CancellationToken ct)
    {
        var stem = p.Stem;
        var jsonOutput = Path.Combine(OutputDir, $"{stem}.diff.json");
        var primJson = Path.Combine(OutputDir, $"{stem}_primary.export.json");
        var secJson = Path.Combine(OutputDir, $"{stem}_secondary.export.json");

        var exported = await ExportJsonPairAsync(p.Primary, primJson, p.Secondary, secJson, p.RelKey, ct);
        if (exported.Primary == null || exported.Secondary == null)
        {
            ErrorOccurred?.Invoke($"Пост-анализ {stem}: не удалось получить export.json");
            Cleanup(primJson, secJson);
            return false;
        }
        EnrichDiffJson(jsonOutput, exported.Primary, exported.Secondary);

        // Hexdump diff
        var orig1 = FindOriginalBinary(p.Primary);
        var orig2 = FindOriginalBinary(p.Secondary);
        if (orig1 != null && orig2 != null)
        {
            try
            {
                var (sim, skipped, skipReason) = HexdumpDiffService.ComputeSimilarity(orig1, orig2);
                var data = ReadJson(jsonOutput) ?? new Dictionary<string, object?>();
                data["global_hex_diff"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["name1"] = Path.GetFileName(orig1), ["path1"] = orig1,
                        ["name2"] = Path.GetFileName(orig2), ["path2"] = orig2,
                        ["hexdump_similarity"] = skipped ? -1.0 : sim,
                    }
                };
                data["hexdump_similarity"] = skipped ? -1.0 : sim;
                data["real_primary"] = orig1;
                data["real_secondary"] = orig2;
                // Аддитивно: отчёт показывает «не рассчитана» вместо отрицательного процента.
                if (skipped)
                {
                    data["hexdump_skipped"] = true;
                    data["hexdump_skip_reason"] = skipReason;
                }
                WriteJson(jsonOutput, data);
            }
            catch { /* не критично */ }
        }

        // Экспорт IDA — источник истины: согласуем matched/unmatched и тоталы
        // с реальным перечнем функций (доработка исполнения 1: 190fff3, ee9d761).
        ApplyIdaExportTruth(jsonOutput, exported.Primary, exported.Secondary);

        Cleanup(primJson, secJson);

        if (!ArtifactValidator.IsValidDiffJson(jsonOutput, out var reason))
        {
            ErrorOccurred?.Invoke($"Пост-анализ {stem}: {reason}");
            return false;
        }
        return true;
    }

    private async Task<(string? Primary, string? Secondary)> ExportJsonPairAsync(
        string primaryI64, string primaryOut, string secondaryI64, string secondaryOut, string relKey, CancellationToken ct)
    {
        var result = (Primary: (string?)null, Secondary: (string?)null);
        foreach (var (i64, outPath, isPrimary) in new[]
                 {
                     (primaryI64, primaryOut, true),
                     (secondaryI64, secondaryOut, false),
                 })
        {
            if (ct.IsCancellationRequested) break;

            // Старый export.json не должен пережить неудачный запуск и попасть в отчёт.
            try { if (File.Exists(outPath)) File.Delete(outPath); } catch { /* файл занят */ }

            var psi = NewIdatPsi();
            psi.ArgumentList.Add("-A");
            psi.ArgumentList.Add($"-S\"{ExportScript}\" pseudocode=1");
            psi.ArgumentList.Add(i64);
            var (rc, _, _) = await RunProcAsync(psi, relKey, ct);
            if (rc != 0) continue;

            var src = i64 + ".export.json";
            if (File.Exists(src) && src != outPath)
            {
                if (File.Exists(outPath)) File.Delete(outPath);
                File.Move(src, outPath);
            }
            if (ArtifactValidator.IsValidExportJson(outPath, out _))
            {
                if (isPrimary) result.Primary = outPath;
                else result.Secondary = outPath;
            }
            else if (File.Exists(outPath))
            {
                try { File.Delete(outPath); } catch { /* файл занят */ }
            }
        }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    //  HTML-отчёты
    // ─────────────────────────────────────────────────────────────────

    private async Task GenerateReportsAsync(CancellationToken ct)
    {
        try
        {
            var reportsDir = Path.Combine(OutputDir, "Reports");
            Directory.CreateDirectory(reportsDir);
            var jsonDir = OutputDir;
            using var worker = new HtmlGenWorker("diff", deleteJson: false, reuseCache: false, platform: "Windows");
            worker.ProgressUpdated += (cur, tot, msg) => StageFile?.Invoke("Генерация HTML", cur, tot, msg);
            worker.ErrorOccurred += e => ErrorOccurred?.Invoke(e);
            // Отмена diff-воркера должна останавливать и Python-мост (CANCEL в stdin).
            using var cancelRegistration = ct.Register(worker.Cancel);

            var jsonFiles = Directory.GetFiles(jsonDir, "*.diff.json").ToList();
            if (jsonFiles.Count == 0) return;
            await Task.Run(() => worker.Run(
                inputDir: LeftDir, reportsDir: reportsDir, jsonDir: jsonDir,
                leftDir: LeftDir, rightDir: RightDir, manpagesDb: null,
                jsonPaths: jsonFiles), ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            ErrorOccurred?.Invoke($"Ошибка генерации отчётов: {e.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Доанализ (engine=bindiff)
    // ─────────────────────────────────────────────────────────────────

    private async Task RunAddAnalysisAsync(List<DiffPair> pairs, CancellationToken ct)
    {
        if (!File.Exists(DiaphoraPath))
        {
            ErrorOccurred?.Invoke("Diaphora не найден — доанализ для пар <99% пропущен");
            return;
        }

        var low = pairs.Where(p =>
        {
            var stem = p.Stem;
            var d = Path.Combine(OutputDir, $"{stem}.diff.json");
            if (!File.Exists(d)) return false;
            if (IsAddAnalysisDone(stem)) return false;
            if (IsIdenticalOrFullHexdump(d)) return false;
            try
            {
                var data = ReadJson(d);
                var sim = data != null && data.TryGetValue("similarity", out var v) && v != null
                    ? Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture) : 0.0;
                return sim < 0.99;
            }
            catch { return false; }
        }).ToList();

        if (low.Count == 0) return;
        ErrorOccurred?.Invoke($"Доанализ Diaphora: {low.Count} пар с similarity < 99%");

        Directory.CreateDirectory(AddOutputDir!);
        foreach (var p in low)
        {
            var src = Path.Combine(OutputDir, $"{p.Stem}.diff.json");
            var dst = Path.Combine(AddOutputDir!, $"{p.Stem}.diff.json");
            if (File.Exists(src)) File.Copy(src, dst, overwrite: true);
        }

        StageChanged?.Invoke("Доанализ (Diaphora)", "started");
        await RunPass("Доанализ (Diaphora)", low, low.Count, "diaphora", "Доанализ (Diaphora)",
            p => ProcessAddDiaphoraPairAsync(p, ct), ct, parallelLarge: true, trackOutcome: false);
        StageChanged?.Invoke("Доанализ (Diaphora)", "done");
        if (ct.IsCancellationRequested) return;

        StageChanged?.Invoke("Доанализ (пост-анализ)", "started");
        await RunPass("Доанализ (пост-анализ)", low, low.Count, "diaphora", "Доанализ (пост-анализ)",
            p => ProcessAddPostPairAsync(p, ct), ct, parallelLarge: true, trackOutcome: false);
        StageChanged?.Invoke("Доанализ (пост-анализ)", "done");
        if (ct.IsCancellationRequested) return;

        StageChanged?.Invoke("Генерация HTML (доанализ)", "started");
        await GenerateAddReportsAsync(low, ct);
        StageChanged?.Invoke("Генерация HTML (доанализ)", "done");
    }

    private async Task<bool> ProcessAddDiaphoraPairAsync(DiffPair p, CancellationToken ct)
    {
        var stem = p.Stem;
        var jsonOutput = Path.Combine(AddOutputDir!, $"{stem}.diff.json");
        var dbPri = Path.Combine(AddOutputDir!, $"{stem}_primary.diaphora.sqlite");
        var dbSec = Path.Combine(AddOutputDir!, $"{stem}_secondary.diaphora.sqlite");
        var result = Path.Combine(AddOutputDir!, $"{stem}_diaphora_result.sqlite");

        // Причина провала — в отчёт доанализа (add_analysis_note): модуль не удалось
        // проанализировать (экспорт/сравнение не состоялись — например, БД не росла).
        string? note = null;
        var (priOk, priReason) = await RunDiaphoraExportAsync(p.Primary, dbPri, p.RelKey, ct);
        if (!priOk)
        {
            Cleanup(dbPri, dbSec, result);
            MarkAddAnalysisDone(jsonOutput, false,
                $"не удался экспорт основной БД ({Path.GetFileName(p.Primary)}): {priReason}");
            return false;
        }
        var (secOk, secReason) = await RunDiaphoraExportAsync(p.Secondary, dbSec, p.RelKey, ct);
        if (!secOk)
        {
            Cleanup(dbPri, dbSec, result);
            MarkAddAnalysisDone(jsonOutput, false,
                $"не удался экспорт сравниваемой БД ({Path.GetFileName(p.Secondary)}): {secReason}");
            return false;
        }

        var merged = false;
        if (File.Exists(dbPri) && File.Exists(dbSec))
        {
            var (diffOk, diffReason) = await RunDiaphoraDiffAsync(dbPri, dbSec, result, p.RelKey, ct);
            if (diffOk)
            {
                MergeDiaphoraIntoJson(jsonOutput, result, stem);
                merged = true;
                // Diaphora отработала штатно, но ни одной новой пары не добавила —
                // так и пишем в отчёт (нейтрально, без «ошибки»).
                var newPairs = 0;
                if (ReadJson(jsonOutput) is { } mergedData &&
                    mergedData.TryGetValue("matched_diaphora_only", out var md) && md is List<object?> ml)
                    newPairs = ml.Count;
                if (newPairs == 0) note = "Доанализ Diaphora дополнительных пар не дал — все совпадения уже найдены BinDiff.";
            }
            else note = diffReason;
        }
        if (!merged && note == null)
            note = "сравнение БД не дало результата (файл результата не создан)";
        // Маркер успешного доанализа — по нему пара исключается из следующих прогонов
        MarkAddAnalysisDone(jsonOutput, merged, note);
        Cleanup(dbPri, dbSec, result);
        return merged;
    }

    /// <summary>Ставит (или снимает) маркер add_analysis_done в add-JSON пары, note —
    /// причина провала либо нейтральный итог (рендерится баннером в HTML).</summary>
    private static void MarkAddAnalysisDone(string jsonPath, bool done, string? failureNote = null)
    {
        try
        {
            if (!File.Exists(jsonPath)) return;
            var data = ReadJson(jsonPath);
            if (data == null) return;
            data["add_analysis_done"] = done;
            if (failureNote == null) data.Remove("add_analysis_note");
            else data["add_analysis_note"] = failureNote;
            WriteJson(jsonPath, data);
        }
        catch { /* не критично */ }
    }

    private async Task<bool> ProcessAddPostPairAsync(DiffPair p, CancellationToken ct)
    {
        var stem = p.Stem;
        var jsonOutput = Path.Combine(AddOutputDir!, $"{stem}.diff.json");
        var primJson = Path.Combine(AddOutputDir!, $"{stem}_primary.export.json");
        var secJson = Path.Combine(AddOutputDir!, $"{stem}_secondary.export.json");

        var exported = await ExportJsonPairAsync(p.Primary, primJson, p.Secondary, secJson, p.RelKey, ct);
        if (exported.Primary == null || exported.Secondary == null)
        {
            ErrorOccurred?.Invoke($"Доанализ {stem}: не удалось получить export.json");
            Cleanup(primJson, secJson);
            return false;
        }
        EnrichDiffJson(jsonOutput, exported.Primary, exported.Secondary);

        var orig1 = FindOriginalBinary(p.Primary);
        var orig2 = FindOriginalBinary(p.Secondary);
        if (orig1 != null && orig2 != null)
        {
            try
            {
                var (sim, skipped, skipReason) = HexdumpDiffService.ComputeSimilarity(orig1, orig2);
                var data = ReadJson(jsonOutput) ?? new Dictionary<string, object?>();
                data["global_hex_diff"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["name1"] = Path.GetFileName(orig1), ["path1"] = orig1,
                        ["name2"] = Path.GetFileName(orig2), ["path2"] = orig2,
                        ["hexdump_similarity"] = skipped ? -1.0 : sim,
                    }
                };
                data["hexdump_similarity"] = skipped ? -1.0 : sim;
                if (skipped)
                {
                    data["hexdump_skipped"] = true;
                    data["hexdump_skip_reason"] = skipReason;
                }
                WriteJson(jsonOutput, data);
            }
            catch { /* не критично */ }
        }

        // Экспорт IDA — источник истины (доработка исполнения 1: 190fff3, ee9d761).
        ApplyIdaExportTruth(jsonOutput, exported.Primary, exported.Secondary);

        Cleanup(primJson, secJson);

        if (!ArtifactValidator.IsValidDiffJson(jsonOutput, out var reason))
        {
            ErrorOccurred?.Invoke($"Доанализ {stem}: {reason}");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Экспорт IDA — источник истины (доработка исполнения 1, коммиты 190fff3/ee9d761):
    /// отбрасывает сопоставления с адресами вне перечня функций IDA (псевдофункции
    /// BinExport, из-за которых показатель превышал 100%), пересобирает
    /// matched_summary/total_matched, перезаписывает unmatched_functions1/2 и
    /// total_functions1/2 фактическими данными экспорта, ставит
    /// unmatched_source=ida_export. Для старых JSON без экспорта — разность тоталов.
    /// </summary>
    private void ApplyIdaExportTruth(string jsonOutput, string? primaryExport, string? secondaryExport)
    {
        if (!File.Exists(jsonOutput)) return;
        var data = ReadJson(jsonOutput);
        if (data == null) return;

        var exportSet1 = ReadExportAddrs(primaryExport);
        var exportSet2 = ReadExportAddrs(secondaryExport);

        // BinExport содержит псевдофункции (jump-thunk'и), которых нет в
        // перечне IDA — из-за них сопоставленных бывает больше, чем функций
        // в файле (показатель > 100%). Оставляем только пары, чьи стороны
        // есть в перечне.
        var matches = data.TryGetValue("matched_functions", out var mf) && mf is List<object?> ml
            ? ml : new List<object?>();
        var kept = matches
            .OfType<Dictionary<string, object?>>()
            .Where(m =>
                (exportSet1.Count == 0 || (Convert.ToString(m.GetValue("address1")) ?? "") != "" &&
                    exportSet1.Contains(Convert.ToString(m.GetValue("address1")) ?? "")) &&
                (exportSet2.Count == 0 || (Convert.ToString(m.GetValue("address2")) ?? "") != "" &&
                    exportSet2.Contains(Convert.ToString(m.GetValue("address2")) ?? "")))
            .ToList();
        var dropped = matches.Count - kept.Count;
        if (dropped > 0)
            ErrorOccurred?.Invoke($"Post: отброшено {dropped} пар с адресами вне перечня IDA (pseudo-functions)");

        data["matched_functions"] = kept;
        data["matched_summary"] = new Dictionary<string, object?>
        {
            ["total"] = kept.Count,
            ["bindiff_only"] = kept.Count(m => Convert.ToString(m.GetValue("source")) == "bindiff"),
            ["diaphora_only"] = kept.Count(m => Convert.ToString(m.GetValue("source")) == "diaphora"),
            ["both"] = kept.Count(m => Convert.ToString(m.GetValue("source")) == "both"),
        };
        data["total_matched"] = kept.Count;
        data["matched_diaphora_only"] = kept
            .Where(m => Convert.ToString(m.GetValue("source")) == "diaphora").ToList<object?>();
        data["diaphora_matched_count"] = kept
            .Count(m => Convert.ToString(m.GetValue("source")) is "diaphora" or "both");

        var matchedPrimary = kept
            .Select(m => Convert.ToString(m.GetValue("address1")) ?? "")
            .Where(a => a != "").ToHashSet();
        var matchedSecondary = kept
            .Select(m => Convert.ToString(m.GetValue("address2")) ?? "")
            .Where(a => a != "").ToHashSet();

        if (primaryExport != null)
        {
            var un1 = ReadUnmatched(primaryExport, matchedPrimary);
            data["unmatched_functions1"] = un1;
            data["total_unmatched"] = un1.Count;
            data["unmatched_source"] = "ida_export";
            if (exportSet1.Count > 0) data["total_functions1"] = exportSet1.Count;
        }
        if (secondaryExport != null)
        {
            var un2 = ReadUnmatched(secondaryExport, matchedSecondary);
            data["unmatched_functions2"] = un2;
            if (exportSet2.Count > 0) data["total_functions2"] = exportSet2.Count;
        }
        if (primaryExport == null)
        {
            if (data.TryGetValue("unmatched_functions1", out var uf1) &&
                uf1 is List<object?> l1 && l1.Count > 0)
            {
                data["total_unmatched"] = l1.Count;
            }
            else
            {
                var total1 = Convert.ToInt64(data.GetValue("total_functions") ?? data.GetValue("total_functions1") ?? 0);
                data["total_unmatched"] = Math.Max(0, total1 - matchedPrimary.Count);
            }
        }

        WriteJson(jsonOutput, data);
    }

    /// <summary>Уникальные адреса функций из экспорта IDA (в нормализованном виде 0xXXXX).</summary>
    private static HashSet<string> ReadExportAddrs(string? exportJson)
    {
        var seen = new HashSet<string>();
        if (string.IsNullOrEmpty(exportJson) || !File.Exists(exportJson)) return seen;
        try
        {
            var d = ReadJson(exportJson);
            if (d == null || !d.TryGetValue("functions", out var f) || f is not List<object?> funcs)
                return seen;
            foreach (var fn in funcs.OfType<Dictionary<string, object?>>())
            {
                var raw = Convert.ToString(fn.GetValue("start_ea")) ?? "";
                if (raw == "") continue;
                if (NormalizeAddr(raw) is { } norm && norm != "")
                    seen.Add(norm);
            }
        }
        catch { /* повреждённый JSON — пропускаем */ }
        return seen;
    }

    /// <summary>Функции экспорта IDA, не вошедшие в сопоставленные.</summary>
    private static List<object?> ReadUnmatched(string? exportJson, HashSet<string> matchedSet)
    {
        var funcs = new List<object?>();
        if (string.IsNullOrEmpty(exportJson) || !File.Exists(exportJson)) return funcs;
        try
        {
            var d = ReadJson(exportJson);
            if (d == null || !d.TryGetValue("functions", out var f) || f is not List<object?> list)
                return funcs;
            foreach (var fn in list.OfType<Dictionary<string, object?>>())
            {
                var raw = Convert.ToString(fn.GetValue("start_ea")) ?? "";
                if (raw == "") continue;
                var norm = NormalizeAddr(raw);
                if (norm == null || matchedSet.Contains(norm)) continue;
                funcs.Add(new Dictionary<string, object?>
                {
                    ["address"] = norm,
                    ["name"] = Convert.ToString(fn.GetValue("name"))?.Trim() ?? "<unnamed>",
                });
            }
        }
        catch { /* повреждённый JSON — пропускаем */ }
        return funcs;
    }

    /// <summary>Нормализует адрес к виду 0xXXXX (как в перечне IDA и разборе BinDiff).</summary>
    private static string? NormalizeAddr(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        try
        {
            var hex = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s;
            return $"0x{long.Parse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture):X}";
        }
        catch
        {
            return s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s : null;
        }
    }

    private async Task GenerateAddReportsAsync(List<DiffPair> pairs, CancellationToken ct)
    {
        try
        {
            var reportsDir = Path.Combine(AddOutputDir!, "Reports");
            Directory.CreateDirectory(reportsDir);
            var jsonFiles = Directory.GetFiles(AddOutputDir!, "*.diff.json").ToList();
            if (jsonFiles.Count == 0) return;

            using var worker = new HtmlGenWorker("diff", deleteJson: false, reuseCache: false, platform: "Windows");
            worker.ProgressUpdated += (cur, tot, msg) => StageFile?.Invoke("Генерация HTML (доанализ)", cur, tot, msg);
            worker.ErrorOccurred += e => ErrorOccurred?.Invoke(e);
            using var cancelRegistration = ct.Register(worker.Cancel);

            await Task.Run(() => worker.Run(
                inputDir: LeftDir, reportsDir: reportsDir, jsonDir: AddOutputDir!,
                leftDir: LeftDir, rightDir: RightDir, manpagesDb: null,
                jsonPaths: jsonFiles), ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            ErrorOccurred?.Invoke($"Ошибка генерации отчётов (доанализ): {e.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Разбор результатов BinDiff (SQLite)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Разбор .BinDiff SQLite в .diff.json — вынесено в Diff/BinDiffParser.</summary>
    private void ParseBindiffResult(string dbPath, string primary, string secondary, string jsonOutput)
        => BinDiffParser.Parse(dbPath, primary, secondary, jsonOutput);

    // ─────────────────────────────────────────────────────────────────
    //  Слияние Diaphora в .diff.json
    // ─────────────────────────────────────────────────────────────────

    private void MergeDiaphoraIntoJson(string jsonPath, string diaphoraSqlite, string stem)
    {
        var dres = ParseDiaphoraResults(diaphoraSqlite);
        var data = File.Exists(jsonPath) ? ReadJson(jsonPath) ?? new Dictionary<string, object?>() : null;

        if (data == null)
        {
            data = new Dictionary<string, object?>
            {
                ["primary"] = "", ["secondary"] = "",
                ["similarity"] = 0.0, ["confidence"] = 0.0,
                ["description"] = "", ["version"] = "", ["created"] = "", ["modified"] = "",
                ["file1"] = new Dictionary<string, object?>(), ["file2"] = new Dictionary<string, object?>(),
                ["matched_functions"] = new List<object?>(),
                ["total_functions1"] = 0, ["total_functions2"] = 0,
                ["error"] = null,
            };
        }

        var matches = data["matched_functions"] as List<object?> ?? new List<object?>();
        // Помечаем уже существующие как bindiff
        foreach (var m in matches.OfType<Dictionary<string, object?>>())
            if (!m.ContainsKey("source")) m["source"] = "bindiff";

        var existing = new Dictionary<(string, string), Dictionary<string, object?>>();
        foreach (var m in matches.OfType<Dictionary<string, object?>>())
        {
            var k = (Convert.ToString(m["address1"]) ?? "", Convert.ToString(m["address2"]) ?? "");
            existing[k] = m;
        }

        foreach (var m in dres.MatchedFunctions)
        {
            var key = (m.Address1, m.Address2);
            if (existing.TryGetValue(key, out var ex))
            {
                ex["source"] = "both";
                ex["bindiff_similarity"] = ex["similarity"];
                ex["diaphora_similarity"] = m.Similarity;
            }
            else
            {
                existing[key] = m.ToDict();
                matches.Add(m.ToDict());
            }
        }

        // Дедупликация
        matches.Clear();
        matches.AddRange(DeduplicateMatched(existing.Values));

        data["matched_summary"] = Summarize(matches);
        data["matched_diaphora_only"] = matches.OfType<Dictionary<string, object?>>()
            .Where(m => Convert.ToString(m["source"]) == "diaphora").ToList();
        data["diaphora_matched_count"] = matches.OfType<Dictionary<string, object?>>()
            .Count(m => m["source"] is "diaphora" or "both");
        data["total_matched"] = matches.Count;

        // Unmatched
        foreach (var (side, list) in new[] { ("unmatched_functions1", dres.Unmatched1), ("unmatched_functions2", dres.Unmatched2) })
        {
            if (!data.ContainsKey(side)) data[side] = new List<object?>();
            var cur = (List<object?>)data[side]!;
            var ext = cur.OfType<Dictionary<string, object?>>().Select(x => Convert.ToString(x["address"]) ?? "").ToHashSet();
            foreach (var u in list)
                if (u.Address != "" && ext.Add(u.Address))
                    cur.Add(new Dictionary<string, object?> { ["address"] = u.Address, ["name"] = u.Name });
        }

        // Алгоритмы
        var ad = data.ContainsKey("algorithm_distribution") && data["algorithm_distribution"] is Dictionary<string, object?> d0
            ? d0 : new Dictionary<string, object?>();
        data["algorithm_distribution"] = ad;
        foreach (var (algo, cnt) in dres.AlgorithmDistribution)
            ad[algo] = (ad.TryGetValue(algo, out var v) && v != null ? Convert.ToInt64(v) : 0) + cnt;

        // engine
        var hasBd = matches.OfType<Dictionary<string, object?>>().Any(m => m["source"] is "bindiff" or "both");
        var hasDp = matches.OfType<Dictionary<string, object?>>().Any(m => m["source"] is "diaphora" or "both");
        data["engine"] = hasBd && hasDp ? "bindiff+diaphora" : hasDp ? "diaphora" : hasBd ? "bindiff" : "";

        WriteJson(jsonPath, data);
    }

    private static Dictionary<string, object?> Summarize(List<object?> matches)
    {
        var bd = matches.OfType<Dictionary<string, object?>>().Count(m => Convert.ToString(m["source"]) == "bindiff");
        var dp = matches.OfType<Dictionary<string, object?>>().Count(m => Convert.ToString(m["source"]) == "diaphora");
        var both = matches.OfType<Dictionary<string, object?>>().Count(m => Convert.ToString(m["source"]) == "both");
        return new Dictionary<string, object?>
        {
            ["total"] = matches.Count,
            ["bindiff_only"] = bd,
            ["diaphora_only"] = dp,
            ["both"] = both,
        };
    }

    private static List<object?> DeduplicateMatched(IEnumerable<Dictionary<string, object?>> matches)
    {
        var prio = new Dictionary<string, int> { ["both"] = 0, ["bindiff"] = 1, ["diaphora"] = 2 };
        var ordered = matches
            .OrderBy(m => prio.TryGetValue(Convert.ToString(m["source"]) ?? "diaphora", out var pr) ? pr : 99)
            .ThenByDescending(m => Convert.ToDouble(m["similarity"]));
        var seen1 = new HashSet<string>();
        var seen2 = new HashSet<string>();
        var result = new List<object?>();
        foreach (var m in ordered)
        {
            var a1 = Convert.ToString(m["address1"]) ?? "";
            var a2 = Convert.ToString(m["address2"]) ?? "";
            if (a1 == "" || a2 == "") continue;
            if (seen1.Contains(a1) || seen2.Contains(a2)) continue;
            seen1.Add(a1); seen2.Add(a2);
            result.Add(m);
        }
        return result;
    }

    private DiaphoraParseResult ParseDiaphoraResults(string sqlite)
    {
        var res = new DiaphoraParseResult();
        if (!File.Exists(sqlite)) return res;
        try
        {
            // Pooling=False: иначе пул соединений держит файл открытым,
            // и Cleanup не может удалить result-sqlite после разбора
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={sqlite};Pooling=False");
            conn.Open();

            var conf = new Dictionary<string, double> { ["best"] = 0.95, ["partial"] = 0.60, ["unreliable"] = 0.25 };
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT type, address, name, address2, name2, ratio, description FROM results ORDER BY ratio DESC";
                try
                {
                    using var rd = cmd.ExecuteReader();
                    while (rd.Read())
                    {
                        var type = Convert.ToString(rd["type"]) ?? "partial";
                        // Diaphora хранит ratio строкой '1.0000000' (с точкой):
                        // парсим строго инвариантно — Convert.ToDouble с текущей
                        // культурой на локалях с запятой даёт FormatException
                        // и весь разбор молча терялся
                        double.TryParse(Convert.ToString(rd["ratio"]), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var ratio);
                        res.MatchedFunctions.Add(new DiaphoraMatch(
                            "0x" + (long.TryParse(Convert.ToString(rd["address"]), System.Globalization.NumberStyles.HexNumber, null, out var a1) ? a1.ToString("X") : "0"),
                            Convert.ToString(rd["name"]) ?? "",
                            "0x" + (long.TryParse(Convert.ToString(rd["address2"]), System.Globalization.NumberStyles.HexNumber, null, out var a2) ? a2.ToString("X") : "0"),
                            Convert.ToString(rd["name2"]) ?? "",
                            ratio,
                            conf.TryGetValue(type, out var c) ? c : 0.50,
                            (Convert.ToString(rd["description"]) ?? "diaphora_auto").Trim(),
                            type));
                        var algo = (Convert.ToString(rd["description"]) ?? "diaphora_auto").Trim();
                        res.AlgorithmDistribution.TryGetValue(algo, out var cnt);
                        res.AlgorithmDistribution[algo] = cnt + 1;
                    }
                }
                catch { /* нет таблицы */ }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT type, address, name FROM unmatched";
                try
                {
                    using var rd = cmd.ExecuteReader();
                    while (rd.Read())
                    {
                        var a = "0x" + (long.TryParse(Convert.ToString(rd["address"]), System.Globalization.NumberStyles.HexNumber, null, out var av) ? av.ToString("X") : "0");
                        var entry = (Address: a, Name: Convert.ToString(rd["name"]) ?? "");
                        // В новых версиях Diaphora type — строка 'primary'/'secondary',
                        // в старых — число 1/2
                        var ut = (Convert.ToString(rd["type"]) ?? "").Trim();
                        if (ut == "1" || ut.Equals("primary", StringComparison.OrdinalIgnoreCase)) res.Unmatched1.Add(entry);
                        else res.Unmatched2.Add(entry);
                    }
                }
                catch { /* нет таблицы */ }
            }
        }
        catch { /* ошибка парсинга */ }
        return res;
    }

    private sealed record DiaphoraParseResult
    {
        public List<DiaphoraMatch> MatchedFunctions { get; } = new();
        public List<(string Address, string Name)> Unmatched1 { get; } = new();
        public List<(string Address, string Name)> Unmatched2 { get; } = new();
        public Dictionary<string, long> AlgorithmDistribution { get; } = new();
    }

    private sealed record DiaphoraMatch(string Address1, string Name1, string Address2, string Name2,
                                        double Similarity, double Confidence, string AlgorithmName, string MatchType)
    {
        public Dictionary<string, object?> ToDict() => new()
        {
            ["address1"] = Address1, ["name1"] = Name1,
            ["address2"] = Address2, ["name2"] = Name2,
            ["similarity"] = Math.Round(Similarity, 4), ["confidence"] = Confidence,
            ["algorithm_name"] = AlgorithmName, ["match_type"] = MatchType,
            ["nodes1"] = 0, ["nodes2"] = 0, ["source"] = "diaphora",
        };
    }

    // ─────────────────────────────────────────────────────────────────
    //  Обогащение .diff.json (imports, pseudocode, hexdump per pair)
    // ─────────────────────────────────────────────────────────────────

    private void EnrichDiffJson(string diffJson, string? primaryJson, string? secondaryJson)
    {
        var diffData = ReadJson(diffJson);
        if (diffData == null) return;

        var primImports = new HashSet<string>();
        var secImports = new HashSet<string>();
        var primFuncs = new Dictionary<string, Dictionary<string, object?>>();
        var secFuncs = new Dictionary<string, Dictionary<string, object?>>();

        foreach (var (jsonPath, impSet, funcDict) in new[]
                 {
                     (primaryJson, primImports, primFuncs),
                     (secondaryJson, secImports, secFuncs),
                 })
        {
            if (jsonPath == null || !File.Exists(jsonPath)) continue;
            var d = ReadJson(jsonPath);
            if (d == null) continue;
            if (d.TryGetValue("imports", out var imports) && imports is List<object?> impList)
                foreach (var imp in impList.OfType<Dictionary<string, object?>>())
                {
                    var n = Convert.ToString(imp.GetValue("name"))?.Trim();
                    if (!string.IsNullOrEmpty(n)) impSet.Add(n);
                }
            if (d.TryGetValue("functions", out var funcs) && funcs is List<object?> fnList)
                foreach (var fn in fnList.OfType<Dictionary<string, object?>>())
                {
                    var addr = Convert.ToString(fn.GetValue("start_ea"));
                    if (string.IsNullOrEmpty(addr)) continue;
                    funcDict[addr] = new Dictionary<string, object?>
                    {
                        ["name"] = Convert.ToString(fn.GetValue("name"))?.Trim() ?? "",
                        ["pseudocode"] = Convert.ToString(fn.GetValue("pseudocode")) ?? "",
                        ["hexdump"] = Convert.ToString(fn.GetValue("hexdump")) ?? "",
                        ["start_ea"] = addr,
                        ["insn_types"] = fn.GetValue("insn_types") ?? new Dictionary<string, object?>(),
                        ["callees"] = fn.GetValue("callees") ?? new List<object?>(),
                    };
                }
        }

        diffData["imports_only_in_primary"] = primImports.Except(secImports).OrderBy(x => x).ToList();
        diffData["imports_only_in_secondary"] = secImports.Except(primImports).OrderBy(x => x).ToList();

        var matches = diffData.TryGetValue("matched_functions", out var m) && m is List<object?> ml
            ? ml.OfType<Dictionary<string, object?>>().ToList() : new List<Dictionary<string, object?>>();

        // Индекс по именам: линейный FirstOrDefault на каждую пару давал O(n²)
        // на больших экспортах (тысячи функций × тысячи совпадений).
        var idx1 = BuildNameIndex(primFuncs);
        var idx2 = BuildNameIndex(secFuncs);

        var allT1 = new Dictionary<string, long>();
        var allT2 = new Dictionary<string, long>();
        foreach (var mf in matches)
        {
            var a1 = Convert.ToString(mf["address1"]) ?? "";
            var a2 = Convert.ToString(mf["address2"]) ?? "";
            var f1 = LookupFunc(primFuncs, idx1, a1, Convert.ToString(mf.GetValue("name1")));
            var f2 = LookupFunc(secFuncs, idx2, a2, Convert.ToString(mf.GetValue("name2")));

            mf["pseudocode1"] = f1?.GetValue("pseudocode") ?? "";
            mf["pseudocode2"] = f2?.GetValue("pseudocode") ?? "";

            // pseudocode diff rows
            mf["pseudocode_diff"] = ComputePseudocodeDiff(
                Convert.ToString(f1?.GetValue("pseudocode")) ?? "",
                Convert.ToString(f2?.GetValue("pseudocode")) ?? "");

            // insn types / callees
            if (f1 != null && f2 != null)
            {
                var it1 = f1["insn_types"] as Dictionary<string, object?> ?? new();
                var it2 = f2["insn_types"] as Dictionary<string, object?> ?? new();
                var mnes = it1.Keys.Union(it2.Keys).OrderBy(x => x, StringComparer.Ordinal).ToList();
                var insnDiff = new List<object?>();
                foreach (var mn in mnes)
                {
                    var c1 = it1.TryGetValue(mn, out var v1) ? Convert.ToInt64(v1) : 0;
                    var c2 = it2.TryGetValue(mn, out var v2) ? Convert.ToInt64(v2) : 0;
                    if (c1 != c2)
                        insnDiff.Add(new Dictionary<string, object?> { ["mnemonic"] = mn, ["count1"] = c1, ["count2"] = c2, ["diff"] = c2 - c1 });
                }
                mf["insn_type_diff"] = insnDiff;
                mf["insn_types1"] = it1;
                mf["insn_types2"] = it2;

                var c1set = (f1["callees"] as List<object?> ?? new()).Select(x => Convert.ToString(x) ?? "").ToHashSet();
                var c2set = (f2["callees"] as List<object?> ?? new()).Select(x => Convert.ToString(x) ?? "").ToHashSet();
                mf["callees_only1"] = c1set.Except(c2set).OrderBy(x => x).ToList();
                mf["callees_only2"] = c2set.Except(c1set).OrderBy(x => x).ToList();
                mf["callees_common"] = c1set.Intersect(c2set).OrderBy(x => x).ToList();
            }
            else
            {
                mf["insn_type_diff"] = new List<object?>();
                mf["insn_types1"] = new Dictionary<string, object?>();
                mf["insn_types2"] = new Dictionary<string, object?>();
                mf["callees_only1"] = new List<object?>();
                mf["callees_only2"] = new List<object?>();
                mf["callees_common"] = new List<object?>();
            }

            // global aggregate
            if (f1 != null && f1["insn_types"] is Dictionary<string, object?> t1)
                foreach (var (mn, cnt) in t1) AddTo(allT1, mn, Convert.ToInt64(cnt));
            if (f2 != null && f2["insn_types"] is Dictionary<string, object?> t2)
                foreach (var (mn, cnt) in t2) AddTo(allT2, mn, Convert.ToInt64(cnt));
        }

        var globalInsn = new List<object?>();
        foreach (var mn in allT1.Keys.Union(allT2.Keys).OrderBy(x => x, StringComparer.Ordinal))
        {
            var c1 = allT1.TryGetValue(mn, out var v1) ? v1 : 0;
            var c2 = allT2.TryGetValue(mn, out var v2) ? v2 : 0;
            globalInsn.Add(new Dictionary<string, object?> { ["mnemonic"] = mn, ["count1"] = c1, ["count2"] = c2, ["diff"] = c2 - c1 });
        }
        diffData["global_insn_diff"] = globalInsn;

        WriteJson(diffJson, diffData);
    }

    private static void AddTo(Dictionary<string, long> d, string key, long value)
        => d[key] = (d.TryGetValue(key, out var v) ? v : 0) + value;

    /// <summary>Индекс функций по имени (первая встретившаяся — как при линейном поиске).</summary>
    private static Dictionary<string, Dictionary<string, object?>> BuildNameIndex(
        Dictionary<string, Dictionary<string, object?>> funcs)
    {
        var idx = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var f in funcs.Values)
        {
            var nm = Convert.ToString(f.GetValue("name"))?.Trim();
            if (string.IsNullOrEmpty(nm)) continue;
            if (!idx.ContainsKey(nm)) idx[nm] = f;
            if (nm.StartsWith("sub_", StringComparison.Ordinal))
            {
                var bare = nm["sub_".Length..];
                if (!idx.ContainsKey(bare)) idx[bare] = f;
            }
        }
        return idx;
    }

    /// <summary>Функция по адресу, при промахе — по имени (напрямую и без префикса sub_).</summary>
    private static Dictionary<string, object?>? LookupFunc(
        Dictionary<string, Dictionary<string, object?>> funcs,
        Dictionary<string, Dictionary<string, object?>> nameIndex,
        string addr, string? name)
    {
        if (addr.Length > 0 && funcs.TryGetValue(addr, out var f)) return f;
        if (string.IsNullOrEmpty(name)) return null;
        var n = name.StartsWith("sub_", StringComparison.Ordinal) ? name["sub_".Length..] : name;
        return nameIndex.TryGetValue(n, out var r) ? r
            : nameIndex.TryGetValue(name, out var r2) ? r2 : null;
    }

    private static List<object?> ComputePseudocodeDiff(string l1, string l2)
    {
        var lines1 = l1.Replace("\r\n", "\n").Split('\n').ToList();
        var lines2 = l2.Replace("\r\n", "\n").Split('\n').ToList();
        if (lines1.Count == 1 && lines1[0] == "") lines1.Clear();
        if (lines2.Count == 1 && lines2[0] == "") lines2.Clear();

        var rows = new List<object?>();
        // Простой LCS-подобный diff (аналог difflib.SequenceMatcher opcodes)
        foreach (var (tag, i1, i2, j1, j2) in SimpleDiff(lines1, lines2))
        {
            if (tag == "equal")
                for (int k = i1; k < i2 && (j1 + (k - i1)) < lines2.Count; k++)
                    rows.Add(new Dictionary<string, object?> { ["type"] = "equal", ["left"] = lines1[k], ["right"] = lines2[j1 + (k - i1)] });
            else if (tag == "delete")
                for (int k = i1; k < i2; k++)
                    rows.Add(new Dictionary<string, object?> { ["type"] = "removed", ["left"] = lines1[k], ["right"] = "" });
            else if (tag == "insert")
                for (int k = j1; k < j2; k++)
                    rows.Add(new Dictionary<string, object?> { ["type"] = "added", ["left"] = "", ["right"] = lines2[k] });
            else if (tag == "replace")
            {
                for (int k = i1; k < i2; k++)
                    rows.Add(new Dictionary<string, object?> { ["type"] = "removed", ["left"] = lines1[k], ["right"] = "" });
                for (int k = j1; k < j2; k++)
                    rows.Add(new Dictionary<string, object?> { ["type"] = "added", ["left"] = "", ["right"] = lines2[k] });
            }
        }
        return rows;
    }

    /// <summary>Простой последовательный diff (упрощённый SequenceMatcher: equal/delete/insert/replace).</summary>
    private static IEnumerable<(string Tag, int I1, int I2, int J1, int J2)> SimpleDiff(List<string> a, List<string> b)
    {
        int i = 0, j = 0;
        var ops = new List<(string, int, int, int, int)>();
        while (i < a.Count || j < b.Count)
        {
            if (i < a.Count && j < b.Count && a[i] == b[j])
            {
                int s = i, sj = j;
                while (i < a.Count && j < b.Count && a[i] == b[j]) { i++; j++; }
                // J1 — начало равного участка в b: у обоих файлов участок одной длины.
                ops.Add(("equal", s, i, sj, j));
            }
            else if (i < a.Count && j < b.Count)
            {
                int s1 = i, s2 = j;
                while (i < a.Count && j < b.Count && a[i] != b[j]) { i++; j++; }
                ops.Add(("replace", s1, i, s2, j));
            }
            else if (i < a.Count)
            {
                int s = i; while (i < a.Count) i++;
                ops.Add(("delete", s, i, j, j));
            }
            else
            {
                int s = j; while (j < b.Count) j++;
                ops.Add(("insert", i, i, s, j));
            }
        }
        return ops;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Hexdump diff (аналог _compute_hexdump_diff)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Побайтовое равенство двух файлов (размер + содержимое, потоково).</summary>
    private static bool FilesEqual(string path1, string path2)
    {
        try
        {
            var fi1 = new FileInfo(path1);
            var fi2 = new FileInfo(path2);
            if (fi1.Length != fi2.Length) return false;
            using var s1 = fi1.OpenRead();
            using var s2 = fi2.OpenRead();
            var buf1 = new byte[81920];
            var buf2 = new byte[81920];
            int n1, n2;
            do
            {
                n1 = s1.Read(buf1, 0, buf1.Length);
                n2 = s2.Read(buf2, 0, buf2.Length);
                if (n1 != n2) return false;
                for (int i = 0; i < n1; i++)
                    if (buf1[i] != buf2[i]) return false;
            } while (n1 > 0);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Заглушка diff.json для побайтно идентичной пары: без запуска
    /// BinDiff/Diaphora — сходство полная и по определению.</summary>
    private void WriteIdenticalJson(DiffPair p)
    {
        var jsonOutput = Path.Combine(OutputDir, $"{p.Stem}.diff.json");
        var orig1 = FindOriginalBinary(p.Primary);
        var orig2 = FindOriginalBinary(p.Secondary);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var data = new Dictionary<string, object?>
        {
            ["primary"] = p.Primary, ["secondary"] = p.Secondary,
            ["real_primary"] = orig1 ?? p.Primary, ["real_secondary"] = orig2 ?? p.Secondary,
            ["similarity"] = 1.0, ["confidence"] = 1.0,
            ["hexdump_similarity"] = 1.0,
            ["description"] = "", ["version"] = "", ["created"] = now, ["modified"] = now,
            ["file1"] = new Dictionary<string, object?>(), ["file2"] = new Dictionary<string, object?>(),
            ["matched_functions"] = new List<object?>(),
            ["matched_summary"] = new Dictionary<string, object?>
                { ["total"] = 0, ["bindiff_only"] = 0, ["diaphora_only"] = 0, ["both"] = 0 },
            ["unmatched_functions1"] = new List<object?>(),
            ["unmatched_functions2"] = new List<object?>(),
            ["total_functions1"] = 0, ["total_functions2"] = 0,
            ["total_matched"] = 0, ["total_unmatched"] = 0,
            ["identical"] = true,
            ["add_analysis_done"] = true,
            ["error"] = null,
        };
        if (orig1 != null && orig2 != null)
        {
            data["global_hex_diff"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["name1"] = Path.GetFileName(orig1), ["path1"] = orig1,
                    ["name2"] = Path.GetFileName(orig2), ["path2"] = orig2,
                    ["hexdump_similarity"] = 1.0,
                }
            };
        }
        WriteJson(jsonOutput, data);
    }


    private static string? FindOriginalBinary(string i64Path)
    {
        var stem = Path.GetFileNameWithoutExtension(i64Path); // uprngctl64.exe.i64 -> uprngctl64.exe
        var dir = Path.GetDirectoryName(i64Path)!;
        var cand = Path.Combine(dir, stem);
        if (File.Exists(cand)) return cand;
        foreach (var ext in new[] { ".exe", ".dll", ".bin", ".sys", ".elf", ".so", ".o", ".out", ".wasm", ".pyc", ".class", ".jar", ".apk", ".dex" })
        {
            cand = Path.Combine(dir, stem + ext);
            if (File.Exists(cand)) return cand;
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Вспомогательное
    // ─────────────────────────────────────────────────────────────────

    private ProcessStartInfo NewIdatPsi()
    {
        var psi = new ProcessStartInfo
        {
            FileName = IdatPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("-A");
        return psi;
    }

    internal record ProcResult(int ExitCode, string Stdout, string Stderr);

    private async Task<ProcResult> RunProcAsync(ProcessStartInfo psi, string relKey, CancellationToken ct)
    {
        using var proc = Process.Start(psi);
        if (proc == null)
        {
            PairProcessStarted?.Invoke(relKey, -1);
            return new ProcResult(-1, "", "Не удалось запустить процесс");
        }
        var pid = proc.Id;
        PairProcessStarted?.Invoke(relKey, pid);
        ProcessRegistry.Register(pid);
        JobObject.Current?.Assign(proc);

        var so = proc.StandardOutput.ReadToEndAsync();
        var se = proc.StandardError.ReadToEndAsync();
        try
        {
            try
            {
                // Ждём полного завершения: большие модули могут считаться часами.
                await proc.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Отмена: гасим дерево процессов, иначе idat.exe остаётся висеть.
                try { proc.Kill(entireProcessTree: true); } catch { /* уже завершён */ }
                throw;
            }
            await Task.WhenAll(so, se);
            return new ProcResult(proc.ExitCode, await so, await se);
        }
        finally
        {
            ProcessRegistry.Unregister(pid);
        }
    }

    /// <summary>
    /// Ожидание процесса Diaphora с двумя уровнями контроля.
    /// 1) Живость: выход процесса или отмена завершают ожидание; kill — при отмене
    ///    или зависании (ни ОЗУ, ни БД не менялись дольше StallTimeout).
    /// 2) Пульс (раз в PulseInterval): ОЗУ процесса (WorkingSet64), при watchDbPath —
    ///    ещё и суммарный размер БД (*.sqlite + *-wal + *-shm — диафора пишет в WAL,
    ///    и рост уходит в -wal), плюс прогресс экспорта из stdout Diaphora.
    /// Живой процесс может сколько угодно не менять ОЗУ (или БД) — зависанием
    /// считается только одновременное затишье обоих сигналов.
    /// </summary>
    private async Task<ProcResult> RunProcWithPulseAsync(ProcessStartInfo psi, string relKey, string phaseLabel,
        string? watchDbPath, CancellationToken ct)
    {
        using var proc = Process.Start(psi);
        if (proc == null)
        {
            PairProcessStarted?.Invoke(relKey, -1);
            return new ProcResult(-1, "", "Не удалось запустить процесс");
        }
        var pid = proc.Id;
        PairProcessStarted?.Invoke(relKey, pid);
        ProcessRegistry.Register(pid);
        JobObject.Current?.Assign(proc);

        // stdout читаем построчно: из него же берём прогресс экспорта Diaphora.
        var progress = new ExportProgress();
        var so = ReadStdoutWithProgressAsync(proc, progress);
        var se = proc.StandardError.ReadToEndAsync();
        var started = Stopwatch.StartNew();
        var exitTask = proc.WaitForExitAsync(ct);
        long lastSize = watchDbPath != null ? DiaphoraDbSize(watchDbPath) : -1;
        long lastWs = ProcWorkingSet(proc);
        var lastActivity = TimeSpan.Zero;
        try
        {
            while (true)
            {
                var done = await Task.WhenAny(exitTask, Task.Delay(PulseInterval, ct));
                if (done == exitTask) break;
                if (ct.IsCancellationRequested)
                {
                    // Отмена: гасим дерево процессов, иначе экспорт Diaphora остаётся висеть.
                    try { proc.Kill(entireProcessTree: true); } catch { }
                    throw new OperationCanceledException(ct);
                }
                if (proc.HasExited) break;

                var elapsed = started.Elapsed;
                var ws = ProcWorkingSet(proc);
                var size = watchDbPath != null ? DiaphoraDbSize(watchDbPath) : -1;
                if (ws != lastWs || (watchDbPath != null && size != lastSize))
                    lastActivity = elapsed;
                lastWs = ws;
                if (watchDbPath != null) lastSize = size;

                if (elapsed - lastActivity >= StallTimeout)
                {
                    ErrorOccurred?.Invoke(
                        $"Diaphora {relKey}: ни ОЗУ, ни БД {Path.GetFileName(watchDbPath)} " +
                        $"не менялись уже {StallTimeout.TotalHours:0} ч — процесс завис, убиваю (PID={proc.Id}).");
                    try { proc.Kill(entireProcessTree: true); } catch { }
                    await Task.WhenAll(so, se);
                    return new ProcResult(-1, await so, await se);
                }

                // Пульс: время, ОЗУ, (для экспорта) БД и процент из stdout Diaphora.
                var status = $"{phaseLabel} {(int)elapsed.TotalMinutes} мин, ОЗУ {ws / 1024 / 1024} ГБ";
                if (watchDbPath != null)
                    status += $", БД {size / 1024 / 1024} МБ";
                if (progress.Total > 0 && progress.Done > 0)
                {
                    var pct = Math.Min(100, progress.Done * 100 / progress.Total);
                    status += $", {pct}%";
                    if (progress.Remaining is { } rem)
                        status += $" (осталось ~{(int)rem.TotalMinutes} мин)";
                }
                PairStatus?.Invoke(relKey, "diaphora", status);
            }
        }
        finally
        {
            if (!exitTask.IsCompleted) exitTask.Ignore();
            ProcessRegistry.Unregister(pid);
        }
        await exitTask;
        await Task.WhenAll(so, se);
        return new ProcResult(proc.ExitCode, await so, await se);
    }

    /// <summary>Рабочее множество процесса в байтах; 0 — процесс уже завершился.</summary>
    private static long ProcWorkingSet(Process proc)
    {
        try { proc.Refresh(); return proc.WorkingSet64; }
        catch { return 0; }
    }

    /// <summary>Читает stdout процесса построчно и обновляет прогресс экспорта
    /// по строкам Diaphora «Exported N function(s) out of M total … remaining time ~H:MM:SS».</summary>
    private async Task<string> ReadStdoutWithProgressAsync(Process proc, ExportProgress progress)
    {
        var sb = new StringBuilder();
        var buffer = new char[256];
        try
        {
            using var reader = proc.StandardOutput;
            int n;
            while ((n = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                sb.Append(buffer, 0, n);
                ParseExportProgress(sb.ToString(), progress);
                // Держим хвост, достаточный для обеих regex-строк, — весь лог не копим.
                if (sb.Length > 4096) sb.Remove(0, sb.Length - 1024);
            }
        }
        catch { /* процесс завершён или поток закрыт */ }
        return sb.ToString();
    }

    private static void ParseExportProgress(string chunk, ExportProgress progress)
    {
        var m = ExportedRe.Match(chunk);
        if (!m.Success) return;
        if (int.TryParse(m.Groups[1].Value, out var done) &&
            int.TryParse(m.Groups[2].Value, out var total) && total > 0)
        {
            progress.Done = done;
            progress.Total = total;
        }
        var r = RemainingRe.Match(chunk);
        if (r.Success && int.TryParse(r.Groups[1].Value, out var h) &&
            int.TryParse(r.Groups[2].Value, out var min) &&
            int.TryParse(r.Groups[3].Value, out var s))
        {
            progress.Remaining = new TimeSpan(h, min, s);
        }
    }

    /// <summary>Суммарный размер БД диафоры: сам файл + WAL/SHM (рост идёт в *-wal).</summary>
    private static long DiaphoraDbSize(string dbPath)
    {
        long total = 0;
        foreach (var p in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
            try { if (File.Exists(p)) total += new FileInfo(p).Length; }
            catch { /* файл занят — берём что есть */ }
        return total;
    }

    public void Dispose() => _cts?.Cancel();
}
