using San14.Core;

namespace San14.Data;

/// <summary>The semantic fields written by170240/170900. Unread padding and
/// presentation pointers are not manufactured by this read-only projection.</summary>
public sealed record NormalBaseCombatSnapshot(ushort PhysicalSlot0, int NativeKind4,
    byte Morale8, ushort TroopsA, int HexC, int Force10, byte AcceptedCount14);
public sealed record NormalBaseCombatActor(NormalBaseCombatSnapshot Snapshot, int RangeHexes);
public sealed record NormalBaseCombatActorsResult(NormalBaseCombatActor[]? Actors,
    string? UnresolvedOwner);

/// <summary>City90/GateA0 actor gates and Base target snapshots only. This does not
/// run170900, allocate phase pools, create messages or claim full combat.</summary>
public static class NormalBaseCombatSnapshots
{
    public static NormalBaseCombatActorsResult ReadCityActors(GameState state,
        SourceWorldContext current) => ReadActors(state, current, BaseKind.City);

    public static NormalBaseCombatActorsResult ReadGateActors(GameState state,
        SourceWorldContext current) => ReadActors(state, current, BaseKind.Gate);

    private static NormalBaseCombatActorsResult ReadActors(GameState state,
        SourceWorldContext current, BaseKind kind)
    {
        string registry = kind.ToString();
        string sourceField = kind == BaseKind.City ? "World90_City" : "WorldA0_Gate";
        var registryKind = kind == BaseKind.City ? WorldEntityKind.City : WorldEntityKind.Gate;
        if (state.NativeArmyLifecycle?.Indexes.Registries.GetValueOrDefault(registryKind) is not { } order)
            return Unknown($"current.{sourceField}_registry");
        var actors = new List<NormalBaseCombatActor>();
        foreach (var entity in order)
        {
            if (entity.Kind != registryKind || entity.Id is not int id)
                return Unknown($"current.{sourceField}_registry_entity");
            int physical = kind == BaseKind.Gate ? id - 51 : id;
            var city = new BaseReference(kind, physical);
            int location = kind == BaseKind.City ? physical : physical + 51;
            bool? excluded = OwnedArmyDestructionCurrent.ReadBaseExcluded2334B0(state, current, location);
            if (excluded is null) return Unknown($"current.{registry}{physical}.2334B0");
            if (excluded.Value) continue;
            if (current.ReadBaseResource(city, BaseResourceKind.Troops) is not int troops)
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
            actors.Add(new(snapshot, kind == BaseKind.City ? 3 : 2));
        }
        return new(actors.ToArray(), null);
    }

    /// <summary>Source target snapshot retains zero/negative troops, pending
    /// destruction and abnormal49. Those fields are not target admission gates.</summary>
    public static NormalBaseCombatSnapshot? ReadCityTarget(GameState state,
        SourceWorldContext current, BaseReference actualCity)
    {
        if (actualCity.Kind != BaseKind.City || actualCity.PhysicalSlot is not >= 1 or > 51 ||
            current.ReadBaseNativeValidity(actualCity) != true ||
            current.ReadBaseResource(actualCity, BaseResourceKind.Troops) is not int troops) return null;
        return Read(state, current, actualCity, troops, target: true);
    }

    public static NormalBaseCombatSnapshot? ReadGateTarget(GameState state,
        SourceWorldContext current, BaseReference actualGate)
    {
        if (actualGate.Kind != BaseKind.Gate || actualGate.PhysicalSlot is not >= 1 or > 10 ||
            current.ReadBaseNativeValidity(actualGate) != true ||
            current.ReadBaseResource(actualGate, BaseResourceKind.Troops) is not int troops) return null;
        return Read(state, current, actualGate, troops, target: true);
    }

    private static NormalBaseCombatSnapshot? Read(GameState state,
        SourceWorldContext current, BaseReference city, int troops, bool target, int? knownHex = null)
    {
        //A legacy missing runtime owner must not reuse scenario44 as current.
        if (city.PhysicalSlot is not >= 1 ||
            (city.Kind == BaseKind.City
                ? state.Cities.GetValueOrDefault(city.PhysicalSlot.Value)?.NativeReturnRuntime?.Morale44
                : state.Gates.GetValueOrDefault(city.PhysicalSlot.Value + 51)?.NativeCombatRuntime?.Morale44) is null ||
            current.ReadBaseMorale44(city) is not byte morale || current.ReadBaseForceSlot(city) is not int force)
            return null;
        int hex = knownHex ?? current.ReadBaseCurrentHexIndex(city);
        return new((ushort)city.PhysicalSlot.Value, city.Kind == BaseKind.City ? 5 : 6, morale,
            unchecked((ushort)(target ? Math.Max(0, troops) : troops)), hex, force, 0);
    }

    private static NormalBaseCombatActorsResult Unknown(string field) => new(null, field);
}
