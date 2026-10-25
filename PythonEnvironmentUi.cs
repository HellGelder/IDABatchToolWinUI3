using IDABatchToolWinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IDABatchToolWinUI;

/// <summary>
/// UI-слой проверки Python-окружения: диалог при нехватке компонентов и диалог
/// автоматической установки. Сама проверка и установка — в Services/PythonEnvironment
/// (сервис); сюда вынесено всё, что строит ContentDialog.
/// </summary>
public static class PythonEnvironmentUi
{
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

        var result = await PythonEnvironment.CheckAsync();
        if (result.AllOk) return;

        var script = PythonEnvironment.FindInstallScript();
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
            RunInstallAsync().SafeFireAndForget("PythonEnv.Install");
        };
        continueButton.Click += (_, _) =>
        {
            try { dialog.Hide(); } catch { /* уже закрыт */ }
        };

        _ = dialog.ShowAsync();
        return closed.Task;

        async Task RunInstallAsync()
        {
            var (code, logPath) = await PythonEnvironment.RunInstallScriptAsync(script);
            var after = await PythonEnvironment.CheckAsync();
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

    private static Task RunOnUiAsync(Microsoft.UI.Dispatching.DispatcherQueue? dq, Func<Task> action)
    {
        if (dq == null) return action();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = dq.TryEnqueue(() => Run().SafeFireAndForget("PythonEnv.RunOnUi"));
        // Исключение действия передаётся ожидающему через tcs, а не теряется:
        // вызывающий (EnsureEnvironmentAsync) запускается через SafeFireAndForget
        // и запишет его в журнал.
        async Task Run()
        {
            try { await action(); tcs.TrySetResult(); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        }
        return queued ? tcs.Task : action();
    }
}
