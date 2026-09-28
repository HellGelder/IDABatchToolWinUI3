using IDABatchToolWinUI.Services;

namespace IDABatchToolWinUI.Workers;

/// <summary>
/// Контракт воркера анализа/экспорта. События гарантированно приходят на UI-поток
/// (страница диспатчит их через DispatcherQueue).
/// </summary>
public sealed class AnalysisWorker : IDisposable
{
    private readonly IReadOnlyList<string> _files;
    private readonly IdaRunner _runner;
    private readonly string _scriptsDir;
    private readonly bool _cleanup;
    private readonly bool _tempCleanup;
    private readonly bool _pseudocode;
    private readonly bool _deleteJson; // не используется в фазе анализа, оставлен для симметрии
    private readonly bool _exportOnly;
    private CancellationTokenSource? _cts;
    private Task? _task;

    /// <summary>(фаза "analysis"|"export")</summary>
    public event Action<string>? PhaseChanged;
    /// <summary>(имя цели, PID процесса IDA, managed thread id) — сразу после запуска idat.exe.</summary>
    public event Action<string, int, int>? ProcessStarted;
    public event Action<string, int, int>? AnalysisProgress;
    public event Action<string>? AnalysisFileStarted;
    public event Action<string, bool>? AnalysisFileCompleted;
    public event Action<string, int, int>? ExportProgress;
    public event Action<string>? ExportFileStarted;
    public event Action<string, bool>? ExportFileCompleted;
    public event Action<string>? ErrorOccurred;
    public event Action<int, int>? Finished;

    public AnalysisWorker(
        IReadOnlyList<string> files,
        string idatPath,
        int maxWorkers,
        string? outputDir,
        bool cleanup,
        bool tempCleanup,
        bool pseudocode,
        bool deleteJson,
        bool exportOnly)
    {
        _files = files;
        _cleanup = cleanup;
        _tempCleanup = tempCleanup;
        _pseudocode = pseudocode;
        _deleteJson = deleteJson;
        _exportOnly = exportOnly;
        _scriptsDir = AppConstants.ScriptsDir;
        _runner = new IdaRunner(idatPath, maxWorkers)
        {
            ProgressCallback = (name, cur, total) => { },
            FileStartCallback = _ => { },
            FileDoneCallback = (_, _) => { },
        };
        _runner.ProcessStartedCallback = (name, pid, tid) => ProcessStarted?.Invoke(name, pid, tid);
    }

    public void Start()
    {
        if (_task != null) return;
        _cts = new CancellationTokenSource();
        _task = Task.Run(RunAsync);
    }

    public void Cancel() => _cts?.Cancel();

    private async Task RunAsync()
    {
        var ct = _cts!.Token;
        List<string> succeededFiles;

        if (!_exportOnly)
        {
            // Фаза 1: анализ файлов
            PhaseChanged?.Invoke("analysis");
            _runner.ProgressCallback = (name, cur, total) => AnalysisProgress?.Invoke(name, cur, total);
            _runner.FileStartCallback = n => AnalysisFileStarted?.Invoke(n);
            _runner.FileDoneCallback = (n, ok) => AnalysisFileCompleted?.Invoke(n, ok);

            Dictionary<string, bool> results;
            try
            {
                results = await _runner.AnalyzeBatchAsync(_files, null, _cleanup, _tempCleanup, ct);
            }
            catch (Exception e)
            {
                ErrorOccurred?.Invoke($"Критическая ошибка при анализе: {e.Message}");
                results = _files.ToDictionary(f => f, _ => false);
            }
            succeededFiles = results.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        }
        else
        {
            // Режим export_only: собираем существующие базы
            PhaseChanged?.Invoke("export");
            succeededFiles = new List<string>();
            foreach (var f in _files)
            {
                var dir = Path.GetDirectoryName(f)!;
                var i64 = Path.Combine(dir, Path.GetFileName(f) + ".i64");
                if (File.Exists(i64)) succeededFiles.Add(i64);
                else
                {
                    var idb = Path.Combine(dir, Path.GetFileName(f) + ".idb");
                    if (File.Exists(idb)) succeededFiles.Add(idb);
                    else ErrorOccurred?.Invoke($"База данных не найдена для {Path.GetFileName(f)} (режим export_only).");
                }
            }
            if (succeededFiles.Count == 0)
            {
                Finished?.Invoke(0, _files.Count);
                return;
            }
        }

        // Фаза 2: экспорт в JSON
        if (succeededFiles.Count > 0 && !ct.IsCancellationRequested)
        {
            PhaseChanged?.Invoke("export");
            var script = Path.Combine(_scriptsDir, "export_data.py");
            if (!File.Exists(script))
            {
                ErrorOccurred?.Invoke($"Скрипт экспорта не найден: {script}");
                Finished?.Invoke(0, _files.Count);
                return;
            }

            var args = new Dictionary<string, string>();
            if (_pseudocode) args["pseudocode"] = "1";

            _runner.ProgressCallback = (name, cur, total) => ExportProgress?.Invoke(name, cur, total);
            _runner.FileStartCallback = n => ExportFileStarted?.Invoke(n);
            _runner.FileDoneCallback = (n, ok) => ExportFileCompleted?.Invoke(n, ok);

            var exportResults = await _runner.RunScriptOnBatchAsync(succeededFiles, script, null, args, ct);
            foreach (var (idb, ok) in exportResults)
                if (!ok) ErrorOccurred?.Invoke($"Ошибка экспорта для {Path.GetFileName(idb)}");
        }
        else
        {
            if (!_exportOnly) ErrorOccurred?.Invoke("Нет успешно проанализированных файлов для экспорта.");
            else ErrorOccurred?.Invoke("Нет доступных баз данных для экспорта.");
        }

        // Успешно обработанные исходные файлы — те, у которых появился .i64
        int successOriginal = 0;
        foreach (var f in _files)
        {
            var dir = Path.GetDirectoryName(f)!;
            if (File.Exists(Path.Combine(dir, Path.GetFileName(f) + ".i64"))) successOriginal++;
        }
        Finished?.Invoke(successOriginal, _files.Count);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _runner.Dispose();
    }
}