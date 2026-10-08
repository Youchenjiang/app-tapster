using System;
using System.Collections.Generic;

namespace Tapster;

/// <summary>
/// Detects hardware-level emergency panic conditions such as rapid cursor shake gestures
/// or rapid triple-press of the Esc key to immediately halt automation tasks.
/// </summary>
public sealed class PanicDetector
{
    public const string EmergencyPanicMessage = "Emergency stop triggered via panic shake/Esc";

    public const double DefaultShakeSpeedThreshold = 2500.0; // pixels per second
    public const double DefaultMinShakeDistance = 30.0;     // pixels
    public const int DefaultTripleEscWindowMs = 500;        // milliseconds
    public const int DefaultMaxSampleIntervalMs = 500;      // milliseconds

    private readonly double _shakeSpeedThreshold;
    private readonly double _minShakeDistance;
    private readonly int _tripleEscWindowMs;
    private readonly int _maxSampleIntervalMs;

    private int? _lastX;
    private int? _lastY;
    private long _lastMouseTimeMs;
    private bool _ignoreNextMouse;

    private bool _lastEscDown;
    private readonly Queue<long> _escPressTimestamps = new();

    public PanicDetector(
        double shakeSpeedThreshold = DefaultShakeSpeedThreshold,
        double minShakeDistance = DefaultMinShakeDistance,
        int tripleEscWindowMs = DefaultTripleEscWindowMs,
        int maxSampleIntervalMs = DefaultMaxSampleIntervalMs)
    {
        _shakeSpeedThreshold = shakeSpeedThreshold;
        _minShakeDistance = minShakeDistance;
        _tripleEscWindowMs = tripleEscWindowMs;
        _maxSampleIntervalMs = maxSampleIntervalMs;
    }

    /// <summary>
    /// Resets all internal tracking states and buffers.
    /// </summary>
    public void Reset()
    {
        _lastX = null;
        _lastY = null;
        _lastMouseTimeMs = 0;
        _ignoreNextMouse = false;
        _lastEscDown = false;
        _escPressTimestamps.Clear();
    }

    /// <summary>
    /// Synchronizes the detector with a programmatically initiated cursor movement,
    /// preventing the programmatic jump from triggering a false-positive shake detection.
    /// </summary>
    public void SyncProgrammaticPosition(int x, int y)
    {
        _lastX = x;
        _lastY = y;
        _lastMouseTimeMs = Environment.TickCount64;
        _ignoreNextMouse = true;
    }

    /// <summary>
    /// Checks whether cursor movement between samples represents a rapid physical shake gesture.
    /// </summary>
    public bool CheckMouseShake(int currentX, int currentY, long currentTimestampMs)
    {
        if (_ignoreNextMouse || !_lastX.HasValue || !_lastY.HasValue)
        {
            UpdateMouseState(currentX, currentY, currentTimestampMs);
            _ignoreNextMouse = false;
            return false;
        }

        long deltaT = currentTimestampMs - _lastMouseTimeMs;
        if (deltaT <= 0 || deltaT > _maxSampleIntervalMs)
        {
            UpdateMouseState(currentX, currentY, currentTimestampMs);
            return false;
        }

        double dx = currentX - _lastX.Value;
        double dy = currentY - _lastY.Value;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        double speed = distance / (deltaT / 1000.0);

        UpdateMouseState(currentX, currentY, currentTimestampMs);

        return distance >= _minShakeDistance && speed >= _shakeSpeedThreshold;
    }

    private void UpdateMouseState(int x, int y, long timestampMs)
    {
        _lastX = x;
        _lastY = y;
        _lastMouseTimeMs = timestampMs;
    }

    /// <summary>
    /// Records the current state of the Esc key and determines whether a rapid triple-press occurred within the window.
    /// Uses rising-edge detection so holding down Esc is not counted as multiple presses.
    /// </summary>
    public bool RecordEscPress(bool isEscDown, long currentTimestampMs)
    {
        bool isRisingEdge = isEscDown && !_lastEscDown;
        _lastEscDown = isEscDown;

        if (!isRisingEdge)
        {
            return false;
        }

        // Purge timestamps outside the sliding window
        while (_escPressTimestamps.Count > 0 && currentTimestampMs - _escPressTimestamps.Peek() > _tripleEscWindowMs)
        {
            _escPressTimestamps.Dequeue();
        }

        _escPressTimestamps.Enqueue(currentTimestampMs);

        if (_escPressTimestamps.Count >= 3)
        {
            _escPressTimestamps.Clear();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks for any panic condition using current live mouse position and Esc key states.
    /// Returns true and populates reason if emergency panic stop should trigger.
    /// </summary>
    public bool CheckPanic(out string? reason)
    {
        long now = Environment.TickCount64;
        var (curX, curY) = Mouse.GetPosition();
        bool isEsc = Keyboard.IsEscPressed();

        if (CheckMouseShake(curX, curY, now))
        {
            reason = EmergencyPanicMessage;
            return true;
        }

        if (RecordEscPress(isEsc, now))
        {
            reason = EmergencyPanicMessage;
            return true;
        }

        reason = null;
        return false;
    }
}
