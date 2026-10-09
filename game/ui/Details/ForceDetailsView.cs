using System.Globalization;
using San14.Core;
using San14.Data;

/// <summary>Binds current force state to the inspected original force detail layout.</summary>
public partial class ForceDetailsView : DetailsView
{
    internal void Configure(GameResourceRepository repository, GameState world,
        ScenarioCityPreview snapshot, int forceId, PreviewText text, Action close)
    {
        ForceState force = world.Forces[forceId];
        CharacterPreviewRecord? ruler = snapshot.Characters.FirstOrDefault(person =>
            person.CharacterId != 0 && person.CharacterId == force.RulerId);
        ConfigureLayout(repository, "res://ui/force-details.json", "ForceDetails", "detail_force_title",
            text, Values(world, forceId, text), ruler?.AppearanceCode,
            force.Name, close);
        SetMeta("force_id", forceId);
        SetMeta("doctrine_reference_known", force.DoctrineId is not null);
        SetMeta("doctrine_level_verified", false);
    }

    private static Dictionary<string, string> Values(GameState world, int forceId, PreviewText text)
    {
        ForceState force = world.Forces[forceId];
        CityState[] cities = world.Cities.Values.Where(city => city.ForceId == forceId).ToArray();
        int land = world.Territory.Values.Count(tile => tile.ForceId == forceId);
        int total = world.Territory.Values.Count(tile => tile.AreaId is not null);
        static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
        return new Dictionary<string, string>
        {
            ["ruler"] = force.Name,
            ["rank"] = force.RankId is int rankId
                ? world.ForceRanks[rankId].Name : text.Get("detail_unknown"),
            ["doctrine"] = force.DoctrineId is int doctrineId
                ? string.Format(CultureInfo.InvariantCulture, text.Get("detail_doctrine_value"),
                    world.Doctrines[doctrineId].Name, text.Get("detail_unknown"))
                : text.Get("detail_unknown"),
            ["districts"] = Number(world.Districts.Values.Count(district => district.ForceId == forceId)),
            ["cities"] = Number(cities.Length),
            ["officers"] = Number(world.Officers.Values.Count(officer => officer.ForceId == forceId &&
                (officer.Presence is null or OfficerPresence.Present) &&
                officer.Role is OfficerRole.Officer or OfficerRole.Ruler or OfficerRole.DistrictLeader or OfficerRole.Governor)),
            ["gold"] = Number(cities.Sum(city => (long)city.Gold)),
            ["supplies"] = Number(cities.Sum(city => (long)city.Supplies)),
            ["troops"] = Number(cities.Sum(city => (long)city.Troops) +
                world.Gates.Values.Where(gate => gate.ForceId == forceId).Sum(gate => (long)gate.Troops)),
            ["land"] = Number(land), ["totalLand"] = Number(total),
            ["landRate"] = total == 0 ? "0.0%" : (100.0 * land / total).ToString("F1", CultureInfo.InvariantCulture) + "%"
        };
    }

}
