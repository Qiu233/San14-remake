namespace San14.Data.AI.Original;

/// <summary>The one signed literal of the installed DistrictSetPurpose family.
/// It is a district state writer, not a blackboard-value producer.</summary>
public static class OriginalAiDistrictPurposeBindings
{
    public static bool TryBind(OriginalAiBehaviorNode node,
        out Func<OriginalAiOperation, OriginalAiNodeStatus>? execute, out string? unresolved)
    {
        execute = null; unresolved = null;
        if (node.TypeId != 0x9CF455EA) return false;
        if (!node.IsNative || !node.ChildIndices.IsEmpty || node.Arguments.Length != 1)
        {
            unresolved = "DistrictSetPurpose requires its native leaf and one signed literal";
            return false;
        }
        // 0360F0 saturates the resource int rather than truncating its low byte.
        sbyte purpose = (sbyte)Math.Clamp(unchecked((int)node.Arguments[0]), sbyte.MinValue, sbyte.MaxValue);
        execute = operation => operation.SetDistrictPurpose(purpose);
        return true;
    }
}
