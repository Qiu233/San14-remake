namespace San14.Data;

/// <summary>Type/slot captured by native target identity lookup; NodeIdentity identifies the saved target-list node.</summary>
public sealed record OriginalTacticAbnormalClearRequest(int SourceType, int SourceSlot, int NodeIdentity);
public sealed record OriginalTacticAbnormalClearNotification(int SourceType, int SourceSlot,
    int PayloadMarker, int Flags);
public sealed record OriginalTacticAbnormalClearResult(bool Applied, byte[] WrittenStatuses);

public interface IOriginalTacticAbnormalClearState
{
    OriginalRegistryObjectReference? ReadTacticClearTarget(int sourceType, int lookupSlot);
    bool? ReadTacticClearTargetValidity(OriginalRegistryObjectReference target);
    byte? ReadTacticClearByte(OriginalRegistryObjectReference target, int offset);
    void WriteTacticClearByte(OriginalRegistryObjectReference target, int offset, byte value);
    void WriteTacticClearUInt16(OriginalRegistryObjectReference target, int offset, ushort value);
    int? ReadTacticAbnormalUpperBound(int statusIndex);
    //Re-read this node's payload and then that payload's native ID getter; do not cache the first target pointer.
    int? ReadTacticClearNodePayloadId(int nodeIdentity);
    void NotifyTacticAbnormalClear(OriginalTacticAbnormalClearNotification notification);
}

/// <summary>CE opcode19's per-target branch (SC1780CF..17822C).
/// Current table pointers, validity, parameter reads and notification are explicit host inputs.
/// This is not the target-list walker, range/permission decision or the notification implementation.</summary>
public static class OriginalTacticAbnormalClear
{
    public static OriginalTacticAbnormalClearResult ApplyTarget(OriginalTacticAbnormalClearRequest request,
        IOriginalTacticAbnormalClearState state)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(state);
        int lookup;
        switch (request.SourceType)
        {
            case 0x1B: lookup = request.SourceSlot <= 500 ? request.SourceSlot : 0; break;
            case 5: lookup = request.SourceSlot < 52 ? request.SourceSlot : 0; break;
            case 6: lookup = unchecked((uint)(request.SourceSlot - 1)) <= 9
                ? unchecked(request.SourceSlot + 51) : 0; break;
            default: return new(false, []);
        }
        OriginalRegistryObjectReference target = state.ReadTacticClearTarget(request.SourceType, lookup)
            ?? throw new InvalidDataException("Current tactic abnormal target pointer is unknown.");
        if (target.Kind is not (OriginalRegistryKind.Army or OriginalRegistryKind.City or OriginalRegistryKind.Gate) ||
            (request.SourceType == 0x1B && target.Kind != OriginalRegistryKind.Army) ||
            (request.SourceType != 0x1B && target.Kind == OriginalRegistryKind.Army))
            throw new InvalidDataException("Current tactic abnormal target has a different receiver type.");
        bool valid = state.ReadTacticClearTargetValidity(target)
            ?? throw new InvalidDataException("Current tactic abnormal target validity is unknown.");
        if (!valid) return new(false, []);
        bool army = request.SourceType == 0x1B;
        if (army)
        {
            byte former = state.ReadTacticClearByte(target, 0x25)
                ?? throw new InvalidDataException("Current army abnormal byte25 is unknown.");
            if (former > 0)
            {
                state.WriteTacticClearByte(target, 0x3C, 0xFF);
                state.WriteTacticClearUInt16(target, 0x3E, 0);
            }
        }
        byte[] written = new byte[3];
        int first = army ? 0x23 : 0x49;
        for (int index = 0; index < 3; index++)
        {
            //2316E0 is a fresh global read on each invocation, even when the previous byte was zero.
            int upperBound = state.ReadTacticAbnormalUpperBound(index)
                ?? throw new InvalidDataException($"Current abnormal limit for index{index} is unknown.");
            byte value = upperBound < 0 ? unchecked((byte)upperBound) : (byte)0;
            state.WriteTacticClearByte(target, first + index, value);
            written[index] = value;
        }
        int marker = state.ReadTacticClearNodePayloadId(request.NodeIdentity)
            ?? throw new InvalidDataException("Current tactic target payload marker is unknown.");
        state.NotifyTacticAbnormalClear(new(request.SourceType, request.SourceSlot, marker, 0));
        return new(true, written);
    }
}
