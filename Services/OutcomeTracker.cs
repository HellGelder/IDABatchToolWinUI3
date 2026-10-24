using System.Collections.Concurrent;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Потокобезопасный учёт результатов по ключам (файл/пара). Элемент считается
/// успешным, пока хотя бы одна фаза не сообщила об ошибке. Итог берётся после
/// завершения всех фаз, поэтому промежуточные состояния не важны.
/// </summary>
public sealed class OutcomeTracker
{
    private readonly ConcurrentDictionary<string, bool> _state;

    public OutcomeTracker(IEnumerable<string> keys)
    {
        _state = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            if (!string.IsNullOrEmpty(key)) _state[key] = true;
        }
    }

    public int Total => _state.Count;
    public int Failed => _state.Count(static kv => !kv.Value);
    public int Succeeded => Total - Failed;

    /// <summary>Отмечает провал фазы; ключ добавляется, даже если его не было.</summary>
    public void Fail(string key)
    {
        if (!string.IsNullOrEmpty(key)) _state[key] = false;
    }

    /// <summary>Успех по ключу (идемпотентно; провал не перекрывает).</summary>
    public void MarkSucceeded(string key)
    {
        if (!string.IsNullOrEmpty(key)) _state.TryAdd(key, true);
    }

    public bool IsFailed(string key)
        => _state.TryGetValue(key, out var ok) && !ok;
}
