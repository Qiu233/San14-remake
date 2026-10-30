namespace San14.Data.AI.Original;

public static class OriginalAiIntelligenceMissionBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId is not (0x964B046E or 0x9C2AB073)) return false;
        if (!node.IsNative || !node.ChildIndices.IsEmpty)
        { unresolved = "intelligence mission requires an actual native leaf"; return false; }
        var args = node.Arguments;
        if (node.TypeId == 0x964B046E && args.Length == 3)
            execute = operation => operation.IntelligenceSpyAway(args[0], args[1], args[2]);
        else if (node.TypeId == 0x9C2AB073 && args.Length == 0)
            execute = operation => operation.IntelligenceInspect();
        else
        { unresolved = "intelligence mission serialized parameter shape"; return false; }
        return true;
    }
}
