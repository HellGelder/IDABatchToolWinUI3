using System.Collections.Concurrent;

namespace IDABatchToolWinUI.Services;

/// <summary>Статус файла в мини-диспетчере задач анализа.</summary>
public enum MonitorFileStatus
{
    Pending,
    Running,
    Done,
    Failed,
    Cancelled,
}

public static class MonitorFileStatusExtensions
{
    public static string ToText(this MonitorFileStatus s) => s switch
    {
        MonitorFileStatus.Pending => "В ожидании",
        MonitorFileStatus.Running => "Анализируется",
        MonitorFileStatus.Done => "Завершён",
        MonitorFileStatus.Failed => "Завершён (ошибка)",
        MonitorFileStatus.Cancelled => "Отменён",
        _ => "—",
    };
}

/// <summary>
/// Строка мини-диспетчера: анализируемый файл + сведения о процессе/потоке.
/// Снимки CPU/памяти заполняет окно диспетчера при опросе системы.
/// </summary>
public sealed class MonitorEntry
{
    public required string FileName { get; init; }
    public MonitorFileStatus Status { get; set; } = MonitorFileStatus.Pending;
    /// <summary>"analysis" | "export" — текущая фаза обработки файла.</summary>
    public string Phase { get; set; } = "";
    public int Pid { get; set; }
    /// <summary>Managed thread id воркера, ожидающего процесс IDA.</summary>
    public int ThreadId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    // Снимок процесса (обновляется при опросе)
    public string CpuTime { get; set; } = "—";
    public string MemoryMb { get; set; } = "—";
    public string OsThreads { get; set; } = "—";

    public string PhaseText => Phase switch
    {
        "analysis" => "Анализ",
        "export" => "Экспорт",
        _ => "—",
    };
}

/// <summary>
/// Общее состояние очереди анализа для окна «мини-диспетчер задач».
/// Заполняется страницей анализа из событий воркера (все методы вызываются на UI-потоке),
/// окно диспетчера только читает Snapshot() и опрашивает систему по PID.
/// </summary>
public static class AnalysisMonitor
{
    private static readonly List<MonitorEntry> _entries = new();
    private static bool _sessionActive;

    public static bool IsSessionActive => _sessionActive;

    /// <summary>Начать новую сессию: очередь всех файлов со статусом «В ожидании».</summary>
    public static void BeginSession(IEnumerable<string> fileNames)
    {
        lock (_entries)
        {
            _entries.Clear();
            foreach (var name in fileNames)
                _entries.Add(new MonitorEntry { FileName = name });
            _sessionActive = true;
        }
    }

    /// <summary>Файл начал обрабатываться (колбэк FileStart).</summary>
    public static void MarkRunning(string fileName, string phase)
    {
        var e = Find(fileName);
        if (e == null) return;
        e.Status = MonitorFileStatus.Running;
        e.Phase = phase;
        e.StartedAt ??= DateTime.Now;
    }

    /// <summary>Процесс IDA запущен: запоминаем PID и тред воркера.</summary>
    public static void SetProcessInfo(string fileName, int pid, int threadId)
    {
        var e = Find(fileName);
        if (e == null) return;
        e.Pid = pid;
        e.ThreadId = threadId;
    }

    /// <summary>Файл обработан (успешно или с ошибкой).</summary>
    public static void MarkCompleted(string fileName, bool ok)
    {
        var e = Find(fileName);
        if (e == null) return;
        e.Status = ok ? MonitorFileStatus.Done : MonitorFileStatus.Failed;
        e.EndedAt = DateTime.Now;
    }

    /// <summary>Конец сессии: необработанные файлы помечаются отменёнными.</summary>
    public static void EndSession()
    {
        lock (_entries)
        {
            foreach (var e in _entries)
            {
                if (e.Status is MonitorFileStatus.Pending or MonitorFileStatus.Running)
                {
                    e.Status = MonitorFileStatus.Cancelled;
                    e.EndedAt = DateTime.Now;
                }
            }
            _sessionActive = false;
        }
    }

    /// <summary>Копия текущих строк для отображения.</summary>
    public static IReadOnlyList<MonitorEntry> Snapshot()
    {
        lock (_entries)
            return _entries.ToList();
    }

    /// <summary>«file.exe.i64» → «file.exe»; полный путь → только имя.</summary>
    public static string NormalizeName(string name)
    {
        name = Path.GetFileName(name.Trim());
        const string i64 = ".i64";
        if (name.EndsWith(i64, StringComparison.OrdinalIgnoreCase))
            name = name[..^i64.Length];
        return name;
    }

    private static MonitorEntry? Find(string fileName)
    {
        var key = NormalizeName(fileName);
        lock (_entries)
            return _entries.FirstOrDefault(e => e.FileName == key);
    }
}
