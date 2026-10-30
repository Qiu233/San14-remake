namespace San14.Data.AI.Original;

/// <summary>The installed zero-argument Force military-intelligence distribution leaf.</summary>
public static class OriginalAiSpyAllocationBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId != 0x85C9FF2C) return false;
        if (!node.IsNative || !node.Arguments.IsEmpty || !node.ChildIndices.IsEmpty)
        {
            unresolved = "DistributeSpyPoint requires its zero-argument native leaf shape";
            return false;
        }
        execute = operation => operation.DistributeSpyPoints();
        return true;
    }
}
