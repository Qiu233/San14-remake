namespace San14.Data.AI.Original;

/// <summary>Native personnel callers, using their serialized input keys and
/// the same formal command route as every other strategy.</summary>
public static class OriginalAiPersonnelCommandBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (!node.IsNative || !node.ChildIndices.IsEmpty) return false;
        var a = node.Arguments;
        switch (node.TypeId)
        {
            case 0x97229B60 when a.Length == 2:
                execute = operation => operation.PersonnelSearch(a[0], a[1]); break;
            case 0x31D0E0B5 when a.Length == 1:
                execute = operation => operation.PersonnelCredit(a[0], rankup: false); break;
            case 0xE4B69D5F when a.Length == 1:
                execute = operation => operation.PersonnelCredit(a[0], rankup: true); break;
            case 0xDC9908B1 when a.Length == 2:
                execute = operation => operation.PersonnelPrize(a[0], a[1]); break;
            case 0xA686B94B when a.Length == 2:
                execute = operation => operation.PersonnelItem(a[0], a[1]); break;
            default: return false;
        }
        return true;
    }
}
