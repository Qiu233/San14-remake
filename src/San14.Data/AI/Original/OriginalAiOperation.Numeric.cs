namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus SelectIntegerValue(uint source, int right, uint destination, bool maximum)
    {
        if (!TryInteger(source, out int left)) return OriginalAiNodeStatus.Failure;
        return SetInteger(destination, maximum ? Math.Max(left, right) : Math.Min(left, right));
    }

    internal OriginalAiNodeStatus SelectIntegerVariable(uint source, uint rightKey, uint destination, bool maximum)
    {
        // Native readers short-circuit in this order. In particular, a missing
        // second integer leaves an aliased destination and its old type alone.
        if (!TryInteger(source, out int left) || !TryInteger(rightKey, out int right))
            return OriginalAiNodeStatus.Failure;
        return SetInteger(destination, maximum ? Math.Max(left, right) : Math.Min(left, right));
    }

    internal OriginalAiNodeStatus RequestLoopBreak() => SetInteger(LoopBreakKey, 1);
}
