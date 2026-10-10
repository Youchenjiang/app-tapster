namespace Tapster;

/// <summary>
/// Represents a single stage in a multi-stage key hold pipeline.
/// </summary>
public class KeyHolderStage
{
    /// <summary>
    /// The target key or combination to hold (e.g. "w", "shift+w", "space").
    /// </summary>
    public string KeyCombo { get; set; } = string.Empty;

    /// <summary>
    /// Duration in seconds to hold the key down (0 = infinite / continuous).
    /// </summary>
    public double HoldDurationSec { get; set; } = 5.0;

    /// <summary>
    /// Rest or pause duration in seconds after releasing the key before advancing to the next stage (0 = immediate).
    /// </summary>
    public double RestDurationSec { get; set; } = 0.5;

    public KeyHolderStage() { }

    public KeyHolderStage(string keyCombo, double holdDurationSec = 5.0, double restDurationSec = 0.5)
    {
        KeyCombo = keyCombo?.Trim() ?? string.Empty;
        HoldDurationSec = Math.Max(0, holdDurationSec);
        RestDurationSec = Math.Max(0, restDurationSec);
    }

    /// <summary>
    /// Creates a deep clone of the stage.
    /// </summary>
    public KeyHolderStage Clone() => new(KeyCombo, HoldDurationSec, RestDurationSec);

    public override string ToString() =>
        $"[{KeyCombo}] Hold: {HoldDurationSec:F1}s, Rest: {RestDurationSec:F1}s";
}
