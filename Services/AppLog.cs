using System.Text;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Постоянный журнал приложения. Пишется одновременно в две папки:
/// рядом с exe (<c>logs\</c>) и в <c>%LOCALAPPDATA%\IDABatchToolWinUI\logs</c>.
/// Если одна папка недоступна (например, Program Files только для чтения),
/// вторая продолжает работать. Уровень задаётся <c>log_level</c> в config.yaml.
/// </summary>
public static class AppLog
{
    private const long MaxFileBytes = 20L * 1024 * 1024;
    private static readonly object Gate = new();
    private static readonly List<string> Directories = new();
    private static int _minLevel = 1; // 0 DEBUG, 1 INFO, 2 WARN, 3 ERROR
    private static bool _initialized;

    public static IReadOnlyList<string> LogDirectories
    {
        get { lock (Gate) return Directories.ToList(); }
    }

    public static void Initialize(string? level = null, IEnumerable<string>? directories = null)
    {
        lock (Gate)
        {
            Directories.Clear();
            _minLevel = ParseLevel(level);
            foreach (var dir in directories ?? DefaultDirectories())
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    Directories.Add(dir);
                }
                catch { /* папка недоступна — пишем в оставшиеся */ }
            }
            _initialized = true;
            WriteLocked("INFO", $"=== IDABatchToolWinUI {AppVersion()} start ===", null);
        }
    }

    public static void Debug(string message) => Write("DEBUG", message, null);
    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message) => Write("WARN", message, null);
    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    /// <summary>Необработанное исключение: в общий лог и в crash.log обеих папок.</summary>
    public static void Crash(Exception exception)
    {
        lock (Gate)
        {
            EnsureInitializedLocked();
            WriteLocked("ERROR", "Необработанное исключение", exception);
            WriteCrashLocked(exception);
        }
    }

    private static void Write(string level, string message, Exception? exception)
    {
        if (LevelValue(level) < _minLevel) return;
        lock (Gate)
        {
            EnsureInitializedLocked();
            WriteLocked(level, message, exception);
        }
    }

    private static void EnsureInitializedLocked()
    {
        if (_initialized) return;
        _initialized = true;
        foreach (var dir in DefaultDirectories())
        {
            try
            {
                Directory.CreateDirectory(dir);
                Directories.Add(dir);
            }
            catch { /* пропускаем */ }
        }
    }

    private static void WriteLocked(string level, string message, Exception? exception)
    {
        var text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        if (exception != null)
            text += Environment.NewLine + exception;

        var fileName = $"app-{DateTime.Now:yyyyMMdd}.log";
        foreach (var dir in Directories)
        {
            try
            {
                var path = Path.Combine(dir, fileName);
                RotateIfNeeded(path);
                File.AppendAllText(path, text + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { /* эта папка недоступна — остальные продолжают работать */ }
        }
    }

    private static void WriteCrashLocked(Exception exception)
    {
        var text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}{Environment.NewLine}{exception}{Environment.NewLine}---{Environment.NewLine}";
        foreach (var dir in Directories)
        {
            try
            {
                File.AppendAllText(Path.Combine(dir, "crash.log"), text, new UTF8Encoding(false));
            }
            catch { /* пропускаем */ }
        }
    }

    private static void RotateIfNeeded(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            if (new FileInfo(path).Length <= MaxFileBytes) return;
            var old = path + ".old";
            if (File.Exists(old)) File.Delete(old);
            File.Move(path, old);
        }
        catch { /* ротация не критична */ }
    }

    private static IEnumerable<string> DefaultDirectories()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "logs");
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(local))
            yield return Path.Combine(local, "IDABatchToolWinUI", "logs");
    }

    private static int ParseLevel(string? level) => (level ?? "INFO").Trim().ToUpperInvariant() switch
    {
        "DEBUG" => 0,
        "INFO" => 1,
        "WARN" or "WARNING" => 2,
        "ERROR" or "FATAL" => 3,
        _ => 1,
    };

    private static int LevelValue(string level) => level switch
    {
        "DEBUG" => 0,
        "INFO" => 1,
        "WARN" => 2,
        _ => 3,
    };

    private static string AppVersion()
        => typeof(AppLog).Assembly.GetName().Version?.ToString() ?? "unknown";
}
