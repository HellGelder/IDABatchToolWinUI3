using System.Diagnostics;
using System.IO;

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

    /// <summary>
    /// Полный цикл при запуске приложения: если чего-то нет — диалог со списком
    /// и предложением автоматической установки (install-prereqs.ps1 из поставки).
    /// Если всё на месте — ничего не показывается. Вызывается с UI-потока.
    /// </summary>
    public static async Task EnsureEnvironmentAsync()
    {
        // Очередь UI-потока захватываем сразу: Window не имеет свойства
        // DispatcherQueue, а после await мы уже в фоновом потоке.
        Microsoft.UI.Dispatching.DispatcherQueue? uiQueue = null;
        try { uiQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread(); }
        catch { /* не UI-поток — диалоги покажутся напрямую (на свой страх) */ }

        var result = await CheckAsync();
        if (result.AllOk) return;

        var script = FindInstallScript();
        await RunOnUiAsync(uiQueue, async () =>
        {
            var missingText = string.Join("\n• ", result.Missing);
            var message = "Выполнена проверка необходимого программного окружения.\n\n" +
                          "Не обнаружено:\n• " + missingText + "\n\n" +
                          "Без этого генерация HTML-отчётов и документация системных функций " +
                          "работать не будут.\n\n";
            message += script != null
                ? "Можно установить автоматически: будет скачан встроенный Python и пакеты " +
                  "в app\\Tools\\Python (нужен интернет; права администратора не требуются)."
                : "Установите Python 3.10+ с пакетами jinja2 и requests и перезапустите приложение.";

            var buttons = script != null
                ? new string[] { "Установить автоматически", "Продолжить" }
                : new string[] { "Продолжить" };
            var choice = await UiDialogs.AskButtonsAsync("Программное окружение", message, buttons, 0);
            if (script == null || choice != 0) return;

            var code = await RunInstallScriptAsync(script);
            var after = await CheckAsync();
            if (after.AllOk)
            {
                await UiDialogs.InfoAsync("Окружение готово",
                    "Установка завершена.\nPython: " + after.Interpreter +
                    "\nПакеты jinja2 и requests на месте.");
            }
            else
            {
                var details = code == 0
                    ? "Скрипт завершился, но компоненты всё ещё не найдены:\n• "
                      + string.Join("\n• ", after.Missing)
                    : "Скрипт установки завершился с ошибкой (код " + code + ").\n" +
                      "Запустите его вручную: " + script;
                await UiDialogs.WarnAsync("Установка не завершена", details);
            }
        });
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

    /// <summary>Видимый запуск ps1 (пользователь видит прогресс); возвращает код выхода, -1 = не запущен.</summary>
    private static async Task<int> RunInstallScriptAsync(string script)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = true,
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"",
            };
            var proc = Process.Start(psi);
            if (proc == null) return -1;
            await proc.WaitForExitAsync();
            return proc.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private static Task RunOnUiAsync(Microsoft.UI.Dispatching.DispatcherQueue? dq, Func<Task> action)
    {
        if (dq == null) return action();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = dq.TryEnqueue(() => _ = Run());
        async Task Run()
        {
            try { await action(); }
            finally { tcs.SetResult(); }
        }
        return queued ? tcs.Task : action();
    }
}
