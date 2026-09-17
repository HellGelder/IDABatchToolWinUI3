using System.Text;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Конфигурация приложения — аналог ida_batch_tool/config/loader.py.
/// Работает с общим config.yaml (в корне суперпроекта), чтобы исполнение 1 и 2
/// использовали одни настройки.
/// </summary>
public sealed class AppConfig
{
    public string IdaExecutable { get; set; } = AppConstants.IdatDefaultExe;
    public string BindiffExecutable { get; set; } = AppConstants.BindiffDefaultExe;
    public int MaxIda { get; set; } = 4;
    public string DefaultInputDir { get; set; } = ".";
    public string LogLevel { get; set; } = "INFO";
    public string Theme { get; set; } = "light";
    public string ManPagesDbPath { get; set; } = "";

    public AppConfig Clone() => (AppConfig)MemberwiseClone();
}

public static class ConfigService
{
    /// <summary>Загружает конфиг из config.yaml, сливая с дефолтами. При отсутствии файла — дефолты.</summary>
    public static AppConfig Load(string? path = null)
    {
        path ??= AppConstants.ConfigPath;
        var cfg = new AppConfig();
        if (!File.Exists(path)) return cfg;

        try
        {
            var yaml = File.ReadAllText(path, Encoding.UTF8);
            var root = MiniYaml.Parse(yaml);
            cfg.Theme = YamlGet(root, "theme", cfg.Theme);
            cfg.MaxIda = YamiInt(root, "max_ida", cfg.MaxIda);
            cfg.DefaultInputDir = YamlGet(root, "default_inputdir", cfg.DefaultInputDir);
            cfg.LogLevel = YamlGet(root, "log_level", cfg.LogLevel);
            cfg.ManPagesDbPath = YamlGet(root, "manpages_db_path", cfg.ManPagesDbPath);
            var ida = root.GetMap("ida");
            if (ida != null) cfg.IdaExecutable = YamiStr(ida, "executable", cfg.IdaExecutable);
            var bindiff = root.GetMap("bindiff");
            if (bindiff != null) cfg.BindiffExecutable = YamiStr(bindiff, "executable", cfg.BindiffExecutable);
        }
        catch
        {
            // Невалидный YAML — используем дефолты; глобальные настройки не должны ломать запуск.
        }
        return cfg;
    }

    public static void Save(AppConfig cfg, string? path = null)
    {
        path ??= AppConstants.ConfigPath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine("ida:");
        sb.AppendLine($"  executable: {QuoteIfNeeded(cfg.IdaExecutable)}");
        sb.AppendLine("bindiff:");
        sb.AppendLine($"  executable: {QuoteIfNeeded(cfg.BindiffExecutable)}");
        sb.AppendLine($"max_ida: {cfg.MaxIda}");
        sb.AppendLine($"default_inputdir: {QuoteIfNeeded(cfg.DefaultInputDir)}");
        sb.AppendLine($"log_level: {cfg.LogLevel}");
        sb.AppendLine($"theme: {cfg.Theme}");
        sb.AppendLine($"manpages_db_path: {QuoteIfNeeded(cfg.ManPagesDbPath)}");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string QuoteIfNeeded(string v)
        => v.Any(c => c == ' ' || c == ':' || c == '#') ? $"\"{v}\"" : v;

    private static string YamlGet(Dictionary<string, object> map, string key, string def)
        => map.TryGetValue(key, out var v) && v is string s ? s : def;

    private static string YamiStr(Dictionary<string, object> map, string key, string def)
        => map.TryGetValue(key, out var v) && v is string s ? s : def;

    private static int YamiInt(Dictionary<string, object> map, string key, int def)
    {
        if (map.TryGetValue(key, out var v))
        {
            if (v is int i) return i;
            if (v is string s && int.TryParse(s, out var n)) return n;
        }
        return def;
    }
}

/// <summary>Минимальный YAML-парсер: маппинги, списки, скаляры, комментарии, кавычки.</summary>
public static class MiniYaml
{
    public static Dictionary<string, object> Parse(string text)
    {
        var root = new Dictionary<string, object>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var stack = new Stack<(int indent, Dictionary<string, object> map)>();
        stack.Push((0, root));

        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.TrimStart().StartsWith('#')) continue;
            var indent = raw.TakeWhile(char.IsWhiteSpace).Count();
            var content = raw.Substring(indent).Trim();
            // Убираем хвостовой комментарий (вне кавычек)
            content = StripComment(content).TrimEnd();

            // Список: "- item" или "- key: value"
            if (content.StartsWith("- "))
            {
                var item = content.Substring(2).Trim();
                // Пока поддерживаем только простые элементы списка
                continue;
            }

            var colon = FindColon(content);
            if (colon < 0) continue;
            var key = content.Substring(0, colon).Trim().Trim('"', '\'');
            var value = content.Substring(colon + 1).Trim();

            // Вернёмся на нужный уровень: если indent <= верхнего уровня стека — pop
            while (stack.Count > 1 && indent <= stack.Peek().indent) stack.Pop();
            var map = stack.Peek().map;

            if (value.Length == 0)
            {
                // Вложенный маппинг
                var child = new Dictionary<string, object>();
                map[key] = child;
                stack.Push((indent, child));
            }
            else
            {
                map[key] = ParseScalar(value);
            }
        }
        return root;
    }

    private static string StripComment(string s)
    {
        bool inS = false, inD = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\'' && !inD) inS = !inS;
            else if (c == '"' && !inS) inD = !inD;
            else if (c == '#' && !inS && !inD && (i == 0 || s[i - 1] == ' ')) return s.Substring(0, i);
        }
        return s;
    }

    private static int FindColon(string s)
    {
        bool inS = false, inD = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\'' && !inD) inS = !inS;
            else if (c == '"' && !inS) inD = !inD;
            else if (c == ':' && !inS && !inD) return i;
        }
        return -1;
    }

    private static object ParseScalar(string v)
    {
        if (v.Length >= 2 && v[0] == '"' && v[^1] == '"') return v.Substring(1, v.Length - 2);
        if (v.Length >= 2 && v[0] == '\'' && v[^1] == '\'') return v.Substring(1, v.Length - 2);
        if (int.TryParse(v, out var i)) return i;
        if (bool.TryParse(v, out var b)) return b;
        return v;
    }
}

public static class YamlMapExtensions
{
    public static Dictionary<string, object>? GetMap(this Dictionary<string, object> map, string key)
        => map.TryGetValue(key, out var v) && v is Dictionary<string, object> d ? d : null;
}