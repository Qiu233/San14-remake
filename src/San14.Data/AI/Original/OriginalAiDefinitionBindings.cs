namespace San14.Data.AI.Original;

public static class OriginalAiDefinitionBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId is not (0x8994D82E or 0x001F906B)) return false;
        if (!node.IsNative || !node.ChildIndices.IsEmpty)
        { unresolved = "definition producer requires a native leaf"; return false; }
        var a = node.Arguments;
        if (node.TypeId == 0x8994D82E && a.Length == 3)
            execute = operation => operation.AddPossibleFacilityCandidates(a[0], a[1], a[2]);
        else if (a.Length == 2)
            execute = node.TypeId switch
            {
                0x001F906B => operation => operation.SetDefinitionEvaluation(a[0], a[1], OriginalAiDefinitionKind.Gem),
                _ => null
            };
        if (execute is not null) return true;
        unresolved = "definition producer serialized parameter shape"; return false;
    }
}
