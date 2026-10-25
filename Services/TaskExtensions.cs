namespace IDABatchToolWinUI.Services;

/// <summary>
/// Fire-and-forget запуск задачи с гарантией, что исключение не потеряется
/// (unobserved task) и не уронит процесс (async void): ошибка пишется в AppLog
/// и опционально передаётся в колбэк (например, в журнал страницы).
/// </summary>
public static class TaskExtensions
{
    public static void SafeFireAndForget(this Task task, string context, Action<Exception>? onError = null)
    {
        async Task RunAsync()
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                try { AppLog.Error($"[{context}] {ex.GetType().Name}: {ex.Message}", ex); }
                catch { /* журнал недоступен — не усугубляем */ }
                try { onError?.Invoke(ex); }
                catch { /* колбэк не должен ронять процесс */ }
            }
        }
        _ = RunAsync();
    }
}
