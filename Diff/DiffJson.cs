using System.Text.Json;

namespace IDABatchToolWinUI.Diff;

/// <summary>Общие JSON/вспомогательные операции diff-конвейера: чтение/запись
/// .diff.json в виде Dictionary/List/примитивов, мелкие утилиты. Вынесено из
/// DiffWorker при декомпозиции (пачка D), поведение не менялось.</summary>
public static class DiffJson
{
    public static Dictionary<string, object?>? ReadJson(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return ConvertElement(doc.RootElement) as Dictionary<string, object?>;
        }
        catch { return null; }
    }

    /// <summary>Преобразует JsonElement в Dictionary/List/примитивы (а не JsonElement), чтобы
    /// последующая работа с OfType&lt;Dictionary&lt;...&gt;&gt; и индексаторами находила данные.</summary>
    public static object? ConvertElement(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                var d = new Dictionary<string, object?>();
                foreach (var p in el.EnumerateObject())
                    d[p.Name] = ConvertElement(p.Value);
                return d;
            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in el.EnumerateArray())
                    list.Add(ConvertElement(item));
                return list;
            case JsonValueKind.String:
                return el.GetString();
            case JsonValueKind.Number:
                return el.TryGetInt64(out var l) ? l
                    : el.TryGetDouble(out var db) ? db : 0.0;
            case JsonValueKind.True:
            case JsonValueKind.False:
                return el.GetBoolean();
            default:
                return null;
        }
    }

    public static void WriteJson(string path, Dictionary<string, object?> data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { /* не критично */ }
    }

    public static int SafeInt(object? v)
        => v == null ? 0 : (v is long l ? (int)l : Convert.ToInt32(v));

    /// <summary>Удаляет перечисленные пути, если существуют; занятые файлы пропускает.</summary>
    public static void Cleanup(params string[] paths)
    {
        foreach (var p in paths)
            try { if (File.Exists(p)) File.Delete(p); } catch { }
    }

    /// <summary>Список колонок таблицы SQLite (пустой, если таблицы нет).</summary>
    public static HashSet<string> GetTableColumns(Microsoft.Data.Sqlite.SqliteConnection conn, string table)
    {
        var cols = new HashSet<string>();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
            using var r = cmd.ExecuteReader();
            while (r.Read()) cols.Add(r.GetString(1));
        }
        catch { /* нет таблицы */ }
        return cols;
    }
}

/// <summary>Расширения словарей diff-JSON и задач, используемые всеми diff-сервисами.</summary>
public static class JsonDictExtensions
{
    public static object? GetValue(this Dictionary<string, object?> d, string key)
        => d.TryGetValue(key, out var v) ? v : null;

    /// <summary>Гасит наблюдение за задачей, которую сознательно не ждём (например,
    /// брошенный WaitForExitAsync при досрочном выходе из цикла пульса).</summary>
    public static void Ignore(this Task task)
    {
        _ = task.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);
    }
}
