namespace San14.Data;

/// <summary>One current ordered node from the completed activation ArmyB8
/// tree. Receiver native validity and22DDF0 pool ID are source query outputs;
/// the damage payload is the signed i32 node+28, not a Core effect report.</summary>
public sealed record OriginalNormalArmyTacticDamageTreeNode(bool? ReceiverNativeValid,
    ushort? ReceiverNativePoolId, int? AccumulatedDamage28);

public sealed record OriginalNormalArmyCombatTacticFrame22Receipt(ushort PreviousWord22,
    ushort Word22After, bool Written, int MatchingNodes);

/// <summary>16FBC0's Army tree→frame22 consumption edge, after actual
/// activation success. It preserves a caller-owned old frame word on no
/// match. Neither fresh frame ownership nor a completed tree is manufactured.</summary>
public static class OriginalNormalArmyCombatTacticFrame22
{
    public static OriginalNormalArmyCombatTacticFrame22Receipt? ConsumeArmyTree(ushort previousWord22,
        ushort targetDescriptorPoolId, IReadOnlyList<OriginalNormalArmyTacticDamageTreeNode>? completedCurrentTree)
    {
        if (completedCurrentTree is null) return null;
        ushort after = previousWord22; int matches = 0;
        foreach (OriginalNormalArmyTacticDamageTreeNode node in completedCurrentTree)
        {
            if (node.ReceiverNativeValid is not bool valid) return null;
            if (!valid) continue;
            if (node.ReceiverNativePoolId is not ushort poolId) return null;
            if (poolId != targetDescriptorPoolId) continue;
            if (node.AccumulatedDamage28 is not int damage) return null;
            after = unchecked((ushort)damage); matches++;
        }
        return new(previousWord22, after, matches != 0, matches);
    }
}
