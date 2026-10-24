using System.Text.Json;

namespace IDABatchToolWinUI.Services;

/// <summary>
/// Проверка артефактов анализа и сравнения по содержимому, а не по факту
/// существования файла. Используется воркерами и страницами, чтобы «успех»
/// означал валидный результат, а не оставшийся от прошлого запуска файл.
/// </summary>
public static class ArtifactValidator
{
    /// <summary>Суффикс JSON-экспорта, который пишет scripts/export_data.py рядом с базой.</summary>
    public static string ExpectedExportJsonPath(string basePath) => basePath + ".export.json";

    /// <summary>
    /// Находит базу IDA (.i64/.idb) для исходного файла или возвращает сам файл,
    /// если это уже база. null — базы рядом нет.
    /// </summary>
    public static string? ResolveBasePath(string sourceOrBase)
    {
        if (string.IsNullOrWhiteSpace(sourceOrBase)) return null;
        if (IsBasePath(sourceOrBase))
            return File.Exists(sourceOrBase) ? sourceOrBase : null;

        foreach (var ext in new[] { ".i64", ".idb" })
        {
            var candidate = sourceOrBase + ext;
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public static bool IsBasePath(string path)
        => path.EndsWith(".i64", StringComparison.OrdinalIgnoreCase)
           || path.EndsWith(".idb", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// База считается пригодной, если файл существует, непустой и рядом нет
    /// маркера аварийного завершения IDA (.id0).
    /// </summary>
    public static bool IsValidIdaDatabase(string basePath, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(basePath))
        {
            error = "путь к базе не задан";
            return false;
        }
        if (!File.Exists(basePath))
        {
            error = "база не найдена";
            return false;
        }
        try
        {
            if (new FileInfo(basePath).Length == 0)
            {
                error = "база пустая";
                return false;
            }
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }

        var crashMarker = Path.ChangeExtension(basePath, ".id0");
        if (File.Exists(crashMarker))
        {
            error = "рядом остался .id0 — IDA завершилась аварийно";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Валидирует JSON экспорта IDA: должен разбираться и содержать поля,
    /// по которым строятся отчёты. Пустой или усечённый файл не проходит.
    /// </summary>
    public static bool IsValidExportJson(string path, out string? error)
    {
        error = null;
        if (!File.Exists(path))
        {
            error = "JSON экспорта не найден";
            return false;
        }
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length == 0)
            {
                error = "JSON экспорта пуст";
                return false;
            }
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "корень JSON — не объект";
                return false;
            }
            if (!root.TryGetProperty("file_name", out var fileName)
                || fileName.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(fileName.GetString()))
            {
                error = "нет поля file_name";
                return false;
            }
            if (!root.TryGetProperty("functions", out var functions)
                || functions.ValueKind != JsonValueKind.Array)
            {
                error = "нет массива functions";
                return false;
            }
            if (!root.TryGetProperty("imports", out var imports)
                || imports.ValueKind != JsonValueKind.Array)
            {
                error = "нет массива imports";
                return false;
            }
            if (!root.TryGetProperty("ida_info", out var idaInfo)
                || idaInfo.ValueKind != JsonValueKind.Object)
            {
                error = "нет объекта ida_info";
                return false;
            }
            return true;
        }
        catch (JsonException e)
        {
            error = "JSON не разбирается: " + e.Message;
            return false;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    /// <summary>
    /// Валидирует .diff.json: обязательные поля для HTML-отчёта и отсутствие
    /// непустого error. Ошибка парсера BinDiff/Diaphora больше не считается успехом.
    /// </summary>
    public static bool IsValidDiffJson(string path, out string? error)
    {
        error = null;
        if (!File.Exists(path))
        {
            error = "JSON сравнения не найден";
            return false;
        }
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length == 0)
            {
                error = "JSON сравнения пуст";
                return false;
            }
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "корень JSON — не объект";
                return false;
            }
            // primary/secondary в Diaphora-only режиме могут быть пустыми —
            // для отчёта важны similarity и matched_functions, а не эти поля.
            if (root.TryGetProperty("primary", out var primary)
                && primary.ValueKind != JsonValueKind.String)
            {
                error = "поле primary — не строка";
                return false;
            }
            if (root.TryGetProperty("secondary", out var secondary)
                && secondary.ValueKind != JsonValueKind.String)
            {
                error = "поле secondary — не строка";
                return false;
            }
            if (!root.TryGetProperty("similarity", out var similarity)
                || (similarity.ValueKind != JsonValueKind.Number
                    && !(similarity.ValueKind == JsonValueKind.String
                         && double.TryParse(similarity.GetString(), System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out _))))
            {
                error = "нет числового поля similarity";
                return false;
            }
            if (!root.TryGetProperty("matched_functions", out var matched)
                || matched.ValueKind != JsonValueKind.Array)
            {
                error = "нет массива matched_functions";
                return false;
            }
            if (root.TryGetProperty("error", out var err) && err.ValueKind != JsonValueKind.Null)
            {
                var message = err.ValueKind == JsonValueKind.String ? err.GetString() : err.ToString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    error = message;
                    return false;
                }
            }
            return true;
        }
        catch (JsonException e)
        {
            error = "JSON не разбирается: " + e.Message;
            return false;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    /// <summary>Есть ли у исходного файла пригодная база и валидный JSON экспорта.</summary>
    public static bool HasValidArtifacts(string sourceOrBase)
    {
        var basePath = ResolveBasePath(sourceOrBase);
        if (basePath == null) return false;
        if (!IsValidIdaDatabase(basePath, out _)) return false;
        return IsValidExportJson(ExpectedExportJsonPath(basePath), out _);
    }
}
