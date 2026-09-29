using System.Diagnostics;

namespace IDABatchToolWinUI.Services;

public sealed record FileResult(string FileName, bool Success);

/// <summary>
/// Пакетный запуск IDA Pro — аналог ida_batch_tool/ida/runner.py.
/// Параллельный анализ файлов (создание .i64) и выполнение скриптов на готовых базах.
/// Прогресс и статусы отдаются через колбэки; отмена — через CancellationToken.
/// </summary>
public sealed class IdaRunner : IDisposable
{
    private const double PollIntervalSec = 0.5;
    private const int CleanupRetries = 3;
    private const double CleanupRetryDelaySec = 1.0;

    public int MaxWorkers { get; }
    public string IdatPath { get; }

    // Колбэки: (имя файла, обработано, всего)
    public Action<string, int, int>? ProgressCallback { get; set; }
    public Action<string>? FileStartCallback { get; set; }
    public Action<string, bool>? FileDoneCallback { get; set; }

    /// <summary>(имя цели, PID процесса IDA, managed thread id) — вызывается сразу после запуска процесса.</summary>
    public Action<string, int, int>? ProcessStartedCallback { get; set; }

    private readonly SemaphoreSlim _gate = new(1, 1);

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
        var results = await RunInParallelAsync(files, outputDir, "analyze", ct);
        if (ct.IsCancellationRequested) return results;

        if (cleanup || tempCleanup)
        {
            foreach (var (f, ok) in results)
            {
                if (!ok) continue;
                var outDir = outputDir ?? Path.GetDirectoryName(f)!;
                var idbPath = Path.Combine(outDir, Path.GetFileName(f) + ".i64");
                var logPath = Path.Combine(outDir, Path.GetFileName(f) + ".log");
                if (cleanup)
                {
                    SafeDelete(logPath);
                    SafeDelete(Path.ChangeExtension(idbPath, ".asm"));
                }
                if (tempCleanup)
                {
                    foreach (var ext in new[] { ".id0", ".id1", ".nam", ".til" })
                        if (Directory.Exists(outDir))
                            foreach (var tmp in Directory.GetFiles(outDir, "*" + ext))
                                SafeDelete(tmp);
                }
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
            await sem.WaitAsync(ct);
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
            }, ct));
        }
        try { await Task.WhenAll(tasks); } catch { /* отмена/ошибки уже учтены */ }
        return results;
    }

    private async Task<Dictionary<string, bool>> RunInParallelAsync(
        IReadOnlyList<string> files, string? outputDir, string kind, CancellationToken ct)
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
            await sem.WaitAsync(ct);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    bool ok = kind == "analyze"
                        ? await AnalyzeFileAsync(f, outputDir, ct)
                        : throw new InvalidOperationException();
                    lock (results)
                    {
                        results[f] = ok;
                        completed++;
                        ProgressCallback?.Invoke(Path.GetFileName(f), completed, total);
                        FileDoneCallback?.Invoke(Path.GetFileName(f), ok);
                    }
                }
                finally { sem.Release(); }
            }, ct));
        }
        try { await Task.WhenAll(tasks); } catch { /* отмена */ }
        return results;
    }

    private async Task<bool> AnalyzeFileAsync(string file, string? outputDir, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return false;
        if (!File.Exists(file)) return false;

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

        var ok = await RunProcessWithCancelAsync(psi, file, ct);
        if (!ok) return false;

        if (File.Exists(Path.ChangeExtension(idbPath, ".id0"))) return false; // IDA рухнула
        return File.Exists(idbPath);
    }

    private async Task<bool> RunScriptOnBaseAsync(
        string idbPath, string scriptPath, Dictionary<string, string>? scriptArgs, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return false;
        if (!File.Exists(idbPath) || !File.Exists(scriptPath)) return false;

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

        return await RunProcessWithCancelAsync(psi, idbPath, ct);
    }

    /// <summary>Запуск процесса с опросом и поддержкой отмены (аналог _run_process).</summary>
    private async Task<bool> RunProcessWithCancelAsync(ProcessStartInfo psi, string target, CancellationToken ct)
    {
        try
        {
            using var proc = Process.Start(psi);
            if (proc == null) return false;

            ProcessStartedCallback?.Invoke(Path.GetFileName(target), proc.Id, Environment.CurrentManagedThreadId);

            // Читаем потоки, чтобы не переполнился pipe-буфер
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();

            while (!proc.HasExited)
            {
                if (ct.IsCancellationRequested)
                {
                    try { proc.Kill(entireProcessTree: true); } catch { }
                    await proc.WaitForExitAsync();
                    return false;
                }
                await Task.Delay(TimeSpan.FromSeconds(PollIntervalSec), CancellationToken.None);
            }
            await Task.WhenAll(stdoutTask, stderrTask);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void SafeDelete(string file, int retries = CleanupRetries, double delay = CleanupRetryDelaySec)
    {
        for (int attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
                return;
            }
            catch
            {
                if (attempt < retries) Thread.Sleep(TimeSpan.FromSeconds(delay));
            }
        }
    }

    public void Dispose() => _gate.Dispose();
}