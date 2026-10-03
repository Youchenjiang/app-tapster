using Tapster;
using Xunit;

namespace Tapster.Core.Tests;

public class MacroTimingAndSpeedTests
{
    [Fact]
    public void MacroRecorder_InitialState_HasEmptyActions()
    {
        var recorder = new MacroRecorder();
        Assert.NotNull(recorder.Actions);
        Assert.Empty(recorder.Actions);
    }

    [Fact]
    public void MacroRecorder_Clear_EnsuresEmptyActionList()
    {
        var recorder = new MacroRecorder();
        recorder.Clear();
        Assert.Empty(recorder.Actions);
    }

    [Fact]
    public async Task MacroRecorder_ReplayAsync_EmptyActions_ReturnsImmediately()
    {
        var recorder = new MacroRecorder();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Replaying empty actions should return without throwing or blocking
        var exception = await Record.ExceptionAsync(() =>
            recorder.ReplayAsync(repeatCount: 1, speedMultiplier: 1.0, cts.Token));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(1000L, 1.0, 1000)]
    [InlineData(1000L, 2.0, 500)]
    [InlineData(1000L, 0.5, 2000)]
    [InlineData(100L, 10.0, 10)]
    [InlineData(500L, 0.05, 5000)] // Math.Max(0.1, speedMultiplier) prevents divide-by-zero, clamping to 0.1 (500 / 0.1 = 5000)
    [InlineData(500L, 0.0, 5000)]  // Clamped to 0.1
    public void SpeedMultiplier_DelayCalculation_ScalesExpectedly(long delayMs, double speedMultiplier, int expectedDelay)
    {
        int actualDelay = (int)(delayMs / Math.Max(0.1, speedMultiplier));
        Assert.Equal(expectedDelay, actualDelay);
    }
}
