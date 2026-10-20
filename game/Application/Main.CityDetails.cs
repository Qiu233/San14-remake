using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private DetailMiniMapValues? CurrentCityMiniMap(int cityId) => _world is null || _campaignComposition is null
        ? null : _campaignComposition.PrepareArmyQueries(_world).ReadCityDetailMiniMap(_world, cityId);

    private DetailForceAppearance? CurrentCityForce(int cityId)
    {
        return _world is null || _campaignComposition is null || !_world.Cities.ContainsKey(cityId)
            ? null : _campaignComposition.CityDetailForce(_world, cityId);
    }

    private CityCurrentCapacityFields CurrentCityCapacity(int cityId) => CityCurrentCapacityFields.Capture(cityId,
        _session?.PlayerForceId is not null ? _ownedArmyCampaign?.CaptureCurrentDefinitions(_world!) : null);

    private CityGeographyValues? CurrentCityGeography(int cityId)
    {
        if (_world is null || _campaignComposition is null) return null;
        var queries = _campaignComposition.PrepareArmyQueries(_world);
        return _session?.PlayerForceId is null ? queries.ReadPreviewCityGeography(cityId)
            : queries.ReadCurrentCityGeography(_world, cityId);
    }
}
