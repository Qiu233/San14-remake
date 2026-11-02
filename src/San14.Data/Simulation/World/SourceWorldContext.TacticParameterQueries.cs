namespace San14.Data;

public sealed partial class SourceWorldContext : ITacticParameterQueryState
{
    /// <summary>Required current decoder-table profile. Unbound means unknown;
    /// raw-fallback queries never require or inspect this table.</summary>
    public ITacticParameterDecoderTable? TacticParameterDecoderTable { get; set; }

    /// <summary>Optional fresh physical-index source, invoked after the buffer
    /// and decoder table have been captured. Default uses the normal owned
    /// current-reference identity already shared with skill queries.</summary>
    public Func<TacticReference, int?>? TacticParameterPhysicalSlotReader { get; set; }

    public ulong? ReadTacticParameterEnd78(TacticReference receiver)
    {
        IReadOnlyList<int>? values = OwnedTacticParameterValues(receiver);
        return values is null ? null : checked((ulong)values.Count * 4);
    }

    public TacticParameterBufferReference? ReadTacticParameterBegin70(TacticReference receiver)
    {
        // Capture the actual shared backing object, without the public getter's
        // defensive copy and without reading an element before the helper.
        IReadOnlyList<int>? values = OwnedTacticParameterValues(receiver);
        return values is null ? null : new(0, values);
    }

    public ITacticParameterDecoderTable? CaptureTacticParameterDecoderTable(TacticReference receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        return TacticParameterDecoderTable;
    }

    public int? ReadTacticParameterPhysicalSlot(TacticReference receiver)
        => TacticParameterPhysicalSlotReader is null ? ReadSkillTacticPhysicalSlot(receiver) :
            TacticParameterPhysicalSlotReader(receiver);

    public byte? ReadTacticParameterEncodedByte(TacticParameterByteLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        // This adapter owns ordinary int buffers; it does not emulate arbitrary
        // native memory surrounding an invalid element address.
        if (location.Begin.ByteAddress != 0 || location.Begin.StorageIdentity is not IReadOnlyList<int> values ||
            location.ElementOrdinal < 0 || location.ElementOrdinal >= values.Count ||
            location.ByteAddress != unchecked((ulong)((long)location.ElementOrdinal * 4))) return null;
        return unchecked((byte)values[location.ElementOrdinal]);
    }

    public byte? ReadTacticParameterRawByte(TacticReference receiver, int nativeOffset)
        => ReadSkillTacticRawByte(receiver, nativeOffset);

    public byte ReadTacticEffectOpcode(TacticReference receiver, int index)
        => TacticParameterQueries.ReadEffectOpcode(receiver, index, this);
    public byte ReadTacticRaw64(TacticReference receiver, int index)
        => TacticParameterQueries.ReadRaw64(receiver, index, this);
    public byte ReadTacticEffectValue(TacticReference receiver, int index)
        => TacticParameterQueries.ReadEffectValue(receiver, index, this);
    public byte ReadTacticRaw68(TacticReference receiver, int index)
        => TacticParameterQueries.ReadRaw68(receiver, index, this);

    private IReadOnlyList<int>? OwnedTacticParameterValues(TacticReference receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        int physical = MapPhysical(receiver.PhysicalSlot, 201, "tactic parameter owned receiver");
        return ReadSkillTacticCodes(physical);
    }
}
