using System.Globalization;
using San14.Core;
using San14.Data;

/// <summary>Binds city state, including its governor identity, to the original city layout.</summary>
public partial class CityDetailsView : DetailsView
{
    internal void Configure(GameResourceRepository repository, GameState world,
        ScenarioCityPreview snapshot, int cityId, PreviewText text, Action close,
        SourceWorldContext? current)
    {
        CityState city = world.Cities[cityId];
        CharacterPreviewRecord? governor = snapshot.Characters.FirstOrDefault(person =>
            person.CharacterId != 0 && person.CharacterId == city.GovernorId);
        CityCurrentCapacityFields capacity = CityCurrentCapacityFields.Capture(cityId, current);
        ConfigureLayout(repository, "res://ui/city-details.json", "CityDetails", "detail_city_title",
            text, Values(world, city, capacity, text),
            governor?.AppearanceCode, city.Name, close);
        SetMeta("current_city_capacity_unavailable", capacity.Unavailable.ToArray());
    }

    private static Dictionary<string, string> Values(GameState world, CityState city,
        CityCurrentCapacityFields capacity, PreviewText text)
    {
        string unknown = text.Get("detail_unknown");
        static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        return new Dictionary<string, string>
        {
            ["city"] = city.Name,
            ["force"] = city.ForceId is int forceId ? world.Forces[forceId].Name : text.Neutral,
            ["governor"] = city.GovernorId is int governorId ? world.Officers[governorId].Name : unknown,
            ["district"] = city.DistrictId is int districtId
                ? text.DistrictNumber(world.Districts[districtId].Number) : unknown,
            ["gold"] = Number(city.Gold), ["supplies"] = Number(city.Supplies),
            ["troops"] = Number(city.Troops),
            ["wounded"] = city.NativeReturnRuntime?.Wounded40 is int wounded ? Number(wounded) : unknown,
            ["morale"] = city.Morale is int morale ? Number(morale) : unknown,
            ["durability"] = city.Durability is int durability ? Number(durability) : unknown,
            ["durabilityCapacity"] = capacity.Durability is int durabilityMax
                ? Number(durabilityMax) : unknown,
            ["moraleCapacity"] = capacity.Morale is int moraleMax
                ? Number(moraleMax) : unknown,
            ["troopCapacity"] = capacity.Troops is int troopMax
                ? Number(troopMax) : unknown,
            ["size"] = city.Size switch
            {
                CitySize.Small => text.Get("size_small"), CitySize.Medium => text.Get("size_medium"),
                CitySize.Large => text.Get("size_large"), null => unknown,
                CitySize.Giant => text.Get("size_giant"),
                _ => throw new InvalidDataException($"Current city size {(int)city.Size.Value} is invalid.")
            },
            // The city detail counts all attached regions, including neutral ones.
            // Pingyuan owns five of its six regions in CE scenario 1.
            ["regions"] = Number(city.AreaIds.Length),
            ["officers"] = Number(world.Officers.Values.Count(officer => officer.LocationId == city.Id &&
                (officer.Presence is null or OfficerPresence.Present) &&
                officer.ForceId == city.ForceId && officer.Role is OfficerRole.Officer or
                    OfficerRole.Ruler or OfficerRole.DistrictLeader or OfficerRole.Governor)),
            ["freeOfficers"] = Number(world.Officers.Values.Count(officer =>
                (officer.Presence is null or OfficerPresence.Present) &&
                officer.LocationId == city.Id && officer.Role == OfficerRole.Free))
        };
    }
}
