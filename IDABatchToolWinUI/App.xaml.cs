using Microsoft.UI.Xaml;
using System.IO;

namespace IDABatchToolWinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();

        // Журнал необработанных исключений XAML (stowed 0xc000027b выглядит в
        // журнале событий как немой нативный краш — здесь берём управляемый стек).
        UnhandledException += (s, e) =>
        {
            try
            {
                var msg = $"[{DateTime.Now:HH:mm:ss.fff}] {e.Exception.GetType().FullName}: {e.Message}\n" +
                          $"{e.Exception}\n────────────────\n";
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "app-crash.log"), msg);
            }
            catch { /* логирование не должно мешать падению */ }
        };
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
