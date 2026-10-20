namespace San14.Data;

public enum DeploymentDraftRouteStatus { Found, NoPath, Unresolved }
public sealed record DeploymentDraftMarchRow(int OfficerId, int? Days, string? UnresolvedInput);

/// <summary>One route group, one segment. Path includes both endpoints;
/// EtaPath omits the last endpoint by the source group concatenation rule.</summary>
public sealed record DeploymentDraftMarch(DeploymentDraftRouteStatus Status,
    IReadOnlyList<int>? Path, IReadOnlyList<int>? EtaPath,
    IReadOnlyList<DeploymentDraftMarchRow> Rows, int ExpandedNodes, string? UnresolvedInput);

/// <summary>353E80 semantic configuration of an unregistered ordinary draft.
/// Reset status1, action0, kindFF, control0, modifier0 and cache36 Skill0 are
/// consumed explicitly by the search and ETA policies, never by a fake Unit.</summary>
internal sealed record DeploymentDraftMarchInputs(int OfficerId, int CommanderPhysicalSlot,
    int ForcePhysicalSlot, int Troops, byte LandFormation, byte NavalFormation,
    int HomeHex, int Morale);
