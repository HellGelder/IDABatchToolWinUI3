using System.Diagnostics;
using IDABatchToolWinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace IDABatchToolWinUI;

/// <summary>Строка таблицы диспетчера — пересобирается при каждом опросе.</summary>
public sealed record TaskRow(
    string FileName,
    string Pid,
    string Status,
    string Phase,
    string Cpu,
    string Memory,
    string OsThreads,
    string StartedAt,
    string Pulse);

/// <summary>Карточка рабочего потока над таблицей: занятый слот показывает файл.</summary>
public sealed class TaskThreadCard
{
    public string Title { get; init; } = "";
    public string FileName { get; init; } = "";
    public bool IsBusy { get; init; }

    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;
    public Visibility FreeVisibility => IsBusy ? Visibility.Collapsed : Visibility.Visible;

    public Brush? CardBorder
    {
        get
        {
            var key = IsBusy ? "AccentFillColorDefaultBrush" : "DividerStrokeColorDefaultBrush";
            return Application.Current.Resources.TryGetValue(key, out var value) ? value as Brush : null;
        }
    }
}

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

    /// <summary>Порядок групп строк: анализируемые сверху, затем ожидающие, завершённые в конце.</summary>
    private static int StatusRank(MonitorFileStatus s) => s switch
    {
        MonitorFileStatus.Running => 0,
        MonitorFileStatus.Pending => 1,
        _ => 2,
    };

    // Текстовые подписи статусов/фаз — UI-слой: сервис знает только enum.
    private static string StatusText(MonitorFileStatus s) => s switch
    {
        MonitorFileStatus.Pending => "В ожидании",
        MonitorFileStatus.Running => "Анализируется",
        MonitorFileStatus.Done => "Завершён",
        MonitorFileStatus.Failed => "Завершён (ошибка)",
        MonitorFileStatus.Cancelled => "Отменён",
        _ => "—",
    };

    private static string PhaseText(string phase) => phase switch
    {
        "analysis" => "Анализ",
        "export" => "Экспорт",
        _ => "—",
    };

    private void Refresh()
    {
        var entries = AnalysisMonitor.Snapshot();

        // Снимки живых процессов активных файлов пишет сам монитор (по PID):
        // Snapshot() отдаёт копии, мутировать их бессмысленно.
        foreach (var e in entries)
        {
            if (e.Status != MonitorFileStatus.Running || e.Pid <= 0) continue;
            try
            {
                using var p = Process.GetProcessById(e.Pid);
                AnalysisMonitor.UpdateRuntimeStats(e.Pid,
                    p.TotalProcessorTime.ToString(@"hh\:mm\:ss"),
                    $"{p.WorkingSet64 / 1024.0 / 1024.0:F1} МБ",
                    p.Threads.Count.ToString());
            }
            catch
            {
                // процесс уже завершился или недоступен — снимок обнуляем
                AnalysisMonitor.ClearRuntimeStats(e.Pid);
            }
        }
        entries = AnalysisMonitor.Snapshot(); // со свежими снимками процессов

        // Анализируемые файлы — вверху, завершённые опускаются в конец
        // (внутри групп исходный порядок очереди сохраняется).
        var ordered = entries
            .Select((e, idx) => (e, idx))
            .OrderBy(t => StatusRank(t.e.Status))
            .ThenBy(t => t.idx)
            .Select(t => t.e)
            .ToList();

        RowsList.ItemsSource = ordered.Select(e => new TaskRow(
            e.FileName,
            e.Pid > 0 ? e.Pid.ToString() : "—",
            StatusText(e.Status),
            PhaseText(e.Phase),
            e.CpuTime,
            e.MemoryMb,
            e.OsThreads,
            e.StartedAt?.ToString("HH:mm:ss") ?? "—",
            string.IsNullOrEmpty(e.Pulse) ? "—" : e.Pulse)).ToList();

        RefreshThreadCards(entries);

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

    /// <summary>
    /// Карточки пула рабочих потоков: слоты нумеруются с 1 по числу выбранных
    /// потоков анализа; занятые слоты (в порядке очереди) показывают свой файл.
    /// </summary>
    private void RefreshThreadCards(IReadOnlyList<MonitorEntry> entries)
    {
        if (entries.Count == 0)
        {
            ThreadsPanel.Visibility = Visibility.Collapsed;
            ThreadCards.ItemsSource = null;
            return;
        }

        ThreadsPanel.Visibility = Visibility.Visible;

        var busy = entries
            .Where(x => x.Status == MonitorFileStatus.Running)
            .ToList();

        var slotCount = Math.Max(busy.Count, AnalysisMonitor.MaxWorkers);
        var cards = new List<TaskThreadCard>(slotCount);
        for (int i = 0; i < slotCount; i++)
        {
            var busyHere = i < busy.Count;
            cards.Add(new TaskThreadCard
            {
                IsBusy = busyHere,
                Title = $"Поток {i + 1}",
                FileName = busyHere ? busy[i].FileName : "Свободен",
            });
        }

        ThreadCards.ItemsSource = cards;
    }
}
