using System.Diagnostics;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Пакетный запуск IDA Pro — аналог ida_batch_tool/ida/runner.py.
/// Параллельный анализ файлов (создание .i64) и выполнение скриптов на готовых базах.
/// Прогресс и статусы отдаются через колбэки; отмена — через CancellationToken.
/// </summary>
public sealed class IdaRunner : IDisposable
{
    public int MaxWorkers { get; }
    public string IdatPath { get; }

    // Колбэки: (имя файла, обработано, всего)
    public Action<string, int, int>? ProgressCallback { get; set; }
    public Action<string>? FileStartCallback { get; set; }
    public Action<string, bool>? FileDoneCallback { get; set; }

    /// <summary>(имя цели, PID процесса IDA, managed thread id) — вызывается сразу после запуска процесса.</summary>
    public Action<string, int, int>? ProcessStartedCallback { get; set; }

    /// <summary>Причина неудачи процесса — для журнала и UI (код выхода, хвост stderr).</summary>
    public Action<string>? ErrorCallback { get; set; }

    /// <summary>(имя файла, текст пульса) — для строки статуса и диспетчера анализа.</summary>
    public Action<string, string>? PulseCallback { get; set; }

    public IdaRunner(string idatPath, int maxWorkers)
    {
        IdatPath = idatPath;
        MaxWorkers = Math.Max(1, maxWorkers);
    }

    /// <summary>Анализ файлов пакетом; после — очистка временных файлов. Возвращает результаты.</summary>
    public async Task<Dictionary<string, bool>> AnalyzeBatchAsync(
        IReadOnlyList<string> files, string? outputDir,
        bool cleanup, bool tempCleanup, CancellationToken ct)
    {
        var results = await RunInParallelAsync(files, outputDir, ct);
        if (ct.IsCancellationRequested) return results;

        if (cleanup || tempCleanup)
        {
            foreach (var (f, ok) in results)
            {
                if (!ok) continue;
                ArtifactCleanup.DeleteKnownArtifacts(f, cleanup, tempCleanup);
            }
        }
        return results;
    }

