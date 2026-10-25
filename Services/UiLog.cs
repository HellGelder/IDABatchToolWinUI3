using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Журнал выполнения в TextBox: хранит хвост из последних <see cref="DefaultMaxLines"/>
/// строк и держит последнюю запись на виду. Прежние постраничные AppendLog делали
/// полную конкатенацию всего текста на каждой записи (O(n²), неограниченный рост) —
/// здесь текст остаётся ограниченным. Полный журнал и так пишется в logs\app-*.log.
/// </summary>
public static class UiLog
{
    public const int DefaultMaxLines = 400;

    public static void Append(TextBox box, string message, int maxLines = DefaultMaxLines)
    {
        var nl = Environment.NewLine;
        var text = string.IsNullOrEmpty(box.Text) ? message : box.Text + nl + message;

        // Отсекаем голову, оставляя последние maxLines строк
        if (maxLines > 0)
        {
            int lines = 1;
            int cut = 0;
            int searchFrom = text.Length - 1;
            while (lines < maxLines)
            {
                var pos = text.LastIndexOf(nl, searchFrom, StringComparison.Ordinal);
                if (pos < 0) break;
                lines++;
                cut = pos + nl.Length;
                searchFrom = pos - 1;
                if (searchFrom < 0) break;
            }
            if (lines >= maxLines && cut > 0)
                text = text[cut..];
        }

        box.Text = text;

        // Держим последнюю строку на виду. У WinUI 3 TextBox нет своего ChangeView —
        // прокручиваем его внутренний ScrollViewer (шаблонная часть ContentElement).
        // Каретку ставим в конец ДО ChangeView: после замены Text TextBox сам
        // подтягивает вью к каретке и без этого сбрасывает прокрутку наверх.
        box.SelectionStart = text.Length;
        box.SelectionLength = 0;
        FindScrollViewer(box)?.ChangeView(null, double.MaxValue, null, disableAnimation: true);
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv) return sv;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var found = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (found != null) return found;
        }
        return null;
    }
}
