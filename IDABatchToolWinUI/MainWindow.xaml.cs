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

        _cfg = ConfigService.Load();
        ThemeHelper.Apply(this, _cfg.Theme);

        UiDialogs.MainWindow = this;

        NavView.SelectedItem = NavAnalysis;
        NavFrame.Navigate(typeof(AnalysisPage));
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
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
                    _analysisPage = new AnalysisPage();
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
                    _diffPage = new DiffPage();
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
                    _sfaPage = new SfaPage();
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
                    var settings = new SettingsPage();
                    settings.ConfigChanged += cfg => ThemeHelper.Apply(this, cfg.Theme);
                    NavFrame.Navigate(typeof(SettingsPage));
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