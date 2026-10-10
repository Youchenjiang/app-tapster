using Tapster;
using Xunit;

namespace Tapster.Core.Tests;

public class KeyboardTests
{
    [Theory]
    [InlineData(0x41, "a")]
    [InlineData(0x42, "b")]
    [InlineData(0x4D, "m")]
    [InlineData(0x59, "y")]
    [InlineData(0x5A, "z")]
    public void GetKeyName_AlphabetKeys_ReturnsLowerCaseLetter(int vk, string expected)
    {
        var result = Keyboard.GetKeyName(vk);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0x30, "0")]
    [InlineData(0x31, "1")]
    [InlineData(0x35, "5")]
    [InlineData(0x39, "9")]
    public void GetKeyName_DigitKeys_ReturnsDigitString(int vk, string expected)
    {
        var result = Keyboard.GetKeyName(vk);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0x70, "f1")]
    [InlineData(0x71, "f2")]
    [InlineData(0x75, "f6")]
    [InlineData(0x7A, "f11")]
    [InlineData(0x7B, "f12")]
    public void GetKeyName_FunctionKeys_ReturnsFNumberedString(int vk, string expected)
    {
        var result = Keyboard.GetKeyName(vk);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0x10, "shift")]
    [InlineData(0xA0, "shift")]
    [InlineData(0xA1, "shift")]
    [InlineData(0x11, "ctrl")]
    [InlineData(0xA2, "ctrl")]
    [InlineData(0xA3, "ctrl")]
    [InlineData(0x12, "alt")]
    [InlineData(0xA4, "alt")]
    [InlineData(0xA5, "alt")]
    [InlineData(0x5B, "win")]
    [InlineData(0x5C, "win")]
    public void GetKeyName_ModifierKeys_ReturnsCanonicalName(int vk, string expected)
    {
        var result = Keyboard.GetKeyName(vk);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0x0D, "enter")]
    [InlineData(0x20, "space")]
    [InlineData(0x09, "tab")]
    [InlineData(0x1B, "esc")]
    [InlineData(0x08, "backspace")]
    [InlineData(0x2E, "delete")]
    [InlineData(0x2D, "insert")]
    [InlineData(0x24, "home")]
    [InlineData(0x23, "end")]
    [InlineData(0x21, "pageup")]
    [InlineData(0x22, "pagedown")]
    [InlineData(0x2C, "printscreen")]
    [InlineData(0x13, "pause")]
    [InlineData(0x90, "numlock")]
    [InlineData(0x91, "scrolllock")]
    [InlineData(0x14, "capslock")]
    [InlineData(0x26, "up")]
    [InlineData(0x28, "down")]
    [InlineData(0x25, "left")]
    [InlineData(0x27, "right")]
    public void GetKeyName_NavigationAndControlKeys_ReturnsFriendlyName(int vk, string expected)
    {
        var result = Keyboard.GetKeyName(vk);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0xC0, "`")]
    [InlineData(0xBD, "-")]
    [InlineData(0xBB, "=")]
    [InlineData(0xDB, "[")]
    [InlineData(0xDD, "]")]
    [InlineData(0xDC, "\\")]
    [InlineData(0xBA, ";")]
    [InlineData(0xDE, "'")]
    [InlineData(0xBC, ",")]
    [InlineData(0xBE, ".")]
    [InlineData(0xBF, "/")]
    public void GetKeyName_PunctuationKeys_ReturnsPunctuationSymbol(int vk, string expected)
    {
        var result = Keyboard.GetKeyName(vk);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0x01, "vk_1")]
    [InlineData(0xFF, "vk_255")]
    public void GetKeyName_UnmappedVirtualKey_ReturnsPrefixedVkFormat(int vk, string expected)
    {
        var result = Keyboard.GetKeyName(vk);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Keyboard_Tap_DoesNotThrow()
    {
        var exception = Record.Exception(() => Keyboard.Tap("enter"));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("insert")]
    [InlineData("home")]
    [InlineData("end")]
    [InlineData("pageup")]
    [InlineData("pagedown")]
    [InlineData(".")]
    [InlineData("-")]
    [InlineData("=")]
    [InlineData("/")]
    [InlineData("vk_65")]
    public void Keyboard_Tap_SpecialKeysAndSymbols_DoesNotThrow(string key)
    {
        var exception = Record.Exception(() => Keyboard.Tap(key));
        Assert.Null(exception);
    }

    [Fact]
    public void Keyboard_ResetEscState_DoesNotThrow()
    {
        var exception = Record.Exception(() => Keyboard.ResetEscState());
        Assert.Null(exception);
    }

    [Fact]
    public void Keyboard_Paste_DoesNotThrow()
    {
        var exception = Record.Exception(() => Keyboard.Paste());
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("numlock")]
    [InlineData("numlk")]
    [InlineData("scrolllock")]
    [InlineData("scrlk")]
    [InlineData("printscreen")]
    [InlineData("prtsc")]
    [InlineData("prtscr")]
    [InlineData("pause")]
    [InlineData("del")]
    [InlineData("ins")]
    [InlineData("pgup")]
    [InlineData("pgdn")]
    public void Keyboard_Tap_NavigationAndLockAliases_DoesNotThrow(string key)
    {
        var exception = Record.Exception(() => Keyboard.Tap(key));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("numpad0")]
    [InlineData("num0")]
    [InlineData("numpad1")]
    [InlineData("num1")]
    [InlineData("numpad5")]
    [InlineData("num5")]
    [InlineData("numpad9")]
    [InlineData("num9")]
    [InlineData("multiply")]
    [InlineData("num*")]
    [InlineData("add")]
    [InlineData("num+")]
    [InlineData("subtract")]
    [InlineData("num-")]
    [InlineData("decimal")]
    [InlineData("num.")]
    [InlineData("divide")]
    [InlineData("num/")]
    [InlineData("numpadenter")]
    [InlineData("numenter")]
    public void Keyboard_Tap_NumpadKeysAndAliases_DoesNotThrow(string key)
    {
        var exception = Record.Exception(() => Keyboard.Tap(key));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0x60, "num0")]
    [InlineData(0x61, "num1")]
    [InlineData(0x65, "num5")]
    [InlineData(0x69, "num9")]
    [InlineData(0x6A, "multiply")]
    [InlineData(0x6B, "add")]
    [InlineData(0x6D, "subtract")]
    [InlineData(0x6E, "decimal")]
    [InlineData(0x6F, "divide")]
    public void GetKeyName_NumpadKeys_ReturnsNumpadFriendlyName(int vk, string expected)
    {
        var result = Keyboard.GetKeyName(vk);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseKeys_NumpadPlus_MapsToVkAdd()
    {
        var strokes = Keyboard.ParseKeys("num+");
        Assert.Single(strokes);
        Assert.Equal(0x6B, strokes[0].Vk);
        Assert.False(strokes[0].Extended);
    }

    [Fact]
    public void ParseKeys_CtrlAndNumpadPlus_MapsToCtrlAndAdd()
    {
        var strokes = Keyboard.ParseKeys("ctrl+num+");
        Assert.Equal(2, strokes.Length);
        Assert.Equal(0x11, strokes[0].Vk);
        Assert.Equal(0x6B, strokes[1].Vk);
    }

    [Fact]
    public void ParseKeys_PlusKey_MapsToPlusVk()
    {
        var strokes = Keyboard.ParseKeys("+");
        Assert.Single(strokes);
        Assert.Equal(0xBB, strokes[0].Vk);
    }

    [Fact]
    public void ParseKeys_CtrlAndPlus_MapsToCtrlAndPlusVk()
    {
        var strokes = Keyboard.ParseKeys("ctrl++");
        Assert.Equal(2, strokes.Length);
        Assert.Equal(0x11, strokes[0].Vk);
        Assert.Equal(0xBB, strokes[1].Vk);
    }

    [Fact]
    public void ParseKeys_NumpadEnter_HasExtendedFlag()
    {
        var strokes = Keyboard.ParseKeys("numpadenter");
        Assert.Single(strokes);
        Assert.Equal(0x0D, strokes[0].Vk);
        Assert.True(strokes[0].Extended);
    }

    [Fact]
    public void ParseKeys_NumEnterAlias_HasExtendedFlag()
    {
        var strokes = Keyboard.ParseKeys("numenter");
        Assert.Single(strokes);
        Assert.Equal(0x0D, strokes[0].Vk);
        Assert.True(strokes[0].Extended);
    }

    [Fact]
    public void ParseKeys_StandardEnter_DoesNotHaveExtendedFlag()
    {
        var strokes = Keyboard.ParseKeys("enter");
        Assert.Single(strokes);
        Assert.Equal(0x0D, strokes[0].Vk);
        Assert.False(strokes[0].Extended);
    }
}
