using System.Net;
using System.Text;
using System.Text.Json;

namespace IDABatchToolWinUI.Services;

/// <summary>Строка манифеста модульного сравнения (outDir\modules.json): итог одного модуля.
/// HasDiff - у модуля есть changes.diff на диске (unchanged/error папки не создают).</summary>
public sealed record ModuleManifestEntry(
    string Module, string Kind, int Modified, int Added, int Deleted, int Binary, string? Error)
{
    public bool HasDiff => Kind is "modified" or "added" or "deleted";
}

/// <summary>
/// Генерация автономных HTML-отчётов по changes.diff (подвкладка «Git-сравнение»):
/// частный отчёт модуля и индекс модульного сравнения. Шаблоны
/// (_python/templates/git_diff_report.html, git_diff_index.html) и вендор diff2html
/// (_python/vendor, MIT) инлайнятся в отчёт целиком - файлы открываются офлайн
/// с file://. Python-мост для git-сравнения запрещён, генерация нативная.
/// </summary>
public static class GitDiffReportService
{
    /// <summary>Имя индексного отчёта в папке результата модульного режима.</summary>
    public const string IndexFileName = "git_index.html";
    /// <summary>Имя файла-манифеста с итогами модулей (источник для перегенерации отчётов).</summary>
    public const string ManifestFileName = "modules.json";
    /// <summary>
    /// Собирает HTML-отчёт рядом с diff-файлом: &lt;имя диффа&gt;_report.html.
    /// backToIndexUrl - относительная ссылка «← К индексу» в шапке (модульный режим:
    /// "../git_index.html"); null - без ссылки (плоский режим, индекса нет).
    /// Возвращает полный путь отчёта.
    /// </summary>
    public static string Generate(string diffPath, string oldDir, string newDir,
        string? backToIndexUrl = null)
    {
        var root = AppConstants.WinUiDir;
        var template = File.ReadAllText(Path.Combine(root, "_python", "templates", "git_diff_report.html"));
        var css = File.ReadAllText(Path.Combine(root, "_python", "vendor", "diff2html.min.css"));
        var js = File.ReadAllText(Path.Combine(root, "_python", "vendor", "diff2html.min.js"));
        var diffText = File.ReadAllText(diffPath);

        var fullDiffPath = Path.GetFullPath(diffPath);
        var backLink = string.IsNullOrEmpty(backToIndexUrl)
            ? ""
            : $"<a href=\"{Html(backToIndexUrl)}\">← К индексу сравнения модулей</a>";

        // Порядок замен важен: сначала метаданные и вендор, diff-JSON - последним.
        // В тексте диффа возможны любые символы - он не должен участвовать в заменах.
        var html = template
            .Replace("__BACK_LINK__", backLink)
            .Replace("__OLD_DIR__", Html(string.IsNullOrWhiteSpace(oldDir) ? null : oldDir))
            .Replace("__NEW_DIR__", Html(string.IsNullOrWhiteSpace(newDir) ? null : newDir))
            .Replace("__GENERATED__", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"))
            .Replace("__SOURCE_NAME__", Html(Path.GetFileName(fullDiffPath)))
            .Replace("__SOURCE_DIR__", Html(Path.GetDirectoryName(fullDiffPath)))
            .Replace("/*__VENDOR_CSS__*/", css)
            .Replace("/*__VENDOR_JS__*/", js)
            // JsonSerializer (дефолтный энкодер) экранирует <, >, &, кавычки и
            // U+2028/2029 - строка безопасна для встраивания внутрь <script>.
            .Replace("\"__DIFF_JSON__\"", JsonSerializer.Serialize(diffText));

        var outPath = Path.Combine(
            Path.GetDirectoryName(fullDiffPath) ?? ".",
            Path.GetFileNameWithoutExtension(fullDiffPath) + "_report.html");
        File.WriteAllText(outPath, html, new UTF8Encoding(false));
        return outPath;
    }

    /// <summary>HTML-экранирование для вставки путей в разметку (null → «-»).</summary>
    private static string Html(string? value) =>
        WebUtility.HtmlEncode(string.IsNullOrEmpty(value) ? "-" : value);

    // ───────────────────── модульный режим: манифест и индекс ─────────────────────

    /// <summary>Сохраняет итоги модулей в outDir\modules.json - источник правды для
    /// перегенерации отчётов после перезапуска приложения.</summary>
    public static void WriteManifest(string outDir, IReadOnlyList<ModuleManifestEntry> entries)
    {
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(outDir, ManifestFileName), json, new UTF8Encoding(false));
    }

