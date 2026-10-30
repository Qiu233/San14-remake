namespace San14.Data.AI.Original;

public static class OriginalAiCautionGroupPlanningBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId is not (0xD4B115CE or 0x9EA63AA3 or 0x6BF6A84F or 0x9FFA7C94)) return false;
        var a = node.Arguments;
        if (node.TypeId == 0x9FFA7C94)
        {
            if (node.ChildIndices.Length == 0 && a.Length == 1)
            { execute = operation => operation.ClearCautionGroup(a[0]); return true; }
            unresolved = "caution group clear native child/serialized parameter shape"; return false;
        }
        if (node.ChildIndices.Length != 0 || a.Length != 2)
        { unresolved = "caution group planning native child/serialized parameter shape"; return false; }
        execute = node.TypeId switch
        {
            0xD4B115CE => operation => operation.SetCautionGroupEvaluation(a[0], a[1]),
            0x9EA63AA3 => operation => operation.SetCautionGroupVirtualTarget(a[0], a[1]),
            _ => operation => operation.UpdateCautionGroupStatus(a[0], a[1] == 1)
        };
        return true;
    }
}
