using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class MapSceneView
{
    private Func<GameState,ArmyDeparturePresentationRequest,ArmyDeparturePresentationWorld>? _departurePresentationQuery;
    private GameState? _departurePresentationOwner;
    private ArmyDeparturePresentationRequest? _departurePresentationRequest;
    private ArmyDeparturePresentationWorld? _departurePresentationSource;
    private Func<GameState,IEnumerable<int>,ArmyMapPersonalitiesPublication>? _armyPersonalityQuery;

    private bool OwnsPeriodArmyViewBorrow(CampaignPeriodPlayback? playback, GameState source) =>
        playback is not null && playback.Candidate is { } current &&
        (ReferenceEquals(_periodMapOwner, playback) || ReferenceEquals(_periodProgressFrameDriver, playback)) &&
        (ReferenceEquals(current, source) || ReferenceEquals(_periodBorrowedCandidate, source) || ReferenceEquals(_traitRefreshWorld, source));

    private void ReleasePeriodArmyViewSources()
    {
        _departurePresentationOwner = null; _departurePresentationRequest = null; _departurePresentationSource = null;
        _traitRefreshWorld = null; _traitRefreshArmy = null; _traitPersonalities = null; _traitPersonalityRefreshed.Clear();
        _d3d11Markers?.ReleaseBorrowedArmyLabelWorld();
    }

    internal void SetArmyDeparturePresentationQuery(
        Func<GameState,ArmyDeparturePresentationRequest,ArmyDeparturePresentationWorld> query,
        Func<GameState,IEnumerable<int>,ArmyMapPersonalitiesPublication> personalities)
        => (_departurePresentationQuery,_armyPersonalityQuery)=(query,personalities);

    internal bool TryPresentArmyDeparturePresentation(GameState owner,
        ArmyDeparturePresentationRequest request,out string? missing, CampaignPeriodPlayback? playback = null)
    {
        missing=null;
        if(!IsPlayableMapReady||!UseD3D11Map||_d3d11Army is null||_d3d11Markers is null||
            (playback is not null ? !OwnsPeriodArmyViewBorrow(playback, owner) : _periodMapActive) ||
            _d3d11Map is not {IsClosed:false,LastError:null})
        {missing="army-departure.map_owner";return false;}
        if(request.RebuildInfoLayout is not bool rebuild)
        {missing="army-departure.info_controller_presence";return false;}
        try
        {
            if(!ReferenceEquals(_departurePresentationOwner,owner)||!ReferenceEquals(_departurePresentationRequest,request))
            {
                var query=_departurePresentationQuery??throw new InvalidDataException("army-departure.source_query");
                _departurePresentationSource=query(owner,request);
                _departurePresentationOwner=owner;_departurePresentationRequest=request;
            }
            var source=_departurePresentationSource!;
            if(request.ReconcileModel)
            {
                if(source.GraphicsInputs is not {} graphics)
                {missing=source.GraphicsUnavailable??"army-departure.source_graphics";return false;}
                var publication=_d3d11Army.CaptureDeparture(source.World,request.ArmyAtSource.Id,graphics,source.FlagInputs, retainSourceWorld: playback is null);
                if(publication.Unavailable.FirstOrDefault(row=>row.UnitId==request.ArmyAtSource.Id) is {} unavailable)
                {missing=unavailable.Reason;return false;}
                if(!publication.UnitCenters.ContainsKey(request.ArmyAtSource.Id))
                {missing="army-departure.source_model";return false;}
            }
            if(rebuild&&!_d3d11Markers.RebuildDepartureInfo(source,request,out missing))return false;
            // Only presentation owners borrow the source projection. The live
            // campaign and its later position/receiver writers stay current.
            ScheduleD3D11MapFrame();
            return true;
        }
        catch(InvalidDataException error){missing=error.Message;return false;}
    }
}
