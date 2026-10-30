namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal bool IntegerKeysGreaterThan(uint leftKey, uint rightKey)
        => TryInteger(leftKey, out int left) && TryInteger(rightKey, out int right) && left > right;

    internal bool IntegerKeysAtLeast(uint leftKey, uint rightKey)
        => TryInteger(leftKey, out int left) && TryInteger(rightKey, out int right) && left >= right;

    internal bool TroopsPurposeEquals(uint groupKey, uint literal)
        => TryTroops(groupKey, out var group) &&
            Adapter.ReadOriginalAiTroopsPurposeEqualsCurrent(Current, InputsCurrent, group, literal);
}
