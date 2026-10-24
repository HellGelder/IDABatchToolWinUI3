using IDABatchToolWinUI.Services;
using Xunit;

namespace IDABatchToolWinUI.Tests;

public sealed class ArtifactCleanupTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        AppContext.BaseDirectory, "cleanup-tmp", Guid.NewGuid().ToString("N"));

    public ArtifactCleanupTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* тестовый мусор */ }
    }

    private string Touch(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void KnownArtifacts_DoNotContainForeignLogs()
    {
        var source = Touch("7z.exe");
        Touch("7z.exe.i64");
        Touch("7z.exe.log");
        Touch("7z.exe.asm");
        Touch("7z.exe.id0");
        Touch("keep.log");
        Touch("keep.asm");

        var paths = ArtifactCleanup.GetKnownArtifacts(source, includeLogs: true, includeIdaTemps: true);

        Assert.Contains(Path.Combine(_dir, "7z.exe.log"), paths);
        Assert.Contains(Path.Combine(_dir, "7z.exe.asm"), paths);
        Assert.Contains(Path.Combine(_dir, "7z.exe.id0"), paths);
        Assert.DoesNotContain(Path.Combine(_dir, "keep.log"), paths);
        Assert.DoesNotContain(Path.Combine(_dir, "keep.asm"), paths);
    }

    [Fact]
    public void DeleteKnownArtifacts_KeepsForeignFiles()
    {
        var source = Touch("7z.exe");
        Touch("7z.exe.i64");
        Touch("7z.exe.log");
        Touch("7z.exe.asm");
        Touch("keep.log");
        Touch("keep.asm");

        var deleted = ArtifactCleanup.DeleteKnownArtifacts(source, includeLogs: true, includeIdaTemps: false);

        Assert.Equal(2, deleted);
        Assert.False(File.Exists(Path.Combine(_dir, "7z.exe.log")));
        Assert.False(File.Exists(Path.Combine(_dir, "7z.exe.asm")));
        Assert.True(File.Exists(Path.Combine(_dir, "keep.log")));
        Assert.True(File.Exists(Path.Combine(_dir, "keep.asm")));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void DeleteKnownArtifacts_WithOnlyTemps_KeepsLogsAndAsm()
    {
        var source = Touch("7z.exe");
        Touch("7z.exe.log");
        Touch("7z.exe.asm");
        Touch("7z.exe.id0");
        Touch("7z.exe.nam");

        var deleted = ArtifactCleanup.DeleteKnownArtifacts(source, includeLogs: false, includeIdaTemps: true);

        Assert.Equal(2, deleted);
        Assert.True(File.Exists(Path.Combine(_dir, "7z.exe.log")));
        Assert.True(File.Exists(Path.Combine(_dir, "7z.exe.asm")));
        Assert.False(File.Exists(Path.Combine(_dir, "7z.exe.id0")));
        Assert.False(File.Exists(Path.Combine(_dir, "7z.exe.nam")));
    }
}
