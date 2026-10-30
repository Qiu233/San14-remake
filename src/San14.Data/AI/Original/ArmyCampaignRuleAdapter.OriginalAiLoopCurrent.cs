using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Current normal City iterator predicate for mode0/resolver8.
    /// Agent scope is explicit. Null means this source table element does not
    /// match; unknown source fields fail instead of becoming empty candidates.</summary>
    internal OriginalAiEntity? ReadOriginalAiActorCityLoopCurrent(SourceWorldContext current,
        OriginalAiActor actor, int tableSlot)
    {
        if ((uint)tableSlot >= 52) throw new ArgumentOutOfRangeException(nameof(tableSlot));
        int actorPhysical;
        switch (actor.AgentKind)
        {
            case OriginalAiAgentKind.District:
                int district = actor.DistrictId ?? throw OriginalAiCityLoopMissing("actor_district_input");
                actorPhysical = current.ReadDistrictTableReference(district is >= 0 and <= 51 ? district : 0)?.PhysicalSlot
                    ?? throw OriginalAiCityLoopMissing("actor_district_pointer");
                break;
            case OriginalAiAgentKind.Force:
                actorPhysical = current.ReadForceTableReference(actor.ForceId is >= 0 and <= 51 ? actor.ForceId : 0)?.PhysicalSlot
                    ?? throw OriginalAiCityLoopMissing("actor_force_pointer");
                break;
            case null:
                throw OriginalAiCityLoopMissing("agent_kind_input");
            default:
                throw OriginalAiCityLoopMissing("agent_kind_" + actor.AgentKind);
        }
        // Both native filtered iterators reject actor physical slots outside
        // 1..51 before reading any field of the pointed-to city.
        if (actorPhysical is < 1 or > 51) return null;
        var city = current.ReadCityTableReference(tableSlot)
            ?? throw OriginalAiCityLoopMissing("city_table_pointer_" + tableSlot);
        int physical = city.PhysicalSlot ?? throw OriginalAiCityLoopMissing("city_null_pointer_" + tableSlot);
        int selected = (actor.AgentKind == OriginalAiAgentKind.District
            ? current.ReadBaseDistrictSlot(city)
            : current.ReadBaseForceSlot(city))
            ?? throw OriginalAiCityLoopMissing("city_scope_field_" + tableSlot);
        // No additional validity, population, facilities or gate predicate is
        // present in either original iterator.
        return selected == actorPhysical ? new(WorldEntityKind.City, physical) : null;
    }

    private static InvalidDataException OriginalAiCityLoopMissing(string field) =>
        new("original_ai.city_loop." + field);
}
