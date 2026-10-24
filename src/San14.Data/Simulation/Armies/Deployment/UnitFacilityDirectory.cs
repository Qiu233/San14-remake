namespace San14.Data;

/// <summary>A definition directory, in native lookup order. Unknown rows are
/// retained; an unresolved directory must not be interpreted as an empty one.</summary>
public sealed record UnitFacilityDirectory(IReadOnlyList<UnitFacilityDirectoryRow> Rows,
    string? UnresolvedInput, string? ColumnLayoutUnresolvedInput,
    string? SortingUnresolvedInput);

public sealed record UnitFacilityDirectoryRow(int DefinitionLookupId,
    string DefinitionKey, int? DefinitionPhysicalSlot, string? Name, byte? Category,
    bool? OriginalIncluded, bool? OriginalAllowed, string? OriginalReason,
    string? OriginalUnresolvedInput, bool CommandSupported)
{
    // These are the definition's base inputs, not a current construction quote
    // and not a claim about the original list's missing column formatters.
    public ushort? BaseDurability { get; init; }
    public ushort? BaseGoldCost { get; init; }
    public ushort? RequiredTrait { get; init; }
    public byte? RequiredPolicyEffect { get; init; }
    public byte? RequiredPolicyLevel { get; init; }
    public byte? ExclusionRadius { get; init; }
    public uint? TerrainMask { get; init; }
    public string? DefinitionUnresolvedInput { get; init; }
}
