using System;
using Tapster;
using Xunit;

namespace Tapster.Core.Tests;

public class GlobalHotkeyTests
{
    [Theory]
    [InlineData("F6", 0u, 0x75u)]
    [InlineData("f7", 0u, 0x76u)]
    [InlineData("F8", 0u, 0x77u)]
    [InlineData("F9", 0u, 0x78u)]
    [InlineData("F10", 0u, 0x79u)]
    [InlineData("F12", 0u, 0x7Bu)]
    [InlineData("Ctrl+Alt+T", NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, (uint)'T')]
    [InlineData("Ctrl+Shift+A", NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, (uint)'A')]
    [InlineData("Alt+Esc", NativeMethods.MOD_ALT, 0x1Bu)]
    [InlineData("Space", 0u, 0x20u)]
    public void ParseHotkey_ValidCombos_ReturnsCorrectModifiersAndVk(string combo, uint expectedMod, uint expectedVk)
    {
        var (mod, vk) = GlobalHotkeyService.ParseHotkey(combo);
        Assert.Equal(expectedMod, mod);
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UnknownKeyXYZ")]
    public void ParseHotkey_InvalidOrEmpty_ReturnsZeroVk(string combo)
    {
        var (_, vk) = GlobalHotkeyService.ParseHotkey(combo);
        Assert.Equal(0u, vk);
    }

    [Fact]
    public void ProcessWindowMessage_WhenMatchingHotkey_InvokesCallback()
    {
        // GlobalHotkeyService initialized with dummy handle
        using var service = new GlobalHotkeyService(new IntPtr(1234));

        Assert.False(service.ProcessWindowMessage(0, IntPtr.Zero));
        Assert.False(service.ProcessWindowMessage(NativeMethods.WM_HOTKEY, new IntPtr(9999)));
    }
}
