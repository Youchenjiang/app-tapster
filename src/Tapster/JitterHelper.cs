using System;

namespace Tapster;

/// <summary>
/// Provides utility functions for simulating human-like timing and coordinate jitter.
/// </summary>
public static class JitterHelper
{
    /// <summary>
    /// Applies random percentage jitter to a base millisecond interval.
    /// E.g., baseMs = 100, jitterPercent = 15 => returns a value between 85 and 115 ms.
    /// </summary>
    public static int ApplyTimeJitter(int baseIntervalMs, double jitterPercent, int minIntervalMs = 5)
    {
        if (jitterPercent <= 0 || baseIntervalMs <= 0)
        {
            return Math.Max(minIntervalMs, baseIntervalMs);
        }

        double fraction = Math.Clamp(jitterPercent / 100.0, 0.0, 1.0);
        double delta = (Random.Shared.NextDouble() * 2.0 - 1.0) * fraction;
        double result = baseIntervalMs * (1.0 + delta);
        return Math.Max(minIntervalMs, (int)Math.Round(result));
    }

    /// <summary>
    /// Applies random spatial jitter within a circular radius around (x, y).
    /// </summary>
    public static (int X, int Y) ApplyLocationJitter(int x, int y, double radiusPx)
    {
        if (radiusPx <= 0)
        {
            return (x, y);
        }

        double angle = Random.Shared.NextDouble() * 2.0 * Math.PI;
        double r = Math.Sqrt(Random.Shared.NextDouble()) * radiusPx;
        int dx = (int)Math.Round(r * Math.Cos(angle));
        int dy = (int)Math.Round(r * Math.Sin(angle));

        return (Math.Max(0, x + dx), Math.Max(0, y + dy));
    }
}
