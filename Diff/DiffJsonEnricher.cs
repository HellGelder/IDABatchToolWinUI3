using static IDABatchToolWinUI.Diff.DiffJson;

namespace IDABatchToolWinUI.Diff;

/// <summary>
/// Обогащение .diff.json данными экспорта IDA: импорты, псевдокод-diff,
/// insn-типы, callees, а также «правда экспорта» (ApplyIdaExportTruth) -
/// отбрасывание псевдофункций BinExport и пересборка matched/unmatched.
/// Перенос из DiffWorker при декомпозиции (пачка D) - поведение не менялось.
/// </summary>
public static class DiffJsonEnricher
{
    public static void Enrich(string diffJson, string? primaryJson, string? secondaryJson)
    {
        var diffData = ReadJson(diffJson);
        if (diffData == null) return;

        var primImports = new HashSet<string>();
        var secImports = new HashSet<string>();
        var primFuncs = new Dictionary<string, Dictionary<string, object?>>();
        var secFuncs = new Dictionary<string, Dictionary<string, object?>>();

        foreach (var (jsonPath, impSet, funcDict) in new[]
                 {
                     (primaryJson, primImports, primFuncs),
                     (secondaryJson, secImports, secFuncs),
                 })
        {
            if (jsonPath == null || !File.Exists(jsonPath)) continue;
            var d = ReadJson(jsonPath);
            if (d == null) continue;
            if (d.TryGetValue("imports", out var imports) && imports is List<object?> impList)
                foreach (var imp in impList.OfType<Dictionary<string, object?>>())
                {
                    var n = Convert.ToString(imp.GetValue("name"))?.Trim();
                    if (!string.IsNullOrEmpty(n)) impSet.Add(n);
                }
            if (d.TryGetValue("functions", out var funcs) && funcs is List<object?> fnList)
                foreach (var fn in fnList.OfType<Dictionary<string, object?>>())
                {
                    var addr = Convert.ToString(fn.GetValue("start_ea"));
                    if (string.IsNullOrEmpty(addr)) continue;
                    funcDict[addr] = new Dictionary<string, object?>
                    {
                        ["name"] = Convert.ToString(fn.GetValue("name"))?.Trim() ?? "",
                        ["pseudocode"] = Convert.ToString(fn.GetValue("pseudocode")) ?? "",
                        ["hexdump"] = Convert.ToString(fn.GetValue("hexdump")) ?? "",
                        ["start_ea"] = addr,
                        ["insn_types"] = fn.GetValue("insn_types") ?? new Dictionary<string, object?>(),
                        ["callees"] = fn.GetValue("callees") ?? new List<object?>(),
                    };
                }
        }

        diffData["imports_only_in_primary"] = primImports.Except(secImports).OrderBy(x => x).ToList();
        diffData["imports_only_in_secondary"] = secImports.Except(primImports).OrderBy(x => x).ToList();

        var matches = diffData.TryGetValue("matched_functions", out var m) && m is List<object?> ml
            ? ml.OfType<Dictionary<string, object?>>().ToList() : new List<Dictionary<string, object?>>();

        // Индекс по именам: линейный FirstOrDefault на каждую пару давал O(n²)
        // на больших экспортах (тысячи функций × тысячи совпадений).
        var idx1 = BuildNameIndex(primFuncs);
        var idx2 = BuildNameIndex(secFuncs);

        var allT1 = new Dictionary<string, long>();
        var allT2 = new Dictionary<string, long>();
        foreach (var mf in matches)
        {
            var a1 = Convert.ToString(mf["address1"]) ?? "";
            var a2 = Convert.ToString(mf["address2"]) ?? "";
            var f1 = LookupFunc(primFuncs, idx1, a1, Convert.ToString(mf.GetValue("name1")));
            var f2 = LookupFunc(secFuncs, idx2, a2, Convert.ToString(mf.GetValue("name2")));

            mf["pseudocode1"] = f1?.GetValue("pseudocode") ?? "";
            mf["pseudocode2"] = f2?.GetValue("pseudocode") ?? "";

            // pseudocode diff rows
            mf["pseudocode_diff"] = ComputePseudocodeDiff(
                Convert.ToString(f1?.GetValue("pseudocode")) ?? "",
                Convert.ToString(f2?.GetValue("pseudocode")) ?? "");

            // insn types / callees
            if (f1 != null && f2 != null)
            {
                var it1 = f1["insn_types"] as Dictionary<string, object?> ?? new();
                var it2 = f2["insn_types"] as Dictionary<string, object?> ?? new();
                var mnes = it1.Keys.Union(it2.Keys).OrderBy(x => x, StringComparer.Ordinal).ToList();
                var insnDiff = new List<object?>();
                foreach (var mn in mnes)
                {
                    var c1 = it1.TryGetValue(mn, out var v1) ? Convert.ToInt64(v1) : 0;
                    var c2 = it2.TryGetValue(mn, out var v2) ? Convert.ToInt64(v2) : 0;
                    if (c1 != c2)
                        insnDiff.Add(new Dictionary<string, object?> { ["mnemonic"] = mn, ["count1"] = c1, ["count2"] = c2, ["diff"] = c2 - c1 });
                }
                mf["insn_type_diff"] = insnDiff;
                mf["insn_types1"] = it1;
                mf["insn_types2"] = it2;

                var c1set = (f1["callees"] as List<object?> ?? new()).Select(x => Convert.ToString(x) ?? "").ToHashSet();
                var c2set = (f2["callees"] as List<object?> ?? new()).Select(x => Convert.ToString(x) ?? "").ToHashSet();
                mf["callees_only1"] = c1set.Except(c2set).OrderBy(x => x).ToList();
                mf["callees_only2"] = c2set.Except(c1set).OrderBy(x => x).ToList();
                mf["callees_common"] = c1set.Intersect(c2set).OrderBy(x => x).ToList();
            }
            else
            {
                mf["insn_type_diff"] = new List<object?>();
                mf["insn_types1"] = new Dictionary<string, object?>();
                mf["insn_types2"] = new Dictionary<string, object?>();
                mf["callees_only1"] = new List<object?>();
                mf["callees_only2"] = new List<object?>();
                mf["callees_common"] = new List<object?>();
            }

            // global aggregate
            if (f1 != null && f1["insn_types"] is Dictionary<string, object?> t1)
                foreach (var (mn, cnt) in t1) AddTo(allT1, mn, Convert.ToInt64(cnt));
            if (f2 != null && f2["insn_types"] is Dictionary<string, object?> t2)
                foreach (var (mn, cnt) in t2) AddTo(allT2, mn, Convert.ToInt64(cnt));
        }

        var globalInsn = new List<object?>();
        foreach (var mn in allT1.Keys.Union(allT2.Keys).OrderBy(x => x, StringComparer.Ordinal))
        {
            var c1 = allT1.TryGetValue(mn, out var v1) ? v1 : 0;
            var c2 = allT2.TryGetValue(mn, out var v2) ? v2 : 0;
            globalInsn.Add(new Dictionary<string, object?> { ["mnemonic"] = mn, ["count1"] = c1, ["count2"] = c2, ["diff"] = c2 - c1 });
        }
        diffData["global_insn_diff"] = globalInsn;

        WriteJson(diffJson, diffData);
    }

