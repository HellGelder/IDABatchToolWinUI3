using System.Text.RegularExpressions;
using Xunit;

namespace IDABatchToolWinUI.Tests;

/// <summary>
/// Единый источник версии (F2): Directory.Build.props. Тесты ловят рассинхрон
/// манифестов и журнала version.txt с props - те же правила, что и assert'ы
/// make-portable.ps1, но работают на каждой прогонке тестов, а не только на релизе.
/// </summary>
public sealed class VersionConsistencyTests
{
    /// <summary>Корень репозитория: поднимаемся от тестовой сборки, пока не встретим csproj.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "IDABatchToolWinUI.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string VersionFromProps(string root)
    {
        var raw = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
        return Regex.Match(raw, "<FileVersion>([^<]+)</FileVersion>").Groups[1].Value;
    }

    [Fact]
    public void PropsFileVersionIsDefined()
    {
        var version = VersionFromProps(RepoRoot());
        Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", version);
        // Четыре поля props совпадают между собой - иначе поле «свойств exe» разъедется.
        var raw = File.ReadAllText(Path.Combine(RepoRoot(), "Directory.Build.props"));
        Assert.True(Regex.Matches(raw, $">{Regex.Escape(version)}<").Count == 4,
            "В Directory.Build.props четыре поля версии должны иметь одно значение.");
    }

    [Fact]
    public void CsprojDoesNotOverrideVersion()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "IDABatchToolWinUI.csproj"));
        Assert.DoesNotMatch("<(Version|FileVersion|AssemblyVersion|InformationalVersion)>", csproj);
    }

    [Fact]
    public void PackageAppxManifestMatchesProps()
    {
        var root = RepoRoot();
        var version = VersionFromProps(root);
        var raw = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));
        var manifest = Regex.Match(raw, @"(?m)^\s*Version=""([^""]+)""\s*/>").Groups[1].Value;
        Assert.Equal(version, manifest);
    }

    [Fact]
    public void AppManifestMatchesProps()
    {
        var root = RepoRoot();
        var version = VersionFromProps(root);
        var raw = File.ReadAllText(Path.Combine(root, "app.manifest"));
        var manifest = Regex.Match(raw, "<assemblyIdentity version=\"([^\"]+)\"").Groups[1].Value;
        Assert.Equal(version, manifest);
    }

    [Fact]
    public void VersionTxtTopEntryMatchesProps()
    {
        var root = RepoRoot();
        var version = VersionFromProps(root);
        var raw = File.ReadAllText(Path.Combine(root, "version.txt"));
        var top = Regex.Match(raw, @"(?m)^(\d+\.\d+\.\d+\.\d+)\s*/\s*\d{4}-\d{2}-\d{2}").Groups[1].Value;
        Assert.Equal(version, top);
    }
}
