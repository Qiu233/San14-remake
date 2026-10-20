using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Null text is unavailable input; an empty name is the original invalid-reference result.</summary>
public sealed record CityGeographyValues(string? Region, string? Province);

public sealed partial class ArmyCampaignRuleAdapter
{
    public CityGeographyValues ReadPreviewCityGeography(int cityId)
    {
        var city = _scenario.Snapshot.Cities.FirstOrDefault(c => c.Slot == cityId && c.Slot != 0);
        return CityGeography(city?.GroupCode, physical => _growthQueries.Definitions.Catalog.States[physical].RawBytes[30]);
    }

    public CityGeographyValues ReadCurrentCityGeography(GameState state, int cityId)
    {
        if (!state.Cities.TryGetValue(cityId, out var city)) return new(null, null);
        // The initializer maps the resource City slot to this domain identity.
        // Verify its fixed representative rather than applying an arbitrary ID as a physical slot.
        var source = _scenario.Snapshot.Cities.FirstOrDefault(c => c.Slot == city.Id && c.Slot != 0 && c.HexId == city.HexId);
        return CityGeography(source is null ? null : _growthQueries.ReadCityState(state, source.Slot),
            physical => BaseCaptureRewardInputs.ReadStateRegion(state, physical));
    }

    private CityGeographyValues CityGeography(byte? rawState, Func<int, byte?> readRegion)
    {
        if (rawState is null) return new(null, null);
        if (rawState is 0 or >= 23) return new("", "");
        var definitions = _growthQueries.Definitions.Catalog;
        string province = S14ForceNameDefinitionReader.ReadLiteralUnits(definitions.States[rawState.Value].RawBytes.AsSpan(24, 6));
        byte? region = readRegion(rawState.Value);
        return new(region is null ? null : region is 0 or >= 12 ? "" : definitions.RegionNames[region.Value], province);
    }
}
