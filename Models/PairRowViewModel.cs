using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IDABatchToolWinUI.Models;

/// <summary>Строка таблицы сопоставленных пар для страницы Сравнения (INPC для x:Bind).</summary>
public sealed class PairRowViewModel : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private string _bindiffStatus = "waiting";
    private string _diaphoraStatus = "waiting";
    private string _pidText = "—";

    public required DiffPair Pair { get; init; }
    public string RelKey => Pair.RelKey;
    public string ToolTip => $"{Pair.Primary}\n{Pair.Secondary}";
    public string SizeText { get; init; } = "";

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    /// <summary>Сырой статус BinDiff: waiting | analysis | done | error | not_selected | no_analysis.</summary>
    public string BindiffStatus
    {
        get => _bindiffStatus;
        set { if (_bindiffStatus != value) { _bindiffStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(BindiffStatusDisplay)); } }
    }

    /// <summary>Сырой статус Diaphora (та же семантика).</summary>
    public string DiaphoraStatus
    {
        get => _diaphoraStatus;
        set { if (_diaphoraStatus != value) { _diaphoraStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiaphoraStatusDisplay)); } }
    }

    /// <summary>Отображаемый текст статуса BinDiff на русском.</summary>
    public string BindiffStatusDisplay => StatusDisplay(_bindiffStatus);

    /// <summary>Отображаемый текст статуса Diaphora на русском.</summary>
    public string DiaphoraStatusDisplay => StatusDisplay(_diaphoraStatus);

    /// <summary>PID последнего запущенного процесса для пары (idat.exe / bindiff.exe / pythonw).</summary>
    public string PidText
    {
        get => _pidText;
        set { if (_pidText != value) { _pidText = value; OnPropertyChanged(); } }
    }

    private static string StatusDisplay(string key) => key switch
    {
        "waiting" => "ожидает",
        "analysis" => "анализ",
        "done" => "завершён",
        "error" => "ошибка",
        "not_selected" => "не выбран",
        "no_analysis" => "без анализа",
        _ => key,
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}