using System.Text;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Единое хранилище конфигурации: один экземпляр на приложение, актуальное
/// значение <see cref="Current"/> и событие <see cref="Changed"/> вместо
/// размороженных снимков ConfigService.Load по страницам.
/// Сохранение сливает новые значения в существующий текст config.yaml
/// (комментарии и неизвестные ключи не теряются) и пишет файл атомарно.
/// </summary>
public interface IConfigStore
{
    AppConfig Current { get; }

    /// <summary>Вызывается после успешного сохранения.</summary>
    event Action<AppConfig>? Changed;

    /// <summary>Сохраняет конфиг; MaxIda нормализуется в диапазон 0..32.</summary>
    void Save(AppConfig cfg);
}

public sealed class ConfigStore : IConfigStore
{
    private readonly string _path;

    public ConfigStore(string? path = null)
    {
        _path = path ?? AppConstants.ConfigPath;
        Current = ConfigService.Load(_path);
    }

    public AppConfig Current { get; private set; }

    public event Action<AppConfig>? Changed;

    public void Save(AppConfig cfg)
    {
        if (cfg.MaxIda is < 0 or > 32)
        {
            var clamped = Math.Clamp(cfg.MaxIda, 0, 32);
            AppLog.Warn($"max_ida = {cfg.MaxIda} вне диапазона 0..32, записано {clamped}.");
            cfg.MaxIda = clamped;
        }

        string? existing = null;
        try
        {
            if (File.Exists(_path)) existing = File.ReadAllText(_path, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            // Без слияния файл перезаписывается известными ключами — хуже, чем
            // отказ от сохранения: настройки всё равно можно перезадать в UI.
            AppLog.Warn($"Не удалось прочитать {_path} для слияния: {ex.Message}");
        }

        AtomicWrite(_path, MergeIntoYaml(existing, cfg));

        Current = cfg;
        Changed?.Invoke(cfg);
    }

    /// <summary>Замена известных ключей в существующем тексте конфига
    /// (комментарии и неизвестные ключи сохраняются), недостающие — дописываются.</summary>
    internal static string MergeIntoYaml(string? existing, AppConfig cfg)
    {
        var lines = existing == null
            ? new List<string>()
            : existing.Replace("\r\n", "\n").Split('\n').ToList();
        // Хвостовой перевод строки не считается содержимым
        if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);

        var seen = new HashSet<string>();
        var section = ""; // текущая секция верхнего уровня ("ida"/"bindiff"/...)

        for (int i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            if (string.IsNullOrWhiteSpace(raw) || raw.TrimStart().StartsWith('#')) continue;
            var indent = raw.TakeWhile(char.IsWhiteSpace).Count();
            var content = raw.Substring(indent);
            var colon = content.IndexOf(':');
            if (colon < 0) continue;
            var key = content.Substring(0, colon).Trim().Trim('"', '\'');

            if (indent == 0)
            {
                section = key;
                var scalar = TopLevelValue(key, cfg);
                if (scalar != null)
                {
                    lines[i] = $"{key}: {scalar}";
                    seen.Add(key);
                }
                continue;
            }

            if (section == "ida" && key == "executable")
            {
                lines[i] = new string(' ', indent) + $"executable: {ConfigService.QuoteIfNeeded(cfg.IdaExecutable)}";
                seen.Add("ida.executable");
            }
            else if (section == "bindiff" && key == "executable")
            {
                lines[i] = new string(' ', indent) + $"executable: {ConfigService.QuoteIfNeeded(cfg.BindiffExecutable)}";
                seen.Add("bindiff.executable");
            }
        }

        if (!seen.Contains("ida.executable"))
        {
            lines.Add("ida:");
            lines.Add($"  executable: {ConfigService.QuoteIfNeeded(cfg.IdaExecutable)}");
        }
        if (!seen.Contains("bindiff.executable"))
        {
            lines.Add("bindiff:");
            lines.Add($"  executable: {ConfigService.QuoteIfNeeded(cfg.BindiffExecutable)}");
        }
        if (!seen.Contains("max_ida")) lines.Add($"max_ida: {cfg.MaxIda}");
        if (!seen.Contains("default_inputdir")) lines.Add($"default_inputdir: {ConfigService.QuoteIfNeeded(cfg.DefaultInputDir)}");
        if (!seen.Contains("log_level")) lines.Add($"log_level: {cfg.LogLevel}");
        if (!seen.Contains("theme")) lines.Add($"theme: {cfg.Theme}");
        if (!seen.Contains("manpages_db_path")) lines.Add($"manpages_db_path: {ConfigService.QuoteIfNeeded(cfg.ManPagesDbPath)}");

        return string.Join("\n", lines) + "\n";
    }

    private static string? TopLevelValue(string key, AppConfig cfg) => key switch
    {
        "max_ida" => cfg.MaxIda.ToString(),
        "default_inputdir" => ConfigService.QuoteIfNeeded(cfg.DefaultInputDir),
        "log_level" => cfg.LogLevel,
        "theme" => cfg.Theme,
        "manpages_db_path" => ConfigService.QuoteIfNeeded(cfg.ManPagesDbPath),
        _ => null,
    };

    /// <summary>Запись через временный файл с заменой: читатель не увидит
    /// наполовину записанный конфиг, даже если процесс умрёт посреди записи.</summary>
    private static void AtomicWrite(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.WriteAllText(tmp, content, new UTF8Encoding(false));
                File.Move(tmp, path, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 3)
            {
                // Антивирус/индексатор может кратко держать файл
                Thread.Sleep(50 * attempt);
            }
            catch
            {
                try { File.Delete(tmp); } catch { /* не критично */ }
                throw;
            }
        }
    }
}
