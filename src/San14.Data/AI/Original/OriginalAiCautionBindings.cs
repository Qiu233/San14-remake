namespace San14.Data.AI.Original;

/// <summary>Typed bindings for the normal caution-relation list writers.</summary>
public static class OriginalAiCautionBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved) =>
        TryBind(node, [], out execute, out unresolved);

    public static bool TryBind(OriginalAiBehaviorNode node,
        IReadOnlyList<Func<OriginalAiOperation, OriginalAiNodeStatus>> children,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        // A different factory is not a malformed caution node. Preserve the
        // diagnostics produced by its actual binding family.
        if (node.TypeId is not (0x317C7F54 or 0x6241B9FF)) return false;
        var a = node.Arguments;
        if (children.Count != 0) { unresolved = "caution native children"; return false; }
        switch (node.TypeId)
        {
            case 0x317C7F54 when a.Length == 3:
                execute = operation => operation.AddUniqueArmyCaution(a[0], a[1], a[2]); return true;
            case 0x6241B9FF when a.Length == 2:
                execute = operation => operation.RemoveArmyCautionCandidate(a[0], a[1]); return true;
            default:
                unresolved = "caution native type or serialized parameter shape"; return false;
        }
    }
}
