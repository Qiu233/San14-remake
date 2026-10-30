namespace San14.Data.AI.Original;

public static class OriginalAiArmyPositionBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId is not (0x79EB5C74 or 0x472B6DDD)) return false;
        if (!node.IsNative || !node.ChildIndices.IsEmpty || !node.Arguments.IsEmpty)
        { unresolved = "Army position native requires no arguments or children"; return false; }
        execute = node.TypeId == 0x79EB5C74
            ? operation => operation.MoveArmyToAdvantagePosition()
            : operation => operation.MoveArmyToObjectAttackPosition();
        return true;
    }
}
