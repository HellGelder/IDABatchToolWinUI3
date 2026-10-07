using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Отправка системных уведомлений (AppNotification) из WinUI.
/// Используется после завершения анализа и генерации HTML-отчётов.
/// </summary>
public static class AppNotifier
{
    /// <summary>Отправить уведомление. Не бросает исключений при недоступности API.</summary>
    public static void Notify(string title, string body)
    {
        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(title)
                .AddText(body);
            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch
        {
            // Уведомления — не критичны; если API недоступен, молча пропускаем.
        }
    }
}