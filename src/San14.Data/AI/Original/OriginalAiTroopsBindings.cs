namespace San14.Data.AI.Original;

/// <summary>Actual ForceInfo34 produces a CTroops list, whose discriminator2
/// is independent of the District object and loop-resolver discriminators.</summary>
public static class OriginalAiTroopsBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (!node.IsNative || !node.ChildIndices.IsEmpty) return false;
        var a = node.Arguments;
        if (node.TypeId == 0x9CDB6038 && a.Length == 2 && a[0] == 34)
        { execute = operation => operation.MyselfForceTroops(a[1]); return true; }
        if (node.TypeId == 0xEBBF8C69 && a.Length == 3 && a[1] == 34)
        { execute = operation => operation.VariableForceTroops(a[0], a[2]); return true; }
        return false;
    }
}
