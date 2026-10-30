namespace San14.Data.AI.Original;

/// <summary>Compile-time binding of actual Hex and Formation information leaves.</summary>
public static class OriginalAiHexFormationInformationBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId is not (0xC9413E99 or 0x5947FAEF)) return false;
        var a = node.Arguments;
        if (!node.IsNative || !node.ChildIndices.IsEmpty || a.Length != 3)
        { unresolved = "Hex/Formation information requires input, selector and output words"; return false; }
        uint maximum = node.TypeId == 0xC9413E99 ? 6u : 5u;
        if (a[1] > maximum)
        { unresolved = "Hex/Formation selector outside actual source switch"; return false; }
        execute = node.TypeId == 0xC9413E99
            ? op => op.HexInformation(a[0], (int)a[1], a[2])
            : op => op.FormationInformation(a[0], (int)a[1], a[2]);
        return true;
    }
}
