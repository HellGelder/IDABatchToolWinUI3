using Microsoft.UI.Xaml;
using IDABatchToolWinUI.Services;

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

        // Постоянный журнал: рядом с exe и в %LOCALAPPDATA% одновременно.
        AppLog.Initialize(ConfigService.Load().LogLevel);
        AppLog.Info($"Запуск приложения: {AppContext.BaseDirectory}");

        // Журнал необработанных исключений XAML (stowed 0xc000027b выглядит в
        // журнале событий как немой нативный краш — здесь берём управляемый стек).
        UnhandledException += (s, e) =>
        {
            try { AppLog.Crash(e.Exception); }
            catch { /* логирование не должно мешать падению */ }
            // Единичный сбой обработчика не должен убивать приложение целиком:
            // помечаем исключение обработанным и продолжаем работу.
            e.Handled = true;
        };

        // Исключения «потерянных» задач (_ = ...) по умолчанию молча пропадают:
        // держим последний рубеж — пишем в журнал, но процесс не роняем.
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            try
            {
                AppLog.Error("Необработанное исключение фоновой задачи", e.Exception);
            }
            catch { /* логирование не должно мешать */ }
            e.SetObserved();
        };
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // Job Object с KILL_ON_JOB_CLOSE: при завершении приложения (включая
        // аварийное) ОС сама убивает дочерние idat/python/bindiff/7z.
        JobObject.Current = JobObject.Create();

        _window = new MainWindow();
        _window.Activate();
    }
}
