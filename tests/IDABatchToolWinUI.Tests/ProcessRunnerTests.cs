using System.Diagnostics;
using IDABatchToolWinUI.Services;
using Xunit;

namespace IDABatchToolWinUI.Tests;

public sealed class ProcessRunnerTests
{
    private static ProcessStartInfo Cmd(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add(command);
        return psi;
    }

    [Fact]
    public async Task ExitZero_IsSuccess()
    {
        var result = await ProcessRunner.RunAsync(Cmd("exit 0"), CancellationToken.None);

        Assert.True(result.Started);
        Assert.True(result.Success, result.Describe("cmd"));
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public async Task NonZeroExit_IsFailureAndCapturesStderr()
    {
        var result = await ProcessRunner.RunAsync(
            Cmd("echo boom 1>&2 & exit 3"), CancellationToken.None);

        Assert.True(result.Started);
        Assert.False(result.Success);
        Assert.Equal(3, result.ExitCode);
        Assert.Contains("boom", result.Stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancellation_KillsProcess()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));

        var result = await ProcessRunner.RunAsync(
            Cmd("ping -n 10 127.0.0.1 >nul"), cts.Token);

        Assert.True(result.Started);
        Assert.True(result.Cancelled);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task MissingExecutable_ReturnsNotStarted()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "definitely-not-installed-" + Guid.NewGuid().ToString("N") + ".exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var result = await ProcessRunner.RunAsync(psi, CancellationToken.None);

        Assert.False(result.Started);
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Describe("missing")));
    }
}
