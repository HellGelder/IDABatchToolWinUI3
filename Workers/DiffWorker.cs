using System.Diagnostics;
using System.Text;
using IDABatchToolWinUI.Diff;
using static IDABatchToolWinUI.Diff.DiffJson;
using IDABatchToolWinUI.Models;
using IDABatchToolWinUI.Services;

namespace IDABatchToolWinUI.Workers;

/// <summary>
/// Параллельное сравнение пар с помощью BinDiff + Diaphora - перенос
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

    /// <summary>Единый запуск внешних процессов diff-конвейера (реестр PID, job, пульс Diaphora).</summary>
    private readonly DiffProcessRunner _proc = new();
    private readonly BinDiffService _binDiff;
    private readonly DiaphoraService _diaphora;
    private readonly ExportJsonService _exportJson;
    private readonly DiffPassRunner _pass;
    private readonly DiffPairProcessor _processor;
    private readonly AddAnalysisRunner _addAnalysis;

    public IReadOnlyList<DiffPair> Pairs { get; }
    public string IdatPath { get; }
    public string BindiffPath { get; }
    public string OutputDir { get; }
    public string Engine { get; }         // bindiff | diaphora | both
    public string LeftDir { get; }
    public string RightDir { get; }
    public string? AddOutputDir { get; }
    public int MaxWorkers { get; }

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
        _proc.PairProcessStarted = (k, pid) => PairProcessStarted?.Invoke(k, pid);
        _proc.PairStatus = (k, eng, st) => PairStatus?.Invoke(k, eng, st);
        _proc.ErrorOccurred = e => ErrorOccurred?.Invoke(e);
        _binDiff = new BinDiffService(_proc, idatPath, bindiffPath, e => ErrorOccurred?.Invoke(e));
        _diaphora = new DiaphoraService(_proc, idatPath, DiaphoraPath, e => ErrorOccurred?.Invoke(e));
        _exportJson = new ExportJsonService(_proc, idatPath, ExportScript);
        _pass = new DiffPassRunner(engine, MaxWorkers,
            (k, eng, st) => PairStatus?.Invoke(k, eng, st),
            (stage, cur, tot, f) => StageFile?.Invoke(stage, cur, tot, f),
            e => ErrorOccurred?.Invoke(e));
        _processor = new DiffPairProcessor(outputDir, DiaphoraPath, _binDiff, _diaphora, _exportJson,
            e => ErrorOccurred?.Invoke(e));
        _addAnalysis = new AddAnalysisRunner(outputDir, addOutputDir, leftDir, rightDir, DiaphoraPath,
            _diaphora, _exportJson, _pass,
            (stage, st) => StageChanged?.Invoke(stage, st), e => ErrorOccurred?.Invoke(e));
        _addAnalysis.StageFile = (stage, cur, tot, f) => StageFile?.Invoke(stage, cur, tot, f);
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
        // Страховка: любое исключение из фаз не должно терять Finished -
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
        // их сходство полная и по определению - BinDiff/Diaphora там не нужны.
        var identical = new HashSet<string>();
        foreach (var p in all)
        {
            var orig1 = IdenticalPairsService.FindOriginalBinary(p.Primary);
            var orig2 = IdenticalPairsService.FindOriginalBinary(p.Secondary);
            if (orig1 == null || orig2 == null) continue;
            if (IdenticalPairsService.FilesEqual(orig1, orig2)) identical.Add(p.Stem);
        }
        if (identical.Count > 0)
            ErrorOccurred?.Invoke($"Идентичных пар (побайтно): {identical.Count} - анализ не запускается, отчёт отмечен «Модули идентичны».");

        var work = all.Where(p => !identical.Contains(p.Stem)).ToList();
        _outcomes = new OutcomeTracker(all.Select(p => p.Stem));
        _pass.Outcomes = _outcomes;

        // Этапы выполнения: счётчик «шагов» не ведём - GUI показывает этапы
        // и статусы обработки каждого файла.
        var phases = new List<(string stage, string statusText, List<DiffPair> pairs, Func<DiffPair, Task<bool>> process)>();
        if (useBindiff)
            phases.Add(("BinDiff", "Экспорт из БД", work, p => _processor.ProcessBindiffPairAsync(p, ct)));
        if (useDiaphora)
            phases.Add(("Diaphora", "Экспорт из БД", work, p => _processor.ProcessDiaphoraPairAsync(p, ct)));
        phases.Add(("Пост-анализ", "Пост-анализ", work, p => _processor.ProcessPostPairAsync(p, ct)));

        foreach (var (stage, statusText, pairs, process) in phases)
        {
            StageChanged?.Invoke(stage, "started");
            await _pass.RunPass(stage, pairs, pairs.Count, null, statusText, process, ct);
            if (ct.IsCancellationRequested) { Abort(); return; }
            StageChanged?.Invoke(stage, "done");
        }

        // Идентичным парам - заглушка без запуска анализаторов.
        foreach (var p in all.Where(p => identical.Contains(p.Stem)))
        {
            IdenticalPairsService.WriteIdenticalJson(p, OutputDir);
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

        // Доанализ для engine=bindiff - только при наличии пар <99%, ещё не прошедших доанализ
        if (Engine == "bindiff" && !string.IsNullOrEmpty(AddOutputDir))
        {
            if (_addAnalysis.CountLowSimilarityPairs(all) > 0)
            {
                await _addAnalysis.RunAsync(all, ct);
                if (ct.IsCancellationRequested) { Abort(); return; }
            }
            else
            {
                ErrorOccurred?.Invoke(
                    "Доанализ не запускался: нет пар, требующих доанализа " +
                    "(similarity ≥ 99%, доанализ уже успешно завершён или в модуле 0 функций).");
            }
        }

        Finished?.Invoke(_outcomes?.Succeeded ?? 0, all.Count);
    }

    private void Abort()
    {
        Finished?.Invoke(_outcomes?.Succeeded ?? 0, Pairs.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    //  HTML-отчёты
    // ─────────────────────────────────────────────────────────────────

    private async Task GenerateReportsAsync(CancellationToken ct)
    {
        try
        {
            var reportsDir = Path.Combine(OutputDir, AppConstants.ReportsDirName);
            Directory.CreateDirectory(reportsDir);
            var jsonDir = OutputDir;
            using var worker = new HtmlGenWorker("diff", deleteJson: false, reuseCache: false, platform: "Windows");
            worker.ProgressUpdated += (cur, tot, msg) => StageFile?.Invoke("Генерация HTML", cur, tot, msg);
            worker.ErrorOccurred += e => ErrorOccurred?.Invoke(e);
            // Отмена diff-воркера должна останавливать и Python-мост (CANCEL в stdin).
            using var cancelRegistration = ct.Register(worker.Cancel);

            var jsonFiles = Directory.GetFiles(jsonDir, "*.diff.json").ToList();
            if (jsonFiles.Count == 0) return;
            await worker.RunAsync(
                inputDir: LeftDir, reportsDir: reportsDir, jsonDir: jsonDir,
                leftDir: LeftDir, rightDir: RightDir, manpagesDb: null,
                jsonPaths: jsonFiles);
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

    // ─────────────────────────────────────────────────────────────────
    //  Вспомогательное
    // ─────────────────────────────────────────────────────────────────

    public void Dispose() => _cts?.Cancel();
}
