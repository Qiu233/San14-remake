using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private FacilitySceneConnectionOwner? _facilityConnectionOwner;
    private uint _facilityConnectionGeneration;
    private FacilitySceneConnectionOwner? _periodOriginalFacilityConnections;
    private GameState? _facilityPreviewConnectionWorld;

    private void BeginPeriodFacilityConnections()
    {
        _periodOriginalFacilityConnections = _facilityConnectionOwner;
        _facilityConnectionOwner = _facilityConnectionOwner?.ForkPresentation();
    }

    private void EndPeriodFacilityConnections(bool adoptPresentation, GameState? displayedWorld)
    {
        if (!adoptPresentation) _facilityConnectionOwner = _periodOriginalFacilityConnections;
        _periodOriginalFacilityConnections = null;
        if (_facilityConnectionOwner is not null && displayedWorld is not null)
            _facilityConnections = _facilityConnectionOwner.Capture(displayedWorld);
    }

    private void ReleaseFacilitySceneConnections()
    {
        _facilityConnectionOwner = null;
        _periodOriginalFacilityConnections = null;
        _facilityPreviewConnectionWorld = null;
        _facilityConnections = Array.Empty<FacilitySceneConnection>();
    }

    private IReadOnlyList<FacilitySceneConnection> CaptureFacilitySceneConnections(GameState world)
    {
        if (IsNewGameScenarioPreview)
        {
            //The actual Startup role owns a read-only source roster. It has
            //not entered a campaign and cannot own construction/retirement
            //intentions or a CampaignEffectRequests cursor yet.
            if (world.Phase != TurnPhase.Initializing || world.NativeClock is not null || world.NativeArmySimulation is not null)
                throw new InvalidDataException("facility.presentation.preview_world_lifecycle");
            if (!ReferenceEquals(world, _facilityPreviewConnectionWorld) || _facilityConnectionGeneration != _sceneLoadGeneration)
            {
                _facilityConnections = (_initialFacilityConnectionsQuery ??
                    throw new InvalidDataException("facility.presentation.initial_connections_query"))(world);
                _facilityPreviewConnectionWorld = world;
                _facilityConnectionGeneration = _sceneLoadGeneration;
            }
            return _facilityConnections;
        }
        //AdoptStartedCampaign clears the Startup role before reaching here.
        //Its real new outbox and receiver lifetime establish the campaign
        //baseline; a preview roster is never a substitute for that owner.
        _facilityPreviewConnectionWorld = null;
        if (_facilityConnectionOwner is null || _facilityConnectionGeneration != _sceneLoadGeneration)
        {
            var initial = (_initialFacilityConnectionsQuery ??
                throw new InvalidDataException("facility.presentation.initial_connections_query"))(world);
            _facilityConnectionOwner = new(initial, world);
            _facilityConnectionGeneration = _sceneLoadGeneration;
        }
        return _facilityConnections = _facilityConnectionOwner.Capture(world);
    }
}
