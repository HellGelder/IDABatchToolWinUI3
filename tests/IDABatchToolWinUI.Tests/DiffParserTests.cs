using IDABatchToolWinUI.Diff;
using Microsoft.Data.Sqlite;
using Xunit;

namespace IDABatchToolWinUI.Tests;

/// <summary>Тесты разборчиков diff-конвейера на синтетических SQLite/JSON
/// фикстурах: контракт .diff.json, слияние Diaphora, «правда экспорта».</summary>
public sealed class DiffParserTests : IDisposable
{
    // Каталог тестовых файлов — рядом с тестовой сборкой: %TEMP% в этом
    // окружении может быть закрыт для процесса тестов.
    private readonly string _dir = Path.Combine(
        AppContext.BaseDirectory, "test-tmp", Guid.NewGuid().ToString("N"));

    public DiffParserTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* не критично */ }
    }

    private string Path_(string name) => Path.Combine(_dir, name);

    // ───────────── BinDiffParser ─────────────

    [Fact]
    public void BinDiffParser_ExtractsMetadataFilesAndMatches()
    {
        var db = Path_("t.BindDiff");
        using (var conn = new SqliteConnection($"Data Source={db};Pooling=False"))
        {
            conn.Open();
            Exec(conn, "CREATE TABLE metadata (id INTEGER PRIMARY KEY, similarity TEXT, confidence TEXT, description TEXT, version TEXT)");
            Exec(conn, "INSERT INTO metadata (similarity, confidence, description, version) VALUES ('0.75', '0.9', 'd', 'BinDiff 9')");
            Exec(conn, "CREATE TABLE file (id INTEGER PRIMARY KEY, filename TEXT, exefilename TEXT, hash TEXT, functions INTEGER, libfunctions INTEGER, calls INTEGER, basicblocks INTEGER, libbasicblocks INTEGER, edges INTEGER, libedges INTEGER, instructions INTEGER, libinstructions INTEGER)");
            Exec(conn, "INSERT INTO file (filename, exefilename, hash, functions, libfunctions) VALUES ('a.exe', 'a.exe', 'h1', 10, 2)");
            Exec(conn, "INSERT INTO file (filename, exefilename, hash, functions, libfunctions) VALUES ('b.exe', 'b.exe', 'h2', 8, 1)");
            Exec(conn, "CREATE TABLE function (address1 INTEGER, name1 TEXT, address2 INTEGER, name2 TEXT, similarity REAL, confidence REAL, flags INTEGER, algorithm INTEGER, basicblocks INTEGER, edges INTEGER, instructions INTEGER)");
            Exec(conn, "INSERT INTO function VALUES (1, 'f1', 100, 'f1b', 1.0, 0.99, 0, 1, 5, 6, 7)");
            Exec(conn, "INSERT INTO function VALUES (2, 'f2', 200, 'f2b', 0.5, 0.4, 0, 1, 5, 6, 7)");
            Exec(conn, "CREATE TABLE functionalgorithm (id INTEGER PRIMARY KEY, name TEXT)");
            Exec(conn, "INSERT INTO functionalgorithm (id, name) VALUES (1, 'VeryStrong')");
        }

        var json = Path_("t.diff.json");
        BinDiffParser.Parse(db, "a.i64", "b.i64", json);

        var d = DiffJson.ReadJson(json)!;
        Assert.Equal(0.75, Convert.ToDouble(d["similarity"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(12L, Convert.ToInt64(d["total_functions1"])); // 10 + 2 lib
        Assert.Equal(9L, Convert.ToInt64(d["total_functions2"]));
        Assert.Equal("bindiff", d["engine"]);
        Assert.Null(d["error"]);

        var matches = Assert.IsType<System.Collections.Generic.List<object?>>(d["matched_functions"]);
        Assert.Equal(2, matches.Count);
        var m1 = Assert.IsType<System.Collections.Generic.Dictionary<string, object?>>(matches[0]);
        Assert.Equal("0x1", m1["address1"]); // адрес в формате 0xXXXX
        Assert.Equal("f1", m1["name1"]);
        Assert.Equal("VeryStrong", m1["algorithm_name"]);
        Assert.Equal("bindiff", m1["source"]);
    }

    [Fact]
    public void BinDiffParser_CorruptDb_WritesErrorField()
    {
        var db = Path_("broken.BindDiff");
        File.WriteAllText(db, "not a sqlite file");
        var json = Path_("broken.diff.json");
        BinDiffParser.Parse(db, "a", "b", json);
        var d = DiffJson.ReadJson(json)!;
        Assert.NotNull(d["error"]);
    }

    // ───────────── DiaphoraParser ─────────────

    [Fact]
    public void DiaphoraParser_MergesResultsAndDeduplicates()
    {
        var db = Path_("t_result.sqlite");
        using (var conn = new SqliteConnection($"Data Source={db};Pooling=False"))
        {
            conn.Open();
            Exec(conn, "CREATE TABLE results (type TEXT, address TEXT, name TEXT, address2 TEXT, name2 TEXT, ratio TEXT, description TEXT)");
            // та же пара, что уже есть в diff.json (biniff) → станет 'both'
            Exec(conn, "INSERT INTO results VALUES ('best', '1', 'f1', '64', 'f1b', '0.9000000', 'VeryStrong')");
            // новая пара → 'diaphora'
            Exec(conn, "INSERT INTO results VALUES ('partial', '2', 'f2', 'c8', 'f2b', '0.5000000', 'StrongMatch')");
            Exec(conn, "CREATE TABLE unmatched (type TEXT, address TEXT, name TEXT)");
            Exec(conn, "INSERT INTO unmatched VALUES ('1', '5', 'u1')");
            Exec(conn, "INSERT INTO unmatched VALUES ('secondary', 'c8', 'u2')");
        }

        var json = Path_("merge.diff.json");
        DiffJson.WriteJson(json, new System.Collections.Generic.Dictionary<string, object?>
        {
            ["matched_functions"] = new System.Collections.Generic.List<object?>
            {
                new System.Collections.Generic.Dictionary<string, object?>
                {
                    ["address1"] = "0x1", ["name1"] = "f1",
                    ["address2"] = "0x64", ["name2"] = "f1b",
                    ["similarity"] = 0.8,
                },
            },
            ["total_functions1"] = 3,
        });

        DiaphoraParser.MergeIntoJson(json, db);
        var d = DiffJson.ReadJson(json)!;

        Assert.Equal("bindiff+diaphora", d["engine"]);
        var ms = Assert.IsType<System.Collections.Generic.Dictionary<string, object?>>(d["matched_summary"]);
        Assert.Equal(2, Convert.ToInt64(ms["total"]));
        Assert.Equal(0, Convert.ToInt64(ms["bindiff_only"]));
        Assert.Equal(1, Convert.ToInt64(ms["diaphora_only"]));
        Assert.Equal(1, Convert.ToInt64(ms["both"]));

        var unmatched = Assert.IsType<System.Collections.Generic.List<object?>>(d["unmatched_functions1"]);
        Assert.Single(unmatched); // 0x5
        // type '1' (старая нумерация) → первичная сторона
        var u = Assert.IsType<System.Collections.Generic.Dictionary<string, object?>>(unmatched[0]);
        Assert.Equal("0x5", u["address"]);
    }

    // ───────────── DiffJsonEnricher ─────────────

    [Fact]
    public void Enricher_AddsImportsAndPseudocodeDiff()
    {
        var prim = Path_("p.export.json");
        var sec = Path_("s.export.json");
        File.WriteAllText(prim, """
            {"functions":[{"start_ea":"0x1","name":"f1","pseudocode":"a;\nb;","hexdump":"","insn_types":{"mov":3,"add":1},"callees":["x"]}],
             "imports":[{"name":"kernel32.ExitProcess"}]}
            """);
        File.WriteAllText(sec, """
            {"functions":[{"start_ea":"0x10","name":"f1","pseudocode":"a;\nc;","hexdump":"","insn_types":{"mov":3},"callees":["y"]}],
             "imports":[{"name":"advapi32.RegOpenKey"}]}
            """);
        var diff = Path_("e.diff.json");
        DiffJson.WriteJson(diff, new System.Collections.Generic.Dictionary<string, object?>
        {
            ["matched_functions"] = new System.Collections.Generic.List<object?>
            {
                new System.Collections.Generic.Dictionary<string, object?>
                {
                    ["address1"] = "0x1", ["name1"] = "f1",
                    ["address2"] = "0x10", ["name2"] = "f1",
                },
            },
        });

        DiffJsonEnricher.Enrich(diff, prim, sec);
        var d = DiffJson.ReadJson(diff)!;

        var io1 = Assert.IsType<System.Collections.Generic.List<object?>>(d["imports_only_in_primary"]);
        Assert.Single(io1); // ExitProcess
        var mf = Assert.IsType<System.Collections.Generic.Dictionary<string, object?>>(
            Assert.IsType<System.Collections.Generic.List<object?>>(d["matched_functions"])[0]);
        Assert.Equal("f1", mf["name1"]);
        // 'a;' совпала, 'b;' удалена, 'c;' добавлена
        var pd = Assert.IsType<System.Collections.Generic.List<object?>>(mf["pseudocode_diff"]);
        Assert.Equal(3, pd.Count);
        var callees1 = Assert.IsType<System.Collections.Generic.List<object?>>(mf["callees_only1"]);
        Assert.Single(callees1);
    }

    [Fact]
    public void ApplyIdaExportTruth_DropsPseudoFunctionsAndRebuildsTotals()
    {
        var prim = Path_("tp.export.json");
        var sec = Path_("ts.export.json");
        File.WriteAllText(prim, """
            {"functions":[{"start_ea":"0x1","name":"f1"},{"start_ea":"0x2","name":"f2"}]}
            """);
        File.WriteAllText(sec, """
            {"functions":[{"start_ea":"0x10","name":"f1b"}]}
            """);
        var diff = Path_("t.diff.json");
        DiffJson.WriteJson(diff, new System.Collections.Generic.Dictionary<string, object?>
        {
            ["matched_functions"] = new System.Collections.Generic.List<object?>
            {
                // настоящая пара
                new System.Collections.Generic.Dictionary<string, object?>
                    { ["address1"] = "0x1", ["address2"] = "0x10", ["source"] = "bindiff" },
                // псевдофункция BinExport: адреса нет в перечне IDA → отбросить
                new System.Collections.Generic.Dictionary<string, object?>
                    { ["address1"] = "0xDEAD", ["address2"] = "0xBEEF", ["source"] = "bindiff" },
            },
            ["total_functions1"] = 99,
        });

        string? note = null;
        DiffJsonEnricher.ApplyIdaExportTruth(diff, prim, sec, e => note = e);

        var d = DiffJson.ReadJson(diff)!;
        var ms = Assert.IsType<System.Collections.Generic.Dictionary<string, object?>>(d["matched_summary"]);
        Assert.Equal(1, Convert.ToInt64(ms["total"]));
        // total_functions1 перезаписан перечнем IDA (две функции в экспорте)
        Assert.Equal(2L, Convert.ToInt64(d["total_functions1"]));
        // несовпавшая функция f2 попала в unmatched
        Assert.Equal(1, Convert.ToInt64(d["total_unmatched"]));
        Assert.Equal("ida_export", d["unmatched_source"]);
        Assert.NotNull(note); // «отброшено 1 пар ...»
    }

    [Fact]
    public void NormalizeAddr_ParsesHexVariants()
    {
        Assert.Equal("0x140001000", DiffJsonEnricher.NormalizeAddr("0x140001000"));
        Assert.Equal("0x140001000", DiffJsonEnricher.NormalizeAddr("140001000"));
        Assert.Null(DiffJsonEnricher.NormalizeAddr(""));
        Assert.Null(DiffJsonEnricher.NormalizeAddr("zzz"));
    }

    // ───────────── IdenticalPairsService ─────────────

    [Fact]
    public void IsIdenticalOrFullHexdump_TrueForIdenticalMarker()
    {
        var json = Path_("ident.diff.json");
        DiffJson.WriteJson(json, new System.Collections.Generic.Dictionary<string, object?>
        {
            ["identical"] = true,
            ["hexdump_similarity"] = 1.0,
        });
        Assert.True(IdenticalPairsService.IsIdenticalOrFullHexdump(json));

        DiffJson.WriteJson(json, new System.Collections.Generic.Dictionary<string, object?>
        {
            ["identical"] = false,
            ["hexdump_similarity"] = 1.0,
            ["matched_functions"] = new System.Collections.Generic.List<object?>(),
            ["total_functions1"] = 0,
        });
        Assert.True(IdenticalPairsService.IsIdenticalOrFullHexdump(json));

        DiffJson.WriteJson(json, new System.Collections.Generic.Dictionary<string, object?>
        {
            ["identical"] = false,
            ["hexdump_similarity"] = 0.42,
        });
        Assert.False(IdenticalPairsService.IsIdenticalOrFullHexdump(json));
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
