using Microsoft.Win32;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Поиск idat.exe / bindiff.exe — аналог функций поиска в config/loader.py.
/// Порядок: значение из конфига → реестр Windows → C:\Program Files → PATH.
/// </summary>
public static class ToolLocator
{
    public static string GetIdaExecutable(AppConfig cfg)
    {
        var name = string.IsNullOrWhiteSpace(cfg.IdaExecutable) ? "idat.exe" : cfg.IdaExecutable;
        return ResolveExe(name, FindIdaInCommonLocations, "idat.exe");
    }

    public static string GetBindiffExecutable(AppConfig cfg)
    {
        var name = string.IsNullOrWhiteSpace(cfg.BindiffExecutable) ? "bindiff.exe" : cfg.BindiffExecutable;
        return ResolveExe(name, FindBindiffInCommonLocations, "bindiff.exe");
    }

    private static string ResolveExe(string name, Func<string?> systemSearch, string exeName)
    {
        // 1. Полный путь из конфига
        if (Path.IsPathFullyQualified(name))
        {
            var p = Path.GetFullPath(name);
            return File.Exists(p) ? p : name;
        }

        // 2. Поиск в системе (реестр, Program Files, PATH)
        var found = systemSearch();
        if (found != null) return found;

        // 3. Рядом с приложением (config.yaml исполнения 2)
        var proj = Path.Combine(AppConstants.WinUiDir, exeName);
        if (File.Exists(proj)) return proj;

        return name;
    }

    private static string? FindIdaInCommonLocations()
    {
        // Реестр Hex-Rays
        foreach (var key in new[] { @"SOFTWARE\Hex-Rays\IDA", @"SOFTWARE\IDA Pro" })
        {
            var dir = ReadRegistryDir(key);
            if (dir != null)
            {
                foreach (var exe in new[] { "idat.exe", "idat64.exe" })
                {
                    var cand = Path.Combine(dir, exe);
                    if (File.Exists(cand)) return cand;
                }
            }
        }

        var candidates = new[]
        {
            "IDA*/idat.exe", "IDA*/idat64.exe",
            "IDA Professional */idat.exe", "IDA Professional */idat64.exe",
            "IDA Pro */idat.exe", "IDA Pro */idat64.exe",
            "Hex-Rays/IDA*/idat.exe", "Hex-Rays/IDA*/idat64.exe",
        };
        var pf = FindInProgramFiles(candidates);
        if (pf != null) return pf;

        if (FindInPath("idat.exe") is { } p1) return p1;
        if (FindInPath("idat64.exe") is { } p2) return p2;
        return null;
    }

    private static string? FindBindiffInCommonLocations()
    {
        foreach (var key in new[]
                 { @"SOFTWARE\Zynamics\BinDiff", @"SOFTWARE\Google\BinDiff", @"SOFTWARE\BinDiff" })
        {
            var dir = ReadRegistryDir(key);
            if (dir != null)
            {
                var cand = Path.Combine(dir, "bindiff.exe");
                if (File.Exists(cand)) return cand;
            }
        }

        var candidates = new[]
        {
            "BinDiff*/bindiff.exe", "BinDiff */bindiff.exe",
            "zynamics/BinDiff*/bindiff.exe", "Google/BinDiff*/bindiff.exe",
        };
        var pf = FindInProgramFiles(candidates);
        if (pf != null) return pf;

        if (FindInPath("bindiff.exe") is { } p1) return p1;
        if (FindInPath("BinDiff.exe") is { } p2) return p2;
        return null;
    }

    private static string? ReadRegistryDir(string keyPath)
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = baseKey.OpenSubKey(keyPath);
                var val = key?.GetValue("InstallDir") as string;
                if (!string.IsNullOrEmpty(val)) return val;
            }
            catch { /* ключ отсутствует */ }
        }
        return null;
    }

    private static string? FindInProgramFiles(params string[] patterns)
    {
        foreach (var pf in new[] { @"C:\Program Files", @"C:\Program Files (x86)" })
        {
            if (!Directory.Exists(pf)) continue;
            foreach (var pattern in patterns)
            {
                try
                {
                    var matches = Directory.GetFiles(pf, pattern, SearchOption.TopDirectoryOnly);
                    // Directory.GetFiles не поддерживает '/' в паттерне; делаем ручной обход верхнего уровня
                    if (matches.Length > 0) return matches[0];
                }
                catch { /* нет доступа */ }
            }
            // Ручной поиск: перебираем папки верхнего уровня по маске
            foreach (var pattern in patterns)
            {
                var parts = pattern.Split('/');
                if (parts.Length != 2) continue;
                try
                {
                    foreach (var dir in Directory.GetDirectories(pf, parts[0], SearchOption.TopDirectoryOnly))
                    {
                        var cand = Path.Combine(dir, parts[1]);
                        if (File.Exists(cand)) return cand;
                        // Регистронезависимый вариант первой буквы
                        var altDir = Path.Combine(Path.GetDirectoryName(dir) ?? pf,
                            char.ToLowerInvariant(Path.GetFileName(dir)[0]) + Path.GetFileName(dir).Substring(1));
                        if (Directory.Exists(altDir))
                        {
                            var alt = Path.Combine(altDir, parts[1]);
                            if (File.Exists(alt)) return alt;
                        }
                    }
                }
                catch { /* нет доступа */ }
            }
        }
        return null;
    }

    private static string? FindInPath(string exeName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                var cand = Path.Combine(dir.Trim('"'), exeName);
                if (File.Exists(cand)) return Path.GetFullPath(cand);
            }
            catch { /* невалидный путь в PATH */ }
        }
        return null;
    }
}