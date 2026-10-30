namespace San14.Data.AI.Original;

public static class OriginalAiTransportBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId is not (0x28F35A4A or 0xBF3A68E1 or 0xADFB412D)) return false;
        if (!node.IsNative || !node.ChildIndices.IsEmpty)
        { unresolved = "transport node requires an actual native leaf"; return false; }
        var a = node.Arguments;
        if (node.TypeId == 0x28F35A4A && a.Length == 5)
            execute = operation => operation.TransportDeparture(a[0], a[1], a[2], a[3], a[4]);
        else if (node.TypeId == 0xBF3A68E1 && a.Length == 0)
            execute = operation => operation.FrontFootholdTransportDeparture();
        else if (node.TypeId == 0xADFB412D && a.Length == 4)
            execute = operation => operation.TransportSoldier(a[0], unchecked((int)a[1]), unchecked((int)a[2]), unchecked((int)a[3]));
        else { unresolved = "transport node serialized parameter shape"; return false; }
        return true;
    }
}
