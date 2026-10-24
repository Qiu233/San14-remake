namespace San14.Data;

/// <summary>268690's unbounded signed result and the menu's separate
/// clamp0..100 projection for one actual selected commander.</summary>
public sealed record UnitFireProbabilityRow(int UnitId, int? Rate,
    int? DisplayRate, string? UnresolvedInput);

/// <summary>7C70D0 displays the maximum of the individually clamped selected
/// rates. Missing inputs remain unknown; they are not replaced by zero.</summary>
public sealed record UnitFireProbability(int TargetHexId,
    IReadOnlyList<UnitFireProbabilityRow> Rows, int? DisplayRate,
    string? UnresolvedInput);
