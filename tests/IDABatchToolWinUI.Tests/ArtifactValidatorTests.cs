using IDABatchToolWinUI.Services;
using Xunit;

namespace IDABatchToolWinUI.Tests;

public sealed class ArtifactValidatorTests : IDisposable
{
    private const string ValidExport =
        """{"file_name":"C:\\bin\\7z.exe","functions":[],"imports":[],"ida_info":{"kernel_version":"9.3"}}""";

    private const string ValidDiff =
        """{"primary":"a.i64","secondary":"b.i64","similarity":0.5,"matched_functions":[],"error":null}""";

    // Каталог тестовых файлов — рядом с тестовой сборкой: %TEMP% в этом
    // окружении может быть закрыт для процесса тестов.
    private readonly string _dir = Path.Combine(
        AppContext.BaseDirectory, "test-tmp", Guid.NewGuid().ToString("N"));

    public ArtifactValidatorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* тестовый мусор */ }
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ValidExportJson_Passes()
    {
        var path = Write("7z.exe.i64.export.json", ValidExport);
        Assert.True(ArtifactValidator.IsValidExportJson(path, out var error), error);
    }

    [Fact]
    public void MissingExportJson_Fails()
    {
        Assert.False(ArtifactValidator.IsValidExportJson(Path.Combine(_dir, "missing.json"), out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void EmptyExportJson_Fails()
    {
        var path = Write("empty.json", "");
        Assert.False(ArtifactValidator.IsValidExportJson(path, out _));
    }

    [Fact]
    public void TruncatedExportJson_Fails()
    {
        var path = Write("broken.json", """{"file_name":"x","functions":[""");
        Assert.False(ArtifactValidator.IsValidExportJson(path, out var error));
        Assert.Contains("разбирается", error);
    }

    [Fact]
    public void ExportWithoutFunctions_Fails()
    {
        var path = Write("nofunc.json", """{"file_name":"x","imports":[],"ida_info":{}}""");
        Assert.False(ArtifactValidator.IsValidExportJson(path, out var error));
        Assert.Contains("functions", error);
    }

    [Fact]
    public void ValidDiffJson_Passes()
    {
        var path = Write("pair.diff.json", ValidDiff);
        Assert.True(ArtifactValidator.IsValidDiffJson(path, out var error), error);
    }

    [Fact]
    public void DiffJsonWithError_Fails()
    {
        var path = Write("error.diff.json",
            """{"primary":"a","secondary":"b","similarity":0.1,"matched_functions":[],"error":"сбой парсера"}""");
        Assert.False(ArtifactValidator.IsValidDiffJson(path, out var error));
        Assert.Contains("сбой парсера", error);
    }

    [Fact]
    public void DiffJsonWithoutSimilarity_Fails()
    {
        var path = Write("nosim.diff.json",
            """{"primary":"a","secondary":"b","matched_functions":[],"error":null}""");
        Assert.False(ArtifactValidator.IsValidDiffJson(path, out var error));
        Assert.Contains("similarity", error);
    }

    [Fact]
    public void DiaphoraOnlyDiffJson_WithoutPrimary_Passes()
    {
        // Diaphora-only режим создаёт JSON без primary/secondary — для отчёта
        // это допустимо, если есть similarity и matched_functions.
        var path = Write("diaphora.diff.json",
            """{"similarity":0.0,"matched_functions":[],"error":null}""");
        Assert.True(ArtifactValidator.IsValidDiffJson(path, out var error), error);
    }

    [Fact]
    public void ZeroLengthBase_Fails()
    {
        var basePath = Write("7z.exe.i64", "");
        Assert.False(ArtifactValidator.IsValidIdaDatabase(basePath, out var error));
        Assert.Contains("пустая", error);
    }

    [Fact]
    public void BaseWithCrashMarker_Fails()
    {
        var basePath = Write("7z.exe.i64", "database");
        File.WriteAllText(Path.ChangeExtension(basePath, ".id0"), "crash");
        Assert.False(ArtifactValidator.IsValidIdaDatabase(basePath, out var error));
        Assert.Contains("id0", error);
    }

    [Fact]
    public void ResolveBasePath_FindsBaseForSource()
    {
        var source = Write("7z.exe", "MZ");
        Write("7z.exe.i64", "database");

        Assert.Equal(source + ".i64", ArtifactValidator.ResolveBasePath(source));
        Assert.Equal(source + ".i64", ArtifactValidator.ResolveBasePath(source + ".i64"));
        Assert.Null(ArtifactValidator.ResolveBasePath(Path.Combine(_dir, "other.exe")));
    }

    [Fact]
    public void HasValidArtifacts_RequiresBothBaseAndExport()
    {
        var source = Write("7z.exe", "MZ");
        Write("7z.exe.i64", "database");
        Assert.False(ArtifactValidator.HasValidArtifacts(source));

        Write("7z.exe.i64.export.json", ValidExport);
        Assert.True(ArtifactValidator.HasValidArtifacts(source));
    }
}
