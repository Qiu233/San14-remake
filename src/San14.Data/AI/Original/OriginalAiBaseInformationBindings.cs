namespace San14.Data.AI.Original;

/// <summary>Compile-time bindings for current foothold, area and district receivers.</summary>
public static class OriginalAiBaseInformationBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId is not (0x513BAD95 or 0x6AB07E75 or 0x70368754 or 0x7B9D3E14)) return false;
        bool myself = node.TypeId == 0x7B9D3E14;
        var a = node.Arguments;
        if (!node.IsNative || !node.ChildIndices.IsEmpty || a.Length != (myself ? 2 : 3))
        { unresolved = "Base/Area/District information leaf parameter shape"; return false; }
        uint mode = a[myself ? 0 : 1];
        bool supported = node.TypeId switch
        {
            0x513BAD95 => mode is <= 12 or 22 or 23 or 26 or 27 or 29 or 31 or 32 or 33 or 34 or 35 or 36 or 37 or 38 or 39 or 41 or 43 or 44 or 45,
            0x6AB07E75 => mode is <= 14 or >= 18 and <= 24,
            _ => mode is 0 or 1 or 2 or 4 or 5 or >= 7 and <= 20
        };
        if (!supported)
        {
            unresolved = (node.TypeId, mode) switch
            {
                _ => $"{node.TypeId:X8} information selector {mode}: additional current source consumer"
            };
            return false;
        }
        execute = node.TypeId switch
        {
            0x513BAD95 => op => op.FootholdInformation(a[0], (int)mode, a[2]),
            0x6AB07E75 => op => op.AreaInformation(a[0], (int)mode, a[2]),
            0x70368754 => op => op.DistrictInformation(a[0], (int)mode, a[2]),
            _ => op => op.MyselfDistrictInformation((int)mode, a[1])
        };
        return true;
    }
}
