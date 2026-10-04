using System;
using Tapster;
using Xunit;

namespace Tapster.Core.Tests;

public class JitterHelperTests
{
    [Fact]
    public void ApplyTimeJitter_ZeroOrNegativeJitter_ReturnsOriginalInterval()
    {
        Assert.Equal(100, JitterHelper.ApplyTimeJitter(100, 0));
        Assert.Equal(100, JitterHelper.ApplyTimeJitter(100, -10));
    }

    [Fact]
    public void ApplyTimeJitter_ZeroOrNegativeBaseInterval_ReturnsClampedMinimum()
    {
        Assert.Equal(5, JitterHelper.ApplyTimeJitter(0, 20));
        Assert.Equal(5, JitterHelper.ApplyTimeJitter(-50, 20));
    }

    [Theory]
    [InlineData(100, 10, 90, 110)]
    [InlineData(200, 25, 150, 250)]
    [InlineData(50, 50, 25, 75)]
    public void ApplyTimeJitter_ValidJitter_ReturnsWithinExpectedBounds(int baseMs, double jitterPct, int minExpected, int maxExpected)
    {
        for (int i = 0; i < 50; i++)
        {
            int jittered = JitterHelper.ApplyTimeJitter(baseMs, jitterPct);
            Assert.InRange(jittered, minExpected, maxExpected);
        }
    }

    [Fact]
    public void ApplyTimeJitter_EnforcesMinimumIntervalMs()
    {
        // 10ms with 90% jitter could drop to 1ms without min clamping
        for (int i = 0; i < 50; i++)
        {
            int jittered = JitterHelper.ApplyTimeJitter(10, 90, minIntervalMs: 5);
            Assert.True(jittered >= 5, $"Expected jittered >= 5, but got {jittered}");
        }
    }

    [Fact]
    public void ApplyLocationJitter_ZeroOrNegativeRadius_ReturnsOriginalCoordinates()
    {
        var (x1, y1) = JitterHelper.ApplyLocationJitter(500, 300, 0);
        Assert.Equal(500, x1);
        Assert.Equal(300, y1);

        var (x2, y2) = JitterHelper.ApplyLocationJitter(500, 300, -5);
        Assert.Equal(500, x2);
        Assert.Equal(300, y2);
    }

    [Fact]
    public void ApplyLocationJitter_ValidRadius_ReturnsWithinRadiusCircle()
    {
        int originX = 500;
        int originY = 500;
        double radius = 20.0;

        for (int i = 0; i < 50; i++)
        {
            var (jitteredX, jitteredY) = JitterHelper.ApplyLocationJitter(originX, originY, radius);
            double dist = Math.Sqrt(Math.Pow(jitteredX - originX, 2) + Math.Pow(jitteredY - originY, 2));
            Assert.True(dist <= radius + 1.0, $"Point ({jitteredX}, {jitteredY}) is outside radius {radius} (dist={dist})");
        }
    }

    [Fact]
    public void ApplyLocationJitter_NeverReturnsNegativeCoordinates()
    {
        for (int i = 0; i < 30; i++)
        {
            var (x, y) = JitterHelper.ApplyLocationJitter(2, 2, 10);
            Assert.True(x >= 0, $"X should be non-negative, got {x}");
            Assert.True(y >= 0, $"Y should be non-negative, got {y}");
        }
    }
}
