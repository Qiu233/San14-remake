namespace San14.Data;

public sealed partial class SourceWorldContext : IPersonAttributePostLoadState
{
    private readonly Dictionary<(int Person, int Offset), byte?> _personAttributeBytes = [];
    public int? ReadPersonPostLoadPhysicalSlot(int receiver)
    { Check(receiver, 1851, "person attribute receiver"); return receiver; }
    public byte? ReadPersonPostLoadByte(int receiver, int offset)
    { CheckPersonAttributeField(receiver, offset); return _personAttributeBytes.GetValueOrDefault((receiver, offset)); }
    public void WritePersonPostLoadByte(int receiver, int offset, byte value) => SetPersonPostLoadByte(receiver, offset, value);
    public void SetPersonPostLoadByte(int receiver, int offset, byte? value)
    { CheckPersonAttributeField(receiver, offset); _personAttributeBytes[(receiver, offset)] = value; }
    public void InitializePersonAttributeCaps(int receiver) => PersonAttributePostLoad.WriteCaps(receiver, this);
    public void NormalizePersonAttributeValues(int receiver) => PersonAttributePostLoad.NormalizeCostAndAbilities(receiver, this);
    private static void CheckPersonAttributeField(int receiver, int offset)
    {
        Check(receiver, 1851, "person attribute receiver");
        if (offset is not (>= 0x124 and <= 0x128) && offset is not (0x1BA or 0x224 or 0x225))
            throw new InvalidDataException("This person attribute view does not cover the requested native field.");
    }
}
