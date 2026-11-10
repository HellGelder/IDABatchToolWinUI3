using IDABatchToolWinUI.Services;
using Xunit;

namespace IDABatchToolWinUI.Tests;

/// <summary>
/// Проверка актуальной версии по version.txt: разбор машиночитаемой строки
/// и сравнение версий (без сети - сам запрос в тестах не выполняется).
/// </summary>
public sealed class VersionCheckServiceTests
{
    [Fact]
    public void ParsesActualVersionLine()
    {
        const string text =
            "IDA BATCH TOOL (WinUI) - журнал версий сборки\r\n" +
            "АКТУАЛЬНАЯ ВЕРСИЯ: 1.0.0.42\r\n\r\n1.0.0.42 / 2026-11-10\r\n";
        Assert.Equal("1.0.0.42", VersionCheckService.ParseLatestVersion(text));
    }

    [Fact]
    public void ParsesLineWithDifferentSpacing()
    {
        const string text = "  Актуальная  версия :  2.1.0  \r\n";
        Assert.Equal("2.1.0", VersionCheckService.ParseLatestVersion(text));
    }

    [Fact]
    public void MissingLineReturnsNull()
    {
        Assert.Null(VersionCheckService.ParseLatestVersion("1.0.0.42 / 2026-11-10"));
        Assert.Null(VersionCheckService.ParseLatestVersion(""));
        Assert.Null(VersionCheckService.ParseLatestVersion(null));
    }

    [Theory]
    [InlineData("1.0.0.41", "1.0.0.42", true)]
    [InlineData("1.0.0.42", "1.0.0.42", false)]
    [InlineData("1.0.0.42", "1.0.0.41", false)]
    [InlineData("1.0.0.42", "1.0.1", true)]
    [InlineData("1.0.0.42", "1.0", false)]
    [InlineData("2.0.0.0", "1.99.99.99", false)]
    public void ComparesVersions(string current, string latest, bool outdated)
    {
        Assert.Equal(outdated, VersionCheckService.IsOutdated(current, latest));
    }

    [Fact]
    public void CurrentVersionIsFourPartNumber()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", VersionCheckService.CurrentVersion);
    }
}
