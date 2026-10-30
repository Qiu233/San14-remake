namespace San14.Data.AI.Original;

/// <summary>Only the two ordinary economy command nodes; diplomacy Exchange is separate.</summary>
public static class OriginalAiEconomyCommandBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId is not (0xD75D0097 or 0xCFBD6F14)) return false;
        var a = node.Arguments;
        if (a.Length != 5)
        { unresolved = "economy command serialized five-argument shape"; return false; }
        execute = node.TypeId == 0xD75D0097
            ? operation => operation.Merchant(a[0], unchecked((int)a[1]), a[2], a[3], a[4])
            : operation => operation.Trade(a[0], a[1], a[2], a[3], a[4]);
        return true;
    }
}
