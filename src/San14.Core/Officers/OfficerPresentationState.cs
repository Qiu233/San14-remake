namespace San14.Core;

public sealed partial record OfficerState
{
    /// <summary>The current two source name keys concatenated for ordinal
    /// UTF-16 comparison. Null is unknown; an empty key remains known.</summary>
    public string? CurrentNameSortKey { get; init; }

    /// <summary>The first current source name-sort text, independent of the
    /// display surname and of the combined key. Null is unknown; empty is known.</summary>
    public string? CurrentSurnameSortKey { get; init; }

    /// <summary>The second current source name-sort text. Knowledge of this
    /// component is independent of the first component and combined key.</summary>
    public string? CurrentGivenNameSortKey { get; init; }

    /// <summary>Current portrait identity, independent of the officer ID and
    /// of the display resource's fallback/variant selection.</summary>
    public ushort? PortraitAppearanceIndex { get; init; }
}
