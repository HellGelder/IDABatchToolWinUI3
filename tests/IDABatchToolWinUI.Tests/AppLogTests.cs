using IDABatchToolWinUI.Services;
using Xunit;

namespace IDABatchToolWinUI.Tests;

public sealed class AppLogTests : IDisposable
{
    private readonly string _dirA = Path.Combine(
        AppContext.BaseDirectory, "log-tmp", Guid.NewGuid().ToString("N"), "a");
    private readonly string _dirB = Path.Combine(
        AppContext.BaseDirectory, "log-tmp", Guid.NewGuid().ToString("N"), "b");

    public AppLogTests()
    {
        Directory.CreateDirectory(_dirA);
        Directory.CreateDirectory(_dirB);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_dirA)!, recursive: true); } catch { /* тестовый мусор */ }
    }

    [Fact]
    public void Error_IsWrittenToBothDirectories()
    {
        AppLog.Initialize("INFO", new[] { _dirA, _dirB });

        AppLog.Error("boom-message");

        foreach (var dir in new[] { _dirA, _dirB })
        {
            var file = Assert.Single(Directory.GetFiles(dir, "app-*.log"));
            Assert.Contains("boom-message", File.ReadAllText(file));
        }
    }

    [Fact]
    public void Crash_IsWrittenToAppLogAndCrashLog()
    {
        AppLog.Initialize("INFO", new[] { _dirA, _dirB });

        AppLog.Crash(new InvalidOperationException("crash-message"));

        foreach (var dir in new[] { _dirA, _dirB })
        {
            var crash = Path.Combine(dir, "crash.log");
            Assert.True(File.Exists(crash));
            Assert.Contains("crash-message", File.ReadAllText(crash));
            var appLog = Assert.Single(Directory.GetFiles(dir, "app-*.log"));
            Assert.Contains("crash-message", File.ReadAllText(appLog));
        }
    }

    [Fact]
    public void Level_Filter_HidesLowerSeverity()
    {
        AppLog.Initialize("ERROR", new[] { _dirA });

        AppLog.Info("info-message");
        AppLog.Error("error-message");

        var file = Assert.Single(Directory.GetFiles(_dirA, "app-*.log"));
        var text = File.ReadAllText(file);
        Assert.DoesNotContain("info-message", text);
        Assert.Contains("error-message", text);
    }
}
