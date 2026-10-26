using System.Diagnostics;
using System.IO;
using System.Text;

namespace IDABatchToolWinUI.Services;

/// <summary>Результат проверки Python-окружения.</summary>
public sealed record PythonEnvResult(string? Interpreter, bool PackagesOk, IReadOnlyList<string> Missing)
{
    public bool AllOk => Missing.Count == 0;
}

/// <summary>
/// Проверка Python-окружения (интерпретатор + пакеты jinja2/requests), нужного
/// Python-мостам (генерация отчётов, документация СФ). Проверка повторяет логику
/// мостов: интерпретатор из Tools\Python, PATH или Py Launcher; site-packages
/// встроенного Python подключается через PYTHONPATH.
/// Диалоги проверки/установки - в PythonEnvironmentUi (UI-слой), сервис UI не трогает.
/// </summary>
public static class PythonEnvironment
{
    /// <summary>Быстрая проверка в фоновом потоке.</summary>
    public static Task<PythonEnvResult> CheckAsync() => Task.Run(Check);

    public static PythonEnvResult Check()
    {
        var interpreter = FindInterpreter();
        if (interpreter == null)
        {
            return new PythonEnvResult(null, false, new[]
            {
                "Python (интерпретатор)",
                "пакеты Python: jinja2, requests",
            });
        }

        var packagesOk = CheckPackages(interpreter);
        var missing = packagesOk
            ? Array.Empty<string>()
            : (IReadOnlyList<string>)new[] { "пакеты Python: jinja2, requests" };
        return new PythonEnvResult(interpreter, packagesOk, missing);
    }

    /// <summary>Интерпретатор: Tools\Python → PATH → Py Launcher. null = не найден.</summary>
    public static string? FindInterpreter()
    {
        var tools = Path.Combine(AppConstants.WinUiDir, "Tools", "Python");
        foreach (var name in new[] { "python.exe", "pythonw.exe" })
        {
            var cand = Path.Combine(tools, name);
            if (File.Exists(cand)) return cand;
        }
        foreach (var name in new[] { "python.exe", "pythonw.exe", "py.exe" })
        {
            var found = PythonHelper.FindOnPath(name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Импорт-тест пакетов: «python -c "import jinja2, requests"» с PYTHONPATH
    /// на site-packages встроенного Python (как делают мосты). Таймаут 20 с.
    /// </summary>
    private static bool CheckPackages(string interpreter)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = interpreter,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("import jinja2, requests");
            var sitePackages = Path.Combine(AppConstants.WinUiDir, "Tools", "Python", "site-packages");
            if (Directory.Exists(sitePackages)) psi.Environment["PYTHONPATH"] = sitePackages;

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            if (!proc.WaitForExit(20000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* уже завершён */ }
                return false;
            }
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>install-prereqs.ps1: корень поставки (рядом с лончером) или каталог приложения.</summary>
    public static string? FindInstallScript()
    {
        var parent = Path.GetDirectoryName(AppConstants.WinUiDir.TrimEnd(Path.DirectorySeparatorChar));
        foreach (var dir in new[] { parent, AppConstants.WinUiDir })
        {
            if (string.IsNullOrEmpty(dir)) continue;
            var cand = Path.Combine(dir, "install-prereqs.ps1");
            if (File.Exists(cand)) return cand;
        }
        return null;
    }

    /// <summary>
    /// Тихий запуск install-prereqs.ps1: без консольного окна (CreateNoWindow),
    /// вывод пишется в журнал рядом со скриптом. Возвращает (код выхода, путь
    /// журнала); код -1 = процесс не запущен.
    /// </summary>
    public static async Task<(int Code, string LogPath)> RunInstallScriptAsync(string script)
    {
        var logPath = Path.Combine(
            Path.GetDirectoryName(script) ?? AppConstants.WinUiDir, "install-prereqs.log");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            psi.Environment["PYTHONUTF8"] = "1";

            using var proc = Process.Start(psi);
            if (proc == null) return (-1, logPath);

            // Читаем каналы до WaitForExit - иначе переполнение пайпа может
            // заблокировать дочерний процесс.
            var outTask = proc.StandardOutput.ReadToEndAsync();
            var errTask = proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            await Task.WhenAll(outTask, errTask);

            var sb = new StringBuilder();
            sb.AppendLine($"=== install-prereqs {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            sb.AppendLine(await outTask);
            var stderr = await errTask;
            if (stderr.Length > 0)
                sb.AppendLine("--- stderr ---").AppendLine(stderr);
            File.WriteAllText(logPath, sb.ToString(), new UTF8Encoding(false));
            return (proc.ExitCode, logPath);
        }
        catch
        {
            return (-1, logPath);
        }
    }
}
