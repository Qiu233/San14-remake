using San14.Core;

namespace San14.Client.Gameplay;

/// <summary>Independent current figures shared by force details and the map HUD.</summary>
internal sealed record ForceSummary(long Gold, long Supplies, long Troops,
    int Cities, int Officers, int Districts)
{
    public static ForceSummary Capture(GameState world, int forceId)
    {
        CityState[] cities = world.Cities.Values.Where(city => city.ForceId == forceId).ToArray();
        return new(cities.Sum(city => (long)city.Gold), cities.Sum(city => (long)city.Supplies),
            cities.Sum(city => (long)city.Troops) +
                world.Gates.Values.Where(gate => gate.ForceId == forceId).Sum(gate => (long)gate.Troops),
            cities.Length, world.Officers.Values.Count(officer => officer.ForceId == forceId &&
                (officer.Presence is null or OfficerPresence.Present) &&
                officer.Role is OfficerRole.Officer or OfficerRole.Ruler or OfficerRole.DistrictLeader or OfficerRole.Governor),
            world.Districts.Values.Count(district => district.ForceId == forceId));
    }
}
