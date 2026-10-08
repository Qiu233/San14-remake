using San14.Core;

namespace San14.Data;

public sealed record OriginalNormalArmyCombatGenerationOrderResult(int[]? OrderedNativeArmySlots,
    string? UnresolvedOwner);

/// <summary>SC16FFA0/TC170240 traverse World60's real current registry chain.
/// This reads its saved owner without importing/reconstructing the primary
/// arena or ordering gameplay Unit IDs. Pending receivers stay in that source
/// chain until the actual cleanup removes them; caller applies source gates.</summary>
public static class OriginalNormalArmyCombatGenerationOrder
{
    public static OriginalNormalArmyCombatGenerationOrderResult ReadArmyManager60(GameState state)
    {
        if (state.NativeArmyLifecycle is not { Slots.Length: 501 } arena)
            return Unknown("current.Native_Army_lifecycle");
        if (!arena.RegistryDescriptors.TryGetValue("Army", out var handle) || handle?.Identity is not int key ||
            !arena.Pool.Descriptors.TryGetValue(key, out var row) || row.Count is not int count)
            return Unknown("current.World60_Army_registry_descriptor");
        var slots = new List<int>(count);
        for (int? at = row.Head; at is int node; at = arena.Pool.Nodes[node].Next)
        {
            if (!arena.Pool.Nodes.TryGetValue(node, out var value) ||
                value.Payload is not { Kind: "Registry:Army", Identity: >= 0 and <= 500 } payload)
                return Unknown("current.World60_Army_registry_payload");
            slots.Add(payload.Identity!.Value);
        }
        if (slots.Count != count) return Unknown("current.World60_Army_registry_count");
        return new(slots.ToArray(), null);
    }

    private static OriginalNormalArmyCombatGenerationOrderResult Unknown(string field) => new(null, field);
}
