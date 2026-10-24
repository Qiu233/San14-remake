using San14.Data;

internal sealed partial class UnitTargetMenuPresenter
{
    // The active groups own their own source start and searched segments.
    // Both partitions retain their actual active segments. A changed candidate
    // searches only its tail; repeated pointer/camera input consumes the cache.
    private readonly Dictionary<UnitSelectionDraft.Group, List<DeploymentDraftRouteSegment>> transportSegments = [];

    internal TransportUnitSelection QueryTransportSelection()
        => rules.QueryTransportUnitSelection(world, Selection.UnitIds);

    private UnitsTargetMenu ReadTargetMenu(int hex)
    {
        if (SelectedPartition != UnitSelectionPartition.Transport)
            return rules.QueryUnitsTargetMenu(world, Selection.UnitIds, hex,
                new(Selection.Groups.Count > 0, Selection.FirstWaypointIndex));
        var first = Selection.Groups.FirstOrDefault();
        IReadOnlyList<int?> statuses = first is not null && transportSegments.TryGetValue(first, out var segments)
            ? segments.Select(segment => segment.Status==DeploymentDraftRouteStatus.Unresolved?null:(int?)segment.Path?.Count).ToArray() : [];
        var result = rules.QueryTransportUnitsTargetMenu(world, Selection.UnitIds, hex,
            new(first is not null, first is null ? null : Selection.FirstWaypointIndex, statuses));
        return new(Selection.UnitIds, result.ClickedHexId, result.TargetHexId, result.CanOpen,
            result.Kind, result.Relation, result.Options, result.UnresolvedInput);
    }

    private bool AppendWaypoint(int target)
    {
        if (Selection.Groups.Count == 0 || Selection.Groups.Any(group => group.Points.Count == 0) || Selection.FirstWaypointIndex >= 5) return false;
        foreach (var group in Selection.Groups)
        {
            var route=RouteTo(group,target);
            if (!transportSegments.TryGetValue(group, out var segments)) transportSegments.Add(group, segments = []);
            segments.Add(route);
        }
        bool appended=Selection.Append(target);
        if(appended){candidateRoutes.Clear();routePublicationDirty=true;}
        return appended;
    }
}