    /// <summary>Читает манифест модулей; null - файла нет (запуска модульного режима не было).</summary>
    public static IReadOnlyList<ModuleManifestEntry>? ReadManifest(string outDir)
    {
        var path = Path.Combine(outDir, ManifestFileName);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<ModuleManifestEntry[]>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Индексный отчёт модульного сравнения: сводная таблица (изменено/добавлено/удалено
    /// по каждому модулю, статус, переходы на частный отчёт и changes.diff) + строка «Итого».
    /// Пишет outDir\git_index.html, возвращает его путь.
    /// </summary>
    public static string GenerateIndex(string outDir, string oldDir, string newDir,
        IReadOnlyList<ModuleManifestEntry> entries)
    {
        var root = AppConstants.WinUiDir;
        var template = File.ReadAllText(Path.Combine(root, "_python", "templates", "git_diff_index.html"));

        // Метаданные заменяем ДО подстановки строк таблицы: имена модулей не должны
        // участвовать в последующих заменах
        var html = template
            .Replace("__OLD_DIR__", Html(oldDir))
            .Replace("__NEW_DIR__", Html(newDir))
            .Replace("__GENERATED__", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"))
            .Replace("__COUNT__", entries.Count.ToString())
            .Replace("__ROWS__", BuildIndexRows(entries));

        var outPath = Path.Combine(outDir, IndexFileName);
        File.WriteAllText(outPath, html, new UTF8Encoding(false));
        return outPath;
    }

    private static string BuildIndexRows(IReadOnlyList<ModuleManifestEntry> entries)
    {
        int sumMod = 0, sumAdd = 0, sumDel = 0, sumDone = 0;
        var rows = new StringBuilder();

        foreach (var e in entries)
        {
            var status = e.Kind switch
            {
                "modified" => "изменён",
                "added" => "новый",
                "deleted" => "удалён",
                "unchanged" => "без изменений",
                _ => "ошибка",
            };
            // Итоговые суммы - только по модулям с содержательным диффом
            if (e.HasDiff)
            {
                sumMod += e.Modified;
                sumAdd += e.Added;
                sumDel += e.Deleted;
                sumDone++;
            }

            string links;
            if (e.HasDiff)
            {
                var dir = Uri.EscapeDataString(e.Module);
                links = $"<a href=\"{dir}/changes_report.html\">отчёт</a> · " +
                        $"<a href=\"{dir}/changes.diff\">diff</a>";
            }
            else
            {
                links = "<span class=\"gdx-quiet\">-</span>";
            }

            var errorAttr = e.Error is null ? "" : $" title=\"{Html(e.Error)}\"";
            rows.AppendLine(
                $"<tr{errorAttr}>" +
                $"<td class=\"name\">{Html(e.Module)}</td>" +
                $"<td class=\"num mod\">{e.Modified}</td>" +
                $"<td class=\"num add\">{e.Added}</td>" +
                $"<td class=\"num del\">{e.Deleted}</td>" +
                $"<td><span class=\"gdx-status gdx-status-{e.Kind}\">{status}</span></td>" +
                $"<td>{links}</td>" +
                "</tr>");
        }

        rows.AppendLine(
            "<tr class=\"gdx-total\">" +
            $"<td>Итого ({sumDone} из {entries.Count} с изменениями)</td>" +
            $"<td class=\"num mod\">{sumMod}</td>" +
            $"<td class=\"num add\">{sumAdd}</td>" +
            $"<td class=\"num del\">{sumDel}</td>" +
            "<td></td><td></td>" +
            "</tr>");
        return rows.ToString();
    }
}
