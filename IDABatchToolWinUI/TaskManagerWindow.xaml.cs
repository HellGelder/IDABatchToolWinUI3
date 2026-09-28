using System.Diagnostics;
using IDABatchToolWinUI.Services;
using Microsoft.UI.Xaml;

namespace IDABatchToolWinUI;

/// <summary>Строка таблицы диспетчера — пересобирается при каждом опросе.</summary>
public sealed record TaskRow(
    string FileName,
    string Pid,
    string ThreadId,
    string Status,
    string Phase,
    string Cpu,
    string Memory,
    string OsThreads,
    string StartedAt);

/// <summary>
/// Окно «мини-диспетчер задач» анализа: по каждому файлу очереди — PID процесса idat.exe,
/// рабочий поток приложения и снимок состояния процесса (CPU, память, потоки ОС).
/// Данные состояния берутся из AnalysisMonitor (заполняется страницей анализа),
/// системные показатели опрашиваются таймером раз в секунду.
/// </summary>
public sealed partial class TaskManagerWindow : Window
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _timer;

    public TaskManagerWindow()
    {
        InitializeComponent();

        AppWindow.Resize(new Windows.Graphics.SizeInt32(1080, 640));

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => Refresh();
        Closed += (_, _) => _timer.Stop();

        Refresh();
        _timer.Start();
    }

    private void Refresh()
    {
        var entries = AnalysisMonitor.Snapshot();

        // Обновляем снимки живых процессов активных файлов
        foreach (var e in entries)
        {
            if (e.Status != MonitorFileStatus.Running || e.Pid <= 0) continue;
            try
            {
                using var p = Process.GetProcessById(e.Pid);
                e.CpuTime = p.TotalProcessorTime.ToString(@"hh\:mm\:ss");
                e.MemoryMb = $"{p.WorkingSet64 / 1024.0 / 1024.0:F1} МБ";
                e.OsThreads = p.Threads.Count.ToString();
            }
            catch
            {
                // процесс уже завершился или недоступен — снимок обнуляем
                e.CpuTime = "—";
                e.MemoryMb = "—";
                e.OsThreads = "—";
            }
        }

        RowsList.ItemsSource = entries.Select(e => new TaskRow(
            e.FileName,
            e.Pid > 0 ? e.Pid.ToString() : "—",
            e.ThreadId > 0 ? e.ThreadId.ToString() : "—",
            e.Status.ToText(),
            e.PhaseText,
            e.CpuTime,
            e.MemoryMb,
            e.OsThreads,
            e.StartedAt?.ToString("HH:mm:ss") ?? "—")).ToList();

        SummaryText.Text =
            $"Файлов: {entries.Count}" +
            $" · В ожидании: {entries.Count(x => x.Status == MonitorFileStatus.Pending)}" +
            $" · Анализируется: {entries.Count(x => x.Status == MonitorFileStatus.Running)}" +
            $" · Завершено: {entries.Count(x => x.Status == MonitorFileStatus.Done)}" +
            $" · Ошибки: {entries.Count(x => x.Status == MonitorFileStatus.Failed)}" +
            $" · Отменено: {entries.Count(x => x.Status == MonitorFileStatus.Cancelled)}";

        SessionText.Text = AnalysisMonitor.IsSessionActive
            ? "Сессия анализа выполняется"
            : "Сессия анализа завершена";
    }
}
