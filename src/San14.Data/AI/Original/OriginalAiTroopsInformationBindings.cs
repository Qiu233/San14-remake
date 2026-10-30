namespace San14.Data.AI.Original;

public static class OriginalAiTroopsInformationBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId is not (0x09B97AA1 or 0xC9EC061B or 0xC4ECFC1B)) return false;
        if (!node.IsNative || !node.ChildIndices.IsEmpty)
        { unresolved = "Troops information/purpose requires a native leaf"; return false; }
        var a = node.Arguments;
        if (node.TypeId == 0xC4ECFC1B)
        {
            if (a.Length != 2) { unresolved = "SetRelayHexList requires group and ordered Hex list"; return false; }
            execute = operation => operation.SetTroopsRelayHexes(a[0], a[1]); return true;
        }
        if (a.Length != 3) { unresolved = "Troops information/purpose requires three parameters"; return false; }
        int mode = unchecked((int)a[1]);
        if (node.TypeId == 0x09B97AA1)
        {
            if (mode == 6) { unresolved = "TroopsInfo6 current IGameData object identity producer"; return false; }
            if (mode is < 0 or > 15) { unresolved = "Troops information selector " + mode; return false; }
            execute = operation => operation.ReadTroopsInformation(a[0], mode, a[2]); return true;
        }
        if (mode is not (0 or 1 or 7 or 12 or 13 or 17 or 18 or 19))
        { unresolved = "Troops purpose semantic variant " + mode; return false; }
        execute = operation => operation.SetTroopsPurpose(a[0], mode, a[2]); return true;
    }
}
