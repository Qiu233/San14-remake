namespace San14.Data.AI.Original;

/// <summary>Real Country selection and ordered-caravan food requirement inputs.</summary>
public static class OriginalAiTradeInputBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId is not (0xCBD7B4F7 or 0xC27030B9)) return false;
        if (!node.IsNative || !node.ChildIndices.IsEmpty) return false;
        var a = node.Arguments;
        switch (node.TypeId)
        {
            case 0xCBD7B4F7 when a.Length == 2:
                execute = operation => operation.SelectTradeCountry(a[0], a[1]); return true;
            case 0xC27030B9 when a.Length == 4:
                execute = operation => operation.TradeNeedFood(a[0], a[1], a[2], a[3]); return true;
            default:
                unresolved = "trade input native type or serialized parameter shape"; return false;
        }
    }
}
