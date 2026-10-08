namespace San14.Core;

/// <summary>Only the type identity is from the installed CE object table. All values
/// and the reduced eligibility below are explicit temporary playable rules.</summary>
public static class PlayableConstructionRules
{
    public const string ProvisionalFormationId = "san14:object/3"; // 阵/陣
    public const string ProvisionalFortId = "san14:object/4"; // 砦
    public const string ProvisionalArcherTowerId = "san14:object/5"; // 箭楼/弓櫓
    public const int ProvisionalGoldCost = 100;
    public const int ProvisionalMaxDurability = 100;
    public const int ProvisionalProgressPerPeriod = 25;

    public readonly record struct Parameters(int GoldCost, int MaxDurability,
        int ProgressPerPeriod);

    public readonly record struct KindDefinition(int ObjectSlot, string KindId,
        Parameters Parameters);

    public static IReadOnlyList<KindDefinition> SupportedKinds { get; } =
        Array.AsReadOnly<KindDefinition>(
        [
            new(3, ProvisionalFormationId, new(ProvisionalGoldCost,
                ProvisionalMaxDurability, ProvisionalProgressPerPeriod)),
            new(4, ProvisionalFortId, new(ProvisionalGoldCost,
                ProvisionalMaxDurability, ProvisionalProgressPerPeriod)),
            new(5, ProvisionalArcherTowerId, new(ProvisionalGoldCost,
                ProvisionalMaxDurability, ProvisionalProgressPerPeriod))
        ]);

    public static bool IsSupportedKind(string? kindId) =>
        SupportedKinds.Any(kind => kind.KindId == kindId);

    public static KindDefinition Describe(string kindId)
    {
        foreach (KindDefinition kind in SupportedKinds)
            if (kind.KindId == kindId) return kind;
        throw new InvalidDataException($"Facility kind {kindId} has no playable parameters.");
    }

    public static Parameters ForKind(string kindId) => Describe(kindId).Parameters;

    public static KindDefinition DescribeSlot(int objectSlot)
    {
        foreach (KindDefinition kind in SupportedKinds)
            if (kind.ObjectSlot == objectSlot) return kind;
        throw new InvalidDataException($"Facility object slot {objectSlot} has no playable parameters.");
    }

    public static bool CanBuildAt(GameState state, PlayableLandRoutes routes, int forceId,
        int hexId, IReadOnlyDictionary<int, StructureState> structures)
    {
        if (!routes.CanEnter(hexId) || !state.Territory.TryGetValue(hexId, out TerritoryState? land) ||
            land.ForceId != forceId || land.AreaId is null ||
            structures.Values.Any(structure => structure.HexId == hexId)) return false;
        // These two distance limits are source-confirmed. The temporary ruleset
        // currently supports only a military facility on source-area land.
        if (state.Cities.Values.Any(city => city.HexId is int baseHex &&
                routes.WithinRadius(hexId, baseHex, 3)) ||
            state.Gates.Values.Any(gate => gate.HexId is int baseHex &&
                routes.WithinRadius(hexId, baseHex, 3))) return false;
        return !structures.Values.Any(structure =>
            routes.WithinRadius(hexId, structure.HexId, 2));
    }
}
