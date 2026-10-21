using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IDABatchToolWinUI.Models;

/// <summary>
/// Карточка потока модульного Git-сравнения (по образцу карточек мини-диспетчера
/// анализа): номер потока, текущий модуль, прогресс по файлам, состояние.
/// Обновляется только с UI-потока (колбэки сервиса пробрасываются через RunOnUi).
/// </summary>
public sealed class ThreadCardViewModel : INotifyPropertyChanged
{
    public ThreadCardViewModel(int index) => Title = $"Поток {index}";

    public string Title { get; }

    private string _module = "—";
    public string ModuleText { get => _module; private set { _module = value; Raise(); } }

    private string _state = "ожидание";
    public string StateText { get => _state; private set { _state = value; Raise(); } }

    private string _progress = "";
    public string ProgressText { get => _progress; private set { _progress = value; Raise(); } }

    /// <summary>Поток взял модуль.</summary>
    public void SetModule(string module)
    {
        ModuleText = module;
        StateText = "работает";
        ProgressText = "";
    }

    /// <summary>Прогресс по файлам внутри модуля.</summary>
    public void SetProgress(int current, int total) => ProgressText = $"файл {current}/{total}";

    /// <summary>Пул потока опустел — поток добирает чужие модули или закончил работу.</summary>
    public void SetIdle()
    {
        ModuleText = "—";
        StateText = "свободен";
        ProgressText = "";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
