using static IDABatchToolWinUI.Diff.DiffJson;

namespace IDABatchToolWinUI.Diff;

/// <summary>
/// Разбор результатов Diaphora (result-SQLite) и слияние их в .diff.json:
/// matched/unmatched, распределение алгоритмов, дедупликация против BinDiff.
/// Перенос из DiffWorker при декомпозиции (пачка D) - поведение не менялось.
/// </summary>
public static class DiaphoraParser
{
    public static DiaphoraParseResult ParseResults(string sqlite)
    {
        var res = new DiaphoraParseResult();
        if (!File.Exists(sqlite)) return res;
        try
        {
            // Pooling=False: иначе пул соединений держит файл открытым,
            // и Cleanup не может удалить result-sqlite после разбора
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={sqlite};Pooling=False");
            conn.Open();

            var conf = new Dictionary<string, double> { ["best"] = 0.95, ["partial"] = 0.60, ["unreliable"] = 0.25 };
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT type, address, name, address2, name2, ratio, description FROM results ORDER BY ratio DESC";
                try
                {
                    using var rd = cmd.ExecuteReader();
                    while (rd.Read())
                    {
                        var type = Convert.ToString(rd["type"]) ?? "partial";
                        // Diaphora хранит ratio строкой '1.0000000' (с точкой):
                        // парсим строго инвариантно - Convert.ToDouble с текущей
                        // культурой на локалях с запятой даёт FormatException
                        // и весь разбор молча терялся
                        double.TryParse(Convert.ToString(rd["ratio"]), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var ratio);
                        res.MatchedFunctions.Add(new DiaphoraMatch(
                            "0x" + (long.TryParse(Convert.ToString(rd["address"]), System.Globalization.NumberStyles.HexNumber, null, out var a1) ? a1.ToString("X") : "0"),
                            Convert.ToString(rd["name"]) ?? "",
                            "0x" + (long.TryParse(Convert.ToString(rd["address2"]), System.Globalization.NumberStyles.HexNumber, null, out var a2) ? a2.ToString("X") : "0"),
                            Convert.ToString(rd["name2"]) ?? "",
                            ratio,
                            conf.TryGetValue(type, out var c) ? c : 0.50,
                            (Convert.ToString(rd["description"]) ?? "diaphora_auto").Trim(),
                            type));
                        var algo = (Convert.ToString(rd["description"]) ?? "diaphora_auto").Trim();
                        res.AlgorithmDistribution.TryGetValue(algo, out var cnt);
                        res.AlgorithmDistribution[algo] = cnt + 1;
                    }
                }
                catch { /* нет таблицы */ }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT type, address, name FROM unmatched";
                try
                {
                    using var rd = cmd.ExecuteReader();
                    while (rd.Read())
                    {
                        var a = "0x" + (long.TryParse(Convert.ToString(rd["address"]), System.Globalization.NumberStyles.HexNumber, null, out var av) ? av.ToString("X") : "0");
                        var entry = (Address: a, Name: Convert.ToString(rd["name"]) ?? "");
                        // В новых версиях Diaphora type - строка 'primary'/'secondary',
                        // в старых - число 1/2
                        var ut = (Convert.ToString(rd["type"]) ?? "").Trim();
                        if (ut == "1" || ut.Equals("primary", StringComparison.OrdinalIgnoreCase)) res.Unmatched1.Add(entry);
                        else res.Unmatched2.Add(entry);
                    }
                }
                catch { /* нет таблицы */ }
            }
        }
        catch { /* ошибка парсинга */ }
        return res;
    }

    public static void MergeIntoJson(string jsonPath, string diaphoraSqlite)
    {
        var dres = ParseResults(diaphoraSqlite);
        var data = File.Exists(jsonPath) ? ReadJson(jsonPath) ?? new Dictionary<string, object?>() : null;

        if (data == null)
        {
            data = new Dictionary<string, object?>
            {
                ["primary"] = "", ["secondary"] = "",
                ["similarity"] = 0.0, ["confidence"] = 0.0,
                ["description"] = "", ["version"] = "", ["created"] = "", ["modified"] = "",
                ["file1"] = new Dictionary<string, object?>(), ["file2"] = new Dictionary<string, object?>(),
                ["matched_functions"] = new List<object?>(),
                ["total_functions1"] = 0, ["total_functions2"] = 0,
                ["error"] = null,
            };
        }

        var matches = data["matched_functions"] as List<object?> ?? new List<object?>();
        // Помечаем уже существующие как bindiff
        foreach (var m in matches.OfType<Dictionary<string, object?>>())
            if (!m.ContainsKey("source")) m["source"] = "bindiff";

        var existing = new Dictionary<(string, string), Dictionary<string, object?>>();
        foreach (var m in matches.OfType<Dictionary<string, object?>>())
        {
            var k = (Convert.ToString(m["address1"]) ?? "", Convert.ToString(m["address2"]) ?? "");
            existing[k] = m;
        }

        foreach (var m in dres.MatchedFunctions)
        {
            var key = (m.Address1, m.Address2);
            if (existing.TryGetValue(key, out var ex))
            {
                ex["source"] = "both";
                ex["bindiff_similarity"] = ex["similarity"];
                ex["diaphora_similarity"] = m.Similarity;
            }
            else
            {
                existing[key] = m.ToDict();
                matches.Add(m.ToDict());
            }
        }

        // Дедупликация
        matches.Clear();
        matches.AddRange(DeduplicateMatched(existing.Values));

        data["matched_summary"] = Summarize(matches);
        data["matched_diaphora_only"] = matches.OfType<Dictionary<string, object?>>()
            .Where(m => Convert.ToString(m["source"]) == "diaphora").ToList();
        data["diaphora_matched_count"] = matches.OfType<Dictionary<string, object?>>()
            .Count(m => m["source"] is "diaphora" or "both");
        data["total_matched"] = matches.Count;

        // Unmatched
        foreach (var (side, list) in new[] { ("unmatched_functions1", dres.Unmatched1), ("unmatched_functions2", dres.Unmatched2) })
        {
            if (!data.ContainsKey(side)) data[side] = new List<object?>();
            var cur = (List<object?>)data[side]!;
            var ext = cur.OfType<Dictionary<string, object?>>().Select(x => Convert.ToString(x["address"]) ?? "").ToHashSet();
            foreach (var u in list)
                if (u.Address != "" && ext.Add(u.Address))
                    cur.Add(new Dictionary<string, object?> { ["address"] = u.Address, ["name"] = u.Name });
        }

        // Алгоритмы
        var ad = data.ContainsKey("algorithm_distribution") && data["algorithm_distribution"] is Dictionary<string, object?> d0
            ? d0 : new Dictionary<string, object?>();
        data["algorithm_distribution"] = ad;
        foreach (var (algo, cnt) in dres.AlgorithmDistribution)
            ad[algo] = (ad.TryGetValue(algo, out var v) && v != null ? Convert.ToInt64(v) : 0) + cnt;

        // engine
        var hasBd = matches.OfType<Dictionary<string, object?>>().Any(m => m["source"] is "bindiff" or "both");
        var hasDp = matches.OfType<Dictionary<string, object?>>().Any(m => m["source"] is "diaphora" or "both");
        data["engine"] = hasBd && hasDp ? "bindiff+diaphora" : hasDp ? "diaphora" : hasBd ? "bindiff" : "";

        WriteJson(jsonPath, data);
    }

    public static Dictionary<string, object?> Summarize(List<object?> matches)
    {
        var bd = matches.OfType<Dictionary<string, object?>>().Count(m => Convert.ToString(m["source"]) == "bindiff");
        var dp = matches.OfType<Dictionary<string, object?>>().Count(m => Convert.ToString(m["source"]) == "diaphora");
        var both = matches.OfType<Dictionary<string, object?>>().Count(m => Convert.ToString(m["source"]) == "both");
        return new Dictionary<string, object?>
        {
            ["total"] = matches.Count,
            ["bindiff_only"] = bd,
            ["diaphora_only"] = dp,
            ["both"] = both,
        };
    }

    public static List<object?> DeduplicateMatched(IEnumerable<Dictionary<string, object?>> matches)
    {
        var prio = new Dictionary<string, int> { ["both"] = 0, ["bindiff"] = 1, ["diaphora"] = 2 };
        var ordered = matches
            .OrderBy(m => prio.TryGetValue(Convert.ToString(m["source"]) ?? "diaphora", out var pr) ? pr : 99)
            .ThenByDescending(m => Convert.ToDouble(m["similarity"]));
        var seen1 = new HashSet<string>();
        var seen2 = new HashSet<string>();
        var result = new List<object?>();
        foreach (var m in ordered)
        {
            var a1 = Convert.ToString(m["address1"]) ?? "";
            var a2 = Convert.ToString(m["address2"]) ?? "";
            if (a1 == "" || a2 == "") continue;
            if (seen1.Contains(a1) || seen2.Contains(a2)) continue;
            seen1.Add(a1); seen2.Add(a2);
            result.Add(m);
        }
        return result;
    }
}

public sealed record DiaphoraParseResult
{
    public List<DiaphoraMatch> MatchedFunctions { get; } = new();
    public List<(string Address, string Name)> Unmatched1 { get; } = new();
    public List<(string Address, string Name)> Unmatched2 { get; } = new();
    public Dictionary<string, long> AlgorithmDistribution { get; } = new();
}

public sealed record DiaphoraMatch(string Address1, string Name1, string Address2, string Name2,
                                    double Similarity, double Confidence, string AlgorithmName, string MatchType)
{
    public Dictionary<string, object?> ToDict() => new()
    {
        ["address1"] = Address1, ["name1"] = Name1,
        ["address2"] = Address2, ["name2"] = Name2,
        ["similarity"] = Math.Round(Similarity, 4), ["confidence"] = Confidence,
        ["algorithm_name"] = AlgorithmName, ["match_type"] = MatchType,
        ["nodes1"] = 0, ["nodes2"] = 0, ["source"] = "diaphora",
    };
}
