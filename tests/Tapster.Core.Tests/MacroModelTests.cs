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
    [InlineData(MacroActionType.MouseMove)]
    [InlineData(MacroActionType.KeyPress)]
    [InlineData(MacroActionType.KeyRelease)]
    [InlineData(MacroActionType.TypeText)]
    public void MacroActionType_AllEnumValues_AreDefined(MacroActionType actionType)
    {
        Assert.True(Enum.IsDefined(typeof(MacroActionType), actionType));
    }

    [Fact]
    public void MacroRecorder_StepManipulation_WorksAccurately()
    {
        var recorder = new MacroRecorder();
        var act1 = new MacroAction { Type = MacroActionType.MouseMove, X = 50, Y = 60, DelayMs = 10 };
        var act2 = new MacroAction { Type = MacroActionType.ClickLeft, X = 50, Y = 60, DelayMs = 20 };

        recorder.InsertAction(0, act1);
        recorder.InsertAction(1, act2);
        Assert.Equal(2, recorder.Actions.Count);
        Assert.Equal(MacroActionType.MouseMove, recorder.Actions[0].Type);

        var updatedAct = new MacroAction { Type = MacroActionType.MouseMove, X = 100, Y = 200, DelayMs = 15 };
        bool updateSuccess = recorder.UpdateAction(0, updatedAct);
        Assert.True(updateSuccess);
        Assert.Equal(100, recorder.Actions[0].X);
        Assert.Equal(200, recorder.Actions[0].Y);

        bool outOfBoundsUpdate = recorder.UpdateAction(99, updatedAct);
        Assert.False(outOfBoundsUpdate);

        bool removeSuccess = recorder.RemoveActionAt(0);
        Assert.True(removeSuccess);
        Assert.Single(recorder.Actions);
        Assert.Equal(MacroActionType.ClickLeft, recorder.Actions[0].Type);

        bool outOfBoundsRemove = recorder.RemoveActionAt(99);
        Assert.False(outOfBoundsRemove);
    }

    [Fact]
    public void MacroRecorder_IgnoreMouseMove_PropertyDefaultsToTrue()
    {
        var recorder = new MacroRecorder();
        Assert.True(recorder.IgnoreMouseMove);

        recorder.IgnoreMouseMove = false;
        Assert.False(recorder.IgnoreMouseMove);
    }

    [Fact]
    public void MacroAction_Serialization_RoundtripsAccurately()
    {
        var actions = new List<MacroAction>
        {
            new() { Type = MacroActionType.ClickLeft, X = 100, Y = 200, DelayMs = 50 },
            new() { Type = MacroActionType.MouseMove, X = 150, Y = 250, DelayMs = 10 },
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
