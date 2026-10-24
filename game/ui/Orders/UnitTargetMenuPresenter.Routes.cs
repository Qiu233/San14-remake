using San14.Data;

internal sealed partial class UnitTargetMenuPresenter
{
    private readonly Dictionary<UnitSelectionDraft.Group,(int Start,int Goal,DeploymentDraftRouteSegment Segment)> candidateRoutes=[];
    private bool routePublicationDirty=true;
    private IReadOnlyList<MapRoutePreviewPath> routePublication=[];

    private DeploymentDraftRouteSegment RouteTo(UnitSelectionDraft.Group group,int goal)
    {
        int start=group.Points[^1];
        if(candidateRoutes.TryGetValue(group,out var previous)&&previous.Start==start&&previous.Goal==goal)return previous.Segment;
        var segment=routeQuery.QuerySegment(group.First.UnitId,start,goal);
        candidateRoutes[group]=(start,goal,segment);return segment;
    }

    private void PublishRoutes()
    {
        if(routePublicationDirty)
        {
            var paths=new List<MapRoutePreviewPath>();
            // 7E4280 mode3 hides every saved display group with7E7230(0).
            // 7F6C90 clears that route when group+B0==0. Only the genuinely
            // rebuilt active groups are displayed here; fresh state0 uses bank0.
            foreach(var group in Selection.Groups)
            {
                if(group.Points.Count==0)continue;
                if(!transportSegments.TryGetValue(group,out var segments))transportSegments.Add(group,segments=[]);
                for(int index=segments.Count;index<group.Points.Count-1;index++)
                    segments.Add(routeQuery.QuerySegment(group.First.UnitId,group.Points[index],group.Points[index+1]));
                foreach(var segment in segments)paths.Add(Path(segment));
                if(previewHex is int goal)paths.Add(Path(RouteTo(group,goal)));
            }
            routePublication=paths.AsReadOnly();routePublicationDirty=false;
        }
        routePreviewChanged?.Invoke(routePublication);
    }

    private static MapRoutePreviewPath Path(DeploymentDraftRouteSegment route)=>
        new(route,route.Status,route.Path,route.StartSurfaceCode,route.EndSurfaceCode,0,0,
            route.PresentationUnresolvedInput??route.UnresolvedInput)
        {ControlStartHex=route.ControlStartHex,ControlGoalHex=route.ControlGoalHex,
            ControlStartSurfaceCode=route.ControlStartSurfaceCode,ControlGoalSurfaceCode=route.ControlGoalSurfaceCode};
}
