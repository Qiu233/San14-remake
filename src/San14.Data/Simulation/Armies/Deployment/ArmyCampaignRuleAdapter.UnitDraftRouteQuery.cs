using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public UnitDraftRouteQuery CreateUnitDraftRouteQuery(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var current=new Lazy<SourceWorldContext>(()=>CaptureDeploymentDraftMarchCurrent(state));
        var ordinary=new Dictionary<int,UnitRoutePreviewQuery>();
        return new((unit,start,goal)=>
        {
            try
            {
                DeploymentDraftRouteSegment segment;
                if(state.Units.TryGetValue(unit,out var army)&&army.Mission?.Kind==UnitMissionKind.Transport)
                {
                    var route=ReadTransportUnitDraftMarch(state,unit,start,goal,()=>current.Value);
                    segment=new(route.Status,route.Path,null,route.ExpandedNodes,route.UnresolvedInput);
                }
                else
                {
                    if(!ordinary.TryGetValue(unit,out var query))
                        ordinary.Add(unit,query=PrepareUnitRoutePreview(state,unit,()=>current.Value));
                    var route=query.QuerySegment(start,goal);
                    segment=new(route.Status,route.Path,null,route.ExpandedNodes,route.UnresolvedInput);
                }
                return segment.Status==DeploymentDraftRouteStatus.Unresolved
                    ?segment with{ControlStartHex=start,ControlGoalHex=goal}
                    :AttachUnitRoutePresentation(segment,current.Value,start,goal);
            }
            catch(InvalidDataException error){return new(DeploymentDraftRouteStatus.Unresolved,null,null,0,error.Message);}
            catch(ArmyRouteSearch.MissingOwner error){return new(DeploymentDraftRouteStatus.Unresolved,null,null,0,error.Message);}
        });
    }

    public TransportDraftRouteQuery CreateTransportDraftRouteQuery(GameState state,int sourceCityId,int officerId,int troops,int navalFormationId)
    {
        var current=new Lazy<SourceWorldContext>(()=>CaptureDeploymentDraftMarchCurrent(state));
        return new((start,goal)=>
        {
            var route=ReadTransportDraftMarch(state,sourceCityId,officerId,troops,navalFormationId,start,goal,()=>current.Value);
            if(route.Status!=DeploymentDraftRouteStatus.Found)return route;
            var details=AttachUnitRoutePresentation(new(route.Status,route.Path,null,route.ExpandedNodes,route.UnresolvedInput),current.Value,start,goal);
            return route with{StartSurfaceCode=details.StartSurfaceCode,EndSurfaceCode=details.EndSurfaceCode,
                ControlStartHex=details.ControlStartHex,ControlGoalHex=details.ControlGoalHex,
                ControlStartSurfaceCode=details.ControlStartSurfaceCode,ControlGoalSurfaceCode=details.ControlGoalSurfaceCode,
                PresentationUnresolvedInput=details.PresentationUnresolvedInput};
        });
    }

    private static DeploymentDraftRouteSegment AttachUnitRoutePresentation(DeploymentDraftRouteSegment segment,SourceWorldContext current,int start,int goal)
    {
        segment=segment with{ControlStartHex=start,ControlGoalHex=goal};
        if(segment.Path is not {Count:>0} path)return segment;
        int? first=Surface(path[0]),last=Surface(path[^1]);
        int? controlFirst=path[0]==start?first:Surface(start),controlLast=path[^1]==goal?last:Surface(goal);
        return segment with{StartSurfaceCode=first,EndSurfaceCode=last,ControlStartSurfaceCode=controlFirst,ControlGoalSurfaceCode=controlLast};

        int? Surface(int raw)
        {
            try
            {
                if(raw is <0 or >=48400)
                    throw new InvalidDataException("unit.route_preview.presentation_hex");
                var hex=current.ReadHexTableReference(raw);
                if(hex?.PhysicalSlot is not null&&current.ReadHexSurfaceCode(hex) is byte value)return value;
                segment=segment with{PresentationUnresolvedInput=segment.PresentationUnresolvedInput??"unit.route_preview.presentation_Surface10"};
            }
            catch(InvalidDataException error)
            {segment=segment with{PresentationUnresolvedInput=segment.PresentationUnresolvedInput??error.Message};}
            return null;
        }
    }
}
