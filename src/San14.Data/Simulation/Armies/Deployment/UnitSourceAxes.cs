namespace San14.Data;

/// <summary>Absolute values in each named source axis. Presentation owns
/// the display order (mobility, field, siege troops, wall, defense).</summary>
public sealed record UnitSourceAxisValues(int? FieldAttack, int? SiegeTroopAttack,
    int? WallAttack, int? Mobility, int? Defense);

public sealed record UnitSourceAxes(int UnitId, bool? LandSelected, int? FormationId,
    byte? SurfaceCode, byte? PreviewSurfaceCode,
    UnitSourceAxisValues WithoutConditionalModifiers,
    UnitSourceAxisValues WithConditionalModifiers,
    UnitSourceAxisValues? SignedModifiers,
    IReadOnlyDictionary<string, string> Missing);
