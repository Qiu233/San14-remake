using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class OriginalEventInitializationContext
{
    private static readonly (int Source, int Native, int Bytes)[] SkillTacticFixedFields =
    [
        (0,0x10,10), (10,0x1A,38), (48,0x40,1), (49,0x42,2),
        (51,0x44,1), (52,0x46,18), (70,0x58,10), (80,0x62,2),
        (82,0x64,2), (84,0x66,2), (86,0x68,2), (88,0x6A,1),
        (89,0x6C,4), (93,0x88,1)
    ];
    private readonly Dictionary<int, byte?[]> _skillTacticDefinitionBytes = [], _skillGemDefinitionBytes = [];
    private readonly Dictionary<int, int> _skillTacticVectorCapacities = [];
    public SourceProvenance? SkillDefinitionSource { get; private set; }
    public string? SkillDefinitionDecodedSha256 { get; private set; }
    public Func<int, int, bool?>? SkillTacticVectorReservation { get; set; }

    /// <summary>Explicit successful normal construction/reset of201 tactics and
    /// the inspected constructor52/53 bytes of31 gems. Current pointers are separate.</summary>
    public void InitializeSkillDefinitionStorage()
    {
        for (int physical = 0; physical < 201; physical++) ResetSkillTacticDefinition(physical);
        for (int physical = 0; physical < S14GemReader.Count; physical++)
        { SetSkillGemRawByte(physical, 0x52, 0); SetSkillGemRawByte(physical, 0x53, 0); }
    }

    public void ResetSkillTacticDefinition(int physical)
    {
        Check(physical, 201, "skill tactic reset receiver");
        foreach (var field in SkillTacticFixedFields)
            for (int index = 0; index < field.Bytes; index++)
                SetSkillTacticRawByte(physical, field.Native + index, 0);
        //2270B0 clears length, releases the old buffer and reserves10.
        //04DC30 preserves logical length; reservation does not add elements.
        _tacticEncodedValues70[physical] = new List<int>();
        _skillTacticVectorCapacities[physical] = 0;
        ReserveSkillTacticVector(physical, 10);
    }

    public byte? ReadSkillTacticRawByte(OriginalTacticReference reference, int offset)
    {
        SkillTacticByteOffset(offset);
        int physical = MapPhysical(reference.PhysicalSlot, 201, "skill tactic raw receiver");
        if (offset == 0x44) return _tacticFlags44.GetValueOrDefault(physical);
        return _skillTacticDefinitionBytes.TryGetValue(physical, out var fields) ? fields[offset] : null;
    }
    public void SetSkillTacticRawByte(int physical, int offset, byte? value)
    {
        Check(physical, 201, "skill tactic raw receiver"); SkillTacticByteOffset(offset);
        if (!_skillTacticDefinitionBytes.TryGetValue(physical, out var fields))
            _skillTacticDefinitionBytes[physical] = fields = new byte?[0x89];
        fields[offset] = value;
        if (offset == 0x44) _tacticFlags44[physical] = value;
    }
    public IReadOnlyList<int>? ReadSkillTacticEncodedValues70(int physical)
    {
        Check(physical, 201, "skill tactic vector receiver");
        if (!_tacticEncodedValues70.TryGetValue(physical, out var values) || values is null) return null;
        return values is List<int> list ? list.AsReadOnly() : Array.AsReadOnly(values.ToArray());
    }
    public int? ReadSkillTacticVectorCapacity(int physical)
    { Check(physical, 201, "skill tactic vector receiver"); return _skillTacticVectorCapacities.TryGetValue(physical, out int value) ? value : null; }

    public byte? ReadSkillGemRawByte(OriginalGemReference reference, int offset)
    {
        SkillGemByteOffset(offset);
        int physical = MapPhysical(reference.PhysicalSlot, S14GemReader.Count, "skill gem raw receiver");
        if (offset == 0x52) return _gemTacticCodes52.GetValueOrDefault(physical);
        if (offset == 0x53) return _gemValidityCodes53.GetValueOrDefault(physical);
        return _skillGemDefinitionBytes.TryGetValue(physical, out var fields) ? fields[offset] : null;
    }
    private void SetSkillGemRawByte(int physical, int offset, byte? value)
    {
        Check(physical, S14GemReader.Count, "skill gem raw receiver"); SkillGemByteOffset(offset);
        if (!_skillGemDefinitionBytes.TryGetValue(physical, out var fields))
            _skillGemDefinitionBytes[physical] = fields = new byte?[0x54];
        fields[offset] = value;
        if (offset == 0x52) _gemTacticCodes52[physical] = value;
        if (offset == 0x53) _gemValidityCodes53[physical] = value;
    }

    public void MaterializeFixedSkillDefinitions(LoadedSkillDefinitions loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ValidateSkillDefinitionCatalogs(loaded);
        foreach (S14TacticDefinition row in loaded.Formations.Tactics)
        {
            OriginalTacticReference reference = ReadSkillTacticReference(row.Slot)
                ?? throw new InvalidDataException("Current fixed tactic table pointer is unknown.");
            int physical = MapPhysical(reference.PhysicalSlot, 201, "fixed tactic receiver");
            foreach (var field in SkillTacticFixedFields)
            {
                //The native two-byte groups are read completely before either
                //append/allocation callback. Preserve that saved local input.
                byte[] input = row.RawBytes.AsSpan(field.Source, field.Bytes).ToArray();
                int rotation = field.Native switch
                { 0x44 => 1, 0x62 => 7, 0x64 => 1, 0x66 => 3, 0x68 => 5, 0x6A => 7, _ => 0 };
                for (int index = 0; index < input.Length; index++)
                {
                    if (rotation != 0) AppendSkillTacticSourceCode(physical, input[index], rotation);
                    SetSkillTacticRawByte(physical, field.Native + index, input[index]);
                }
            }
        }
        foreach (S14GemDefinition row in loaded.Gems.Gems)
        {
            OriginalGemReference reference = ReadSkillGemReference(row.Slot)
                ?? throw new InvalidDataException("Current fixed gem table pointer is unknown.");
            int physical = MapPhysical(reference.PhysicalSlot, S14GemReader.Count, "fixed gem receiver");
            for (int index = 0; index < 14; index++) SetSkillGemRawByte(physical, 0x10 + index, row.RawBytes[index]);
            SetSkillGemRawByte(physical, 0x52, row.RawBytes[14]);
            SetSkillGemRawByte(physical, 0x53, row.RawBytes[15]);
            for (int index = 0; index < 5; index++) SetSkillGemRawByte(physical, 0x4D + index, row.RawBytes[16 + index]);
        }
        SkillDefinitionSource = loaded.Source;
        SkillDefinitionDecodedSha256 = loaded.Formations.DecodedSha256;
    }

    private void AppendSkillTacticSourceCode(int physical, byte raw, int rotation)
    {
        IReadOnlyList<int> values = _tacticEncodedValues70.GetValueOrDefault(physical)
            ?? throw new InvalidDataException("Current tactic70 vector is unknown; construction/reset must precede fixed loading.");
        if (!_skillTacticVectorCapacities.TryGetValue(physical, out int capacity))
            throw new InvalidDataException("Current tactic70 vector capacity is unknown.");
        if (values.Count > capacity) throw new InvalidDataException("Current tactic70 length exceeds its owned capacity.");
        if (values.Count == capacity)
            ReserveSkillTacticVector(physical, Math.Max(checked(values.Count + 1), checked(capacity + capacity / 2)));
        values = _tacticEncodedValues70[physical]
            ?? throw new InvalidDataException("Current tactic70 vector is unknown after reservation.");
        if (values is not List<int> mutable)
        {
            mutable = new List<int>(_skillTacticVectorCapacities[physical]);
            mutable.AddRange(values);
            _tacticEncodedValues70[physical] = mutable;
        }
        byte mixed = (byte)(raw ^ (byte)physical);
        int encoded = (byte)((mixed >> rotation) | (mixed << (8 - rotation)));
        mutable.Add(encoded);
    }
    private void ReserveSkillTacticVector(int physical, int capacity)
    {
        IReadOnlyList<int> values = _tacticEncodedValues70.GetValueOrDefault(physical)
            ?? throw new InvalidDataException("Current tactic70 vector is unknown during reservation.");
        if (SkillTacticVectorReservation is not null && !(SkillTacticVectorReservation(physical, capacity)
            ?? throw new InvalidDataException("Current tactic70 vector reservation outcome is unknown.")))
            throw new InvalidDataException("Current tactic70 vector reservation failed.");
        var replacement = new List<int>(capacity);
        replacement.AddRange(values);
        _tacticEncodedValues70[physical] = replacement;
        _skillTacticVectorCapacities[physical] = capacity;
    }

    private static void ValidateSkillDefinitionCatalogs(LoadedSkillDefinitions loaded)
    {
        var tactics = loaded.Formations;
        var gems = loaded.Gems;
        if (tactics.FormatMarker != 0x66 || tactics.TacticRecordOffset != 188569 ||
            tactics.TacticRecordStride != 94 || tactics.Tactics.Count != 201 ||
            tactics.Tactics.Where((row, slot) => row.Slot != slot || row.RawBytes.Length != 94 ||
                row.FormationMask != BinaryPrimitives.ReadUInt32LittleEndian(row.RawBytes.AsSpan(89, 4))).Any() ||
            gems.FormatMarker != 0x66 || gems.RecordOffset != S14GemReader.RecordStart ||
            gems.RecordStride != S14GemReader.RecordStride || gems.Gems.Count != S14GemReader.Count ||
            gems.Gems.Where((row, slot) => row.Slot != slot || row.RawBytes.Length != S14GemReader.RecordStride ||
                row.RawField52 != row.RawBytes[14] || row.RawField53 != row.RawBytes[15]).Any() ||
            !tactics.DecodedSha256.Equals(gems.DecodedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Skill definition source requires complete consistent CE tactic201x94 and gem31x21 catalogs from the same payload.");
    }
    private static void SkillTacticByteOffset(int offset)
    {
        if (offset is >= 0x10 and <= 0x40 || offset is 0x42 or 0x43 or 0x44 ||
            offset is >= 0x46 and <= 0x6A || offset is >= 0x6C and <= 0x6F || offset == 0x88) return;
        throw new InvalidDataException("Tactic byte is outside the inspected fixed definition fields.");
    }
    private static void SkillGemByteOffset(int offset)
    {
        if (offset is >= 0x10 and <= 0x1D || offset is >= 0x4D and <= 0x53) return;
        throw new InvalidDataException("Gem byte is outside the inspected fixed definition fields.");
    }
}
