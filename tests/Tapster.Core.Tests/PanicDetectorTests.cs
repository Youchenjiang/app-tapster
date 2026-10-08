using System;
using Tapster;
using Xunit;

namespace Tapster.Core.Tests;

public class PanicDetectorTests
{
    [Fact]
    public void MouseShake_InitialSample_ReturnsFalse()
    {
        var detector = new PanicDetector();
        bool shaken = detector.CheckMouseShake(100, 100, 1000);
        Assert.False(shaken);
    }

    [Fact]
    public void MouseShake_SlowMovement_ReturnsFalse()
    {
        var detector = new PanicDetector();
        detector.CheckMouseShake(100, 100, 1000);

        // Moved 20px in 100ms => 200 px/sec (< 2500 px/s threshold)
        bool shaken = detector.CheckMouseShake(120, 100, 1100);
        Assert.False(shaken);
    }

    [Fact]
    public void MouseShake_TinyDistance_ReturnsFalse()
    {
        var detector = new PanicDetector();
        detector.CheckMouseShake(100, 100, 1000);

        // Moved 5px in 1ms => mathematically 5000 px/s, but distance (5px) < MinShakeDistance (30px)
        bool shaken = detector.CheckMouseShake(105, 100, 1001);
        Assert.False(shaken);
    }

    [Fact]
    public void MouseShake_ExpiredInterval_ReturnsFalse()
    {
        var detector = new PanicDetector();
        detector.CheckMouseShake(100, 100, 1000);

        // 600ms elapsed (> MaxSampleIntervalMs 500ms), resets baseline
        bool shaken = detector.CheckMouseShake(500, 500, 1600);
        Assert.False(shaken);
    }

    [Fact]
    public void MouseShake_ViolentShake_ReturnsTrue()
    {
        var detector = new PanicDetector();
        detector.CheckMouseShake(100, 100, 1000);

        // Moved 200px in 40ms => 5000 px/s (> 2500 px/s threshold, distance >= 30px)
        bool shaken = detector.CheckMouseShake(300, 100, 1040);
        Assert.True(shaken);
    }

    [Fact]
    public void MouseShake_SyncProgrammaticPosition_PreventsFalsePositive()
    {
        var detector = new PanicDetector();
        detector.CheckMouseShake(100, 100, 1000);

        // Programmatic click or move leaps to (800, 800)
        detector.SyncProgrammaticPosition(800, 800);

        // Next sample at (800, 800) must ignore the jump
        bool shaken = detector.CheckMouseShake(800, 800, 1010);
        Assert.False(shaken);
    }

    [Fact]
    public void MouseShake_Reset_ClearsTracking()
    {
        var detector = new PanicDetector();
        detector.CheckMouseShake(100, 100, 1000);
        detector.Reset();

        // After reset, this acts as an initial sample
        bool shaken = detector.CheckMouseShake(500, 500, 1020);
        Assert.False(shaken);
    }

    [Fact]
    public void TripleEsc_HoldingKey_DoesNotTriggerMultiplePresses()
    {
        var detector = new PanicDetector();

        // Rising edge
        Assert.False(detector.RecordEscPress(true, 1000));
        // Held down across subsequent checks
        Assert.False(detector.RecordEscPress(true, 1050));
        Assert.False(detector.RecordEscPress(true, 1100));
        Assert.False(detector.RecordEscPress(true, 1150));
    }

    [Fact]
    public void TripleEsc_ThreeRapidPressesWithinWindow_TriggersPanic()
    {
        var detector = new PanicDetector();

        // Press 1
        Assert.False(detector.RecordEscPress(true, 1000));
        Assert.False(detector.RecordEscPress(false, 1050));

        // Press 2
        Assert.False(detector.RecordEscPress(true, 1150));
        Assert.False(detector.RecordEscPress(false, 1200));

        // Press 3 (total duration 300ms <= 500ms window)
        bool triggered = detector.RecordEscPress(true, 1300);
        Assert.True(triggered);

        // Next press starts fresh window
        Assert.False(detector.RecordEscPress(false, 1350));
        Assert.False(detector.RecordEscPress(true, 1400));
    }

    [Fact]
    public void TripleEsc_SpreadOutPresses_DoesNotTrigger()
    {
        var detector = new PanicDetector();

        // Press 1 at 1000ms
        Assert.False(detector.RecordEscPress(true, 1000));
        Assert.False(detector.RecordEscPress(false, 1100));

        // Press 2 at 1300ms
        Assert.False(detector.RecordEscPress(true, 1300));
        Assert.False(detector.RecordEscPress(false, 1400));

        // Press 3 at 1700ms (press 1 at 1000ms has expired because 1700 - 1000 = 700ms > 500ms)
        bool triggered = detector.RecordEscPress(true, 1700);
        Assert.False(triggered);
    }

    [Fact]
    public void TripleEsc_Reset_ClearsBuffer()
    {
        var detector = new PanicDetector();

        // 2 rapid presses
        detector.RecordEscPress(true, 1000);
        detector.RecordEscPress(false, 1050);
        detector.RecordEscPress(true, 1100);
        detector.RecordEscPress(false, 1150);

        detector.Reset();

        // 3rd press after reset should be counted as only 1st press
        bool triggered = detector.RecordEscPress(true, 1200);
        Assert.False(triggered);
    }
}
