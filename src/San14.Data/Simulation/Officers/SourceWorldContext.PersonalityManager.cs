using System.Text;

namespace San14.Data;

public sealed partial class SourceWorldContext : IPersonalityManagerState
{
    private readonly Dictionary<int,int?> _customPersonalityPointers = [];
    private readonly Dictionary<int,CustomPersonalityRecord> _customPersonalityRecords = [];
    public bool? PersonalityManagerPresent { get; set; }

    /// <summary>Explicit empty configuration for this independent program's
    /// manager. This is not an inference about original installed user settings.</summary>
    public void InitializeEmptyCustomPersonalityManager()
    {
        PersonalityManagerPresent=true;
        for (int slot=0;slot<100;slot++) SetCustomPersonalityTablePointer(slot,null);
    }
    public bool? ReadPersonalityManagerPresent() => PersonalityManagerPresent;
    public void SetCustomPersonalityTablePointer(int slot,int? physical)
        => SetPointer(_customPersonalityPointers,slot,100,physical,100);
    public CustomPersonalityReference? ReadCustomPersonalityReference(int slot)
    {
        Check(slot,100,"custom personality table slot");
        return _customPersonalityPointers.TryGetValue(slot,out int? physical) ? new(physical) : null;
    }
    public void SetCustomPersonalityRecord(CustomPersonalityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);Check(record.PhysicalSlot,100,"custom personality receiver");
        _customPersonalityRecords[record.PhysicalSlot]=record;
    }
    private CustomPersonalityRecord? CustomPersonality(CustomPersonalityReference source)
        => _customPersonalityRecords.GetValueOrDefault(MapPhysical(source.PhysicalSlot,100,"custom personality"));
    public int? ReadCustomPersonalityType(CustomPersonalityReference source) => CustomPersonality(source)?.TypeCode;
    public ushort? ReadCustomPersonalityTargetSlot(CustomPersonalityReference source) => CustomPersonality(source)?.TargetSlot;
    public string? ReadCustomPersonalityName(CustomPersonalityReference source) => CustomPersonality(source)?.Name;
    public uint? ReadCustomPersonalityUInt32(CustomPersonalityReference source,int offset) => offset switch
    {
        0x38=>CustomPersonality(source)?.RawField38,0x3C=>CustomPersonality(source)?.RawField3C,
        0x40=>CustomPersonality(source)?.RawField40,_=>throw new InvalidDataException("Unsupported custom trait dword field.")
    };
    public byte? ReadCustomPersonalityByte(CustomPersonalityReference source,int offset) => offset switch
    {
        0x48=>CustomPersonality(source)?.RawField48,0x4C=>CustomPersonality(source)?.RawField4C,
        _=>throw new InvalidDataException("Unsupported custom trait byte field.")
    };
    public short? ReadCustomPersonalityInt16(CustomPersonalityReference source,int offset) => offset switch
    {
        0x4A=>CustomPersonality(source)?.RawField4A,0x4E=>CustomPersonality(source)?.RawField4E,
        _=>throw new InvalidDataException("Unsupported custom trait signed word field.")
    };
    public void WritePersonalityUInt32(PersonalityReference target,int offset,uint value)
    {
        if (offset<0 || offset>0xDD)throw new InvalidDataException("Personality dword is outside the current layout.");
        for (int index=0;index<4;index++)WritePersonalityByte(target,offset+index,(byte)(value>>(index*8)));
    }
    public void WritePersonalityName(PersonalityReference target,string sourceCString)
    {
        ArgumentNullException.ThrowIfNull(sourceCString);
        int terminator=sourceCString.IndexOf('\0');
        if (terminator>=0)sourceCString=sourceCString[..terminator];
        int count=Math.Min(6,sourceCString.Length+1);
        for (int index=0;index<count;index++)
            WritePersonalityWord(target,0x10+index*2,index<sourceCString.Length ? sourceCString[index] : (ushort)0);
    }
    public string ReadPersonalityName(PersonalityReference target)
    {
        var name=new StringBuilder(6);
        for (int index=0;index<6;index++)
        {
            ushort unit=ReadPersonalityWord(target,0x10+index*2)
                ?? throw new InvalidDataException("Current personality name unit is unknown.");
            if (unit==0)break;
            name.Append((char)unit);
        }
        return name.ToString();
    }
    public void ApplyPersonalityManager() => PersonalityManager.Apply(this);
}
