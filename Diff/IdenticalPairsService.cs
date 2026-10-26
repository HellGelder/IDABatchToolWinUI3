using IDABatchToolWinUI.Models;
using static IDABatchToolWinUI.Diff.DiffJson;

namespace IDABatchToolWinUI.Diff;

/// <summary>
/// Побайтно идентичные пары: проверка равенства исходников, поиск исходного
/// бинарника по .i64, заглушка diff.json и особые случаи (100% hexdump при
/// нуле функций). Перенос из DiffWorker при декомпозиции (пачка D).
/// </summary>
public static class IdenticalPairsService
{
    /// <summary>Побайтовое равенство двух файлов (размер + содержимое, потоково).</summary>
    public static bool FilesEqual(string path1, string path2)
    {
        try
        {
            var fi1 = new FileInfo(path1);
            var fi2 = new FileInfo(path2);
            if (fi1.Length != fi2.Length) return false;
            using var s1 = fi1.OpenRead();
            using var s2 = fi2.OpenRead();
            var buf1 = new byte[81920];
            var buf2 = new byte[81920];
            int n1, n2;
            do
            {
                n1 = s1.Read(buf1, 0, buf1.Length);
                n2 = s2.Read(buf2, 0, buf2.Length);
                if (n1 != n2) return false;
                for (int i = 0; i < n1; i++)
                    if (buf1[i] != buf2[i]) return false;
            } while (n1 > 0);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Исходный бинарник рядом с базой IDA: 7z.exe.i64 → 7z.exe.</summary>
    public static string? FindOriginalBinary(string i64Path)
    {
        var stem = Path.GetFileNameWithoutExtension(i64Path); // uprngctl64.exe.i64 -> uprngctl64.exe
        var dir = Path.GetDirectoryName(i64Path)!;
        var cand = Path.Combine(dir, stem);
        if (File.Exists(cand)) return cand;
        foreach (var ext in new[] { ".exe", ".dll", ".bin", ".sys", ".elf", ".so", ".o", ".out", ".wasm", ".pyc", ".class", ".jar", ".apk", ".dex" })
        {
            cand = Path.Combine(dir, stem + ext);
            if (File.Exists(cand)) return cand;
        }
        return null;
    }

    /// <summary>Заглушка diff.json для побайтно идентичной пары: без запуска
    /// BinDiff/Diaphora - сходство полная и по определению.</summary>
    public static void WriteIdenticalJson(DiffPair p, string outputDir)
    {
        var jsonOutput = Path.Combine(outputDir, $"{p.Stem}.diff.json");
        var orig1 = FindOriginalBinary(p.Primary);
        var orig2 = FindOriginalBinary(p.Secondary);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var data = new Dictionary<string, object?>
        {
            ["primary"] = p.Primary, ["secondary"] = p.Secondary,
            ["real_primary"] = orig1 ?? p.Primary, ["real_secondary"] = orig2 ?? p.Secondary,
            ["similarity"] = 1.0, ["confidence"] = 1.0,
            ["hexdump_similarity"] = 1.0,
            ["description"] = "", ["version"] = "", ["created"] = now, ["modified"] = now,
            ["file1"] = new Dictionary<string, object?>(), ["file2"] = new Dictionary<string, object?>(),
            ["matched_functions"] = new List<object?>(),
            ["matched_summary"] = new Dictionary<string, object?>
                { ["total"] = 0, ["bindiff_only"] = 0, ["diaphora_only"] = 0, ["both"] = 0 },
            ["unmatched_functions1"] = new List<object?>(),
            ["unmatched_functions2"] = new List<object?>(),
            ["total_functions1"] = 0, ["total_functions2"] = 0,
            ["total_matched"] = 0, ["total_unmatched"] = 0,
            ["identical"] = true,
            ["add_analysis_done"] = true,
            ["error"] = null,
        };
        if (orig1 != null && orig2 != null)
        {
            data["global_hex_diff"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["name1"] = Path.GetFileName(orig1), ["path1"] = orig1,
                    ["name2"] = Path.GetFileName(orig2), ["path2"] = orig2,
                    ["hexdump_similarity"] = 1.0,
                }
            };
        }
        WriteJson(jsonOutput, data);
    }

    /// <summary>Пара не нуждается в доанализе независимо от similarity:
    /// побайтно идентична (маркер identical) или BinDiff не дал функций при
    /// 100% hexdump-схожести (анализаторы здесь бессильны, файлы одинаковые).</summary>
    public static bool IsIdenticalOrFullHexdump(string diffJsonPath)
    {
        try
        {
            var data = ReadJson(diffJsonPath);
            if (data == null) return false;
            if (data.TryGetValue("identical", out var id) && id is true) return true;
            var hd = data.TryGetValue("hexdump_similarity", out var hv) && hv != null
                ? Convert.ToDouble(hv, System.Globalization.CultureInfo.InvariantCulture) : 0.0;
            if (hd < 1.0) return false;
            var matched = data.TryGetValue("matched_functions", out var mf) && mf is List<object?> ml ? ml.Count : 0;
            var total1 = data.TryGetValue("total_functions1", out var t1) && t1 != null ? Convert.ToInt64(t1) : 0;
            return matched == 0 && total1 == 0;
        }
        catch { return false; }
    }
}
