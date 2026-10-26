using IDABatchToolWinUI.Services;
using Xunit;

namespace IDABatchToolWinUI.Tests;

/// <summary>
/// InputScanner: общий сканер входной папки для AnalysisPage/SfaPage -
/// поиск исполняемых файлов, сборка FileItem, ошибки распаковки архивов.
/// </summary>
public sealed class InputScannerTests : IDisposable
{
    private readonly string _dir;

    public InputScannerTests()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "scantest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* не критично */ }
    }

    [Fact]
    public async Task Scan_FindsExecutableFiles_WithSizeAndStatus()
    {
        var exe = Path.Combine(_dir, "tool.exe");
        File.WriteAllBytes(exe, new byte[] { 0x4D, 0x5A, 1, 2, 3, 4 }); // MZ-сигнатура
        File.WriteAllBytes(Path.Combine(_dir, "readme.txt"), new byte[] { 1 }); // не исполняемый

        var scan = await InputScanner.ScanAsync(_dir);

        var item = Assert.Single(scan.Files);
        Assert.Equal("tool.exe", item.Name);
        Assert.Equal(Path.GetFullPath(exe), item.Path);
        Assert.Equal(6, item.Size);
        Assert.Equal(AnalysisStatus.NotAnalyzed, item.Status); // без базы и export.json
        Assert.Empty(scan.ArchiveErrors);
    }

    [Fact]
    public async Task Scan_MissingDirectory_ReturnsEmpty()
    {
        var scan = await InputScanner.ScanAsync(Path.Combine(_dir, "no-such-dir"));

        Assert.Empty(scan.Files);
        Assert.Empty(scan.ArchiveErrors);
    }

    [Fact]
    public async Task Scan_BrokenDmg_ProducesArchiveError()
    {
        // Мусорный .dmg: без 7z не распакуется, с 7z - тоже (не образ).
        // В обоих случаях сканер обязан сообщить об ошибке, а не молчать.
        File.WriteAllBytes(Path.Combine(_dir, "broken.dmg"), new byte[] { 1, 2, 3, 4 });

        var scan = await InputScanner.ScanAsync(_dir);

        Assert.Empty(scan.Files);
        var err = Assert.Single(scan.ArchiveErrors);
        Assert.Contains("broken.dmg", err);
    }
}
