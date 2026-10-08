namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalTacticAbnormalClearState
{
    private readonly Dictionary<(OriginalRegistryKind Kind, int Physical, int Offset), byte?> _tacticAbnormalBytes = [];
    private readonly Dictionary<int, ushort?> _tacticArmyTargets3E = [];
    private readonly Dictionary<int, int?> _tacticNodePayloadIds = [];
    public int? TacticAbnormalUpperBound { get; set; }
    public Func<int, int?>? TacticAbnormalUpperBoundReader { get; set; }
    public Func<int, int, OriginalRegistryObjectReference?>? TacticClearTargetReader { get; set; }
    public Func<int, int?>? TacticClearNodePayloadIdReader { get; set; }
    public Action<OriginalTacticAbnormalClearNotification>? TacticAbnormalClearNotifier { get; set; }

    public OriginalRegistryObjectReference? ReadTacticClearTarget(int sourceType, int lookupSlot)
    {
        if (TacticClearTargetReader is not null) return TacticClearTargetReader(sourceType, lookupSlot);
        if (sourceType == 0x1B) return ReadRegistryTableReference(OriginalRegistryKind.Army, lookupSlot);
        if (sourceType is not (5 or 6)) throw new InvalidDataException("Unsupported abnormal target source type.");
        //The already inspected combined CBaseData lookup has cities0..51 and gates1..10 at52..61.
        return lookupSlot < 52 ? ReadRegistryTableReference(OriginalRegistryKind.City, lookupSlot)
            : ReadRegistryTableReference(OriginalRegistryKind.Gate, lookupSlot - 51);
    }

    public bool? ReadTacticClearTargetValidity(OriginalRegistryObjectReference target) =>
        ReadRegistryObjectNativeValidity(target);
    public byte? ReadTacticClearByte(OriginalRegistryObjectReference target, int offset) =>
        _tacticAbnormalBytes.GetValueOrDefault(TacticAbnormalKey(target, offset));
    public void WriteTacticClearByte(OriginalRegistryObjectReference target, int offset, byte value) =>
        _tacticAbnormalBytes[TacticAbnormalKey(target, offset)] = value;
    public void SetTacticAbnormalRawByte(OriginalRegistryObjectReference target, int offset, byte? value) =>
        _tacticAbnormalBytes[TacticAbnormalKey(target, offset)] = value;
    public void WriteTacticClearUInt16(OriginalRegistryObjectReference target, int offset, ushort value)
    {
        if (target.Kind != OriginalRegistryKind.Army || offset != 0x3E)
            throw new InvalidDataException("Uninspected abnormal-clear word write.");
        int physical = MapPhysical(target.PhysicalSlot, 501, "army abnormal target3E");
        _tacticArmyTargets3E[physical] = value;
    }
    public ushort? ReadTacticArmyTarget3E(OriginalArmyUnitReference target) =>
        _tacticArmyTargets3E.GetValueOrDefault(MapPhysical(target.PhysicalSlot, 501, "army abnormal target3E"));
    public int? ReadTacticAbnormalUpperBound(int statusIndex)
    {
        if (statusIndex is < 0 or > 2) throw new InvalidDataException("Abnormal status index is out of range.");
        return TacticAbnormalUpperBoundReader is null ? TacticAbnormalUpperBound : TacticAbnormalUpperBoundReader(statusIndex);
    }
    public void SetTacticClearNodePayloadId(int identity, int? marker)
    { if (identity < 0) throw new InvalidDataException("Negative tactic node identity."); _tacticNodePayloadIds[identity] = marker; }
    public int? ReadTacticClearNodePayloadId(int nodeIdentity) => TacticClearNodePayloadIdReader is null
        ? _tacticNodePayloadIds.GetValueOrDefault(nodeIdentity) : TacticClearNodePayloadIdReader(nodeIdentity);
    public void NotifyTacticAbnormalClear(OriginalTacticAbnormalClearNotification notification)
    {
        if (TacticAbnormalClearNotifier is null)
            throw new InvalidDataException("Native abnormal-clear notification adapter is unavailable.");
        TacticAbnormalClearNotifier(notification);
    }

    private static (OriginalRegistryKind, int, int) TacticAbnormalKey(OriginalRegistryObjectReference target, int offset)
    {
        if (target.Kind == OriginalRegistryKind.Army && offset is >= 0x23 and <= 0x25 or 0x3C)
            return (target.Kind, MapPhysical(target.PhysicalSlot, 501, "army abnormal byte"), offset);
        if (target.Kind is OriginalRegistryKind.City or OriginalRegistryKind.Gate && offset is >= 0x49 and <= 0x4B)
            return (target.Kind, MapPhysical(target.PhysicalSlot,
                target.Kind == OriginalRegistryKind.City ? 52 : 11, "base abnormal byte"), offset);
        throw new InvalidDataException("Uninspected abnormal-clear byte field.");
    }
}
