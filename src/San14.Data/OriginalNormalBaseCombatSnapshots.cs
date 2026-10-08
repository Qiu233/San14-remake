using San14.Core;

namespace San14.Data;

/// <summary>The semantic fields written by170240/170900. Unread padding and
/// presentation pointers are not manufactured by this read-only projection.</summary>
public sealed record OriginalNormalBaseCombatSnapshot(ushort PhysicalSlot0, int NativeKind4,
    byte Morale8, ushort TroopsA, int HexC, int Force10, byte AcceptedCount14);
public sealed record OriginalNormalBaseCombatActor(OriginalNormalBaseCombatSnapshot Snapshot, int RangeHexes);
public sealed record OriginalNormalBaseCombatActorsResult(OriginalNormalBaseCombatActor[]? Actors,
    string? UnresolvedOwner);

/// <summary>City90/GateA0 actor gates and Base target snapshots only. This does not
/// run170900, allocate phase pools, create messages or claim full combat.</summary>
public static class OriginalNormalBaseCombatSnapshots
{
    public static OriginalNormalBaseCombatActorsResult ReadCityActors(GameState state,
        OriginalEventInitializationContext current) => ReadActors(state, current, OriginalBaseKind.City);

    public static OriginalNormalBaseCombatActorsResult ReadGateActors(GameState state,
        OriginalEventInitializationContext current) => ReadActors(state, current, OriginalBaseKind.Gate);

    private static OriginalNormalBaseCombatActorsResult ReadActors(GameState state,
        OriginalEventInitializationContext current, OriginalBaseKind kind)
    {
        string registry = kind.ToString();
        string sourceField = kind == OriginalBaseKind.City ? "World90_City" : "WorldA0_Gate";
        int maximum = kind == OriginalBaseKind.City ? 51 : 10;
        if (state.NativeArmyLifecycle is not { } arena ||
            !arena.RegistryDescriptors.TryGetValue(registry, out var handle) || handle?.Identity is not int key ||
            !arena.Pool.Descriptors.TryGetValue(key, out var descriptor) || descriptor.Count is not int count)
            return Unknown($"current.{sourceField}_registry");
        var actors = new List<OriginalNormalBaseCombatActor>(); int visited = 0;
        for (int? at = descriptor.Head; at is int node; at = arena.Pool.Nodes[node].Next)
        {
            if (!arena.Pool.Nodes.TryGetValue(node, out var value) ||
                value.Payload is not { Identity: >= 0 } payload || payload.Identity > maximum || payload.Kind != "Base:" + registry)
                return Unknown($"current.{sourceField}_registry_payload:{value?.Payload}");
            visited++;
            int physical = payload.Identity!.Value;
            var city = new OriginalBaseReference(kind, physical);
            int location = kind == OriginalBaseKind.City ? physical : physical + 51;
            bool? excluded = OriginalOwnedArmyDestructionCurrent.ReadBaseExcluded2334B0(state, current, location);
            if (excluded is null) return Unknown($"current.{registry}{physical}.2334B0");
            if (excluded.Value) continue;
            if (current.ReadBaseResource(city, OriginalBaseResourceKind.Troops) is not int troops)
                return Unknown($"current.{registry}{physical}.3C");
            if (troops == 0) continue;
            if (current.ReadCaptureBaseThreatFields(city)?.Byte49 is not byte abnormal)
                return Unknown($"current.{registry}{physical}.49");
            if (abnormal != 0) continue;
            int representative = current.ReadBaseCurrentHexIndex(city);
            var hex = current.ReadHexTableReference(representative < 48400 ? representative : 0);
            if (hex is null) return Unknown($"current.{registry}{physical}.Hex_pointer");
            if (hex.PhysicalSlot is null) continue;
            var snapshot = Read(state, current, city, troops, target: false, representative);
            if (snapshot is null) return Unknown($"current.{registry}{physical}.descriptor");
            actors.Add(new(snapshot, kind == OriginalBaseKind.City ? 3 : 2));
        }
        return visited == count ? new(actors.ToArray(), null) : Unknown($"current.{sourceField}_registry_count");
    }

    /// <summary>Source target snapshot retains zero/negative troops, pending
    /// destruction and abnormal49. Those fields are not target admission gates.</summary>
    public static OriginalNormalBaseCombatSnapshot? ReadCityTarget(GameState state,
        OriginalEventInitializationContext current, OriginalBaseReference actualCity)
    {
        if (actualCity.Kind != OriginalBaseKind.City || actualCity.PhysicalSlot is not >= 1 or > 51 ||
            current.ReadBaseNativeValidity(actualCity) != true ||
            current.ReadBaseResource(actualCity, OriginalBaseResourceKind.Troops) is not int troops) return null;
        return Read(state, current, actualCity, troops, target: true);
    }

    public static OriginalNormalBaseCombatSnapshot? ReadGateTarget(GameState state,
        OriginalEventInitializationContext current, OriginalBaseReference actualGate)
    {
        if (actualGate.Kind != OriginalBaseKind.Gate || actualGate.PhysicalSlot is not >= 1 or > 10 ||
            current.ReadBaseNativeValidity(actualGate) != true ||
            current.ReadBaseResource(actualGate, OriginalBaseResourceKind.Troops) is not int troops) return null;
        return Read(state, current, actualGate, troops, target: true);
    }

    private static OriginalNormalBaseCombatSnapshot? Read(GameState state,
        OriginalEventInitializationContext current, OriginalBaseReference city, int troops, bool target, int? knownHex = null)
    {
        //A legacy missing runtime owner must not reuse scenario44 as current.
        if (city.PhysicalSlot is not >= 1 ||
            (city.Kind == OriginalBaseKind.City
                ? state.Cities.GetValueOrDefault(city.PhysicalSlot.Value)?.NativeReturnRuntime?.Morale44
                : state.Gates.GetValueOrDefault(city.PhysicalSlot.Value + 51)?.NativeCombatRuntime?.Morale44) is null ||
            current.ReadBaseMorale44(city) is not byte morale || current.ReadBaseForceSlot(city) is not int force)
            return null;
        int hex = knownHex ?? current.ReadBaseCurrentHexIndex(city);
        return new((ushort)city.PhysicalSlot.Value, city.Kind == OriginalBaseKind.City ? 5 : 6, morale,
            unchecked((ushort)(target ? Math.Max(0, troops) : troops)), hex, force, 0);
    }

    private static OriginalNormalBaseCombatActorsResult Unknown(string field) => new(null, field);
}
