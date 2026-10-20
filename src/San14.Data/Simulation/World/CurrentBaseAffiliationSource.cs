using San14.Core;

namespace San14.Data;

/// <summary>Current Base ownership, including an explicitly saved City
/// unaffiliated District byte. A nullable domain projection alone is unknown.</summary>
internal static class CurrentBaseAffiliationSource
{
    internal static int? ReadForce(GameState state, SourceWorldContext current, BaseReference target)
    {
        if (current.ReadBaseForceSlot(target) is int force) return force;
        if (target.Kind == BaseKind.City && target.PhysicalSlot is int physical &&
            state.Cities.TryGetValue(physical, out var saved) && saved.DistrictId is null &&
            state.NativeArmySimulation?.Parameters.Int32Values is { } values &&
            values.GetValueOrDefault(PersonGrowthCurrentQueries.VersionKey) == 1 &&
            values.TryGetValue(PersonGrowthCurrentQueries.CityDistrict26Key(physical), out int raw) &&
            raw is >= 0 and <= 255 && (raw == 0 || raw > 51))
        {
            current.SetCityDistrictSlot(physical, (byte)raw);
            return current.ReadBaseForceSlot(target);
        }
        return null;
    }
}
