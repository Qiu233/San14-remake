namespace San14.Data.AI.Original;

/// <summary>The two zero-argument Army-neighbor policy actions.</summary>
public static class OriginalAiArmyNeighborBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId is not (0xB553B8D4 or 0xF715BB7F)) return false;
        if (!node.IsNative || !node.ChildIndices.IsEmpty || !node.Arguments.IsEmpty)
        { unresolved = "Army neighbor occupation action requires a zero-argument native leaf"; return false; }
        execute = node.TypeId == 0xB553B8D4
            ? op => op.OccupyArmyNeighborCapital()
            : op => op.OccupyArmyNeighborScenery();
        return true;
    }
}
