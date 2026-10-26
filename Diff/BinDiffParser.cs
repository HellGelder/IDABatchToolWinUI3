using static IDABatchToolWinUI.Diff.DiffJson;

namespace IDABatchToolWinUI.Diff;

/// <summary>
/// Разбор результата BinDiff (.BinDiff SQLite) в .diff.json: metadata, file info,
/// matched functions (с учётом вариантов имён колонок), распределения.
/// Перенос ParseBindiffResult из DiffWorker при декомпозиции (пачка D) - поведение не менялось.
/// </summary>
public static class BinDiffParser
{
    public static void Parse(string dbPath, string primary, string secondary, string jsonOutput)
    {
        var result = new Dictionary<string, object?>
        {
            ["primary"] = primary, ["secondary"] = secondary,
            ["similarity"] = 0.0, ["confidence"] = 0.0,
            ["description"] = "", ["version"] = "", ["created"] = "", ["modified"] = "",
            ["file1"] = new Dictionary<string, object?>(), ["file2"] = new Dictionary<string, object?>(),
            ["matched_functions"] = new List<object?>(),
            ["total_functions1"] = 0, ["total_functions2"] = 0,
            ["error"] = null, ["engine"] = "bindiff",
        };
        try
        {
            // Pooling=False - не держим файл .BinDiff открытым после разбора
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Pooling=False");
            conn.Open();

            // metadata
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM metadata";
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    for (int i = 0; i < r.FieldCount; i++)
                        if (!r.IsDBNull(i))
                            result[r.GetName(i)] = Convert.ToString(r.GetValue(i)) ?? "";
                    if (result.TryGetValue("similarity", out var s) && double.TryParse(s?.ToString(), out var sd))
                        result["similarity"] = sd;
                    if (result.TryGetValue("confidence", out var c) && double.TryParse(c?.ToString(), out var cd))
                        result["confidence"] = cd;
                }
            }

