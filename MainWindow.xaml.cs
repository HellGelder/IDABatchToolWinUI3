using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IDABatchToolWinUI.Pages;
using IDABatchToolWinUI.Services;

namespace IDABatchToolWinUI;

public sealed partial class MainWindow : Window
{
    private readonly IConfigStore _store;
    private readonly Dictionary<string, Type> _pages = new()
    {
        ["analysis"] = typeof(AnalysisPage),
        ["diff"] = typeof(DiffPage),
        ["sfa"] = typeof(SfaPage),
        ["settings"] = typeof(SettingsPage),
    };

    // Страницы кэшируются, чтобы переходы по вкладкам не сбрасывали состояние
    // (путь, прогресс анализа и т.п.). NavFrame.Navigate() выбрасывает старую страницу,
    // поэтому переключение делаем переназначением Frame.Content.
    private AnalysisPage? _analysisPage;
    private DiffPage? _diffPage;
    private SfaPage? _sfaPage;

    private bool _allowClose;
    private bool _closePromptOpen;

    public MainWindow(IConfigStore store)
    {
        InitializeComponent();

        _store = store;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Ширина открывающегося окна — на 30% меньше стандартной
        var initial = AppWindow.Size;
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(initial.Width * 0.7), initial.Height));

        ThemeHelper.Apply(this, _store.Current.Theme);
        // Тема применяется сразу при сохранении конфига (SettingsPage пишет
        // через тот же store) — подписка живёт столько же, сколько окно.
        _store.Changed += cfg => ThemeHelper.Apply(this, cfg.Theme);

        UiDialogs.MainWindow = this;

        NavView.SelectedItem = NavAnalysis;
        _analysisPage = new AnalysisPage(_store);
        NavFrame.Content = _analysisPage;

        // Проверка Python-окружения — после показа окна; диалог только при нехватке.
        Activated += OnFirstActivated;

        // Закрытие окна: не оставляем IDA/Python/BinDiff сиротами.
        AppWindow.Closing += OnAppWindowClosing;
        Closed += (_, _) =>
        {
            AppLog.Info("Завершение приложения");
            JobObject.Current?.Terminate();
            JobObject.Current?.Dispose();
            JobObject.Current = null;
        };
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose) return;

        var busy = _analysisPage?.IsBusy == true
                   || _diffPage?.IsBusy == true
                   || _sfaPage?.IsBusy == true
                   || ProcessRegistry.HasActive;
        if (!busy)
        {
            _allowClose = true;
            return;
        }

        args.Cancel = true;
        if (_closePromptOpen) return;
        _closePromptOpen = true;
        try
        {
            bool confirmed = false;
            try { confirmed = await ShowCloseConfirmAsync(); }
            catch { confirmed = false; }
            finally { _closePromptOpen = false; }
            if (!confirmed) return;

            _analysisPage?.CancelAll();
            _diffPage?.CancelAll();
            _sfaPage?.CancelAll();

            // Ожидание завершения при закрытии (не таймаут фазы): даём процессам
            // корректно завершиться, затем добиваем оставшиеся.
            await ProcessRegistry.WaitForExitAsync(TimeSpan.FromSeconds(10));
            ProcessRegistry.KillAll();
            JobObject.Current?.Terminate();

            _allowClose = true;
            Close();
        }
        catch (Exception ex)
        {
            // Ошибка процедуры закрытия не должна оставлять окно незакрываемым.
            AppLog.Error("Ошибка при закрытии окна", ex);
            _allowClose = true;
            Close();
        }
    }

    private async Task<bool> ShowCloseConfirmAsync()
    {
        // XamlRoot может быть ещё не готов, если закрытие пришло очень рано:
        // в этом случае не блокируем выход, иначе окно станет невозможно закрыть.
        var xamlRoot = Content?.XamlRoot;
        if (xamlRoot == null) return true;

        var dialog = new ContentDialog
        {
            Title = "Идут задачи",
            Content = "Анализ, сравнение или генерация отчётов ещё выполняется. " +
                      "Отменить задачи и закрыть приложение?",
            PrimaryButtonText = "Отменить и закрыть",
            CloseButtonText = "Не закрывать",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private bool _envCheckStarted;

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_envCheckStarted) return;
        _envCheckStarted = true;
        Activated -= OnFirstActivated;
        PythonEnvironment.EnsureEnvironmentAsync().SafeFireAndForget("Startup.PythonEnv");
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        var tag = item.Tag as string;
        if (tag == null || !_pages.ContainsKey(tag)) return;

        // Блокировка навигации во время работы (аналог main_window.py)
        if (_analysisPage?.IsAnalysisRunning() == true)
        {
            if (tag != "analysis") { RestoreSelection("analysis").SafeFireAndForget("Nav.RestoreSelection"); return; }
        }
        else if (_diffPage?.IsDiffRunning() == true)
        {
            if (tag != "diff") { RestoreSelection("diff").SafeFireAndForget("Nav.RestoreSelection"); return; }
        }
        else if (_sfaPage?.IsAnalysisRunning() == true)
        {
            if (tag != "sfa") { RestoreSelection("sfa").SafeFireAndForget("Nav.RestoreSelection"); return; }
        }

        switch (tag)
        {
            case "analysis":
                _analysisPage ??= new AnalysisPage(_store);
                if (!ReferenceEquals(NavFrame.Content, _analysisPage))
                    NavFrame.Content = _analysisPage;
                break;
            case "diff":
                _diffPage ??= new DiffPage(_store);
                if (!ReferenceEquals(NavFrame.Content, _diffPage))
                    NavFrame.Content = _diffPage;
                break;
            case "sfa":
                _sfaPage ??= new SfaPage(_store);
                if (!ReferenceEquals(NavFrame.Content, _sfaPage))
                    NavFrame.Content = _sfaPage;
                break;
            case "settings":
                // SettingsPage не кэшируется (как и раньше: Navigate создавал
                // новый экземпляр на каждый показ) — читает актуальный конфиг.
                if (NavFrame.Content is not SettingsPage)
                    NavFrame.Content = new SettingsPage(_store);
                break;
        }
    }

    private async Task RestoreSelection(string tag)
    {
        // Ждём завершения перехода, затем восстанавливаем выбранный пункт.
        await Task.Delay(1);
        NavView.SelectedItem = tag switch
        {
            "analysis" => NavAnalysis,
            "diff" => NavDiff,
            "sfa" => NavSfa,
            _ => NavSettings,
        };
    }
}