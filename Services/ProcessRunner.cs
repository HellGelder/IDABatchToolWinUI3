using System.Diagnostics;

namespace IDABatchToolWinUI.Services;

/// <summary>Результат запуска внешнего процесса.</summary>
public sealed record ProcessResult(
    bool Started,
    int ExitCode,
    string Stdout,
    string Stderr,
    bool Cancelled,
    TimeSpan Duration)
{
    public bool Success => Started && !Cancelled && ExitCode == 0;

    /// <summary>Короткое описание для сообщения пользователю.</summary>
    public string Describe(string what)
    {
        if (!Started) return $"{what}: не удалось запустить процесс";
        if (Cancelled) return $"{what}: отменено";
        var tail = LastLines(Stderr, 3);
        if (string.IsNullOrEmpty(tail)) tail = LastLines(Stdout, 3);
        return string.IsNullOrEmpty(tail)
            ? $"{what}: код выхода {ExitCode}"
            : $"{what}: код выхода {ExitCode}: {tail}";
    }

    public static string LastLines(string text, int count)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
        return string.Join(" | ", lines.TakeLast(count));
    }
}

/// <summary>
/// Единая точка запуска внешних процессов: читает оба потока одновременно,
/// назначает процесс в Job Object, регистрирует PID и поддерживает отмену.
/// Жёстких таймаутов нет: большие модули могут анализироваться часами,
/// дождаться полного завершения — требование владельца.
/// </summary>
public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        ProcessStartInfo psi,
        CancellationToken ct,
        Action<int>? onStarted = null,
        ProcessPulse? pulse = null)
    {
        var started = Stopwatch.StartNew();
        using var proc = new Process { StartInfo = psi };

        try
        {
            if (!proc.Start())
                return new ProcessResult(false, -1, "", "Process.Start вернул false", false, started.Elapsed);
        }
        catch (Exception e)
        {
            return new ProcessResult(false, -1, "", e.Message, false, started.Elapsed);
        }

        var pid = proc.Id;
        onStarted?.Invoke(pid);
        ProcessRegistry.Register(pid);
        JobObject.Current?.Assign(proc);
        pulse?.Attach(proc);

        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();

        var cancelled = false;
        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            TryKill(proc);
        }
        finally
        {
            pulse?.Detach();
            // После kill даём процессу и потокам шанс закрыться, но не бесконечно.
            try { await proc.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); }
            catch { /* уже завершён или не отвечает */ }
        }

        var stdout = await SafeReadAsync(stdoutTask);
        var stderr = await SafeReadAsync(stderrTask);
        ProcessRegistry.Unregister(pid);

        var exitCode = cancelled ? -1 : SafeExitCode(proc);
        return new ProcessResult(true, exitCode, stdout, stderr, cancelled, started.Elapsed);
    }

    private static void TryKill(Process proc)
    {
        try
        {
            if (!proc.HasExited) proc.Kill(entireProcessTree: true);
        }
        catch { /* уже завершён или нет прав */ }
    }

    private static async Task<string> SafeReadAsync(Task<string> task)
    {
        try { return await task.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch { return ""; }
    }

    private static int SafeExitCode(Process proc)
    {
        try { return proc.ExitCode; }
        catch { return -1; }
    }
}
