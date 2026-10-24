namespace San14.Data;

public enum UnitSelectionPartition { Ordinary, Transport }

/// <summary>One drag-start candidate read in native Army registry order.
/// Completed means traversal reached its real end; unresolved rows may still
/// exist. Known rejected rows are omitted, unknown rows are retained.</summary>
public sealed record UnitSelectionCandidates(IReadOnlyList<UnitSelectionCandidate> Rows,
    bool Completed, string? UnresolvedInput);

/// <summary>UI owns canonical projection and inclusive rectangle hit testing.
/// CurrentHexLookupId is the source-clamped Army2A lookup; CurrentHexId is
/// its resolved physical identity. Neither is a guessed model position.</summary>
public sealed record UnitSelectionCandidate(int SourceOrdinal, int? UnitId,
    int? ArmyPhysicalSlot, int? CurrentHexLookupId, int? CurrentHexId,
    UnitSelectionPartition? Partition, bool? Included, string? UnresolvedInput)
{
    public byte? ActionCode { get; init; }
    public byte? TargetKind { get; init; }
    public ushort? TargetIdentity { get; init; }
    public int? DistrictPhysicalSlot { get; init; }
    public string? RowUnresolvedInput { get; init; }
}

public sealed record UnitSelectionRows(IReadOnlyList<UnitSelectionRow> Rows, string? UnresolvedInput);

/// <summary>Actual existing Army UI row fields, without Box-only filters.
/// ClickSelectionAllowed covers the native Army/District/tutorial leaf;
/// the caller owns gesture, hit, scene readiness and modal input gates.</summary>
public sealed record UnitSelectionRow(int UnitId, int? ArmyPhysicalSlot,
    int? CurrentHexLookupId, int? CurrentHexId, UnitSelectionPartition? Partition,
    byte? ActionCode, byte? TargetKind, ushort? TargetIdentity,
    int? DistrictPhysicalSlot, bool? NativeValid, bool? ClickSelectionAllowed,
    string? UnresolvedInput)
{
    public string? ClickSelectionUnresolvedInput { get; init; }
}
