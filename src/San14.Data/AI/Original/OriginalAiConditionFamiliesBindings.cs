namespace San14.Data.AI.Original;

/// <summary>Load-time predicates for typed location, district-policy,
/// Hex facility and effective Army formation queries.</summary>
public static class OriginalAiConditionFamiliesBindings
{
    public static bool TryBindCondition(OriginalAiConditionQuery query,
        out Func<OriginalAiOperation, bool>? predicate, out string? unresolved)
    {
        predicate = null; unresolved = null;
        var record = query.Record;
        if (record.QueryId is not (5 or 110 or 124 or 129)) return false;
        var p = record.Parameters;
        if (p.Length != (record.QueryId == 5 ? 5 : 2))
        {
            unresolved = "condition family " + record.QueryId + " serialized parameter shape";
            return false;
        }
        predicate = record.QueryId switch
        {
            5 => operation => operation.ConditionLocationsWithinDistance(p[0], p[1], p[2], p[3], unchecked((int)p[4])),
            110 => operation => operation.ConditionDistrictPolicy(p[0], p[1]),
            124 => operation => operation.ConditionHexFacility(p[0], p[1]),
            _ => operation => operation.ConditionArmyEffectiveFormation(p[0], p[1])
        };
        return true;
    }
}
