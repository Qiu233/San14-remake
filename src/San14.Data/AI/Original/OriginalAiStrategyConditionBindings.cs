namespace San14.Data.AI.Original;

/// <summary>Typed load-time predicates for the source strategy settings and doctrine guards.</summary>
public static class OriginalAiStrategyConditionBindings
{
    public static bool TryBindCondition(OriginalAiConditionQuery query,
        out Func<OriginalAiOperation, bool>? predicate, out string? unresolved)
    {
        predicate = null; unresolved = null;
        var r = query.Record;
        var a = r.Parameters;
        // Negate belongs to the enclosing condition expression. No binding
        // applies it again, and no saved state is created by a predicate.
        switch (r.QueryId)
        {
            case 111 when a.Length == 3:
                predicate = operation => operation.StrategyDistrictResourceSetting(a[0],
                    unchecked((int)a[1]), unchecked((int)a[2])); return true;
            case 115 when a.Length == 1:
                predicate = operation => operation.StrategyDifficulty(unchecked((int)a[0])); return true;
            case 125 when a.Length == 2:
                predicate = operation => operation.StrategyDoctrine(a[0], unchecked((int)a[1])); return true;
            default:
                unresolved = $"strategy condition {r.QueryId} or serialized parameter shape";
                return false;
        }
    }
}
