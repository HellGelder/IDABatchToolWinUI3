using IDABatchToolWinUI.Services;
using Xunit;

namespace IDABatchToolWinUI.Tests;

/// <summary>
/// AnalysisMonitor: ключи-пути с фолбэком по имени, независимость снимков,
/// рантайм-статистика по PID, завершение сессии. Класс потокобезопасен -
/// параллельные мутации проверяются аналогично OutcomeTrackerTests.
/// </summary>
public sealed class MonitorTests
{
    [Fact]
    public void Snapshot_ReturnsIndependentCopies()
    {
        AnalysisMonitor.BeginSession(new[] { @"C:\in\a.exe" }, 2);
        AnalysisMonitor.MarkRunning(@"C:\in\a.exe", "analysis");

        var snapshot = AnalysisMonitor.Snapshot();
        snapshot[0].Status = MonitorFileStatus.Done;

        var fresh = AnalysisMonitor.Snapshot();
        Assert.Equal(MonitorFileStatus.Running, fresh[0].Status);
        Assert.Equal("a.exe", fresh[0].FileName);
    }

    [Fact]
    public void Events_MatchEntryByPathFirst_ThenByName()
    {
        AnalysisMonitor.BeginSession(new[] { @"C:\in\a.exe", @"C:\in\b.exe" }, 2);

        // Точный ключ попадает в одну запись
        AnalysisMonitor.MarkRunning(@"C:\in\b.exe", "analysis");
        var snap = AnalysisMonitor.Snapshot();
        Assert.Equal(MonitorFileStatus.Pending, snap[0].Status);
        Assert.Equal(MonitorFileStatus.Running, snap[1].Status);

        // Событие воркера несёт только имя - фолбэк по нормализованному имени
        AnalysisMonitor.MarkCompleted("b.exe", ok: true);
        snap = AnalysisMonitor.Snapshot();
        Assert.Equal(MonitorFileStatus.Done, snap[1].Status);
        Assert.Equal(MonitorFileStatus.Pending, snap[0].Status);
    }

    [Fact]
    public void ExportEvent_WithBaseSuffix_MatchesSourceFileEntry()
    {
        // События анализа приходят с путём исходника, события экспорта - с путём
        // базы (.i64). Оба должны попадать в одну запись, иначе в диспетчере
        // появляются «выполняющиеся» файлы сверх числа потоков.
        AnalysisMonitor.BeginSession(new[] { @"C:\in\a.exe" }, 1);

        AnalysisMonitor.MarkRunning(@"C:\in\a.exe", "analysis");
        AnalysisMonitor.MarkCompleted(@"C:\in\a.exe", ok: true);
        AnalysisMonitor.MarkRunning(@"C:\in\a.exe.i64", "export");

        var snap = AnalysisMonitor.Snapshot();
        Assert.Single(snap);
        Assert.Equal(MonitorFileStatus.Running, snap[0].Status);
        Assert.Equal("export", snap[0].Phase);
    }

    [Fact]
    public void RuntimeStats_UpdatedByPid()
    {
        AnalysisMonitor.BeginSession(new[] { @"C:\in\a.exe", @"C:\in\b.exe" }, 2);
        AnalysisMonitor.SetProcessInfo(@"C:\in\a.exe", pid: 111, threadId: 1);
        AnalysisMonitor.SetProcessInfo(@"C:\in\b.exe", pid: 222, threadId: 2);

        AnalysisMonitor.UpdateRuntimeStats(222, "00:01:02", "12.3 МБ", "7");

        var snap = AnalysisMonitor.Snapshot();
        Assert.Equal("-", snap[0].CpuTime);
        Assert.Equal("00:01:02", snap[1].CpuTime);
        Assert.Equal("12.3 МБ", snap[1].MemoryMb);
        Assert.Equal("7", snap[1].OsThreads);

        AnalysisMonitor.ClearRuntimeStats(222);
        snap = AnalysisMonitor.Snapshot();
        Assert.Equal("-", snap[1].CpuTime);
    }

    [Fact]
    public void EndSession_MarksUnfinishedAsCancelled()
    {
        AnalysisMonitor.BeginSession(new[] { @"C:\in\a.exe", @"C:\in\b.exe", @"C:\in\c.exe" }, 2);
        AnalysisMonitor.MarkCompleted(@"C:\in\a.exe", ok: true);
        AnalysisMonitor.MarkRunning(@"C:\in\b.exe", "analysis");

        AnalysisMonitor.EndSession();

        var snap = AnalysisMonitor.Snapshot();
        Assert.Equal(MonitorFileStatus.Done, snap[0].Status);
        Assert.Equal(MonitorFileStatus.Cancelled, snap[1].Status);
        Assert.Equal(MonitorFileStatus.Cancelled, snap[2].Status);
        Assert.False(AnalysisMonitor.IsSessionActive);
    }

