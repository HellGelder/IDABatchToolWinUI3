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

/// <summary>Поиск интерпретатора Python для запуска скриптов (pythonw предпочтительнее — без консоли).</summary>
public static class PythonHelper
{
    public static string ResolvePython()
    {
        // 1) Python, положенный рядом с приложением (Tools\Python) — для
        //    переносимой поставки без установленного Python в системе.
        var portable = Path.Combine(AppConstants.WinUiDir, "Tools", "Python");
        foreach (var name in new[] { "pythonw.exe", "python.exe" })
        {
            var cand = Path.Combine(portable, name);
            if (File.Exists(cand)) return cand;
        }
        // 2) pythonw.exe/python.exe в PATH → 3) Py Launcher
        foreach (var name in new[] { "pythonw.exe", "python.exe" })
        {
            var found = FindInPath(name);
            if (found != null) return found;
        }
        foreach (var name in new[] { "pyw.exe", "py.exe" })
        {
            var found = FindInPath(name);
            if (found != null) return found;
        }
        return "pythonw.exe";
    }

    /// <summary>Публичный поиск exe по PATH (для проверки окружения).</summary>
    public static string? FindOnPath(string exe) => FindInPath(exe);

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

    public static string[] Keys => Platforms.Keys.ToArray();

    /// <summary>Все расширения всех платформ одним списком (без дубликатов) —
    /// для поиска файлов, когда платформа ещё не определена.</summary>
    public static string[] AllExtensions()
    {
        var all = new List<string>();
        foreach (var pl in Platforms.Values)
            foreach (var e in pl.Exts)
                if (!all.Contains(e)) all.Add(e);
        return all.ToArray();
    }

    /// <summary>Отображение расширений платформы для инфобара: «.exe, .dll, …».</summary>
    public static string ExtsDisplay(string key) =>
        Platforms.TryGetValue(key, out var d) ? string.Join(", ", d.Exts) : "";

    /// <summary>
    /// Определяет платформу по фактическим файлам: по расширениям; файлы
    /// без расширения — по сигнатуре (PE / ELF / Mach-O). При равных счётчиках
    /// и при пустом результате возвращается Windows (как и раньше).
    /// </summary>
    public static string DetectPlatform(IEnumerable<string> paths)
    {
        var counts = new Dictionary<string, int> { ["Windows"] = 0, ["Linux / Android"] = 0, ["macOS / iOS"] = 0 };
        foreach (var p in paths)
        {
            var ext = Path.GetExtension(p).ToLowerInvariant();
            if (ext == "")
            {
                var sig = ClassifyBySignature(p);
                if (sig != null) counts[sig]++;
                continue;
            }
            foreach (var (key, d) in Platforms)
            {
                if (d.Exts.Contains(ext)) { counts[key]++; break; }
            }
        }
        var best = counts.OrderByDescending(kv => kv.Value).First().Key;
        return counts[best] > 0 ? best : "Windows";
    }

    private static string? ClassifyBySignature(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> header = stackalloc byte[4];
            int n = fs.Read(header);
            if (n < 4) return null;
            if (header[0] == 0x7F && header[1] == (byte)'E' && header[2] == (byte)'L' && header[3] == (byte)'F')
                return "Linux / Android";
            if (header[0] == (byte)'M' && header[1] == (byte)'Z') return "Windows";
            var magic = BitConverter.ToUInt32(header);
            if (magic is 0xFEEDFACE or 0xFEEDFACF or 0xCAFEBABE or 0xCEFAEDFE or 0xCFFAEDFE)
                return "macOS / iOS";
            return null;
        }
        catch
        {
            return null;
        }
    }
}