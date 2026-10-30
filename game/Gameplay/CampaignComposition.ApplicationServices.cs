using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

public sealed partial class CampaignComposition
{
    private TransferServiceProfileStore? _applicationProfile;
    private CampaignEndingBindings? _endingBindings;

    public void ConfigureCampaignEndingBindings(CampaignEndingBindings? bindings)
    {
        _endingBindings = bindings;
        _army?.ConfigureCampaignEndingBindings(bindings);
    }
    private MainMapCameraTerrainSource? _baseCaptureTerrain;
    private bool _baseCaptureTerrainBound;
    private ArmyTraitSpeechViews? _armyTraitSpeechViews;
    private Func<ArmyReceiverId,FacilityArmyViewSnapshot?>? _facilityPeriodicArmyViews;
    private Func<ArmyReceiverId,ArmyFireViewSnapshot?>? _armyFireViews;

    internal void ConfigureArmyFireViews(Func<ArmyReceiverId,ArmyFireViewSnapshot?>? views)
    {
        _armyFireViews = views;
        _army?.ConfigureArmyFireViews(views);
    }

    internal void ConfigureFacilityPeriodicArmyViews(Func<ArmyReceiverId,FacilityArmyViewSnapshot?>? views)
    {
        _facilityPeriodicArmyViews=views;
        _army?.ConfigureFacilityPeriodicArmyViews(views);
    }

    internal void ConfigureArmyTraitSpeechViews(ArmyTraitSpeechViews? views)
    {
        _armyTraitSpeechViews = views;
        _army?.ConfigureArmyTraitSpeechView(views is null ? null : views.Read);
        _army?.ConfigureArmyDepartureController(views is null ? null : () => views.ControllerAvailable);
    }

    public void ConfigureBaseCapturePresentationTerrain(MainMapCameraTerrainSource? terrain)
    {
        _baseCaptureTerrain = terrain;
        _baseCaptureTerrainBound = true;
        _army?.ConfigureBaseCapturePresentationTerrain(terrain);
    }

    public void ConfigureApplicationProfile(TransferServiceProfileStore profile)
    {
        _applicationProfile = profile;
        _army?.ConfigureApplicationProfile(profile);
    }

    public void PublishApplicationServices(GameState adopted)
        => PrepareArmyQueries(adopted).PublishPendingApplicationServices(adopted);
}
