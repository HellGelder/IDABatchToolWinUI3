namespace IDABatchToolWinUI.Services;

/// <summary>
/// Жёсткие пределы времени для внешних процессов. Значения намеренно
/// консервативные: лучше явная ошибка «таймаут», чем вечно висящее окно.
/// Позже вынести в config.yaml (пачка C), сохранив эти значения дефолтами.
/// </summary>
public static class ProcessTimeouts
{
    /// <summary>Анализ одного файла IDA (-B) — крупные бинарники бывают долгими.</summary>
    public static readonly TimeSpan AnalyzeFile = TimeSpan.FromHours(2);

    /// <summary>Экспорт JSON через export_data.py (в т.ч. для крупных баз).</summary>
    public static readonly TimeSpan ExportJson = TimeSpan.FromHours(1);

    /// <summary>BinExport из базы.</summary>
    public static readonly TimeSpan BinExport = TimeSpan.FromMinutes(30);

    /// <summary>BinDiff CLI.</summary>
    public static readonly TimeSpan BinDiff = TimeSpan.FromMinutes(30);

    /// <summary>Diaphora: экспорт базы и сравнение.</summary>
    public static readonly TimeSpan Diaphora = TimeSpan.FromHours(2);

    /// <summary>Распаковка DMG через 7z.</summary>
    public static readonly TimeSpan SevenZip = TimeSpan.FromMinutes(10);

    /// <summary>Генерация HTML-отчётов Python-мостом.</summary>
    public static readonly TimeSpan HtmlGeneration = TimeSpan.FromHours(2);

    /// <summary>Сколько ждать завершения процессов при закрытии окна.</summary>
    public static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(10);
}
