using System.Collections.Concurrent;
using System.Diagnostics;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Реестр запущенных дочерних процессов. Нужен как страховка на случай, если
/// Job Object недоступен (например, приложение уже внутри чужого job) или
/// процесс запущен без назначения в job. По реестру закрытие окна может
/// дождаться завершения и добить оставшиеся процессы.
/// </summary>
public static class ProcessRegistry
{
    private static readonly ConcurrentDictionary<int, byte> _pids = new();

    public static bool HasActive => !_pids.IsEmpty;
    public static int Count => _pids.Count;

    public static void Register(int pid)
    {
        if (pid > 0) _pids[pid] = 0;
    }

    public static void Unregister(int pid) => _pids.TryRemove(pid, out _);

    /// <summary>Ждёт, пока все зарегистрированные процессы завершатся или истечёт таймаут.</summary>
    public static async Task WaitForExitAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (HasActive && DateTime.UtcNow < deadline)
            await Task.Delay(100);
    }

    /// <summary>Принудительно завершает все зарегистрированные процессы (дерево).</summary>
    public static void KillAll()
    {
        foreach (var pid in _pids.Keys)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
            }
            catch { /* процесс уже завершился или нет прав */ }
            finally { _pids.TryRemove(pid, out _); }
        }
    }
}
