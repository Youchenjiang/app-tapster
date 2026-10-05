namespace Tapster;

/// <summary>
/// Model and geometric calculation engine for click target crosshair marker and jitter spread boundary.
/// </summary>
public sealed class TargetMarkerModel
{
    public const int DefaultBaseDiameterPx = 48;
    public const int MinCrosshairRadiusPx = 10;
    public const int MaxCrosshairRadiusPx = 20;

    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public double JitterRadiusPx { get; set; }
    public int BaseDiameterPx { get; set; } = DefaultBaseDiameterPx;
    public bool IsVisible { get; set; }

    /// <summary>
    /// Calculates the required diameter of the overlay window to cover both the crosshair and jitter spread area.
    /// </summary>
    public int GetEffectiveDiameter()
    {
        int jitterDiameter = (int)Math.Ceiling(Math.Max(0, JitterRadiusPx) * 2) + 16;
        return Math.Max(BaseDiameterPx, jitterDiameter);
    }

    /// <summary>
    /// Calculates the top-left screen position and dimensions (X, Y, Width, Height) for the overlay window.
    /// Centered at (TargetX, TargetY).
    /// </summary>
    public (int X, int Y, int Width, int Height) CalculateWindowBounds()
    {
        int diameter = GetEffectiveDiameter();
        int radius = diameter / 2;
        return (TargetX - radius, TargetY - radius, diameter, diameter);
    }

    /// <summary>
    /// Calculates relative drawing parameters inside the local window DC:
    /// (CenterX, CenterY, CrosshairRadius, JitterRadius).
    /// </summary>
    public (int CenterX, int CenterY, int CrosshairRadius, int JitterRadius) CalculateRelativeGeometry()
    {
        int diameter = GetEffectiveDiameter();
        int center = diameter / 2;
        int crosshairRadius = Math.Clamp(center / 2, MinCrosshairRadiusPx, MaxCrosshairRadiusPx);
        int jitterRadius = (int)Math.Round(Math.Max(0, JitterRadiusPx));
        return (center, center, crosshairRadius, jitterRadius);
    }

    /// <summary>
    /// Determines whether a given screen point falls within the jitter spread boundary circle.
    /// </summary>
    public bool IsInsideJitterRadius(int screenX, int screenY)
    {
        if (JitterRadiusPx <= 0)
        {
            return screenX == TargetX && screenY == TargetY;
        }

        double dx = screenX - TargetX;
        double dy = screenY - TargetY;
        return (dx * dx + dy * dy) <= (JitterRadiusPx * JitterRadiusPx);
    }
}
