using Xunit;

namespace Tapster.Core.Tests;

public class TargetMarkerTests
{
    [Fact]
    public void TargetMarkerModel_Defaults_AreAccurate()
    {
        var model = new TargetMarkerModel();

        Assert.Equal(48, model.BaseDiameterPx);
        Assert.Equal(0, model.TargetX);
        Assert.Equal(0, model.TargetY);
        Assert.Equal(0, model.JitterRadiusPx);
        Assert.False(model.IsVisible);
    }

    [Theory]
    [InlineData(0, 48)]
    [InlineData(10, 48)] // 10*2 + 16 = 36 <= 48 -> 48
    [InlineData(20, 56)] // 20*2 + 16 = 56 > 48 -> 56
    [InlineData(50, 116)] // 50*2 + 16 = 116
    public void TargetMarkerModel_GetEffectiveDiameter_AccountsForJitter(double jitterRadius, int expectedDiameter)
    {
        var model = new TargetMarkerModel
        {
            JitterRadiusPx = jitterRadius
        };

        int actual = model.GetEffectiveDiameter();
        Assert.Equal(expectedDiameter, actual);
    }

    [Fact]
    public void TargetMarkerModel_CalculateWindowBounds_CentersProperly()
    {
        var model = new TargetMarkerModel
        {
            TargetX = 500,
            TargetY = 300,
            JitterRadiusPx = 0
        };

        var (x, y, w, h) = model.CalculateWindowBounds();

        Assert.Equal(476, x); // 500 - 24
        Assert.Equal(276, y); // 300 - 24
        Assert.Equal(48, w);
        Assert.Equal(48, h);
    }

    [Fact]
    public void TargetMarkerModel_CalculateWindowBounds_HandlesNegativeCoordinates()
    {
        var model = new TargetMarkerModel
        {
            TargetX = -200,
            TargetY = -150,
            JitterRadiusPx = 20 // diameter = 56, radius = 28
        };

        var (x, y, w, h) = model.CalculateWindowBounds();

        Assert.Equal(-228, x); // -200 - 28
        Assert.Equal(-178, y); // -150 - 28
        Assert.Equal(56, w);
        Assert.Equal(56, h);
    }

    [Fact]
    public void TargetMarkerModel_CalculateRelativeGeometry_ClampsCrosshair()
    {
        var model = new TargetMarkerModel
        {
            TargetX = 100,
            TargetY = 100,
            JitterRadiusPx = 30 // diameter = 76, center = 38
        };

        var (cx, cy, crosshairRadius, jitterRadius) = model.CalculateRelativeGeometry();

        Assert.Equal(38, cx);
        Assert.Equal(38, cy);
        Assert.True(crosshairRadius is >= TargetMarkerModel.MinCrosshairRadiusPx and <= TargetMarkerModel.MaxCrosshairRadiusPx);
        Assert.Equal(30, jitterRadius);
    }

    [Fact]
    public void TargetMarkerModel_IsInsideJitterRadius_IdentifiesPoints()
    {
        var model = new TargetMarkerModel
        {
            TargetX = 100,
            TargetY = 200,
            JitterRadiusPx = 10
        };

        Assert.True(model.IsInsideJitterRadius(100, 200));
        Assert.True(model.IsInsideJitterRadius(106, 208)); // dx=6, dy=8 -> 36+64=100 <= 100
        Assert.False(model.IsInsideJitterRadius(108, 208)); // dx=8, dy=8 -> 64+64=128 > 100

        model.JitterRadiusPx = 0;
        Assert.True(model.IsInsideJitterRadius(100, 200));
        Assert.False(model.IsInsideJitterRadius(101, 200));
    }
}
