namespace San14.Data.AI.Original;

/// <summary>Actual caution-relation and caution-group information consumers.
/// A missing persistent producer is not replaced by a derived or zero value.</summary>
public static class OriginalAiCautionInformationBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        bool group = node.TypeId == 0xC01282EA;
        if (!group && node.TypeId != 0x37E75BA6) return false;
        var a = node.Arguments;
        if (node.ChildIndices.Length != 0 || a.Length != 3)
        {
            unresolved = "caution information native child/serialized parameter shape";
            return false;
        }
        int mode = unchecked((int)a[1]);
        if (group && mode is 1 or 2 or 3 or 4 or 7 or 8)
            execute = operation => operation.ReadCautionGroupInformation(a[0], mode, a[2]);
        else if (!group && mode is 1 or 2 or 3)
            execute = operation => operation.ReadCautionArmyInformation(a[0], mode, a[2]);
        else
        {
            unresolved = group ? "caution group information selector " + mode
                : "caution relation information selector " + mode;
            return false;
        }
        return true;
    }
}
