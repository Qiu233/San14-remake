namespace San14.Core;

/// <summary>The placeholder area's three current development stocks, without
/// creating a normal Area identity. Null ownership preserves unknown legacy values.</summary>
public sealed record PlaceholderAreaDevelopmentState(ushort Commerce, ushort Agriculture, ushort Barracks);

public sealed partial record GameState
{
    public PlaceholderAreaDevelopmentState? PlaceholderAreaDevelopment { get; init; }
    /// <summary>The placeholder Force's current administration flags. A null
    /// value preserves unknown legacy knowledge without adding a normal Force.</summary>
    public byte? PlaceholderForceAdministrationFlags { get; init; }
}
