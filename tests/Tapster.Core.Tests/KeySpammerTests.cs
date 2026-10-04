using Tapster;
using Xunit;

namespace Tapster.Core.Tests;

public class KeySpammerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void KeySpammer_SpamEmptyOrWhitespace_DoesNotThrow(string key)
    {
        var ex = Record.Exception(() => KeySpammer.Spam(key));
        Assert.Null(ex);
    }

    [Fact]
    public void KeySpammer_IsKeyDown_DoesNotThrow()
    {
        // Testing that calling IsKeyDown returns a boolean without throwing native interop exception
        bool state = KeySpammer.IsKeyDown(0x1B); // VK_ESCAPE
        Assert.IsType<bool>(state);
    }
}
