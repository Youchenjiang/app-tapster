using System.Text.Json;
using Tapster;
using Xunit;

namespace Tapster.Core.Tests;

public class KeyHolderStageTests
{
    [Fact]
    public void DefaultConstructor_SetsExpectedDefaults()
    {
        var stage = new KeyHolderStage();

        Assert.Equal(string.Empty, stage.KeyCombo);
        Assert.Equal(5.0, stage.HoldDurationSec);
        Assert.Equal(0.5, stage.RestDurationSec);
    }

    [Fact]
    public void ParameterizedConstructor_InitializesProperties()
    {
        var stage = new KeyHolderStage("shift+w", 3.5, 1.2);

        Assert.Equal("shift+w", stage.KeyCombo);
        Assert.Equal(3.5, stage.HoldDurationSec);
        Assert.Equal(1.2, stage.RestDurationSec);
    }

    [Fact]
    public void ParameterizedConstructor_TrimsKeyComboAndEnforcesNonNegativeDurations()
    {
        var stage = new KeyHolderStage("  space  ", -2.0, -1.0);

        Assert.Equal("space", stage.KeyCombo);
        Assert.Equal(0.0, stage.HoldDurationSec);
        Assert.Equal(0.0, stage.RestDurationSec);
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new KeyHolderStage("ctrl+c", 2.0, 0.4);
        var copy = original.Clone();

        Assert.NotSame(original, copy);
        Assert.Equal(original.KeyCombo, copy.KeyCombo);
        Assert.Equal(original.HoldDurationSec, copy.HoldDurationSec);
        Assert.Equal(original.RestDurationSec, copy.RestDurationSec);

        copy.KeyCombo = "ctrl+v";
        Assert.NotEqual(original.KeyCombo, copy.KeyCombo);
    }

    [Fact]
    public void JsonSerialization_PreservesAllProperties()
    {
        var stage = new KeyHolderStage("alt+f4", 10.0, 2.5);
        string json = JsonSerializer.Serialize(stage);
        var deserialized = JsonSerializer.Deserialize<KeyHolderStage>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(stage.KeyCombo, deserialized.KeyCombo);
        Assert.Equal(stage.HoldDurationSec, deserialized.HoldDurationSec);
        Assert.Equal(stage.RestDurationSec, deserialized.RestDurationSec);
    }

    [Fact]
    public void ToString_ReturnsFormattedSummary()
    {
        var stage = new KeyHolderStage("w", 5.0, 0.5);
        string str = stage.ToString();

        Assert.Contains("[w]", str);
        Assert.Contains("5.0s", str);
        Assert.Contains("0.5s", str);
    }
}
