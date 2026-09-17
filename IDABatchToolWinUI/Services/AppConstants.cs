using System.IO;

namespace IDABatchToolWinUI.Services;

/// <summary>Константы приложения и платформ — аналог ida_batch_tool/ui/constants.py.</summary>
public static class AppConstants
{
    // Исполнение 2 полностью самодостаточно: свои config.yaml, scripts/, _python/.
    public static readonly string WinUiDir = ResolveWinUiDir();
    public static readonly string ConfigPath = Path.Combine(WinUiDir, "config.yaml");
    public static readonly string ScriptsDir = Path.Combine(WinUiDir, "scripts");
    public static readonly string PythonwPath = PythonHelper.ResolvePython();
    public static readonly string IdatDefaultExe = "idat.exe";
    public static readonly string BindiffDefaultExe = "bindiff.exe";
    public static readonly string ManpagesDbFileName = "manpages.db";

    /// <summary>
    /// Каталог приложения (исполнения 2), где лежат config.yaml, scripts/, _python/.
    /// Ищется от каталога exe вверх до каталога с _python (после копирования в bin — на месте).
    /// </summary>
    private static string ResolveWinUiDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            // рядом с exe (bin/.../_python при копировании) или в исходном дереве
            if (Directory.Exists(Path.Combine(dir.FullName, "_python")) ||
                (File.Exists(Path.Combine(dir.FullName, "config.yaml")) &&
                 File.Exists(Path.Combine(dir.FullName, "IDABatchToolWinUI.csproj"))))
                return dir.FullName;
            dir = dir.Parent;
        }
        return AppContext.BaseDirectory;
    }
}

/// <summary>Статусы анализа файла для тремапа и таблиц.</summary>
public enum AnalysisStatus
{
    NotAnalyzed,
    InProgress,
    Success,
    Error,
}

public static class AnalysisStatusExtensions
{
    public static string ToText(this AnalysisStatus s) => s switch
    {
        AnalysisStatus.NotAnalyzed => "not_analyzed",
        AnalysisStatus.InProgress => "in_progress",
        AnalysisStatus.Success => "success",
        AnalysisStatus.Error => "error",
        _ => "not_analyzed",
    };
}

/// <summary>Описания целевых платформ и их расширений.</summary>
/// <summary>Поиск интерпретатора Python для запуска скриптов (pythonw предпочтительнее — без консоли).</summary>
public static class PythonHelper
{
    public static string ResolvePython()
    {
        // Приоритет: pythonw.exe в PATH → python.exe → зарегистрированный в системе
        foreach (var name in new[] { "pythonw.exe", "python.exe" })
        {
            var found = FindInPath(name);
            if (found != null) return found;
        }
        // Py Launcher
        foreach (var name in new[] { "pyw.exe", "py.exe" })
        {
            var found = FindInPath(name);
            if (found != null) return found;
        }
        return "pythonw.exe";
    }

    private static string? FindInPath(string exe)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                var cand = Path.Combine(dir.Trim('"'), exe);
                if (File.Exists(cand)) return Path.GetFullPath(cand);
            }
            catch { /* невалидный путь */ }
        }
        return null;
    }
}

public static class PlatformInfo
{
    public sealed record PlatformDef(string Label, string[] Exts);

    public static readonly IReadOnlyDictionary<string, PlatformDef> Platforms = new Dictionary<string, PlatformDef>
    {
        ["Windows"] = new("Windows", new[] { ".exe", ".dll", ".sys", ".ocx", ".cpl", ".scr", ".drv", ".efi" }),
        ["Linux / Android"] = new("Linux / Android", new[] { ".elf", ".so", ".o", ".ko", ".dex" }),
        ["macOS / iOS"] = new("macOS / iOS", new[] { ".mach-o", ".dylib", ".bundle", ".app" }),
    };
    // Для macOS оставим пустое расширение в списке — файлы без расширения проверяются по сигнатуре,
    // как в оригинале (там в exts есть "").

    public static string[] ExtsFor(string? key)
    {
        if (key != null && Platforms.TryGetValue(key, out var d)) return d.Exts;
        var all = new List<string>();
        foreach (var pl in Platforms.Values)
            foreach (var e in pl.Exts)
                if (!all.Contains(e)) all.Add(e);
        return all.ToArray();
    }

    public static string[] Keys => Platforms.Keys.ToArray();

    /// <summary>Определяет платформу по расширениям файлов (по числу совпадений).</summary>
    public static string DetectByFiles(IEnumerable<string> suffixes)
    {
        var counts = new Dictionary<string, int> { ["Windows"] = 0, ["Linux / Android"] = 0, ["macOS / iOS"] = 0 };
        foreach (var s in suffixes.Select(x => x.ToLowerInvariant()))
            foreach (var (key, d) in Platforms)
                if (d.Exts.Contains(s)) counts[key]++;
        var best = counts.OrderByDescending(kv => kv.Value).First().Key;
        return counts[best] > 0 ? best : "Windows";
    }
}