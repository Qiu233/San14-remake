namespace San14.Data;

/// <summary>Ordinary kind0 selection. Null index means no active group only
/// when HasActiveRouteGroup=false; a missing index of an existing group is unknown.</summary>
public sealed record DeploymentEmptyHexSelection(IReadOnlyList<int> SelectedOfficerIds,
    bool HasActiveRouteGroup, int? FirstActiveWaypointIndex);

/// <summary>Only ordinary empty-Hex pointer and Move-menu qualification.
/// Application focus/modal/input handling remains with the UI owner.
/// Reachability and ETA are deliberately absent from this contract.</summary>
public sealed record DeploymentEmptyHexMove(int HexId, bool? IsEmpty, bool? PointerAllowed,
    bool? ShowMove, string? ClassificationUnresolvedInput, string? PointerUnresolvedInput,
    string? MenuUnresolvedInput);
