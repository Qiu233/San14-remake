using System.Collections.ObjectModel;
using System.Globalization;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

namespace San14.Client.Gameplay;

internal sealed record DetailContent(IReadOnlyDictionary<string, string> Values,
    int? PortraitAppearanceCode, string Caption);
internal sealed record CityDetailView(DetailContent Content, IReadOnlyList<string> UnavailableCapacities);
internal sealed record ForceDetailView(DetailContent Content, int ForceId, bool DoctrineReferenceKnown);
internal sealed record AbilityFigures(int Leadership, int Strength, int Intelligence, int Politics, int Charm)
{
    public int[] ToArray() => [Leadership, Strength, Intelligence, Politics, Charm];
    public static AbilityFigures From(OfficerAbilities value) =>
        new(value.Leadership, value.Strength, value.Intelligence, value.Politics, value.Charm);
}
internal sealed record OfficerDetailView(DetailContent Content, int OfficerId,
    AbilityFigures? BaseAbilities, int? RulerRankId, AbilityFigures? RulerRankBonus);

/// <summary>Projects one current publication into independent, localized display values.
/// Resource appearance lookup and current capacity queries finish before controls bind.</summary>
internal static class CampaignDetailViews
{
    public static CityDetailView City(GameState world, ScenarioCityPreview snapshot, int cityId,
        PreviewText text, CityCurrentCapacityFields capacity)
    {
        CityState city = world.Cities[cityId];
        int? portrait = Appearance(snapshot, city.GovernorId);
        string unknown = text.Get("detail_unknown");
        var values = new Dictionary<string, string>
        {
            ["city"] = city.Name,
            ["force"] = city.ForceId is int forceId ? world.Forces[forceId].Name : text.Neutral,
            ["governor"] = city.GovernorId is int governorId ? world.Officers[governorId].Name : unknown,
            ["district"] = city.DistrictId is int districtId ? text.DistrictNumber(world.Districts[districtId].Number) : unknown,
            ["gold"] = Number(city.Gold), ["supplies"] = Number(city.Supplies), ["troops"] = Number(city.Troops),
            ["wounded"] = city.NativeReturnRuntime?.Wounded40 is int wounded ? Number(wounded) : unknown,
            ["morale"] = city.Morale is int morale ? Number(morale) : unknown,
            ["durability"] = city.Durability is int durability ? Number(durability) : unknown,
            ["durabilityCapacity"] = capacity.Durability is int durabilityMax ? Number(durabilityMax) : unknown,
            ["moraleCapacity"] = capacity.Morale is int moraleMax ? Number(moraleMax) : unknown,
            ["troopCapacity"] = capacity.Troops is int troopMax ? Number(troopMax) : unknown,
            ["size"] = city.Size switch
            {
                CitySize.Small => text.Get("size_small"), CitySize.Medium => text.Get("size_medium"),
                CitySize.Large => text.Get("size_large"), null => unknown,
                CitySize.Giant => text.Get("size_giant"),
                _ => throw new InvalidDataException($"Current city size {(int)city.Size.Value} is invalid.")
            },
            // Includes all attached regions, even those currently neutral.
            ["regions"] = Number(city.AreaIds.Length),
            ["officers"] = Number(world.Officers.Values.Count(officer => officer.LocationId == city.Id &&
                (officer.Presence is null or OfficerPresence.Present) && officer.ForceId == city.ForceId &&
                officer.Role is OfficerRole.Officer or OfficerRole.Ruler or OfficerRole.DistrictLeader or OfficerRole.Governor)),
            ["freeOfficers"] = Number(world.Officers.Values.Count(officer =>
                (officer.Presence is null or OfficerPresence.Present) && officer.LocationId == city.Id && officer.Role == OfficerRole.Free))
        };
        return new(Content(values, portrait, city.Name), Array.AsReadOnly(capacity.Unavailable.ToArray()));
    }

