using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IDABatchToolWinUI.Models;

/// <summary>Строка таблицы сопоставленных пар для страницы Сравнения (INPC для x:Bind).</summary>
public sealed class PairRowViewModel : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private string _bindiffStatus = "—";
    private string _diaphoraStatus = "—";

    public required DiffPair Pair { get; init; }
    public string RelKey => Pair.RelKey;
    public string ToolTip => $"{Pair.Primary}\n{Pair.Secondary}";
    public string SizeText { get; init; } = "";
    public string StatusText { get; set; } = "—";

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public string BindiffStatus
    {
        get => _bindiffStatus;
        set { if (_bindiffStatus != value) { _bindiffStatus = value; OnPropertyChanged(); } }
    }

    public string DiaphoraStatus
    {
        get => _diaphoraStatus;
        set { if (_diaphoraStatus != value) { _diaphoraStatus = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}