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
    //The independent TC/SC source tables contain the same four values.
    //They count active wait updates, not seconds, characters or voice duration.
    internal static bool SupportsLocale(string sourceLocale) => sourceLocale is "TC" or "SC";
    internal static int? ReadThreshold(string sourceLocale, AutomaticMessageAdvanceMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (!SupportsLocale(sourceLocale)) return null;
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
