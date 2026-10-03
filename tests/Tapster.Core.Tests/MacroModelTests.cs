using System.Text.Json;
using Tapster;
using Xunit;

namespace Tapster.Core.Tests;

public class MacroModelTests
{
    [Fact]
    public void MacroAction_DefaultValues_AreProperlyInitialized()
    {
        var action = new MacroAction();

        Assert.Equal(MacroActionType.ClickLeft, action.Type);
        Assert.Equal(0, action.X);
        Assert.Equal(0, action.Y);
        Assert.Equal(string.Empty, action.Data);
        Assert.Equal(0L, action.DelayMs);
    }

    [Fact]
    public void MacroAction_CustomValues_RetainAssignedProperties()
    {
        var action = new MacroAction
        {
            Type = MacroActionType.KeyPress,
            X = 120,
            Y = 340,
            Data = "ctrl",
            DelayMs = 250
        };

        Assert.Equal(MacroActionType.KeyPress, action.Type);
        Assert.Equal(120, action.X);
        Assert.Equal(340, action.Y);
        Assert.Equal("ctrl", action.Data);
        Assert.Equal(250L, action.DelayMs);
    }

    [Theory]
    [InlineData(MacroActionType.ClickLeft)]
    [InlineData(MacroActionType.ClickRight)]
    [InlineData(MacroActionType.ClickMiddle)]
    [InlineData(MacroActionType.KeyPress)]
    [InlineData(MacroActionType.KeyRelease)]
    [InlineData(MacroActionType.TypeText)]
    public void MacroActionType_AllEnumValues_AreDefined(MacroActionType actionType)
    {
        Assert.True(Enum.IsDefined(typeof(MacroActionType), actionType));
    }

    [Fact]
    public void MacroAction_Serialization_RoundtripsAccurately()
    {
        var actions = new List<MacroAction>
        {
            new() { Type = MacroActionType.ClickLeft, X = 100, Y = 200, DelayMs = 50 },
            new() { Type = MacroActionType.KeyPress, Data = "ctrl", DelayMs = 120 },
            new() { Type = MacroActionType.TypeText, Data = "Tapster Test", DelayMs = 300 },
            new() { Type = MacroActionType.KeyRelease, Data = "ctrl", DelayMs = 80 }
        };

        var json = JsonSerializer.Serialize(actions);
        Assert.False(string.IsNullOrWhiteSpace(json));

        var deserialized = JsonSerializer.Deserialize<List<MacroAction>>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(actions.Count, deserialized.Count);

        for (int i = 0; i < actions.Count; i++)
        {
            Assert.Equal(actions[i].Type, deserialized[i].Type);
            Assert.Equal(actions[i].X, deserialized[i].X);
            Assert.Equal(actions[i].Y, deserialized[i].Y);
            Assert.Equal(actions[i].Data, deserialized[i].Data);
            Assert.Equal(actions[i].DelayMs, deserialized[i].DelayMs);
        }
    }
}
