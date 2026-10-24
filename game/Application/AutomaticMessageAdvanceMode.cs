/// <summary>Application preference for ordinary EventView narration waits.</summary>
internal enum AutomaticMessageAdvanceMode
{
    Manual = 0,
    Slow = 1,
    Normal = 2,
    Fast = 3
}

internal static class AutomaticMessageAdvanceSource
{
    // The four little-endian TC source values were extracted offline. These
    // count active wait updates, not seconds; SC has no independently read table.
    internal static int? ReadThreshold(string sourceLocale, AutomaticMessageAdvanceMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (sourceLocale != "TC") return null;
        return mode switch
        {
            AutomaticMessageAdvanceMode.Manual => int.MaxValue,
            AutomaticMessageAdvanceMode.Slow => 60,
            AutomaticMessageAdvanceMode.Normal => 35,
            AutomaticMessageAdvanceMode.Fast => 15,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
