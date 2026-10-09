using San14.Core;

namespace San14.Data;

public sealed record NormalArmyCombatGenerationOrderResult(int[]? OrderedNativeArmySlots,
    string? UnresolvedOwner);

/// <summary>SC16FFA0/TC170240 traverse World60's real current registry chain.
/// This reads its saved owner without importing/reconstructing the primary
/// arena or ordering gameplay Unit IDs. Pending receivers stay in that source
/// chain until the actual cleanup removes them; caller applies source gates.</summary>
public static class NormalArmyCombatGenerationOrder
{
    public static NormalArmyCombatGenerationOrderResult ReadArmyManager60(GameState state)
    {
        if (state.NativeArmyLifecycle is not { Slots.Length: 501 } arena)
            return Unknown("current.Native_Army_lifecycle");
        if (arena.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Army) is not { } order)
            return Unknown("current.Army_registry");
        var slots = new List<int>(order.Count);
        foreach (var entity in order)
        {
            if (entity.Kind != WorldEntityKind.Army) return Unknown("current.Army_registry_entity_kind");
            slots.Add(WorldIndexContext.FindArmyPhysical(state, entity.Id) ?? 0);
        }
        return new(slots.ToArray(), null);
    }

    private static NormalArmyCombatGenerationOrderResult Unknown(string field) => new(null, field);
}
