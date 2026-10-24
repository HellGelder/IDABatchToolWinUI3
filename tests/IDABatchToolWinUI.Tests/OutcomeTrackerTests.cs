using IDABatchToolWinUI.Services;
using Xunit;

namespace IDABatchToolWinUI.Tests;

public sealed class OutcomeTrackerTests
{
    [Fact]
    public void OneFailedPhase_MakesPairFailed()
    {
        var tracker = new OutcomeTracker(new[] { "a", "b", "c" });

        tracker.Fail("b");

        Assert.Equal(3, tracker.Total);
        Assert.Equal(2, tracker.Succeeded);
        Assert.Equal(1, tracker.Failed);
        Assert.True(tracker.IsFailed("b"));
        Assert.False(tracker.IsFailed("a"));
    }

    [Fact]
    public void DuplicateKeys_AreCountedOnce()
    {
        var tracker = new OutcomeTracker(new[] { "a", "a", "b" });

        Assert.Equal(2, tracker.Total);
        tracker.Fail("a");
        Assert.Equal(1, tracker.Succeeded);
    }

    [Fact]
    public void UnknownFailedKey_IsAddedAsFailure()
    {
        var tracker = new OutcomeTracker(new[] { "a" });

        tracker.Fail("late");

        Assert.Equal(2, tracker.Total);
        Assert.Equal(1, tracker.Succeeded);
        Assert.Equal(1, tracker.Failed);
    }

    [Fact]
    public void MarkSucceeded_DoesNotOverrideFailure()
    {
        var tracker = new OutcomeTracker(new[] { "a" });

        tracker.Fail("a");
        tracker.MarkSucceeded("a");

        Assert.True(tracker.IsFailed("a"));
        Assert.Equal(0, tracker.Succeeded);
    }

    [Fact]
    public void ParallelFailures_AreAllCounted()
    {
        var keys = Enumerable.Range(0, 200).Select(i => $"pair-{i}").ToArray();
        var tracker = new OutcomeTracker(keys);

        Parallel.ForEach(keys.Where((_, i) => i % 2 == 0), key => tracker.Fail(key));

        Assert.Equal(200, tracker.Total);
        Assert.Equal(100, tracker.Failed);
        Assert.Equal(100, tracker.Succeeded);
    }
}
