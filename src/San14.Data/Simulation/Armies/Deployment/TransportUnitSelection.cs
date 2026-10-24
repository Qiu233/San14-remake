namespace San14.Data;

/// <summary>One actual type2 receiver. Null properties are unknown, not
/// default options or a new-transport Make packet. CurrentHexId is the real
/// physical Hex; CurrentHexLookupId preserves the clamped Army2A table key.</summary>
public sealed record TransportUnitRow(int UnitId, int? ArmyPhysicalSlot,
    int? OfficerId, bool? NativeValid, byte? StatusCode,
    int? CurrentHexLookupId, int? CurrentHexId, int? Troops, int? Gold, int? Supplies,
    byte? LandFormationId, byte? NavalFormationId, byte? ActionCode,
    byte? TargetKind, ushort? TargetIdentity, bool? ReturnOfficerToSource,
    int? SourceBehaviorChoice, DeploymentTargetCard? Card,
    IReadOnlyList<string> UnresolvedInputs)
{
    public bool? DetailsAvailable { get; init; }
    public string? DetailsUnresolvedInput { get; init; }
    public bool? WithdrawalAvailable { get; init; }
    public string? WithdrawalUnresolvedInput { get; init; }
}

/// <summary>Mixed is known only when all selected values are known. A known
/// mixed set has Value=null/Mixed=true; a missing input has Mixed=null.</summary>
public sealed record TransportSelectedOption<T>(T? Value, bool? Mixed, string? UnresolvedInput) where T : struct;

public sealed record TransportUnitSelection(IReadOnlyList<TransportUnitRow> Rows,
    bool? CanEdit, TransportSelectedOption<bool> ReturnOfficerToSource,
    TransportSelectedOption<int> SourceBehaviorChoice, string? UnresolvedInput);