    private static void AddTo(Dictionary<string, long> d, string key, long value)
        => d[key] = (d.TryGetValue(key, out var v) ? v : 0) + value;

    /// <summary>Индекс функций по имени (первая встретившаяся - как при линейном поиске).</summary>
    private static Dictionary<string, Dictionary<string, object?>> BuildNameIndex(
        Dictionary<string, Dictionary<string, object?>> funcs)
    {
        var idx = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var f in funcs.Values)
        {
            var nm = Convert.ToString(f.GetValue("name"))?.Trim();
            if (string.IsNullOrEmpty(nm)) continue;
            if (!idx.ContainsKey(nm)) idx[nm] = f;
            if (nm.StartsWith("sub_", StringComparison.Ordinal))
            {
                var bare = nm["sub_".Length..];
                if (!idx.ContainsKey(bare)) idx[bare] = f;
            }
        }
        return idx;
    }

    /// <summary>Функция по адресу, при промахе - по имени (напрямую и без префикса sub_).</summary>
    private static Dictionary<string, object?>? LookupFunc(
        Dictionary<string, Dictionary<string, object?>> funcs,
        Dictionary<string, Dictionary<string, object?>> nameIndex,
        string addr, string? name)
    {
        if (addr.Length > 0 && funcs.TryGetValue(addr, out var f)) return f;
        if (string.IsNullOrEmpty(name)) return null;
        var n = name.StartsWith("sub_", StringComparison.Ordinal) ? name["sub_".Length..] : name;
        return nameIndex.TryGetValue(n, out var r) ? r
            : nameIndex.TryGetValue(name, out var r2) ? r2 : null;
    }

    private static List<object?> ComputePseudocodeDiff(string l1, string l2)
    {
        var lines1 = l1.Replace("\r\n", "\n").Split('\n').ToList();
        var lines2 = l2.Replace("\r\n", "\n").Split('\n').ToList();
        if (lines1.Count == 1 && lines1[0] == "") lines1.Clear();
        if (lines2.Count == 1 && lines2[0] == "") lines2.Clear();

        var rows = new List<object?>();
        // Простой LCS-подобный diff (аналог difflib.SequenceMatcher opcodes)
        foreach (var (tag, i1, i2, j1, j2) in SimpleDiff(lines1, lines2))
        {
            if (tag == "equal")
                for (int k = i1; k < i2 && (j1 + (k - i1)) < lines2.Count; k++)
                    rows.Add(new Dictionary<string, object?> { ["type"] = "equal", ["left"] = lines1[k], ["right"] = lines2[j1 + (k - i1)] });
            else if (tag == "delete")
                for (int k = i1; k < i2; k++)
                    rows.Add(new Dictionary<string, object?> { ["type"] = "removed", ["left"] = lines1[k], ["right"] = "" });
            else if (tag == "insert")
                for (int k = j1; k < j2; k++)
                    rows.Add(new Dictionary<string, object?> { ["type"] = "added", ["left"] = "", ["right"] = lines2[k] });
            else if (tag == "replace")
            {
                for (int k = i1; k < i2; k++)
                    rows.Add(new Dictionary<string, object?> { ["type"] = "removed", ["left"] = lines1[k], ["right"] = "" });
                for (int k = j1; k < j2; k++)
                    rows.Add(new Dictionary<string, object?> { ["type"] = "added", ["left"] = "", ["right"] = lines2[k] });
            }
        }
        return rows;
    }

    /// <summary>Простой последовательный diff (упрощённый SequenceMatcher: equal/delete/insert/replace).</summary>
    private static IEnumerable<(string Tag, int I1, int I2, int J1, int J2)> SimpleDiff(List<string> a, List<string> b)
    {
        int i = 0, j = 0;
        var ops = new List<(string, int, int, int, int)>();
        while (i < a.Count || j < b.Count)
        {
            if (i < a.Count && j < b.Count && a[i] == b[j])
            {
                int s = i, sj = j;
                while (i < a.Count && j < b.Count && a[i] == b[j]) { i++; j++; }
                // J1 - начало равного участка в b: у обоих файлов участок одной длины.
                ops.Add(("equal", s, i, sj, j));
            }
            else if (i < a.Count && j < b.Count)
            {
                int s1 = i, s2 = j;
                while (i < a.Count && j < b.Count && a[i] != b[j]) { i++; j++; }
                ops.Add(("replace", s1, i, s2, j));
            }
            else if (i < a.Count)
            {
                int s = i; while (i < a.Count) i++;
                ops.Add(("delete", s, i, j, j));
            }
            else
            {
                int s = j; while (j < b.Count) j++;
                ops.Add(("insert", i, i, s, j));
            }
        }
        return ops;
    }

    /// <summary>
    /// Экспорт IDA - источник истины (доработка исполнения 1, коммиты 190fff3/ee9d761):
    /// отбрасывает сопоставления с адресами вне перечня функций IDA (псевдофункции
    /// BinExport, из-за которых показатель превышал 100%), пересобирает
    /// matched_summary/total_matched, перезаписывает unmatched_functions1/2 и
    /// total_functions1/2 фактическими данными экспорта, ставит
    /// unmatched_source=ida_export. Для старых JSON без экспорта - разность тоталов.
    /// </summary>
    public static void ApplyIdaExportTruth(string jsonOutput, string? primaryExport, string? secondaryExport,
        Action<string>? onError = null)
    {
        if (!File.Exists(jsonOutput)) return;
        var data = ReadJson(jsonOutput);
        if (data == null) return;

        var exportSet1 = ReadExportAddrs(primaryExport);
        var exportSet2 = ReadExportAddrs(secondaryExport);

        // BinExport содержит псевдофункции (jump-thunk'и), которых нет в
        // перечне IDA - из-за них сопоставленных бывает больше, чем функций
        // в файле (показатель > 100%). Оставляем только пары, чьи стороны
        // есть в перечне.
        var matches = data.TryGetValue("matched_functions", out var mf) && mf is List<object?> ml
            ? ml : new List<object?>();
        var kept = matches
            .OfType<Dictionary<string, object?>>()
            .Where(m =>
                (exportSet1.Count == 0 || (Convert.ToString(m.GetValue("address1")) ?? "") != "" &&
                    exportSet1.Contains(Convert.ToString(m.GetValue("address1")) ?? "")) &&
                (exportSet2.Count == 0 || (Convert.ToString(m.GetValue("address2")) ?? "") != "" &&
                    exportSet2.Contains(Convert.ToString(m.GetValue("address2")) ?? "")))
            .ToList();
        var dropped = matches.Count - kept.Count;
        if (dropped > 0)
            onError?.Invoke($"Post: отброшено {dropped} пар с адресами вне перечня IDA (pseudo-functions)");

        data["matched_functions"] = kept;
        data["matched_summary"] = new Dictionary<string, object?>
        {
            ["total"] = kept.Count,
            ["bindiff_only"] = kept.Count(m => Convert.ToString(m.GetValue("source")) == "bindiff"),
            ["diaphora_only"] = kept.Count(m => Convert.ToString(m.GetValue("source")) == "diaphora"),
            ["both"] = kept.Count(m => Convert.ToString(m.GetValue("source")) == "both"),
        };
        data["total_matched"] = kept.Count;
        data["matched_diaphora_only"] = kept
            .Where(m => Convert.ToString(m.GetValue("source")) == "diaphora").ToList<object?>();
        data["diaphora_matched_count"] = kept
            .Count(m => Convert.ToString(m.GetValue("source")) is "diaphora" or "both");

        var matchedPrimary = kept
            .Select(m => Convert.ToString(m.GetValue("address1")) ?? "")
            .Where(a => a != "").ToHashSet();
        var matchedSecondary = kept
            .Select(m => Convert.ToString(m.GetValue("address2")) ?? "")
            .Where(a => a != "").ToHashSet();

        if (primaryExport != null)
        {
            var un1 = ReadUnmatched(primaryExport, matchedPrimary);
            data["unmatched_functions1"] = un1;
            data["total_unmatched"] = un1.Count;
            data["unmatched_source"] = "ida_export";
            if (exportSet1.Count > 0) data["total_functions1"] = exportSet1.Count;
        }
        if (secondaryExport != null)
        {
            var un2 = ReadUnmatched(secondaryExport, matchedSecondary);
            data["unmatched_functions2"] = un2;
            if (exportSet2.Count > 0) data["total_functions2"] = exportSet2.Count;
        }
        if (primaryExport == null)
        {
            if (data.TryGetValue("unmatched_functions1", out var uf1) &&
                uf1 is List<object?> l1 && l1.Count > 0)
            {
                data["total_unmatched"] = l1.Count;
            }
            else
            {
                var total1 = Convert.ToInt64(data.GetValue("total_functions") ?? data.GetValue("total_functions1") ?? 0);
                data["total_unmatched"] = Math.Max(0, total1 - matchedPrimary.Count);
            }
        }

        WriteJson(jsonOutput, data);
    }

    /// <summary>Уникальные адреса функций из экспорта IDA (в нормализованном виде 0xXXXX).</summary>
    public static HashSet<string> ReadExportAddrs(string? exportJson)
    {
        var seen = new HashSet<string>();
        if (string.IsNullOrEmpty(exportJson) || !File.Exists(exportJson)) return seen;
        try
        {
            var d = ReadJson(exportJson);
            if (d == null || !d.TryGetValue("functions", out var f) || f is not List<object?> funcs)
                return seen;
            foreach (var fn in funcs.OfType<Dictionary<string, object?>>())
            {
                var raw = Convert.ToString(fn.GetValue("start_ea")) ?? "";
                if (raw == "") continue;
                if (NormalizeAddr(raw) is { } norm && norm != "")
                    seen.Add(norm);
            }
        }
        catch { /* повреждённый JSON - пропускаем */ }
        return seen;
    }

    /// <summary>Функции экспорта IDA, не вошедшие в сопоставленные.</summary>
    public static List<object?> ReadUnmatched(string? exportJson, HashSet<string> matchedSet)
    {
        var funcs = new List<object?>();
        if (string.IsNullOrEmpty(exportJson) || !File.Exists(exportJson)) return funcs;
        try
        {
            var d = ReadJson(exportJson);
            if (d == null || !d.TryGetValue("functions", out var f) || f is not List<object?> list)
                return funcs;
            foreach (var fn in list.OfType<Dictionary<string, object?>>())
            {
                var raw = Convert.ToString(fn.GetValue("start_ea")) ?? "";
                if (raw == "") continue;
                var norm = NormalizeAddr(raw);
                if (norm == null || matchedSet.Contains(norm)) continue;
                funcs.Add(new Dictionary<string, object?>
                {
                    ["address"] = norm,
                    ["name"] = Convert.ToString(fn.GetValue("name"))?.Trim() ?? "<unnamed>",
                });
            }
        }
        catch { /* повреждённый JSON - пропускаем */ }
        return funcs;
    }

    /// <summary>Нормализует адрес к виду 0xXXXX (как в перечне IDA и разборе BinDiff).</summary>
    public static string? NormalizeAddr(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        try
        {
            var hex = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s;
            return $"0x{long.Parse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture):X}";
        }
        catch
        {
            return s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s : null;
        }
    }
}
