using Microsoft.UI.Xaml.Controls;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Журнал выполнения в TextBox: хранит хвост из последних <see cref="DefaultMaxLines"/>
/// строк. Прежние постраничные AppendLog делали полную конкатенацию всего текста
/// на каждой записи (O(n²), неограниченный рост) — здесь текст остаётся ограниченным.
/// Полный журнал и так пишется в logs\app-*.log.
/// </summary>
public static class UiLog
{
    public const int DefaultMaxLines = 400;

    public static void Append(TextBox box, string message, int maxLines = DefaultMaxLines,
        bool autoscroll = false)
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
        if (autoscroll) box.SelectionStart = text.Length;
    }
}
