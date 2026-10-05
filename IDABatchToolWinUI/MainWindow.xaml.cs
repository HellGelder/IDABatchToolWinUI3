using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IDABatchToolWinUI.Pages;
using IDABatchToolWinUI.Services;

namespace IDABatchToolWinUI;

public sealed partial class MainWindow : Window
{
    private readonly AppConfig _cfg;
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

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Ширина открывающегося окна — на 30% меньше стандартной
        var initial = AppWindow.Size;
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(initial.Width * 0.7), initial.Height));

        _cfg = ConfigService.Load();
        ThemeHelper.Apply(this, _cfg.Theme);

        UiDialogs.MainWindow = this;

        NavView.SelectedItem = NavAnalysis;
        NavFrame.Navigate(typeof(AnalysisPage));
        _analysisPage = NavFrame.Content as AnalysisPage;

        // Проверка Python-окружения — после показа окна; диалог только при нехватке.
        Activated += OnFirstActivated;
    }

    private bool _envCheckStarted;

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_envCheckStarted) return;
        _envCheckStarted = true;
        Activated -= OnFirstActivated;
        _ = PythonEnvironment.EnsureEnvironmentAsync();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        var tag = item.Tag as string;
        if (tag == null || !_pages.ContainsKey(tag)) return;

        // Блокировка навигации во время работы (аналог main_window.py)
        if (_analysisPage?.IsAnalysisRunning() == true)
        {
            if (tag != "analysis") { _ = RestoreSelection("analysis"); return; }
        }
        else if (_diffPage?.IsDiffRunning() == true)
        {
            if (tag != "diff") { _ = RestoreSelection("diff"); return; }
        }
        else if (_sfaPage?.IsAnalysisRunning() == true)
        {
            if (tag != "sfa") { _ = RestoreSelection("sfa"); return; }
        }

        switch (tag)
        {
            case "analysis":
                if (_analysisPage == null)
                {
                    NavFrame.Navigate(typeof(AnalysisPage));
                    _analysisPage = NavFrame.Content as AnalysisPage;
                }
                else if (!ReferenceEquals(NavFrame.Content, _analysisPage))
                {
                    NavFrame.Content = _analysisPage;
                }
                break;
            case "diff":
                if (_diffPage == null)
                {
                    NavFrame.Navigate(typeof(DiffPage));
                    _diffPage = NavFrame.Content as DiffPage;
                }
                else if (!ReferenceEquals(NavFrame.Content, _diffPage))
                {
                    NavFrame.Content = _diffPage;
                }
                break;
            case "sfa":
                if (_sfaPage == null)
                {
                    NavFrame.Navigate(typeof(SfaPage));
                    _sfaPage = NavFrame.Content as SfaPage;
                }
                else if (!ReferenceEquals(NavFrame.Content, _sfaPage))
                {
                    NavFrame.Content = _sfaPage;
                }
                break;
            case "settings":
                if (NavFrame.Content is not SettingsPage)
                {
                    NavFrame.Navigate(typeof(SettingsPage));
                    // Подписка — на фактический контент Frame, а не на временный
                    // экземпляр: иначе событие изменения конфигурации не доходило.
                    if (NavFrame.Content is SettingsPage sp)
                        sp.ConfigChanged += cfg => ThemeHelper.Apply(this, cfg.Theme);
                }
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