namespace San14.Data.AI.Original;

/// <summary>Bindings for saved planning facts and the formal computations which produce them.</summary>
public static class OriginalAiStrategyMemoryBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        IReadOnlyList<Func<OriginalAiOperation, OriginalAiNodeStatus>> children,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        var a = node.Arguments;
        if (node.TypeId == 0x78F9C619 && a.Length == 4 && a[0] == 2 && a[1] == 10)
        {
            execute = operation => operation.LoopFootholdCandidates(a[2], a[3], children);
            return true;
        }
        if (!node.IsNative || children.Count != 0) return false;
        switch (node.TypeId)
        {
            case 0x487CD1D8 when a.Length == 4:
                execute = operation => operation.EstimateAllFoodNeeds(a[0], unchecked((int)a[1]),
                    unchecked((int)a[2]), unchecked((int)a[3])); break;
            case 0xA6176FFA when a.Length == 2:
                execute = operation => operation.EstimateAllGoldNeeds(a[0], unchecked((int)a[1])); break;
            case 0x513BAD95 when a.Length == 3 && a[1] is 38 or 39:
                execute = operation => operation.FootholdSavedNeed(a[0], (int)a[1], a[2]); break;
            case 0x9CDB6038 when a.Length == 2 && a[0] is 19 or 21:
                execute = operation => operation.MyselfForceSavedFootholds((int)a[0], a[1]); break;
            case 0xEBBF8C69 when a.Length == 3 && a[1] is 19 or 21:
                execute = operation => operation.ForceSavedFootholds(a[0], (int)a[1], a[2]); break;
            // The installed CE bundles contain neither single-foothold form.
            // Their pure calculation/output contract is not the All command's
            // Force-wide saved-memory mutation and must not be substituted.
            case 0x4B89B99A:
            case 0x96DCB8BC:
                unresolved = "single-foothold need calculation/output requires its actual resource shape and pure current formula binding";
                return false;
            default: return false;
        }
        return true;
    }
}
