using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The ordinary inspection city's ordered neighborhood and area
    /// security summaries. They belong to this borrowed current world, not to
    /// a forecast or an invented hostile-city predicate.</summary>
    internal (int Threat, int AverageAreaOrder) ReadOriginalAiInspectCityContextCurrent(
        GameState state, SourceWorldContext current, BaseReference city)
    {
        // The source maps omit invalid receivers. Their subsequent indexed
        // access supplies the actual integer-zero entry for those receivers.
        if (!InspectCityKnown(current.ReadBaseNativeValidity(city), "city_validity")) return (0, 0);
        int cityId = InspectCityKnown(city.PhysicalSlot, "city_identity");
        int forceId = InspectCityKnown(current.ReadBaseForceSlot(city), "city_Force");
        var force = current.ReadForceTableReference(forceId is >= 0 and < 52 ? forceId : 0)
            ?? throw InspectCityMissing("Force_lookup");
        if (!InspectCityKnown(current.ReadForceNativeValidity(force), "Force_validity")) return (0, 0);

        int threat = 0;
        var neighbors = current.CreateTemporaryHexList() ?? throw InspectCityMissing("neighbor_list");
        try
        {
            new PersonFreedomHomeSource(_scenario).FillCities22F430(current, city, neighbors);
            var node = current.ReadRegistryHead(new(neighbors.Identity)) ?? throw InspectCityMissing("neighbor_head");
            while (node.Identity.HasValue)
            {
                var value = current.ReadRegistryNodePayload(node) ?? throw InspectCityMissing("neighbor_payload");
                if (value.Kind != RegistryKind.City) throw InspectCityMissing("neighbor_kind");
                var otherCity = new BaseReference(BaseKind.City, value.PhysicalSlot);
                // These are breaks, not filters. In particular, a friendly
                // neighbor ends the scan rather than permitting later cities.
                if (!InspectCityKnown(current.ReadBaseNativeValidity(otherCity), "neighbor_validity") ||
                    InspectCityKnown(current.ReadConditionBaseSpecialGovernor(otherCity), "neighbor_special_governor")) break;
                int otherId = InspectCityKnown(current.ReadBaseForceSlot(otherCity), "neighbor_Force");
                var other = current.ReadForceTableReference(otherId is >= 0 and < 52 ? otherId : 0)
                    ?? throw InspectCityMissing("neighbor_Force_lookup");
                if (!InspectCityKnown(current.ReadForceNativeValidity(other), "neighbor_Force_validity") ||
                    InspectCityKnown(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, force, other), "protected_pair")) break;
                int relation = InspectCityKnown(current.ReadConditionForceFriendshipLevel(
                    new(InspectCityKnown(force.PhysicalSlot, "Force_identity")),
                    new(InspectCityKnown(other.PhysicalSlot, "neighbor_Force_identity"))), "friendship_level");
                // The last eligible neighbor replaces the previous score;
                // the original does not aggregate the maximum threat.
                threat = relation == 0 ? 2 : 1;
                node = current.ReadRegistryNodeNext(node) ?? throw InspectCityMissing("neighbor_next");
            }
        }
        finally
        {
            current.ClearTemporaryHexList(neighbors);
            current.ReleaseTemporaryHexList(neighbors);
        }

        int total = 0, count = 0;
        // CityAreaIterator follows the actual World Area registry, selecting
        // Area35, before the source's separate ownership/validity checks.
        foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw InspectCityMissing("Area_registry"))
        {
            var area = entry ?? throw InspectCityMissing("Area_pointer");
            if (cityId is < 1 or > 51 || InspectCityKnown(current.ReadAreaCitySlot(area), "Area_City35") != cityId) continue;
            if (InspectCityKnown(current.ReadAreaCurrentForceSlot(area), "Area_Force") != forceId ||
                !InspectCityKnown(current.ReadAreaNativeValidity(area), "Area_validity")) continue;
            total = unchecked(total + InspectCityKnown(current.ReadConditionAreaSecurity(area), "Area_security54"));
            count = unchecked(count + 1);
        }
        return (threat, total > 0 && count > 0 ? total / count : 0);
    }

    private static T InspectCityKnown<T>(T? value, string field) where T : struct =>
        value ?? throw InspectCityMissing(field);
    private static InvalidDataException InspectCityMissing(string field) =>
        new("original_ai.intelligence.inspect_city." + field);
}
