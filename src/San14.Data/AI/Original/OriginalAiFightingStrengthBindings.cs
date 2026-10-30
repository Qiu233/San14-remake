namespace San14.Data.AI.Original;

public static class OriginalAiFightingStrengthBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (!node.IsNative || !node.ChildIndices.IsEmpty) return false;
        var a = node.Arguments;
        if (node.TypeId == 0x184B7500)
        {
            if (a.Length != 6) { unresolved = "Foothold FightingStrength six-argument shape"; return false; }
            execute = operation => operation.FootholdFightingStrength(a[0], a[1], unchecked((int)a[2]), a[3], a[4], a[5]);
            return true;
        }
        if (node.TypeId == 0x9CDB6038 && a.Length == 2 && a[0] is 4 or 5)
        { execute = operation => operation.MyselfForceEvaluation((int)a[0], a[1]); return true; }
        if (node.TypeId == 0xEBBF8C69 && a.Length == 3 && a[1] is 4 or 5)
        { execute = operation => operation.VariableForceEvaluation(a[0], (int)a[1], a[2]); return true; }
        return false;
    }
}
