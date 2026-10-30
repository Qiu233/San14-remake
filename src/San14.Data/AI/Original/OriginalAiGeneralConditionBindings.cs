namespace San14.Data.AI.Original;

/// <summary>Load-time bindings for key-to-key integer comparisons and the
/// explicitly selected military group's actual purpose. The condition
/// expression remains the sole owner of source negation and short circuit.</summary>
public static class OriginalAiGeneralConditionBindings
{
    public static bool TryBindCondition(OriginalAiConditionQuery query,
        out Func<OriginalAiOperation, bool>? predicate, out string? unresolved)
    {
        predicate = null; unresolved = null;
        var record = query.Record;
        if (record.QueryId is not (18 or 19 or 28)) return false;
        var parameters = record.Parameters;
        if (parameters.Length != 2)
        {
            unresolved = "general condition " + record.QueryId + " requires two serialized parameters";
            return false;
        }
        predicate = record.QueryId switch
        {
            // Both operands name signed integer variables. Neither right
            // operand is a literal as it is for the separate queries20/21.
            18 => operation => operation.IntegerKeysGreaterThan(parameters[0], parameters[1]),
            19 => operation => operation.IntegerKeysAtLeast(parameters[0], parameters[1]),
            // Here only the first operand is a variable. The second is the
            // uint purpose literal compared to the selected receiver word.
            _ => operation => operation.TroopsPurposeEquals(parameters[0], parameters[1])
        };
        return true;
    }
}
