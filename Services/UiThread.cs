using Microsoft.UI.Dispatching;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Маршалинг на UI-поток — единая точка вместо четырёх постраничных копий RunOnUi.
/// Ошибки в действии не теряются: задача-вариант идёт через SafeFireAndForget.
/// </summary>
public static class UiThread
{
    /// <summary>Синхронное действие на UI-потоке (постановка в очередь DispatcherQueue).</summary>
    public static void Run(DispatcherQueue queue, Action action)
        => queue.TryEnqueue(() => action());

    /// <summary>Асинхронное действие на UI-потоке: исключение пишется в журнал
    /// (см. TaskExtensions.SafeFireAndForget), а не роняет процесс.</summary>
    public static void Run(DispatcherQueue queue, Func<Task> action, string context)
        => queue.TryEnqueue(() => action().SafeFireAndForget(context));
}
