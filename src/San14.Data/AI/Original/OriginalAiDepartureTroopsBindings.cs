namespace San14.Data.AI.Original;

public static class OriginalAiDepartureTroopsBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId != 0xD6821309) return false;
        if (!node.IsNative || !node.ChildIndices.IsEmpty || node.Arguments.Length != 4)
        { unresolved = "Departure military-group creation requires four arguments and no children"; return false; }
        var a = node.Arguments;
        execute = operation => operation.CreateDepartureTroops(a[0], unchecked((int)a[1]), a[2], a[3]);
        return true;
    }
}
