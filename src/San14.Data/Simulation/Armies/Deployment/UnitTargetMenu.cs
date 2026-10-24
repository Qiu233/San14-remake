using San14.Core;

namespace San14.Data;

public enum UnitTargetMenuKind { Empty, Army, Base, Facility, Obstacle, Trap }
public enum UnitTargetMenuRelation { Own, Friendly, Other }

/// <summary>Actual target editor state. The active index is not the count of
/// the core military plan's saved waypoints. Null means that index is unknown.</summary>
public sealed record UnitTargetRouteContext(bool HasActiveRouteGroup, int? FirstActiveWaypointIndex);

/// <summary>One original menu position. Included and Enabled retain independent
/// unknowns. Action is a command target, not a command result or a route preview.
/// Build requires a definition selection; waypoint is an editor operation.</summary>
public sealed record UnitTargetMenuOption(string ActionKey, int CaptionDataIndex, int TooltipMessageId,
    bool? Included, bool? Enabled, DeploymentAction? Action, string? UnresolvedInput)
{
    public int? WaypointOrdinal { get; init; }
    /// <summary>Only the initial occupation option substitutes its actual Area
    /// representative endpoint. The menu's normalized map target stays distinct.</summary>
    public int? GoalHexId { get; init; }
    /// <summary>Fire tooltip56A9's integer argument: maximum of selected
    /// per-person rates after individual clamp0..100. It is not execution RNG.</summary>
    public int? SuccessRate { get; init; }
    public string? PresentationUnresolvedInput { get; init; }
}

/// <summary>Menu content for one normal deployed army. A missing secondary
/// option does not discard already known options. No generic command-view
/// enabled flag, path search or command execution participates in this query.</summary>
public sealed record UnitTargetMenu(int UnitId, int ClickedHexId, int? TargetHexId,
    bool? CanOpen, UnitTargetMenuKind? Kind, UnitTargetMenuRelation? Relation,
    IReadOnlyList<UnitTargetMenuOption> Options, string? UnresolvedInput);

/// <summary>One common menu for an ordered selected set. This is produced by
/// one current reader, not an intersection of independent single-Army menus.
/// Route contains the actual first active group's index; UI owns all groups.</summary>
public sealed record UnitsTargetMenu(IReadOnlyList<int> UnitIds, int ClickedHexId, int? TargetHexId,
    bool? CanOpen, UnitTargetMenuKind? Kind, UnitTargetMenuRelation? Relation,
    IReadOnlyList<UnitTargetMenuOption> Options, string? UnresolvedInput);
