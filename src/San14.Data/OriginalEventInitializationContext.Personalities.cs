namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalPersonalityNormalizationState
{
    private readonly Dictionary<int,byte?[]> _personalityFields = [];

    public OriginalPersonalityReference? ReadPersonalityTableReference(int slot)
    {
        Check(slot,356,"personality table slot");
        return _staticTablePointers.TryGetValue((0x7DD18,slot),out int? physical) ? new(physical) : null;
    }
    public int? ReadPersonalityPhysicalSlot(OriginalPersonalityReference personality)
        => MapPhysical(personality.PhysicalSlot,356,"personality");
    public byte? ReadPersonalityByte(OriginalPersonalityReference personality, int offset)
    {
        Check(offset,0xE1,"personality byte offset");
        int physical = MapPhysical(personality.PhysicalSlot,356,"personality");
        return _personalityFields.TryGetValue(physical,out byte?[]? fields) ? fields[offset] : null;
    }
    public ushort? ReadPersonalityWord(OriginalPersonalityReference personality, int offset)
    {
        byte? low = ReadPersonalityByte(personality,offset), high = ReadPersonalityByte(personality,offset+1);
        return low.HasValue && high.HasValue ? (ushort)(low.Value|high.Value<<8) : null;
    }
    public void WritePersonalityByte(OriginalPersonalityReference personality, int offset, byte value)
        => SetPersonalityByte(MapPhysical(personality.PhysicalSlot,356,"personality"),offset,value);
    public void SetPersonalityByte(int physical, int offset, byte? value)
    {
        Check(physical,356,"personality physical slot");Check(offset,0xE1,"personality byte offset");
        if (!_personalityFields.TryGetValue(physical,out byte?[]? fields)) _personalityFields[physical]=fields=new byte?[0xE1];
        fields[offset]=value;
    }
    public void WritePersonalityWord(OriginalPersonalityReference personality, int offset, ushort value)
    {
        if (offset < 0 || offset >= 0xE0) throw new InvalidDataException("Personality word offset is outside the supported current layout.");
        WritePersonalityByte(personality,offset,(byte)value);
        WritePersonalityByte(personality,offset+1,(byte)(value>>8));
    }
    public void NormalizePersonalityTable() => OriginalPersonalityNormalization.ApplyManagerTable(this);
    /// <summary>2253A0, the independent slot7 signed-word clamp. This does
    /// not repeat217C60 or change the manager's classification flags.</summary>
    public void PostProcessPersonality(OriginalPersonalityReference personality)
    {
        foreach (int offset in new[] { 0xB8, 0xBC })
        {
            ushort raw = ReadPersonalityWord(personality, offset)
                ?? throw new InvalidDataException($"Current personality word{offset:X} is unknown at slot7.");
            short value = Math.Clamp(unchecked((short)raw), (short)-100, (short)2000);
            WritePersonalityWord(personality, offset, unchecked((ushort)value));
        }
    }
}
