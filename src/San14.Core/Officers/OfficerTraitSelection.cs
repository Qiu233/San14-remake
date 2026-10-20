namespace San14.Core;

public sealed partial record OfficerState
{
    /// <summary>Current nine ordered trait references after fresh skill post-load
    /// and subsequent edits. Zero, duplicates and invalid source identities are
    /// retained; Data resolves their source-defined fallback. Null is unknown.</summary>
    public int[]? CurrentTraitIds { get; init; }
}