            // file info
            var fileRows = new List<Dictionary<string, object?>>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM file ORDER BY id";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < r.FieldCount; i++)
                        row[r.GetName(i)] = r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i));
                    fileRows.Add(row);
                }
            }
            if (fileRows.Count >= 2)
            {
                foreach (var (f, key) in new[] { (fileRows[0], "file1"), (fileRows[1], "file2") })
                {
                    var fi = new Dictionary<string, object?>
                    {
                        ["filename"] = f.GetValue("filename") ?? "",
                        ["exefilename"] = f.GetValue("exefilename") ?? "",
                        ["hash"] = f.GetValue("hash") ?? "",
                        ["functions"] = SafeInt(f.GetValue("functions")),
                        ["libfunctions"] = SafeInt(f.GetValue("libfunctions")),
                        ["calls"] = SafeInt(f.GetValue("calls")),
                        ["basicblocks"] = SafeInt(f.GetValue("basicblocks")),
                        ["libbasicblocks"] = SafeInt(f.GetValue("libbasicblocks")),
                        ["edges"] = SafeInt(f.GetValue("edges")),
                        ["libedges"] = SafeInt(f.GetValue("libedges")),
                        ["instructions"] = SafeInt(f.GetValue("instructions")),
                        ["libinstructions"] = SafeInt(f.GetValue("libinstructions")),
                    };
                    result[key] = fi;
                }
                result["total_functions1"] = SafeInt((result["file1"] as Dictionary<string, object?>)?.GetValue("functions"))
                    + SafeInt((result["file1"] as Dictionary<string, object?>)?.GetValue("libfunctions"));
                result["total_functions2"] = SafeInt((result["file2"] as Dictionary<string, object?>)?.GetValue("functions"))
                    + SafeInt((result["file2"] as Dictionary<string, object?>)?.GetValue("libfunctions"));
            }

            // functions
            var matches = new List<object?>();
            var columns = GetTableColumns(conn, "function");
            var aliases = new Dictionary<string, string[]>
            {
                ["address1"] = new[] { "address1" }, ["name1"] = new[] { "name1" },
                ["address2"] = new[] { "address2" }, ["name2"] = new[] { "name2" },
                ["similarity"] = new[] { "similarity", "sim" },
                ["confidence"] = new[] { "confidence", "conf" },
                ["flags"] = new[] { "flags" },
                ["algorithm"] = new[] { "algorithm", "algo" },
                ["basicblocks"] = new[] { "basicblocks", "basic_blocks", "basicblocks_count" },
                ["edges"] = new[] { "edges", "edgecount", "edge_count" },
                ["instructions"] = new[] { "instructions", "instructioncount", "instruction_count" },
            };
            var actuals = new List<string>();
            foreach (var (canon, al) in aliases)
                foreach (var a in al)
                    if (columns.Contains(a)) { actuals.Add(a); break; }

            if (actuals.Count > 0)
            {
                var algoNames = new Dictionary<int, string>();
                try
                {
                    var algoCmd = conn.CreateCommand();
                    algoCmd.CommandText = "SELECT id, name FROM functionalgorithm";
                    using var algoRdr = algoCmd.ExecuteReader();
                    while (algoRdr.Read()) algoNames[algoRdr.GetInt32(0)] = algoRdr.GetString(1);
                }
                catch { /* нет таблицы */ }

                var funcCmd = conn.CreateCommand();
                funcCmd.CommandText = "SELECT " + string.Join(",", actuals) + " FROM function ORDER BY similarity DESC";
                using var funcRdr = funcCmd.ExecuteReader();
                while (funcRdr.Read())
                {
                    var e = new Dictionary<string, object?>
                    {
                        ["address1"] = "", ["name1"] = "<unnamed>",
                        ["address2"] = "", ["name2"] = "<unnamed>",
                        ["similarity"] = 0.0, ["confidence"] = 0.0,
                        ["flags"] = 0, ["algorithm"] = 0,
                        ["basicblocks"] = 0, ["edges"] = 0, ["instructions"] = 0,
                        ["source"] = "bindiff",
                    };
                    for (int i = 0; i < funcRdr.FieldCount; i++)
                    {
                        var col = funcRdr.GetName(i);
                        if (col == "address1" || col == "address2")
                        {
                            long? v = funcRdr.IsDBNull(i) ? null : Convert.ToInt64(funcRdr.GetValue(i));
                            e[col] = v == null ? "" : $"0x{v:X}";
                        }
                        else if (col == "similarity" || col == "confidence")
                            e[col] = funcRdr.IsDBNull(i) ? 0.0 : Math.Round(Convert.ToDouble(funcRdr.GetValue(i)), 4);
                        else if (col == "name1" || col == "name2")
                            e[col] = funcRdr.IsDBNull(i) || string.IsNullOrEmpty(Convert.ToString(funcRdr.GetValue(i)))
                                ? "<unnamed>" : Convert.ToString(funcRdr.GetValue(i));
                        else e[col] = funcRdr.IsDBNull(i) ? 0 : Convert.ToInt64(funcRdr.GetValue(i));
                    }
                    e["algorithm_name"] = algoNames.TryGetValue(SafeInt(e["algorithm"]), out var nm)
                        ? nm : $"#{e["algorithm"]}";
                    matches.Add(e);
                }
            }
            result["matched_functions"] = matches;

            // Распределение по similarity
            var buckets = new Dictionary<string, int>
            {
                ["1.0"] = 0, ["0.95_0.99"] = 0, ["0.80_0.94"] = 0, ["0.50_0.79"] = 0, ["below_0.50"] = 0,
            };
            foreach (var m in matches.OfType<Dictionary<string, object?>>())
            {
                var s = Convert.ToDouble(m["similarity"]);
                if (s >= 1.0) buckets["1.0"]++;
                else if (s >= 0.95) buckets["0.95_0.99"]++;
                else if (s >= 0.80) buckets["0.80_0.94"]++;
                else if (s >= 0.50) buckets["0.50_0.79"]++;
                else buckets["below_0.50"]++;
            }
            result["similarity_distribution"] = buckets;

            // Алгоритмы
            try
            {
                var ad = new Dictionary<string, object?>();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    SELECT fa.name, COUNT(*) FROM function f
                    LEFT JOIN functionalgorithm fa ON f.algorithm = fa.id
                    GROUP BY f.algorithm ORDER BY COUNT(*) DESC
                    """;
                using var r = cmd.ExecuteReader();
                while (r.Read()) ad[r.GetString(0)] = r.GetInt64(1);
                result["algorithm_distribution"] = ad;
            }
            catch { /* нет таблицы */ }
        }
        catch (Exception e)
        {
            result["error"] = e.Message;
        }
        WriteJson(jsonOutput, result);
    }
}
