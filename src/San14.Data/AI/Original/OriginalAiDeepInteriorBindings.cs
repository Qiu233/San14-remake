namespace San14.Data.AI.Original;

/// <summary>Only ForceInfo33. Other information modes retain their own consumers.</summary>
public static class OriginalAiDeepInteriorBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (!node.IsNative || !node.ChildIndices.IsEmpty) return false;
        var a = node.Arguments;
        if (node.TypeId == 0x9CDB6038 && a.Length == 2 && a[0] == 33)
        { execute = operation => operation.MyselfDeepInteriorFootholds(a[1]); return true; }
        if (node.TypeId == 0xEBBF8C69 && a.Length == 3 && a[1] == 33)
        { execute = operation => operation.VariableDeepInteriorFootholds(a[0], a[2]); return true; }
        return false;
    }
}
