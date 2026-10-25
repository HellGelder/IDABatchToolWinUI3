using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Помощник для диалогов WinUI (аналог QMessageBox / QFileDialog из исполнения 1).
/// Диалоги привязаны к главному окну и показываются асинхронно.
/// </summary>
public static class UiDialogs
{
    public static Window? MainWindow { get; set; }

    public static XamlRoot? XamlRoot => MainWindow?.Content?.XamlRoot;

    // Одновременно может быть показан только один модальный ContentDialog:
    // второй ShowAsync в WinUI бросает исключение. Вместо исключения второй
    // вызов сразу получает «отмену» (и пишется в журнал).
    private static readonly object DialogGate = new();
    private static bool _dialogOpen;

    private static bool TryBeginDialog()
    {
        lock (DialogGate)
        {
            if (_dialogOpen) return false;
            _dialogOpen = true;
            return true;
        }
    }

    private static void EndDialog()
    {
        lock (DialogGate) _dialogOpen = false;
    }

    private static ContentDialog MakeDialog(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
        };
        if (XamlRoot != null) dialog.XamlRoot = XamlRoot;
        return dialog;
    }

    public static async Task InfoAsync(string title, string message)
    {
        if (!TryBeginDialog())
        {
            AppLog.Warn($"Диалог «{title}» не показан: уже открыт другой диалог.");
            return;
        }
        try
        {
            var dlg = MakeDialog(title, message);
            try { await dlg.ShowAsync(); } catch { /* окно могло закрыться */ }
        }
        finally { EndDialog(); }
    }

    public static async Task WarnAsync(string title, string message)
    {
        if (!TryBeginDialog())
        {
            AppLog.Warn($"Диалог «{title}» не показан: уже открыт другой диалог.");
            return;
        }
        try
        {
            var dlg = MakeDialog(title, message);
            try { await dlg.ShowAsync(); } catch { /* окно могло закрыться */ }
        }
        finally { EndDialog(); }
    }

    /// <summary>
    /// Выбор папки. Используется пикер из Windows App SDK
    /// (Microsoft.Windows.Storage.Pickers): он принимает WindowId в конструкторе,
    /// не требует InitializeWithWindow и не требует FileTypeFilter.
    /// </summary>
    public static async Task PickFolderAsync(TextBox target)
    {
        var window = MainWindow;
        if (window == null) return;
        try
        {
            var picker = new FolderPicker(window.AppWindow.Id)
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                CommitButtonText = "Выбрать папку",
            };
            var result = await picker.PickSingleFolderAsync();
            if (result == null) return;

            if (string.IsNullOrEmpty(result.Path))
            {
                await WarnAsync("Папка недоступна",
                    $"У выбранной папки «{result.Path}» нет файлового пути.\nВыберите реальную папку на диске.");
                return;
            }
            target.Text = result.Path;
        }
        catch (Exception ex)
        {
            // Обработчик кнопки — async void: исключение здесь уронило бы процесс.
            await WarnAsync("Ошибка выбора папки", ex.Message);
        }
    }

    /// <summary>Выбор файла по фильтру расширения (например, ".exe").</summary>
    public static async Task PickFileAsync(TextBox target, string filter)
    {
        var window = MainWindow;
        if (window == null) return;
        try
        {
            var picker = new FileOpenPicker(window.AppWindow.Id)
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
            };
            picker.FileTypeFilter.Add(filter);
            var result = await picker.PickSingleFileAsync();
            if (result != null && !string.IsNullOrEmpty(result.Path)) target.Text = result.Path;
        }
        catch (Exception ex)
        {
            await WarnAsync("Ошибка выбора файла", ex.Message);
        }
    }

    /// <summary>Диалог с выбором файла (idat.exe/bindiff.exe).</summary>
    public static async Task<string?> PickExeAsync(string title)
    {
        var window = MainWindow;
        if (window == null) return null;
        try
        {
            var picker = new FileOpenPicker(window.AppWindow.Id)
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
            };
            picker.FileTypeFilter.Add(".exe");
            picker.FileTypeFilter.Add("*");
            var result = await picker.PickSingleFileAsync();
            return result?.Path;
        }
        catch (Exception ex)
        {
            await WarnAsync("Ошибка выбора файла", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Кастомный диалог с произвольным набором кнопок (аналог QMessageBox.addButton).
    /// Возвращает Index выбранной кнопки (-1 = закрыто/отменено).
    /// Если ShowAsync падает (например, окно закрыто) — задача завершается
    /// исключением, а не висит вечно, как прежний fire-and-forget вариант.
    /// </summary>
    public static async Task<int> AskButtonsAsync(string title, string message,
        IReadOnlyList<string> buttons, int defaultIndex = 0)
    {
        if (!TryBeginDialog())
        {
            AppLog.Warn($"Диалог «{title}» не показан: уже открыт другой диалог; возвращена отмена.");
            return -1;
        }
        try
        {
            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dialog = new ContentDialog { Title = title, Content = stack, DefaultButton = ContentDialogButton.Primary };
            if (XamlRoot != null) dialog.XamlRoot = XamlRoot;

            for (int i = 0; i < buttons.Count; i++)
            {
                var idx = i;
                var btn = new Button
                {
                    Content = buttons[i],
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                };
                btn.Click += (_, _) => tcs.TrySetResult(idx);
                stack.Children.Add(btn);
            }

            // Если пользователь закрыл диалог крестиком — вернуть -1
            dialog.Closing += (_, _) => tcs.TrySetResult(-1);

            // Ожидаем ShowAsync: при ошибке исключение уйдёт вызывающему через tcs,
            // а не оставит его ждать вечно.
            try { await dialog.ShowAsync(); }
            catch (Exception ex) { tcs.TrySetException(ex); }

            var result = await tcs.Task;
            try { dialog.Hide(); } catch { /* уже закрыт */ }
            return result;
        }
        finally { EndDialog(); }
    }
}