    /// <summary>Сессия завершилась без отмены, но файл так и не получил событие
    /// завершения: строка не должна оставаться в состоянии «Анализируется».</summary>
    [Fact]
    public void EndSession_NotCancelled_MarksUnfinishedRunningAsFailed()
    {
        AnalysisMonitor.BeginSession(new[] { @"C:\in\a.exe", @"C:\in\b.exe" }, 1);
        AnalysisMonitor.MarkRunning(@"C:\in\a.exe", "analysis");

        AnalysisMonitor.EndSession(cancelled: false);

        var snap = AnalysisMonitor.Snapshot();
        Assert.Equal(MonitorFileStatus.Failed, snap[0].Status);
        Assert.Equal("", snap[0].Phase);
        Assert.Equal(MonitorFileStatus.Cancelled, snap[1].Status);
    }

    /// <summary>Терминальный статус сбрасывает фазу и снимок живого процесса:
    /// иначе в таблице остаются «Анализ» и последние CPU/память завершённого idat.</summary>
    [Fact]
    public void MarkCompleted_ClearsPhaseAndRuntimeStats()
    {
        AnalysisMonitor.BeginSession(new[] { @"C:\in\a.exe" }, 1);
        AnalysisMonitor.MarkRunning(@"C:\in\a.exe", "analysis");
        AnalysisMonitor.SetProcessInfo(@"C:\in\a.exe", pid: 4321, threadId: 1);
        AnalysisMonitor.UpdateRuntimeStats(4321, "00:00:05", "9.9 МБ", "5");
        AnalysisMonitor.SetPulse(@"C:\in\a.exe", "жив");

        AnalysisMonitor.MarkCompleted(@"C:\in\a.exe", ok: true);

        var entry = AnalysisMonitor.Snapshot()[0];
        Assert.Equal(MonitorFileStatus.Done, entry.Status);
        Assert.Equal("", entry.Phase);
        Assert.Equal("", entry.Pulse);
        Assert.Equal("-", entry.CpuTime);
        Assert.Equal("-", entry.MemoryMb);
        Assert.Equal("-", entry.OsThreads);
    }

    /// <summary>Имена из воркера и из сканирования могут отличаться регистром
    /// (Windows регистронезависим) - событие всё равно находит запись.</summary>
    [Fact]
    public void Events_MatchEntryByNameIgnoringCase()
    {
        AnalysisMonitor.BeginSession(new[] { @"C:\in\Module.exe" }, 1);

        AnalysisMonitor.MarkRunning("module.EXE", "analysis");
        Assert.Equal(MonitorFileStatus.Running, AnalysisMonitor.Snapshot()[0].Status);

        AnalysisMonitor.MarkCompleted("MODULE.exe", ok: true);
        Assert.Equal(MonitorFileStatus.Done, AnalysisMonitor.Snapshot()[0].Status);
    }

    [Fact]
    public void ChangedAndSessionEnded_AreRaised()
    {
        int changed = 0;
        bool? endedCancelled = null;
        void OnChanged() => changed++;
        void OnEnded(bool cancelled) => endedCancelled = cancelled;

        AnalysisMonitor.Changed += OnChanged;
        AnalysisMonitor.SessionEnded += OnEnded;
        try
        {
            AnalysisMonitor.BeginSession(new[] { @"C:\in\a.exe" }, 1);
            AnalysisMonitor.MarkRunning(@"C:\in\a.exe", "analysis");
            AnalysisMonitor.EndSession(cancelled: true);

            Assert.True(changed >= 3, $"Changed должно подниматься на каждую мутацию, получено {changed}");
            Assert.True(endedCancelled);
        }
        finally
        {
            AnalysisMonitor.Changed -= OnChanged;
            AnalysisMonitor.SessionEnded -= OnEnded;
        }
    }

    [Fact]
    public void ParallelUpdates_AreThreadSafe()
    {
        AnalysisMonitor.BeginSession(
            Enumerable.Range(0, 64).Select(i => $@"C:\in\f{i}.exe"), 4);

        Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            var key = $@"C:\in\f{i}.exe";
            AnalysisMonitor.MarkRunning(key, "analysis");
            AnalysisMonitor.SetProcessInfo(key, pid: 1000 + i, threadId: i);
            AnalysisMonitor.UpdateRuntimeStats(1000 + i, "00:00:01", "1 МБ", "1");
            AnalysisMonitor.MarkCompleted(key, ok: i % 2 == 0);
        });

        var snap = AnalysisMonitor.Snapshot();
        Assert.Equal(64, snap.Count);
        Assert.Equal(32, snap.Count(e => e.Status == MonitorFileStatus.Done));
        Assert.Equal(32, snap.Count(e => e.Status == MonitorFileStatus.Failed));
    }
}
