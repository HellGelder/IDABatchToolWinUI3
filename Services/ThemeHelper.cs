using Microsoft.UI.Xaml;

namespace IDABatchToolWinUI.Services;

/// <summary>Применение темы оформления (платформенной) к окну.</summary>
public static class ThemeHelper
{
    public static void Apply(Window? window, string theme)
    {
        if (window == null) return;
        var root = window.Content as FrameworkElement;
        if (root == null) return;
        root.RequestedTheme = theme == "dark" ? ElementTheme.Dark : ElementTheme.Light;
    }
}