    public static ForceDetailView Force(GameState world, ScenarioCityPreview snapshot, int forceId, PreviewText text)
    {
        ForceState force = world.Forces[forceId];
        ForceSummary figures = ForceSummary.Capture(world, forceId);
        int land = world.Territory.Values.Count(tile => tile.ForceId == forceId);
        int total = world.Territory.Values.Count(tile => tile.AreaId is not null);
        var values = new Dictionary<string, string>
        {
            ["ruler"] = force.Name,
            ["rank"] = force.RankId is int rankId ? world.ForceRanks[rankId].Name : text.Get("detail_unknown"),
            ["doctrine"] = force.DoctrineId is int doctrineId
                ? string.Format(CultureInfo.InvariantCulture, text.Get("detail_doctrine_value"),
                    world.Doctrines[doctrineId].Name, text.Get("detail_unknown")) : text.Get("detail_unknown"),
            ["districts"] = Number(figures.Districts), ["cities"] = Number(figures.Cities),
            ["officers"] = Number(figures.Officers), ["gold"] = Number(figures.Gold),
            ["supplies"] = Number(figures.Supplies), ["troops"] = Number(figures.Troops),
            ["land"] = Number(land), ["totalLand"] = Number(total),
            ["landRate"] = total == 0 ? "0.0%" : (100.0 * land / total).ToString("F1", CultureInfo.InvariantCulture) + "%"
        };
        return new(Content(values, Appearance(snapshot, force.RulerId), force.Name), forceId, force.DoctrineId is not null);
    }

    public static OfficerDetailView Officer(GameState world, ScenarioCityPreview snapshot, int officerId, PreviewText text)
    {
        OfficerState officer = world.Officers[officerId];
        string unknown = text.Get("detail_unknown");
        string location = officer.LocationId is int locationId ? world.Cities.GetValueOrDefault(locationId)?.Name ??
            world.Gates.GetValueOrDefault(locationId)?.Name ?? unknown : unknown;
        var values = new Dictionary<string, string>
        {
            ["name"] = officer.Name,
            ["sex"] = officer.Sex switch
            {
                OfficerSex.Male => text.Get("detail_sex_male"), OfficerSex.Female => text.Get("detail_sex_female"), _ => unknown
            },
            ["force"] = officer.ForceId is int forceId ? world.Forces[forceId].Name : unknown,
            ["district"] = officer.DistrictId is int districtId ? text.DistrictNumber(world.Districts[districtId].Number) : unknown,
            ["role"] = text.Get(officer.Presence switch
            {
                OfficerPresence.NotAppeared => "detail_presence_not_appeared",
                OfficerPresence.Undiscovered => "detail_presence_undiscovered", OfficerPresence.Dead => "detail_presence_dead",
                _ => officer.Role switch
                {
                    OfficerRole.Ruler => "detail_role_ruler", OfficerRole.DistrictLeader => "detail_role_district_leader",
                    OfficerRole.Governor => "detail_role_governor", OfficerRole.Officer => "detail_role_officer",
                    OfficerRole.Free => "detail_role_free", _ => "detail_unknown"
                }
            }),
            // Current placement does not establish the independent home affiliation.
            ["location"] = location
        };
        int? rulerRankId = null;
        AbilityFigures? rulerBonus = null;
        if (officer.ForceId is int id && world.Forces[id].RulerId == officer.Id &&
            world.Forces[id].RankId is int rankId && world.ForceRanks.TryGetValue(rankId, out var rank))
        {
            rulerRankId = rankId;
            rulerBonus = AbilityFigures.From(rank.AbilityBonus);
        }
        return new(Content(values, Appearance(snapshot, officerId), officer.Name), officer.Id,
            officer.BaseAbilities is { } abilities ? AbilityFigures.From(abilities) : null, rulerRankId, rulerBonus);
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static int? Appearance(ScenarioCityPreview snapshot, int? officerId) =>
        snapshot.Characters.FirstOrDefault(person => person.CharacterId != 0 && person.CharacterId == officerId)?.AppearanceCode;
    private static DetailContent Content(Dictionary<string, string> values, int? portrait, string caption) =>
        new(new ReadOnlyDictionary<string, string>(values), portrait, caption);
}