    /// <summary>Выполнение скрипта на готовых базах .i64/.idb пакетом.</summary>
    public async Task<Dictionary<string, bool>> RunScriptOnBatchAsync(
        IReadOnlyList<string> idbFiles, string scriptPath,
        string? outputDir, Dictionary<string, string>? scriptArgs, CancellationToken ct)
    {
        var results = new Dictionary<string, bool>();
        var total = idbFiles.Count;
        var completed = 0;

        using var sem = new SemaphoreSlim(MaxWorkers);
        var tasks = new List<Task>();
        foreach (var f in idbFiles)
        {
            if (ct.IsCancellationRequested) break;
            // Без ct в WaitAsync: при отмене исключение не рвёт цикл (см. RunInParallelAsync).
            await sem.WaitAsync(CancellationToken.None);
            var file = f;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var ok = await RunScriptOnBaseAsync(file, scriptPath, scriptArgs, ct);
                    lock (results)
                    {
                        results[file] = ok;
                        completed++;
                        // Вход — либо исходный файл (экспорт после анализа:
                        // добавляем .i64), либо готовая база .i64/.idb
                        // (export_only: имя уже полное, ничего не добавляем).
                        var name = Path.GetFileName(file);
                        if (!name.EndsWith(".i64", StringComparison.OrdinalIgnoreCase)
                            && !name.EndsWith(".idb", StringComparison.OrdinalIgnoreCase))
                            name += ".i64";
                        ProgressCallback?.Invoke(name, completed, total);
                        FileDoneCallback?.Invoke(name, ok);
                    }
                }
                finally { sem.Release(); }
            }, CancellationToken.None));
        }
        try { await Task.WhenAll(tasks); } catch { /* отмена/ошибки уже учтены */ }
        return results;
    }

    private async Task<Dictionary<string, bool>> RunInParallelAsync(
        IReadOnlyList<string> files, string? outputDir, CancellationToken ct)
    {
        // Жадный алгоритм: крупные файлы первыми
        var ordered = files
            .Select(f => (Path: f, Size: File.Exists(f) ? new FileInfo(f).Length : 0))
            .OrderByDescending(x => x.Size)
            .Select(x => x.Path)
            .ToList();

        var results = new Dictionary<string, bool>();
        var total = ordered.Count;
        var completed = 0;
        using var sem = new SemaphoreSlim(MaxWorkers);
        var tasks = new List<Task>();

        foreach (var f in ordered)
        {
            if (ct.IsCancellationRequested) break;
            // Без ct в WaitAsync: при отмене исключение не рвёт цикл и не оставляет
            // уже запущенные задачи с освобождаемым semaphore-ом.
            await sem.WaitAsync(CancellationToken.None);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    bool ok = await AnalyzeFileAsync(f, outputDir, ct);
                    lock (results)
                    {
                        results[f] = ok;
                        completed++;
                        ProgressCallback?.Invoke(Path.GetFileName(f), completed, total);
                        FileDoneCallback?.Invoke(Path.GetFileName(f), ok);
                    }
                }
                finally { sem.Release(); }
            }, CancellationToken.None));
        }
        try { await Task.WhenAll(tasks); } catch { /* отмена */ }
        return results;
    }

    private async Task<bool> AnalyzeFileAsync(string file, string? outputDir, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return false;
        if (!File.Exists(file)) return false;

        var startedUtc = DateTime.UtcNow;
        FileStartCallback?.Invoke(Path.GetFileName(file));

        var outDir = outputDir ?? Path.GetDirectoryName(file)!;
        Directory.CreateDirectory(outDir);
        var idbPath = Path.Combine(outDir, Path.GetFileName(file) + ".i64");
        var logPath = Path.Combine(outDir, Path.GetFileName(file) + ".log");

        var psi = new ProcessStartInfo
        {
            FileName = IdatPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-B");
        psi.ArgumentList.Add($"-o{idbPath}");
        psi.ArgumentList.Add($"-L{logPath}");
        psi.ArgumentList.Add("-P+");
        psi.ArgumentList.Add(file);

        var pulseName = Path.GetFileName(file);
        var pulse = new ProcessPulse(
            $"Анализ {pulseName}",
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMinutes(30),
            AnalysisWatchPaths(idbPath, logPath));
        pulse.Updated += s => PulseCallback?.Invoke(pulseName, s.Text);

        var result = await ProcessRunner.RunAsync(
            psi, ct,
            pid => ProcessStartedCallback?.Invoke(Path.GetFileName(file), pid, Environment.CurrentManagedThreadId),
            pulse);

        if (!result.Success)
        {
            ErrorCallback?.Invoke(result.Describe($"Анализ {Path.GetFileName(file)}"));
            DeletePartialBase(idbPath, startedUtc);
            return false;
        }

        if (File.Exists(Path.ChangeExtension(idbPath, ".id0")))
        {
            ErrorCallback?.Invoke($"Анализ {Path.GetFileName(file)}: IDA оставила .id0 (аварийное завершение)");
            DeletePartialBase(idbPath, startedUtc);
            return false; // IDA рухнула
        }
        if (!File.Exists(idbPath))
        {
            ErrorCallback?.Invoke($"Анализ {Path.GetFileName(file)}: база .i64 не создана");
            return false;
        }
        try
        {
            if (new FileInfo(idbPath).Length == 0)
            {
                ErrorCallback?.Invoke($"Анализ {Path.GetFileName(file)}: база .i64 пустая");
                DeletePartialBase(idbPath, startedUtc);
                return false;
            }
        }
        catch (Exception e)
        {
            ErrorCallback?.Invoke($"Анализ {Path.GetFileName(file)}: {e.Message}");
            return false;
        }
        return true;
    }

    /// <summary>Удаляет только ту базу, которую создал или изменил текущий запуск:
    /// старую валидную базу при неудачном resume не трогаем.</summary>
    private static void DeletePartialBase(string idbPath, DateTime startedUtc)
    {
        try
        {
            if (!File.Exists(idbPath)) return;
            if (File.GetLastWriteTimeUtc(idbPath) >= startedUtc.AddSeconds(-2))
                File.Delete(idbPath);
        }
        catch { /* файл занят — оставляем как есть */ }
    }

    private async Task<bool> RunScriptOnBaseAsync(
        string idbPath, string scriptPath, Dictionary<string, string>? scriptArgs, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return false;
        if (!File.Exists(idbPath) || !File.Exists(scriptPath)) return false;
        // Скрипт запускается только на готовой базе; исходник здесь недопустим,
        // иначе IDA может незаметно переанализировать файл вместо resume.
        if (!ArtifactValidator.IsBasePath(idbPath)) return false;

        FileStartCallback?.Invoke(Path.GetFileName(idbPath));

        var outDir = Path.GetDirectoryName(idbPath)!;
        var logPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(idbPath) + "_script.log");

        var argsStr = scriptArgs is { Count: > 0 }
            ? string.Join(" ", scriptArgs.Select(kv => $"\"{kv.Key}={kv.Value}\""))
            : "";
        var scriptCmd = string.IsNullOrEmpty(argsStr) ? $"\"{scriptPath}\"" : $"\"{scriptPath}\" {argsStr}";

        var psi = new ProcessStartInfo
        {
            FileName = IdatPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-A");
        psi.ArgumentList.Add($"-S{scriptCmd}");
        psi.ArgumentList.Add($"-L{logPath}");
        psi.ArgumentList.Add(idbPath);

        var pulseName = Path.GetFileName(idbPath);
        var pulse = new ProcessPulse(
            $"Экспорт {pulseName}",
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMinutes(30),
            ExportWatchPaths(idbPath, logPath));
        pulse.Updated += s => PulseCallback?.Invoke(pulseName, s.Text);

        var result = await ProcessRunner.RunAsync(
            psi, ct,
            pid => ProcessStartedCallback?.Invoke(Path.GetFileName(idbPath), pid, Environment.CurrentManagedThreadId),
            pulse);

        if (!result.Success)
            ErrorCallback?.Invoke(result.Describe($"Экспорт {Path.GetFileName(idbPath)}"));
        return result.Success;
    }

    /// <summary>Файлы, по которым видно прогресс анализа IDA (база, временные, лог).</summary>
    private static IEnumerable<string> AnalysisWatchPaths(string idbPath, string logPath)
    {
        yield return idbPath;
        yield return Path.ChangeExtension(idbPath, ".id0");
        yield return Path.ChangeExtension(idbPath, ".id1");
        yield return Path.ChangeExtension(idbPath, ".nam");
        yield return Path.ChangeExtension(idbPath, ".til");
        yield return logPath;
    }

    /// <summary>Файлы, по которым видно прогресс экспорта (база, JSON, лог скрипта).</summary>
    private static IEnumerable<string> ExportWatchPaths(string idbPath, string logPath)
    {
        yield return idbPath;
        yield return idbPath + ".export.json";
        yield return logPath;
    }

    public void Dispose() { }
}