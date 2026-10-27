using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Тема оформления (платформенная). Тема применяется ко всем открытым окнам
/// (главное и «мини-диспетчер задач») и запоминается для окон и диалогов,
/// созданных позже. ContentDialog живёт в слое XamlRoot и не наследует
/// RequestedTheme корня окна - диалогам тема задаётся явно (ApplyToDialog).
/// </summary>
public static class ThemeHelper
{
    private static readonly List<WeakReference<Window>> Windows = new();
    private static string _currentTheme = "light";

    /// <summary>Текущая тема: "light" | "dark".</summary>
    public static string CurrentTheme => _currentTheme;

    public static ElementTheme CurrentElementTheme =>
        _currentTheme == "dark" ? ElementTheme.Dark : ElementTheme.Light;

    /// <summary>Применить тему и запомнить её: смена темы доходит до всех уже
    /// открытых окон, а новые окна и диалоги сразу получают актуальную.</summary>
    public static void Apply(Window? window, string theme)
    {
        _currentTheme = Normalize(theme);
        if (window != null) Track(window);
        ApplyToWindows();
    }

    /// <summary>Регистрация окна, созданного после смены темы: запоминаем его,
    /// чтобы дальнейшие переключения темы доходили и до него, и сразу применяем
    /// текущую тему.</summary>
    public static void Register(Window? window)
    {
        if (window == null) return;
        Track(window);
        ApplyTo(window);
    }

    /// <summary>Повторно применить запомненную тему ко всем окнам.</summary>
    public static void ApplyToAll() => ApplyToWindows();

    /// <summary>Тема для контекстного диалога (ContentDialog не наследует тему окна).</summary>
    public static void ApplyToDialog(ContentDialog? dialog)
    {
        if (dialog != null) dialog.RequestedTheme = CurrentElementTheme;
    }

    private static string Normalize(string? theme)
        => string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase) ? "dark" : "light";

    private static void Track(Window window)
    {
        PruneClosedWindows();
        foreach (var reference in Windows)
        {
            if (reference.TryGetTarget(out var target) && ReferenceEquals(target, window)) return;
        }
        Windows.Add(new WeakReference<Window>(window));
    }

    private static void ApplyToWindows()
    {
        // Список чистится с конца: удаление сдвигает только последующие элементы.
        for (int i = Windows.Count - 1; i >= 0; i--)
        {
            if (Windows[i].TryGetTarget(out var window)) ApplyTo(window);
            else Windows.RemoveAt(i);
        }
    }

    private static void PruneClosedWindows()
    {
        for (int i = Windows.Count - 1; i >= 0; i--)
        {
            if (!Windows[i].TryGetTarget(out _)) Windows.RemoveAt(i);
        }
    }

    private static void ApplyTo(Window window)
    {
        if (window.Content is FrameworkElement root) root.RequestedTheme = CurrentElementTheme;
    }
}
