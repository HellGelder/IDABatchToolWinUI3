namespace IDABatchToolWinUI.Services;

/// <summary>Статус файла в мини-диспетчере задач анализа.
/// Текстовые подписи - в UI-слое (TaskManagerWindow).</summary>
public enum MonitorFileStatus
{
    Pending,
    Running,
    Done,
    Failed,
    Cancelled,
}

/// <summary>
/// Строка мини-диспетчера: анализируемый файл + сведения о процессе/потоке.
/// Ключ записи - полный путь (уникален); FileName - отображаемое имя.
/// Снимки CPU/памяти заполняет монитор по запросу окна диспетчера
/// (UpdateRuntimeStats), наружу отдаются только копии (Snapshot).
/// </summary>
public sealed class MonitorEntry
{
    public required string Key { get; init; }
    public required string FileName { get; init; }
    public MonitorFileStatus Status { get; set; } = MonitorFileStatus.Pending;
    /// <summary>"analysis" | "export" - текущая фаза обработки файла.</summary>
    public string Phase { get; set; } = "";
    public int Pid { get; set; }
    /// <summary>Managed thread id воркера, ожидающего процесс IDA.</summary>
    public int ThreadId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    // Снимок процесса (обновляется при опросе)
    public string CpuTime { get; set; } = "-";
    public string MemoryMb { get; set; } = "-";
    public string OsThreads { get; set; } = "-";
    /// <summary>Текст пульса: время, CPU, артефакты, последняя активность.</summary>
    public string Pulse { get; set; } = "";

    public MonitorEntry Clone() => new()
    {
        Key = Key,
        FileName = FileName,
        Status = Status,
        Phase = Phase,
        Pid = Pid,
        ThreadId = ThreadId,
        StartedAt = StartedAt,
        EndedAt = EndedAt,
        CpuTime = CpuTime,
        MemoryMb = MemoryMb,
        OsThreads = OsThreads,
        Pulse = Pulse,
    };
}

/// <summary>
/// Общее состояние очереди анализа для окна «мини-диспетчер задач».
/// Заполняется страницей анализа из событий воркера (все методы вызываются на
/// UI-потоке); все мутации - под общим локом, Snapshot() возвращает независимые
/// копии, поэтому окно диспетчера не может изменить состояние монитора.
/// Идентификация записи: точное совпадение ключа (путь), иначе - по
/// нормализованному имени (события воркера передают только имя файла).
/// </summary>
public static class AnalysisMonitor
{
    private static readonly List<MonitorEntry> _entries = new();
    private static bool _sessionActive;
    private static int _maxWorkers;

    /// <summary>Состояние очереди изменилось (статус, фаза, PID, пульс).
    /// Окно диспетчера обновляется по событию, не дожидаясь тика таймера.
    /// Событие поднимается вне лока и после мутации.</summary>
    public static event Action? Changed;

    /// <summary>Сессия закончилась; аргумент - была ли она отменена. Окно
    /// диспетчера делает по нему финальное обновление таблицы.</summary>
    public static event Action<bool>? SessionEnded;

    public static bool IsSessionActive => _sessionActive;

    /// <summary>Число рабочих потоков сессии (ползунок «Одновременно IDA»).</summary>
    public static int MaxWorkers
    {
        get { lock (_entries) return _maxWorkers; }
    }

    /// <summary>Начать новую сессию: очередь всех файлов со статусом «В ожидании».
    /// Ключи - полные пути файлов; для отображения имя нормализуется.</summary>
    public static void BeginSession(IEnumerable<string> keys, int maxWorkers)
    {
        lock (_entries)
        {
            _entries.Clear();
            foreach (var key in keys)
                _entries.Add(new MonitorEntry { Key = key, FileName = NormalizeName(key) });
            _maxWorkers = Math.Max(1, maxWorkers);
            _sessionActive = true;
        }
        RaiseChanged();
    }

    /// <summary>Файл начал обрабатываться (колбэк FileStart).</summary>
    public static void MarkRunning(string id, string phase)
    {
        lock (_entries)
        {
            foreach (var e in FindAllLocked(id))
            {
                e.Status = MonitorFileStatus.Running;
                e.Phase = phase;
                e.StartedAt ??= DateTime.Now;
            }
        }
        RaiseChanged();
    }

    /// <summary>Процесс IDA запущен: запоминаем PID и тред воркера.</summary>
    public static void SetProcessInfo(string id, int pid, int threadId)
    {
        lock (_entries)
        {
            foreach (var e in FindAllLocked(id))
            {
                e.Pid = pid;
                e.ThreadId = threadId;
            }
        }
        RaiseChanged();
    }

