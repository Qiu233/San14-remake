namespace San14.Data;

public sealed partial class SourceWorldContext : ITacticAbnormalClearState
{
    private readonly Dictionary<(RegistryKind Kind, int Physical, int Offset), byte?> _tacticAbnormalBytes = [];
    private readonly Dictionary<int, int?> _tacticNodePayloadIds = [];
    public int? TacticAbnormalUpperBound { get; set; }
    public Func<int, int?>? TacticAbnormalUpperBoundReader { get; set; }
    public Func<int, int, RegistryObjectReference?>? TacticClearTargetReader { get; set; }
    public Func<int, int?>? TacticClearNodePayloadIdReader { get; set; }
    public Action<TacticAbnormalClearNotification>? TacticAbnormalClearNotifier { get; set; }

    public RegistryObjectReference? ReadTacticClearTarget(int sourceType, int lookupSlot)
    {
        if (TacticClearTargetReader is not null) return TacticClearTargetReader(sourceType, lookupSlot);
        if (sourceType == 0x1B) return ReadRegistryTableReference(RegistryKind.Army, lookupSlot);
        if (sourceType is not (5 or 6)) throw new InvalidDataException("Unsupported abnormal target source type.");
        //The already inspected combined CBaseData lookup has cities0..51 and gates1..10 at52..61.
        return lookupSlot < 52 ? ReadRegistryTableReference(RegistryKind.City, lookupSlot)
            : ReadRegistryTableReference(RegistryKind.Gate, lookupSlot - 51);
    }

    public bool? ReadTacticClearTargetValidity(RegistryObjectReference target) =>
        ReadRegistryObjectNativeValidity(target);
    public byte? ReadTacticClearByte(RegistryObjectReference target, int offset) =>
        target.Kind == RegistryKind.Army && offset == 0x3C ? ReadOverrideSource(target.PhysicalSlot).Kind3C :
        _tacticAbnormalBytes.GetValueOrDefault(TacticAbnormalKey(target, offset));
    public void WriteTacticClearByte(RegistryObjectReference target, int offset, byte value) =>
        SetTacticAbnormalRawByte(target, offset, value);
    public void SetTacticAbnormalRawByte(RegistryObjectReference target, int offset, byte? value)
    {
        if (target.Kind == RegistryKind.Army && offset == 0x3C)
        {
            var previous = ReadOverrideSource(target.PhysicalSlot);
            if (HasOverrideDestination(target.PhysicalSlot) && previous.Identity3E is null)
                throw new InvalidDataException("A source category write cannot encode this independent override destination.");
            WriteOverrideSource(target.PhysicalSlot, previous with { Kind3C = value });
        }
        else _tacticAbnormalBytes[TacticAbnormalKey(target, offset)] = value;
    }
    public void WriteTacticClearUInt16(RegistryObjectReference target, int offset, ushort value)
    {
        if (target.Kind != RegistryKind.Army || offset != 0x3E)
            throw new InvalidDataException("Uninspected abnormal-clear word write.");
        WriteOverrideSource(target.PhysicalSlot, ReadOverrideSource(target.PhysicalSlot) with { Identity3E = value });
    }
    public ushort? ReadTacticArmyTarget3E(ArmyUnitReference target) =>
        ReadOverrideSource(target.PhysicalSlot).Identity3E;
    public int? ReadTacticAbnormalUpperBound(int statusIndex)
    {
        if (statusIndex is < 0 or > 2) throw new InvalidDataException("Abnormal status index is out of range.");
        return TacticAbnormalUpperBoundReader is null ? TacticAbnormalUpperBound : TacticAbnormalUpperBoundReader(statusIndex);
    }
    public void SetTacticClearNodePayloadId(int identity, int? marker)
    { if (identity < 0) throw new InvalidDataException("Negative tactic node identity."); _tacticNodePayloadIds[identity] = marker; }
    public int? ReadTacticClearNodePayloadId(int nodeIdentity) => TacticClearNodePayloadIdReader is null
        ? _tacticNodePayloadIds.GetValueOrDefault(nodeIdentity) : TacticClearNodePayloadIdReader(nodeIdentity);
    public void NotifyTacticAbnormalClear(TacticAbnormalClearNotification notification)
    {
        if (TacticAbnormalClearNotifier is null)
            throw new InvalidDataException("Native abnormal-clear notification adapter is unavailable.");
        TacticAbnormalClearNotifier(notification);
    }

    private static (RegistryKind, int, int) TacticAbnormalKey(RegistryObjectReference target, int offset)
    {
        if (target.Kind == RegistryKind.Army && offset is >= 0x23 and <= 0x25)
            return (target.Kind, MapPhysical(target.PhysicalSlot, 501, "army abnormal byte"), offset);
        if (target.Kind is RegistryKind.City or RegistryKind.Gate && offset is >= 0x49 and <= 0x4B)
            return (target.Kind, MapPhysical(target.PhysicalSlot,
                target.Kind == RegistryKind.City ? 52 : 11, "base abnormal byte"), offset);
        throw new InvalidDataException("Uninspected abnormal-clear byte field.");
    }
}
