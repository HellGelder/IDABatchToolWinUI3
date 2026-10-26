using System.Diagnostics;
using System.Text;
using static IDABatchToolWinUI.Diff.DiffJson;

namespace IDABatchToolWinUI.Diff;

/// <summary>Результат запуска внешнего процесса diff-конвейера.</summary>
public sealed record DiffProcResult(int ExitCode, string Stdout, string Stderr);

/// <summary>
/// Единая точка запуска внешних процессов diff-конвейера (idat, pythonw, bindiff):
/// реестр PID, Job Object, чтение stdout/stderr без дедлока, ожидание без таймаутов
/// (большие модули считаются часами; отмена - только через ct), пульс Diaphora
/// (ОЗУ + БД + прогресс из stdout) с 4-часовым stall-killer'ом по решению владельца.
/// Перенос из DiffWorker при декомпозиции (пачка D) - поведение не менялось.
/// </summary>
public sealed class DiffProcessRunner
{
    private static readonly TimeSpan PulseInterval = TimeSpan.FromSeconds(30);
    /// <summary>Ни ОЗУ, ни БД диафоры не менялись дольше этого срока - процесс завис, убиваем.</summary>
    private static readonly TimeSpan StallTimeout = TimeSpan.FromHours(4);

    private static readonly System.Text.RegularExpressions.Regex ExportedRe = new(
        @"Exported (\d+) function\(s\) out of (\d+) total",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex RemainingRe = new(
        @"remaining time ~(\d+):(\d+):(\d+)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Прогресс экспорта Diaphora, разобранный из stdout («Exported N out of M total»).</summary>
    private sealed class ExportProgress
    {
        public int Done;            // экспортировано функций
        public int Total;           // всего функций
        public TimeSpan? Remaining; // оценка Diaphora «remaining time»
    }

    // События воркера пробрасываются колбэками (DiffWorker подписывает их на свои события).
    public Action<string, int>? PairProcessStarted { get; set; }        // (relKey, PID)
    public Action<string, string, string>? PairStatus { get; set; }    // (relKey, engine, status)
    public Action<string>? ErrorOccurred { get; set; }

    /// <summary>Порог «крупного файла» (последовательная обработка в основных фазах).</summary>
    public const long LargeFileThreshold = 100L * 1024 * 1024;

    public static bool IsLarge(string path)
        => File.Exists(path) && new FileInfo(path).Length >= LargeFileThreshold;

    /// <summary>Базовый ProcessStartInfo для idat: UTF-8 потоки, скрытое окно, -A.</summary>
    public static ProcessStartInfo CreateIdatPsi(string idatPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = idatPath,
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

    public async Task<DiffProcResult> RunAsync(ProcessStartInfo psi, string relKey, CancellationToken ct)
    {
        using var proc = Process.Start(psi);
        if (proc == null)
        {
            PairProcessStarted?.Invoke(relKey, -1);
            return new DiffProcResult(-1, "", "Не удалось запустить процесс");
        }
        var pid = proc.Id;
        PairProcessStarted?.Invoke(relKey, pid);
        IDABatchToolWinUI.Services.ProcessRegistry.Register(pid);
        IDABatchToolWinUI.Services.JobObject.Current?.Assign(proc);

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
            return new DiffProcResult(proc.ExitCode, await so, await se);
        }
        finally
        {
            IDABatchToolWinUI.Services.ProcessRegistry.Unregister(pid);
        }
    }

    /// <summary>
    /// Ожидание процесса Diaphora с двумя уровнями контроля.
    /// 1) Живость: выход процесса или отмена завершают ожидание; kill - при отмене
    ///    или зависании (ни ОЗУ, ни БД не менялись дольше StallTimeout).
    /// 2) Пульс (раз в PulseInterval): ОЗУ процесса (WorkingSet64), при watchDbPath -
    ///    ещё и суммарный размер БД (*.sqlite + *-wal + *-shm - диафора пишет в WAL,
    ///    и рост уходит в -wal), плюс прогресс экспорта из stdout Diaphora.
    /// Живой процесс может сколько угодно не менять ОЗУ (или БД) - зависанием
    /// считается только одновременное затишье обоих сигналов.
    /// </summary>
    public async Task<DiffProcResult> RunWithPulseAsync(ProcessStartInfo psi, string relKey, string phaseLabel,
        string? watchDbPath, CancellationToken ct)
    {
        using var proc = Process.Start(psi);
        if (proc == null)
        {
            PairProcessStarted?.Invoke(relKey, -1);
            return new DiffProcResult(-1, "", "Не удалось запустить процесс");
        }
        var pid = proc.Id;
        PairProcessStarted?.Invoke(relKey, pid);
        IDABatchToolWinUI.Services.ProcessRegistry.Register(pid);
        IDABatchToolWinUI.Services.JobObject.Current?.Assign(proc);

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
                        $"не менялись уже {StallTimeout.TotalHours:0} ч - процесс завис, убиваю (PID={proc.Id}).");
                    try { proc.Kill(entireProcessTree: true); } catch { }
                    await Task.WhenAll(so, se);
                    return new DiffProcResult(-1, await so, await se);
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
            IDABatchToolWinUI.Services.ProcessRegistry.Unregister(pid);
        }
        await exitTask;
        await Task.WhenAll(so, se);
        return new DiffProcResult(proc.ExitCode, await so, await se);
    }

    /// <summary>Рабочее множество процесса в байтах; 0 - процесс уже завершился.</summary>
    private static long ProcWorkingSet(Process proc)
    {
        try { proc.Refresh(); return proc.WorkingSet64; }
        catch { return 0; }
    }

    /// <summary>Читает stdout процесса построчно и обновляет прогресс экспорта
    /// по строкам Diaphora «Exported N function(s) out of M total … remaining time ~H:MM:SS».</summary>
    private static async Task<string> ReadStdoutWithProgressAsync(Process proc, ExportProgress progress)
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
                // Держим хвост, достаточный для обеих regex-строк, - весь лог не копим.
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
    public static long DiaphoraDbSize(string dbPath)
    {
        long total = 0;
        foreach (var p in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
            try { if (File.Exists(p)) total += new FileInfo(p).Length; }
            catch { /* файл занят - берём что есть */ }
        return total;
    }

    /// <summary>Короткая причина неудачи запуска Diaphora для заметки в отчёте:
    /// код выхода и хвост stderr/stdout (последние непустые строки).</summary>
    public static string DescribeProcFailure(int rc, string stdout, string stderr, string phase)
    {
        var tail = LastNonEmptyLines(stderr is "" or null ? stdout : stderr, 3);
        if (tail == "") tail = LastNonEmptyLines(stdout, 3);
        var baseMsg = $"код выхода {rc}, фаза {phase}";
        return tail == "" ? baseMsg : $"{baseMsg}: {tail}";
    }

    /// <summary>Последние непустые строки журнала (без ANSI-прочих артефактов), ≤ 500 символов.</summary>
    public static string LastNonEmptyLines(string text, int count)
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
}
