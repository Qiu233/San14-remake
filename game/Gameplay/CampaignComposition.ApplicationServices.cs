using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

public sealed partial class CampaignComposition
{
    private TransferServiceProfileStore? _applicationProfile;
    private MainMapCameraTerrainSource? _baseCaptureTerrain;
    private bool _baseCaptureTerrainBound;

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
