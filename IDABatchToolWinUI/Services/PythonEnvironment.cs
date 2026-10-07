using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
            if (script == null)
            {
                await UiDialogs.WarnAsync("Программное окружение",
                    message + "Установите Python 3.10+ с пакетами jinja2 и requests " +
                    "и перезапустите приложение.");
                return;
            }

            message += "Можно установить автоматически: будет скачан встроенный Python и пакеты " +
                       "в app\\Tools\\Python (нужен интернет; права администратора не требуются). " +
                       "Установка выполняется тихо, без консольных окон.";

            await ShowInstallDialogAsync(script, message);
        });
    }

    /// <summary>
    /// Диалог автоматической установки: после нажатия «Установить автоматически»
    /// в кнопке крутится прогрессринг, пока тихо (без консольных окон) работает
    /// install-prereqs.ps1; закрытие диалога на время установки заблокировано.
    /// «Продолжить» закрывает диалог без установки.
    /// </summary>
    private static Task ShowInstallDialogAsync(string script, string message)
    {
        var installing = false;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

        var ring = new ProgressRing
        {
            Width = 16, Height = 16, IsActive = false, Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var installLabel = new TextBlock { Text = "Установить автоматически", VerticalAlignment = VerticalAlignment.Center };
        var installContent = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        installContent.Children.Add(ring);
        installContent.Children.Add(installLabel);

        var installButton = new Button
        {
            Content = installContent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var continueButton = new Button
        {
            Content = "Продолжить",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        stack.Children.Add(installButton);
        stack.Children.Add(continueButton);

        var dialog = new ContentDialog { Title = "Программное окружение", Content = stack };
        if (UiDialogs.XamlRoot != null) dialog.XamlRoot = UiDialogs.XamlRoot;
        // Во время установки диалог закрыть нельзя (Esc/крестик не сработают);
        // закрытие вне установки (Esc или «Продолжить») завершает ожидание.
        dialog.Closing += (_, e) =>
        {
            if (installing) e.Cancel = true;
            else closed.TrySetResult();
        };

        installButton.Click += (_, _) =>
        {
            if (installing) return;
            installing = true;
            ring.Visibility = Visibility.Visible;
            ring.IsActive = true;
            installLabel.Text = "Установка...";
            installButton.IsEnabled = false;
            continueButton.IsEnabled = false;
            _ = RunInstallAsync();
        };
        continueButton.Click += (_, _) =>
        {
            try { dialog.Hide(); } catch { /* уже закрыт */ }
        };

        _ = dialog.ShowAsync();
        return closed.Task;

        async Task RunInstallAsync()
        {
            var (code, logPath) = await RunInstallScriptAsync(script);
            var after = await CheckAsync();
            installing = false;
            closed.TrySetResult();
            try { dialog.Hide(); } catch { /* уже закрыт */ }

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
                      "Подробности — в журнале " + logPath + "\n" +
                      "Скрипт можно запустить и вручную: " + script;
                await UiDialogs.WarnAsync("Установка не завершена", details);
            }
        }
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
    private static async Task<(int Code, string LogPath)> RunInstallScriptAsync(string script)
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

            // Читаем каналы до WaitForExit — иначе переполнение пайпа может
            // заблокировать дочерний процесс.
            var outTask = proc.StandardOutput.ReadToEndAsync();
            var errTask = proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            Task.WhenAll(outTask, errTask).GetAwaiter().GetResult();

            var sb = new StringBuilder();
            sb.AppendLine($"=== install-prereqs {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            sb.AppendLine(outTask.Result);
            if (errTask.Result.Length > 0)
                sb.AppendLine("--- stderr ---").AppendLine(errTask.Result);
            File.WriteAllText(logPath, sb.ToString(), new UTF8Encoding(false));
            return (proc.ExitCode, logPath);
        }
        catch
        {
            return (-1, logPath);
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
