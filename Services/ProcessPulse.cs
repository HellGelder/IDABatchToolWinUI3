using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IDABatchToolWinUI.Services;

/// <summary>Снимок «пульса» процесса: жив ли, работает ли, растут ли артефакты.</summary>
public sealed record ProcessPulseSnapshot(
    TimeSpan Elapsed,
    TimeSpan CpuTime,
    double CpuPercent,
    long WorkingSetBytes,
    long ArtifactBytes,
    TimeSpan Idle,
    bool IsQuiet,
    string Text);

/// <summary>
/// Наблюдатель за долгим процессом IDA. Раз в интервал сэмплирует независимые
/// сигналы (CPU, память, I/O, размер/время файлов базы и логов) и сообщает,
/// работает процесс или затих. Ничего не убивает: при затишье только помечает
/// снимок как «нет видимой активности», решение принимает пользователь.
/// </summary>
public sealed class ProcessPulse : IDisposable
{
    private readonly string _label;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _quietAfter;
    private readonly IReadOnlyList<string> _watchPaths;

    private Process? _process;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private volatile bool _attached;

    public event Action<ProcessPulseSnapshot>? Updated;

    public ProcessPulse(string label, TimeSpan interval, TimeSpan quietAfter, IEnumerable<string> watchPaths)
    {
        _label = label;
        _interval = interval;
        _quietAfter = quietAfter;
        _watchPaths = watchPaths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
    }

    /// <summary>Начать наблюдение за уже запущенным процессом.</summary>
    public void Attach(Process process)
    {
        if (_attached) return;
        _process = process;
        _attached = true;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    /// <summary>Остановить наблюдение (процесс завершился или отменён).</summary>
    public void Detach()
    {
        if (!_attached) return;
        _attached = false;
        try { _cts?.Cancel(); } catch { /* уже отменён */ }
        // CTS не диспозим: цикл мог ещё регистрировать callback в Task.Delay.
        _cts = null;
    }

    public void Dispose() => Detach();

    private async Task LoopAsync(CancellationToken ct)
    {
        var process = _process;
        if (process == null) return;

        var started = Stopwatch.StartNew();
        var lastCpu = TimeSpan.Zero;
        var lastWs = 0L;
        var lastIo = 0UL;
        var lastArtifactBytes = -1L;
        var lastArtifactMtime = DateTime.MinValue;
        var lastActivityAt = TimeSpan.Zero;

        while (!ct.IsCancellationRequested && _attached)
        {
            try { await Task.Delay(_interval, ct); }
            catch (OperationCanceledException) { break; }
            if (ct.IsCancellationRequested || !_attached) break;

            try
            {
                if (process.HasExited) break;
                process.Refresh();

                var cpu = process.TotalProcessorTime;
                var ws = process.WorkingSet64;
                var io = ReadIo(process);
                var (artifactBytes, artifactMtime) = ReadArtifacts();
                var elapsed = started.Elapsed;

                var cpuDelta = (cpu - lastCpu).TotalMilliseconds;
                var active = cpuDelta > 20
                             || Math.Abs(ws - lastWs) > 256 * 1024
                             || io != lastIo
                             || artifactBytes != lastArtifactBytes
                             || artifactMtime != lastArtifactMtime;
                if (active) lastActivityAt = elapsed;

                lastCpu = cpu;
                lastWs = ws;
                lastIo = io;
                lastArtifactBytes = artifactBytes;
                lastArtifactMtime = artifactMtime;

                var idle = elapsed - lastActivityAt;
                var quiet = idle >= _quietAfter;
                var cpuPercent = _interval.TotalMilliseconds > 0
                    ? Math.Max(0, cpuDelta / _interval.TotalMilliseconds * 100)
                    : 0;

                var text = $"{_label}: {FormatDuration(elapsed)}, CPU {cpuPercent:0}%, " +
                           $"ОЗУ {ws / 1024.0 / 1024.0:0.0} МБ";
                if (artifactBytes >= 0)
                    text += $", артефакты {artifactBytes / 1024.0 / 1024.0:0.0} МБ";
                text += $", активность {FormatDuration(idle)} назад";
                if (quiet)
                    text += " · НЕТ ВИДИМОЙ АКТИВНОСТИ";

                Updated?.Invoke(new ProcessPulseSnapshot(
                    elapsed, cpu, cpuPercent, ws, artifactBytes, idle, quiet, text));
            }
            catch
            {
                break; // процесс завершился или недоступен
            }
        }
    }

    private (long Bytes, DateTime Mtime) ReadArtifacts()
    {
        long bytes = 0;
        var newest = DateTime.MinValue;
        var any = false;
        foreach (var path in _watchPaths)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) continue;
                any = true;
                bytes += info.Length;
                if (info.LastWriteTimeUtc > newest) newest = info.LastWriteTimeUtc;
            }
            catch { /* файл занят/недоступен */ }
        }
        return any ? (bytes, newest) : (-1, DateTime.MinValue);
    }

    private static string FormatDuration(TimeSpan value)
        => value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");

    private static ulong ReadIo(Process process)
    {
        try
        {
            return GetProcessIoCounters(process.Handle, out var counters)
                ? counters.ReadOperationCount + counters.WriteOperationCount
                : 0;
        }
        catch { return 0; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessIoCounters(IntPtr processHandle, out IoCounters ioCounters);
}
