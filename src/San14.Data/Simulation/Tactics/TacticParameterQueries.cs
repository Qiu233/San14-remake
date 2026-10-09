namespace San14.Data;

/// <summary>A captured original vector-begin identity and its byte address.
/// Owned managed adapters may use a relative byte address under an explicit
/// normal-buffer profile; that is not a native process address.</summary>
public sealed record TacticParameterBufferReference(ulong ByteAddress, object? StorageIdentity);

/// <summary>The address selected before the physical-index helper runs.</summary>
public sealed record TacticParameterByteLocation(
    TacticParameterBufferReference Begin, int ElementOrdinal, ulong ByteAddress);

/// <summary>The captured vtable identity. Its slot is invoked after the source
/// physical-index helper and byte read; neither receiver nor table is recaptured.</summary>
public interface ITacticParameterDecoderTable
{
    byte? Decode(TacticReference receiver, byte encodedLowByte, byte rotation, int physicalSlot);
}

public interface ITacticParameterQueryState
{
    ulong? ReadTacticParameterEnd78(TacticReference receiver);
    TacticParameterBufferReference? ReadTacticParameterBegin70(TacticReference receiver);
    ITacticParameterDecoderTable? CaptureTacticParameterDecoderTable(TacticReference receiver);
    int? ReadTacticParameterPhysicalSlot(TacticReference receiver);
    byte? ReadTacticParameterEncodedByte(TacticParameterByteLocation location);
    byte? ReadTacticParameterRawByte(TacticReference receiver, int nativeOffset);
}

/// <summary>Current CE CTacticsData getters SC16D300/390/420/4B0 and their
/// paired TC16D5A0/630/6C0/750. These getters do not check tactic validity or
/// decide its permission, target range, duration, or effect execution.</summary>
public static class TacticParameterQueries
{
    public static byte ReadEffectOpcode(TacticReference receiver, int index,
        ITacticParameterQueryState state) => Read(receiver, index, state, 0x62, 1, 7);
    public static byte ReadRaw64(TacticReference receiver, int index,
        ITacticParameterQueryState state) => Read(receiver, index, state, 0x64, 3, 1);
    public static byte ReadEffectValue(TacticReference receiver, int index,
        ITacticParameterQueryState state) => Read(receiver, index, state, 0x66, 5, 3);
    public static byte ReadRaw68(TacticReference receiver, int index,
        ITacticParameterQueryState state) => Read(receiver, index, state, 0x68, 7, 5);

    private static byte Read(TacticReference receiver, int index,
        ITacticParameterQueryState state, int nativeOffset, int ordinalBase, byte rotation)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(state);
        ulong end = Known(state.ReadTacticParameterEnd78(receiver), "current tactic vector end78");
        TacticParameterBufferReference firstBegin = state.ReadTacticParameterBegin70(receiver)
            ?? throw Unknown("current tactic vector begin70 for length");
        // SUB wraps as a 64-bit address difference. AND ~3, not a count or
        // exact pointer-distance equality, selects the native encoded branch.
        ulong byteDifference = unchecked(end - firstBegin.ByteAddress);
        if ((byteDifference & ~3UL) != 40)
        {
            int rawIndex = index < 0 ? 0 : index > 1 ? 1 : index;
            return Known(state.ReadTacticParameterRawByte(receiver, nativeOffset + rawIndex),
                $"current tactic raw byte{nativeOffset + rawIndex:X}");
        }
        // LEA EAX,[EDX+base] is an unchecked int32 addition, then MOVSXD.
        // Even an abnormal index stays in this branch: missing byte access
        // fails explicitly instead of falling back to the raw pair.
        int ordinal = unchecked(index + ordinalBase);
        TacticParameterBufferReference begin = state.ReadTacticParameterBegin70(receiver)
            ?? throw Unknown("captured current tactic vector begin70");
        ITacticParameterDecoderTable table = state.CaptureTacticParameterDecoderTable(receiver)
            ?? throw Unknown("captured current tactic decoder vtable");
        var location = new TacticParameterByteLocation(begin, ordinal,
            unchecked(begin.ByteAddress + (ulong)((long)ordinal * 4)));
        int physical = Known(state.ReadTacticParameterPhysicalSlot(receiver), "fresh tactic physical index");
        byte encoded = Known(state.ReadTacticParameterEncodedByte(location), "captured tactic encoded byte location");
        return Known(table.Decode(receiver, encoded, rotation, physical), "captured tactic decoder result");
    }

    private static T Known<T>(T? value, string name) where T : struct => value ?? throw Unknown(name);
    private static InvalidDataException Unknown(string name) => new($"Original {name} is unknown.");
}

/// <summary>Explicit immutable successful current-CE decoder profile for the
/// inspected slot9 leaf D9D0. Choosing this table is not native type detection.</summary>
public sealed class TacticCeParameterDecoderTable : ITacticParameterDecoderTable
{
    public static TacticCeParameterDecoderTable Instance { get; } = new();
    private TacticCeParameterDecoderTable() { }

    public byte? Decode(TacticReference receiver, byte encodedLowByte, byte rotation, int physicalSlot)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        // The source leaf uses low8 rotation and x86 masks the shift counts.
        // These four getters provide only rotations 7,1,3,5, respectively.
        int left = rotation & 31, right = unchecked(8 - rotation) & 31;
        uint value = encodedLowByte;
        uint rotated = (value << left) | (value >> right);
        return unchecked((byte)(rotated ^ (byte)physicalSlot));
    }
}
