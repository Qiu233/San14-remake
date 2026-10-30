using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal bool ConditionDistrictPolicy(uint key, uint literal) =>
        TryEntity(key, out var at) && at.Kind == WorldEntityKind.District &&
        Adapter.ReadOriginalAiConditionDistrictPolicyCurrent(InputsCurrent, at.Id, literal);

    internal bool ConditionHexFacility(uint key, uint literal) =>
        TryHex(key, out var at) && Adapter.ReadOriginalAiConditionHexFacilityCurrent(InputsCurrent, at, literal);

    internal bool ConditionArmyEffectiveFormation(uint key, uint literal) =>
        TryEntity(key, out var at) && at.Kind == WorldEntityKind.Army &&
        Adapter.ReadOriginalAiConditionArmyEffectiveFormationCurrent(Current, InputsCurrent, at.Id, literal);

    internal bool ConditionLocationsWithinDistance(uint firstKind, uint firstKey,
        uint secondKind, uint secondKey, int maximum)
    {
        //0A8940 resolves both operands before checking the invalid-Hex
        //sentinel. These tags are receiver types, not literal coordinates.
        int first = ConditionLocation(firstKind, firstKey);
        int second = ConditionLocation(secondKind, secondKey);
        return first != 48400 && second != 48400 && ArmyRouteSearch.Distance(first, second) <= maximum;
    }

    private int ConditionLocation(uint kind, uint key)
    {
        if (kind == 2)
            return TryTroops(key, out var troops)
                ? Adapter.ReadOriginalAiConditionTroopsHexCurrent(Current, InputsCurrent, troops) : 48400;
        if (kind == 7)
            return TryHex(key, out var hex) ? hex.Id : 48400;
        if (!TryEntity(key, out var at)) return 48400;
        bool matches = kind switch
        {
            0 => at.Kind == WorldEntityKind.Force,
            1 => at.Kind == WorldEntityKind.District,
            3 => at.Kind == WorldEntityKind.Army,
            4 => at.Kind == WorldEntityKind.Officer,
            5 or 8 or 9 => at.Kind is WorldEntityKind.City or WorldEntityKind.Gate,
            6 => at.Kind == WorldEntityKind.Area,
            10 => at.Kind == WorldEntityKind.MapObject,
            18 => at.Kind == WorldEntityKind.Landmark,
            _ => false
        };
        return matches ? Adapter.ReadOriginalAiConditionEntityHexCurrent(Current, InputsCurrent, at, kind) : 48400;
    }
}
