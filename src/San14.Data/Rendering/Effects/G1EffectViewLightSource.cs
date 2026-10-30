using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The normal View's EffectLightGroupID after the actual Common
/// configuration overlay. This selects the independent modifier record;
/// Common ViewAux selects the base light separately.</summary>
public sealed class G1EffectViewLightSource
{
    private G1EffectViewLightSource(uint groupId) => GroupId = groupId;
    public uint GroupId { get; }
    public uint GroupMask => GroupId == 0 ? 0 : 1u << unchecked((int)(GroupId - 1) & 31);

    public static G1EffectViewLightSource Import(DokObject view, DokObject common)
    {
        ArgumentNullException.ThrowIfNull(view); ArgumentNullException.ThrowIfNull(common);
        if (view.ClassId != 0x97AF26FD || common.ClassId != 0xEF3D849D ||
            view.Kind != DokObjectKind.Inline || common.Kind != DokObjectKind.Inline)
            throw new InvalidDataException("Effect light selection requires the actual View and Common declarations.");
        uint declared = Group(view), overlay = Group(common);
        // D20D50 only overrides the declaration for actual setting values1..8.
        // D4B9E0 supplies zero or the native modulo32 mask to D78900, which
        // publishes the first intersecting kind-one record at View1B18.
        return new(overlay is >= 1 and <= 8 ? overlay : declared);
    }

    public bool Selects(uint actualRecordGroupMask) => (GroupMask & actualRecordGroupMask) != 0;

    private static uint Group(DokObject owner)
    {
        var fields = owner.Properties.Where(value => value.NameHash == 0x52F556B5).ToArray();
        if (fields.Length != 1 || fields[0].TypeCode != 5 || fields[0].ElementCount != 1 ||
            fields[0].ElementSize != 4 || fields[0].RawValue.Length != 4)
            throw new InvalidDataException("EffectLightGroupID requires one original UInt32 property.");
        return BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
    }
}
