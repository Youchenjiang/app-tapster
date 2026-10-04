using Tapster;
using Xunit;

namespace Tapster.Core.Tests;

public class KeySpammerTests
{
    [Fact]
    public void KeySpammer_SpamEmptyOrWhitespace_DoesNotThrow()
    {
        KeySpammer.Spam("");
        KeySpammer.Spam("   ");
    }

    [Fact]
    public void KeySpammer_IsKeyDown_DoesNotThrow()
    {
        // Testing that calling IsKeyDown returns a boolean without throwing native interop exception
        bool state = KeySpammer.IsKeyDown(0x1B); // VK_ESCAPE
        Assert.IsType<bool>(state);
    }
}
