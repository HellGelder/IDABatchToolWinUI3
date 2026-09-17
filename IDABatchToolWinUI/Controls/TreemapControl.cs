using IDABatchToolWinUI.Models;
using IDABatchToolWinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.UI;

namespace IDABatchToolWinUI.Controls;

/// <summary>
/// Горизонтальная полоса равномерных блоков по числу файлов — аналог TreemapWidget из Qt.
/// Цвет блока зависит от статуса анализа.
/// </summary>
public sealed class TreemapControl : Grid
{
    private readonly Canvas _canvas;
    private List<TreemapSegment> _items = new();

    private sealed record TreemapSegment(string Path, AnalysisStatus Status);

    public TreemapControl()
    {
        MinHeight = 34;
        MaxHeight = 48;
        Height = 40;
        Background = new SolidColorBrush(Colors.Transparent);
        _canvas = new Canvas();
        Children.Add(_canvas);
        SizeChanged += (_, _) => Redraw();
        Loaded += (_, _) => Redraw();
    }

    private static Color ColorForStatus(AnalysisStatus status) => status switch
    {
        AnalysisStatus.NotAnalyzed => Color.FromArgb(255, 192, 192, 192),
        AnalysisStatus.InProgress => Color.FromArgb(255, 255, 255, 0),
        AnalysisStatus.Success => Color.FromArgb(255, 0, 122, 255),
        AnalysisStatus.Error => Color.FromArgb(255, 255, 0, 0),
        _ => Color.FromArgb(255, 128, 128, 128),
    };

    public void SetData(IEnumerable<FileItem> items)
    {
        _items = items.Select(i => new TreemapSegment(i.Path, i.Status)).ToList();
        Redraw();
    }

    public void UpdateStatus(string filePath, AnalysisStatus status)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var seg = _items[i];
            if (seg.Path == filePath)
            {
                _items[i] = seg with { Status = status };
                break;
            }
        }
        Redraw();
    }

    private void Redraw()
    {
        _canvas.Children.Clear();
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        if (_items.Count == 0)
        {
            var tb = new TextBlock
            {
                Text = "Нет данных для отображения",
                Foreground = new SolidColorBrush(Color.FromArgb(255, 140, 140, 140)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            Canvas.SetLeft(tb, 8);
            _canvas.Children.Add(tb);
            return;
        }

        double bw = w / _items.Count;
        for (int i = 0; i < _items.Count; i++)
        {
            var rect = new Rectangle
            {
                Width = bw,
                Height = h,
                Fill = new SolidColorBrush(ColorForStatus(_items[i].Status)),
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 1,
            };
            Canvas.SetLeft(rect, i * bw);
            _canvas.Children.Add(rect);
        }
    }
}