namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalPersonSkillPostLoadState
{
    private readonly Dictionary<int, uint?> _personFormationMasks164 = [];
    private readonly Dictionary<(int Person, int Index), ushort?> _personTraitSlots150 = [], _personTraitExtras1BE = [];
    private readonly Dictionary<int, ushort?> _personTraitMasks1BC = [];
    private readonly Dictionary<(int Person, int Index), byte?> _personTacticSlots168 = [];
    private readonly Dictionary<int, byte?> _tacticFlags44 = [], _gemTacticCodes52 = [], _gemValidityCodes53 = [];
    private readonly Dictionary<int, IReadOnlyList<int>?> _tacticEncodedValues70 = [];

    /// <summary>Load the CE static windows without resetting omitted runtime
    /// mask1BC/extra1BE fields or reconstructing raw slots from UI projections.</summary>
    private void MaterializePersonSkillSource(CharacterPreviewRecord person)
    {
        var source = person.SkillSource;
        if (source is not null && (source.RecordSlot != person.Slot || source.CharacterId != person.CharacterId ||
            source.RawPersonalitySlots150.Count != 9 || source.RawTacticSlots168.Count != 10 || source.RawFieldBytes.Length != 32))
            throw new InvalidDataException("Current person skill source has contradictory identity or field windows.");
        for (int index = 0; index < 9; index++) SetPersonTraitSlot150(person.Slot, index, source?.RawPersonalitySlots150[index]);
        SetPersonFormationMask164(person.Slot, source?.RawFormationMask164);
        for (int index = 0; index < 10; index++) SetPersonTacticSlot168(person.Slot, index, source?.RawTacticSlots168[index]);
    }

    public uint? ReadPersonFormationMask164(int receiver) { PersonSkillIndex(receiver, 0, 1); return _personFormationMasks164.GetValueOrDefault(receiver); }
    public void WritePersonFormationMask164(int receiver, uint value) => SetPersonFormationMask164(receiver, value);
    public void SetPersonFormationMask164(int receiver, uint? value) { PersonSkillIndex(receiver, 0, 1); _personFormationMasks164[receiver] = value; }
    public ushort? ReadPersonTraitSlot150(int receiver, int index) { PersonSkillIndex(receiver, index, 9); return _personTraitSlots150.GetValueOrDefault((receiver, index)); }
    public void WritePersonTraitSlot150(int receiver, int index, ushort value) => SetPersonTraitSlot150(receiver, index, value);
    public void SetPersonTraitSlot150(int receiver, int index, ushort? value) { PersonSkillIndex(receiver, index, 9); _personTraitSlots150[(receiver, index)] = value; }
    public ushort? ReadPersonTraitMask1BC(int receiver) { PersonSkillIndex(receiver, 0, 1); return _personTraitMasks1BC.GetValueOrDefault(receiver); }
    public void WritePersonTraitMask1BC(int receiver, ushort value) => SetPersonTraitMask1BC(receiver, value);
    public void SetPersonTraitMask1BC(int receiver, ushort? value) { PersonSkillIndex(receiver, 0, 1); _personTraitMasks1BC[receiver] = value; }
    public ushort? ReadPersonTraitExtra1BE(int receiver, int index) { PersonSkillIndex(receiver, index, 9); return _personTraitExtras1BE.GetValueOrDefault((receiver, index)); }
    public void WritePersonTraitExtra1BE(int receiver, int index, ushort value) => SetPersonTraitExtra1BE(receiver, index, value);
    public void SetPersonTraitExtra1BE(int receiver, int index, ushort? value) { PersonSkillIndex(receiver, index, 9); _personTraitExtras1BE[(receiver, index)] = value; }
    public byte? ReadPersonTacticSlot168(int receiver, int index) { PersonSkillIndex(receiver, index, 10); return _personTacticSlots168.GetValueOrDefault((receiver, index)); }
    public void WritePersonTacticSlot168(int receiver, int index, byte value) => SetPersonTacticSlot168(receiver, index, value);
    public void SetPersonTacticSlot168(int receiver, int index, byte? value) { PersonSkillIndex(receiver, index, 10); _personTacticSlots168[(receiver, index)] = value; }

    /// <summary>Only226AA0's proved trait150/mask1BC/extra1BE zero windows.
    /// Static loading does not reset these omitted auxiliary fields.</summary>
    public void ResetPersonTraitFields(int receiver)
    {
        for (int index = 0; index < 9; index++) SetPersonTraitSlot150(receiver, index, 0);
        SetPersonTraitMask1BC(receiver, 0);
        for (int index = 0; index < 9; index++) SetPersonTraitExtra1BE(receiver, index, 0);
    }
    public bool? ReadSkillPersonalityValidity(OriginalPersonalityReference reference)
    {
        if (!reference.PhysicalSlot.HasValue) return false;
        int physical = MapPhysical(reference.PhysicalSlot, 356, "skill personality receiver");
        if (physical == 0) return false;
        if (physical is < 256 or > 355) return true;
        byte? value = ReadPersonalityByte(reference, 0xB6);
        return value.HasValue ? value.Value != 0 : null;
    }
    public byte? ReadSkillPersonalityFlags(OriginalPersonalityReference reference)
    {
        int physical = MapPhysical(reference.PhysicalSlot, 356, "skill personality receiver");
        if (physical is >= 256 and <= 355) return ReadPersonalityByte(reference, 0xBF);
        var values = ReadPersonalitySourceCodes(physical);
        if (values is null) return null;
        if (values.Count != 1) return ReadPersonalityByte(reference, 0xBF);
        ushort encoded = unchecked((ushort)values[0]);
        ushort rotated = (ushort)((encoded << 1) | (encoded >> 15));
        return (byte)(rotated ^ (ushort)physical);
    }
    public OriginalTacticReference? ReadSkillTacticReference(int slot)
    { var reference = ReadStaticTableReference(0x774D0, slot); return reference is null ? null : new(reference.PhysicalSlot); }
    public bool? ReadSkillTacticValidity(OriginalTacticReference reference)
        => reference.PhysicalSlot.HasValue && MapPhysical(reference.PhysicalSlot, 201, "skill tactic receiver") != 0;
    public int? ReadSkillTacticPhysicalSlot(OriginalTacticReference reference) => MapPhysical(reference.PhysicalSlot, 201, "skill tactic receiver");
    public byte? ReadSkillTacticFlags(OriginalTacticReference reference)
    {
        int physical = MapPhysical(reference.PhysicalSlot, 201, "skill tactic receiver");
        if (!_tacticEncodedValues70.TryGetValue(physical, out var values) || values is null) return null;
        if (values.Count != 10) return _tacticFlags44.GetValueOrDefault(physical);
        byte encoded = unchecked((byte)values[0]);
        return (byte)(((encoded << 1) | (encoded >> 7)) ^ (byte)physical);
    }
    public void SetSkillTacticFlags44(int physical, byte? value) { Check(physical, 201, "tactic receiver"); _tacticFlags44[physical] = value; }
    public void SetSkillTacticEncodedValues(int physical, IReadOnlyList<int>? values)
    { Check(physical, 201, "tactic receiver"); _tacticEncodedValues70[physical] = values?.ToArray(); }
    public OriginalGemReference? ReadSkillGemReference(int slot)
    { var reference = ReadStaticTableReference(0x79118, slot); return reference is null ? null : new(reference.PhysicalSlot); }
    public bool? ReadSkillGemValidity(OriginalGemReference reference)
    {
        if (!reference.PhysicalSlot.HasValue) return false;
        int physical = MapPhysical(reference.PhysicalSlot, 31, "gem receiver");
        if (physical == 0) return false;
        byte? flag = _gemValidityCodes53.GetValueOrDefault(physical);
        return flag.HasValue ? flag.Value != 0 : null;
    }
    public byte? ReadSkillGemTactic52(OriginalGemReference reference) => _gemTacticCodes52.GetValueOrDefault(MapPhysical(reference.PhysicalSlot, 31, "gem receiver"));
    public void SetSkillGemFields(int physical, byte? tactic52, byte? validity53)
    { Check(physical, 31, "gem receiver"); _gemTacticCodes52[physical] = tactic52; _gemValidityCodes53[physical] = validity53; }

    public OriginalHexListNodeReference? ReadSkillTemporaryHead(OriginalHexListHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!handle.Identity.HasValue || !KnownPoolPresent()) return new(null);
        //224A1D reads the saved descriptor's current key before the head table.
        int key = RegistryKey(new(handle.Identity));
        bool heads = (RegistryHeadTablePresenceReader is null ? RegistryHeadTablePresent : RegistryHeadTablePresenceReader())
            ?? throw new InvalidDataException("Current personality temporary head table is unknown.");
        return heads && NativeListKeyInRange(key) ? ReadHexListHead(new(key)) : new(null);
    }
    public OriginalPersonalityReference? ReadSkillTemporaryPayload(OriginalHexListNodeReference node)
        => ReadNativeListPayload<OriginalPersonalityReference>(NodeIdentity(node));
    public bool AppendSkillTemporaryPersonality(OriginalHexListHandle handle, OriginalPersonalityReference reference)
    {
        int key = RegistryKey(new(handle.Identity));
        OriginalHexListNodeReference node = AllocateNativeListNode(key);
        if (!node.Identity.HasValue) return false;
        WriteCurrentListPayload(NodeIdentity(node), reference);
        return true;
    }
    public OriginalPersonRoleAndPositionResult PostProcessPerson(int receiver)
    {
        InitializePersonAttributeCaps(receiver);
        OriginalHexListHandle temporary = OriginalPersonSkillPostLoad.Apply(receiver, this);
        NormalizePersonAttributeValues(receiver);
        OriginalPersonRoleAndPositionResult result = RepairPersonRoleAndLocations(receiver);
        if (temporary.Identity.HasValue && KnownPoolPresent())
        {
            ConditionalRegistryEnter();
            int key = RegistryKey(new(temporary.Identity));
            bool heads = (RegistryHeadTablePresenceReader is null ? RegistryHeadTablePresent : RegistryHeadTablePresenceReader())
                ?? throw new InvalidDataException("Current skill cleanup head table is unknown.");
            if (heads && NativeListKeyInRange(key)) ClearAddressableHexList(key);
            ReleaseTemporaryHexList(temporary);
            ConditionalRegistryExit();
        }
        return result;
    }
    private static void PersonSkillIndex(int receiver, int index, int count)
    { Check(receiver, 1851, "person skill receiver"); Check(index, count, "person skill index"); }
}