    /// <summary>Пульс процесса: время, CPU, артефакты, давность активности.</summary>
    public static void SetPulse(string id, string text)
    {
        lock (_entries)
        {
            foreach (var e in FindAllLocked(id))
                e.Pulse = text;
        }
        RaiseChanged();
    }

    /// <summary>Файл обработан (успешно или с ошибкой). Терминальный статус:
    /// фаза и снимок живого процесса сбрасываются, иначе в таблице остаются
    /// «Анализ» и последние CPU/память уже завершённого процесса.</summary>
    public static void MarkCompleted(string id, bool ok)
    {
        lock (_entries)
        {
            foreach (var e in FindAllLocked(id))
            {
                e.Status = ok ? MonitorFileStatus.Done : MonitorFileStatus.Failed;
                e.EndedAt = DateTime.Now;
                e.Phase = "";
                e.Pulse = "";
                ClearStats(e);
            }
        }
        RaiseChanged();
    }

    /// <summary>Снимок состояния живого процесса: пишет окно диспетчера при
    /// опросе системы; запись находится по PID (уникален в пределах сессии).</summary>
    public static void UpdateRuntimeStats(int pid, string cpu, string memoryMb, string osThreads)
    {
        lock (_entries)
        {
            foreach (var e in _entries)
            {
                if (e.Pid != pid) continue;
                e.CpuTime = cpu;
                e.MemoryMb = memoryMb;
                e.OsThreads = osThreads;
            }
        }
    }

    /// <summary>Процесс недоступен (завершился) - снимок обнуляется.</summary>
    public static void ClearRuntimeStats(int pid)
    {
        lock (_entries)
        {
            foreach (var e in _entries)
            {
                if (e.Pid != pid) continue;
                e.CpuTime = "-";
                e.MemoryMb = "-";
                e.OsThreads = "-";
            }
        }
    }

    /// <summary>Конец сессии. Незавершённые файлы получают терминальный статус:
    /// «Отменён» при отмене, иначе «Ошибка» (файл, у которого не пришло
    /// событие завершения, фактически не обработан - строка не должна
    /// оставаться в состоянии «Анализируется» после конца анализа).</summary>
    public static void EndSession(bool cancelled = true)
    {
        lock (_entries)
        {
            foreach (var e in _entries)
            {
                if (e.Status == MonitorFileStatus.Running)
                {
                    e.Status = cancelled ? MonitorFileStatus.Cancelled : MonitorFileStatus.Failed;
                    e.EndedAt = DateTime.Now;
                    e.Phase = "";
                    e.Pulse = "";
                    ClearStats(e);
                }
                else if (e.Status == MonitorFileStatus.Pending)
                {
                    e.Status = MonitorFileStatus.Cancelled;
                    e.EndedAt = DateTime.Now;
                }
            }
            _sessionActive = false;
        }
        RaiseChanged();
        RaiseSessionEnded(cancelled);
    }

    /// <summary>Снимок процесса сбрасывается (процесс завершился).</summary>
    private static void ClearStats(MonitorEntry e)
    {
        e.CpuTime = "-";
        e.MemoryMb = "-";
        e.OsThreads = "-";
    }

    private static void RaiseChanged()
    {
        try { Changed?.Invoke(); }
        catch { /* подписчик не должен ломать состояние монитора */ }
    }

    private static void RaiseSessionEnded(bool cancelled)
    {
        try { SessionEnded?.Invoke(cancelled); }
        catch { /* подписчик не должен ломать состояние монитора */ }
    }

    /// <summary>Независимые копии текущих строк для отображения.</summary>
    public static IReadOnlyList<MonitorEntry> Snapshot()
    {
        lock (_entries)
            return _entries.Select(e => e.Clone()).ToList();
    }

    /// <summary>«file.exe.i64» → «file.exe» (и .idb); полный путь → только имя.</summary>
    public static string NormalizeName(string name)
    {
        name = Path.GetFileName(name.Trim());
        foreach (var suffix in new[] { ".i64", ".idb" })
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
                break;
            }
        }
        return name;
    }

    /// <summary>Точное совпадение ключа, иначе - совпадение по нормализованному
    /// имени (события воркера несут только имя; одноимённые файлы обновляются все).
    /// Сравнение без учёта регистра: файловая система Windows регистронезависима,
    /// имена из воркера и из сканирования могут отличаться регистром.
    /// Вызывается под локом.</summary>
    private static List<MonitorEntry> FindAllLocked(string id)
    {
        var exact = _entries
            .Where(e => string.Equals(e.Key, id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exact.Count > 0) return exact;
        var key = NormalizeName(id);
        return _entries
            .Where(e => string.Equals(e.FileName, key, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
