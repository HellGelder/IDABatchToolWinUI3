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
    private readonly bool _exportOnly;
    private CancellationTokenSource? _cts;
    private Task? _task;

    /// <summary>(фаза "analysis"|"export")</summary>
    public event Action<string>? PhaseChanged;
    /// <summary>(имя цели, PID процесса IDA, managed thread id) - сразу после запуска idat.exe.</summary>
    public event Action<string, int, int>? ProcessStarted;
    public event Action<string, int, int>? AnalysisProgress;
    public event Action<string>? AnalysisFileStarted;
    public event Action<string, bool>? AnalysisFileCompleted;
    public event Action<string, int, int>? ExportProgress;
    public event Action<string>? ExportFileStarted;
    public event Action<string, bool>? ExportFileCompleted;
    public event Action<string>? ErrorOccurred;
    /// <summary>(имя файла, текст пульса процесса) - для строки статуса/диспетчера.</summary>
    public event Action<string, string>? PulseUpdated;
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
        _exportOnly = exportOnly;
        _ = deleteJson; // параметр оставлен для совместимости вызовов: JSON удаляет HTML-воркер
        _scriptsDir = AppConstants.ScriptsDir;
        _runner = new IdaRunner(idatPath, maxWorkers)
        {
            ProgressCallback = (name, cur, total) => { },
            FileStartCallback = _ => { },
            FileDoneCallback = (_, _) => { },
        };
        _runner.ProcessStartedCallback = (name, pid, tid) => ProcessStarted?.Invoke(name, pid, tid);
        _runner.ErrorCallback = msg => ErrorOccurred?.Invoke(msg);
        _runner.PulseCallback = (name, text) => PulseUpdated?.Invoke(name, text);
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
        var candidates = new List<(string Source, string BasePath)>();

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
            catch (OperationCanceledException)
            {
                results = new Dictionary<string, bool>();
            }
            catch (Exception e)
            {
                ErrorOccurred?.Invoke($"Критическая ошибка при анализе: {e.Message}");
                results = _files.Distinct().ToDictionary(f => f, _ => false);
            }

            foreach (var (file, ok) in results)
            {
                if (!ok) continue;
                string? reason = null;
                var basePath = ArtifactValidator.ResolveBasePath(file);
                if (basePath == null || !ArtifactValidator.IsValidIdaDatabase(basePath, out reason))
                {
                    ErrorOccurred?.Invoke(
                        $"База не готова для {Path.GetFileName(file)}: {reason ?? "файл базы не найден"}");
                    continue;
                }
                candidates.Add((file, basePath));
            }
        }
        else
        {
            // Режим export_only: работаем только с существующими базами.
            foreach (var file in _files)
            {
                string? reason = null;
                var basePath = ArtifactValidator.ResolveBasePath(file);
                if (basePath == null || !ArtifactValidator.IsValidIdaDatabase(basePath, out reason))
                {
                    ErrorOccurred?.Invoke(
                        $"База данных не найдена для {Path.GetFileName(file)}: {reason ?? "файл базы не найден"}");
                    continue;
                }
                candidates.Add((file, basePath));
            }
        }

        if (candidates.Count == 0)
        {
            ErrorOccurred?.Invoke(_exportOnly
                ? "Нет доступных баз данных для экспорта."
                : "Нет успешно проанализированных файлов для экспорта.");
            Finished?.Invoke(0, _files.Count);
            return;
        }

        // Фаза 2: экспорт в JSON - успех только по валидному JSON, а не по факту
        // существования базы: старый .i64 мог остаться от прошлого запуска.
        PhaseChanged?.Invoke("export");

        // Прошлый JSON удаляем до любых проверок: если экспорт не состоится,
        // старый файл не должен выглядеть результатом текущего прогона.
        foreach (var (_, basePath) in candidates)
            SafeDelete(ArtifactValidator.ExpectedExportJsonPath(basePath));

        var script = Path.Combine(_scriptsDir, "export_data.py");
        if (!File.Exists(script))
        {
            ErrorOccurred?.Invoke($"Скрипт экспорта не найден: {script}");
            Finished?.Invoke(0, _files.Count);
            return;
        }

        var args = new Dictionary<string, string>();
        if (_pseudocode) args["pseudocode"] = "1";

        _runner.ProgressCallback = (name, cur, total) => ExportProgress?.Invoke(DisplayName(name), cur, total);
        _runner.FileStartCallback = n => ExportFileStarted?.Invoke(DisplayName(n));
        // Результат экспорта подтверждаем сами после проверки JSON, а не по факту
        // завершения процесса: pythonw может выйти с кодом 0, не записав результат.
        _runner.FileDoneCallback = (_, _) => { };

        Dictionary<string, bool> exportResults;
        try
        {
            exportResults = await _runner.RunScriptOnBatchAsync(
                candidates.Select(c => c.BasePath).ToList(), script, null, args, ct);
        }
        catch (OperationCanceledException)
        {
            exportResults = new Dictionary<string, bool>();
        }
        catch (Exception e)
        {
            ErrorOccurred?.Invoke($"Критическая ошибка при экспорте: {e.Message}");
            exportResults = new Dictionary<string, bool>();
        }

        var succeeded = 0;
        foreach (var (_, basePath) in candidates)
        {
            var jsonPath = ArtifactValidator.ExpectedExportJsonPath(basePath);
            var processOk = exportResults.TryGetValue(basePath, out var ok) && ok;
            string? reason = null;
            var valid = processOk && ArtifactValidator.IsValidExportJson(jsonPath, out reason);

            if (valid)
            {
                succeeded++;
            }
            else
            {
                var message = processOk ? reason : "процесс экспорта завершился с ошибкой";
                ErrorOccurred?.Invoke($"Экспорт {Path.GetFileName(basePath)}: {message ?? "неизвестная ошибка"}");
            }
            ExportFileCompleted?.Invoke(Path.GetFileName(basePath), valid);
        }

        Finished?.Invoke(succeeded, _files.Count);
    }

    /// <summary>Имя базы для UI-событий - без служебного суффикса .i64/.idb.</summary>
    private static string DisplayName(string fileName)
    {
        if (fileName.EndsWith(".i64", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".idb", StringComparison.OrdinalIgnoreCase))
            return fileName[..^4];
        return fileName;
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* файл занят - экспорт всё равно перезапишет его */ }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _runner.Dispose();
    }
}
