namespace San14.Data;

public sealed partial class SourceWorldContext : IPersonSkillPostLoadState
{
    private readonly Dictionary<int, uint?> _personFormationMasks164 = [];
    private readonly Dictionary<(int Person, int Index), ushort?> _personTraitSlots150 = [], _personTraitExtras1BE = [];
    private readonly Dictionary<int, ushort?> _personTraitMasks1BC = [];
    private readonly Dictionary<(int Person, int Index), byte?> _personTacticSlots168 = [];
    private readonly Dictionary<int, byte?> _tacticFlags44 = [], _gemTacticCodes52 = [], _gemValidityCodes53 = [];
    private readonly Dictionary<int, IReadOnlyList<int>?> _tacticEncodedValues70 = [];
    internal Func<int, byte?>? CurrentTacticQualificationFlagsReader { get; set; }
    private PersonSkillInitialValues? _initialPersonFormations, _initialPersonTraits, _initialPersonActivation, _initialPersonTactics;
    // The completed projection transfers ownership of this set. Later overlays
    // replace it; no consumer mutates a set retained by another context.
    private HashSet<int>? _personFormationHiddenReceivers, _personTraitHiddenReceivers, _personTacticHiddenReceivers;
    private static bool PersonSkillProjected(HashSet<int>? receivers,int receiver)
        => receivers is null || !receivers.Contains(receiver);

    private void ResetOwnedPersonSkillProjection()
        => _personFormationHiddenReceivers = _personTraitHiddenReceivers = _personTacticHiddenReceivers = null;

    private void BindUnprojectedPersonSkillReceivers(HashSet<int> unprojectedReceivers)
    {
        foreach (int receiver in _personFormationMasks164.Keys.Where(unprojectedReceivers.Contains).ToArray())
            _personFormationMasks164.Remove(receiver);
        foreach (var key in _personTraitSlots150.Keys.Where(k=>unprojectedReceivers.Contains(k.Person)).ToArray())
            _personTraitSlots150.Remove(key);
        foreach (var key in _personTacticSlots168.Keys.Where(k=>unprojectedReceivers.Contains(k.Person)).ToArray())
            _personTacticSlots168.Remove(key);
        _personFormationHiddenReceivers = _personTraitHiddenReceivers = _personTacticHiddenReceivers = unprojectedReceivers;
    }


    internal void BindInitialPersonSkills(PersonSkillInitialValues values)
        => _initialPersonFormations = _initialPersonTraits = _initialPersonActivation = _initialPersonTactics = values;

    private static void StorePersonSkillOverride<TKey,TValue>(Dictionary<TKey,TValue?> fields,TKey key,TValue? value,
        bool initialKnown,TValue? initial) where TKey:notnull where TValue:struct
    {
        if (initialKnown && EqualityComparer<TValue?>.Default.Equals(value,initial)) fields.Remove(key);
        else fields[key]=value;
    }

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

    public uint? ReadPersonFormationMask164(int receiver) { PersonSkillIndex(receiver, 0, 1); return _personFormationMasks164.TryGetValue(receiver,out var value) ? value : PersonSkillProjected(_personFormationHiddenReceivers,receiver) ? _initialPersonFormations?.Formation(receiver) : null; }
    public void WritePersonFormationMask164(int receiver, uint value) => SetPersonFormationMask164(receiver, value);
    public void SetPersonFormationMask164(int receiver, uint? value) { PersonSkillIndex(receiver, 0, 1); StorePersonSkillOverride(_personFormationMasks164,receiver,value,_initialPersonFormations is not null || !PersonSkillProjected(_personFormationHiddenReceivers,receiver),PersonSkillProjected(_personFormationHiddenReceivers,receiver) ? _initialPersonFormations?.Formation(receiver) : null); }
    public ushort? ReadPersonTraitSlot150(int receiver, int index) { PersonSkillIndex(receiver, index, 9); return _personTraitSlots150.TryGetValue((receiver,index),out var value) ? value : PersonSkillProjected(_personTraitHiddenReceivers,receiver) ? _initialPersonTraits?.Trait(receiver,index) : null; }
    public void WritePersonTraitSlot150(int receiver, int index, ushort value) => SetPersonTraitSlot150(receiver, index, value);
    public void SetPersonTraitSlot150(int receiver, int index, ushort? value) { PersonSkillIndex(receiver, index, 9); StorePersonSkillOverride(_personTraitSlots150,(receiver,index),value,_initialPersonTraits is not null || !PersonSkillProjected(_personTraitHiddenReceivers,receiver),PersonSkillProjected(_personTraitHiddenReceivers,receiver) ? _initialPersonTraits?.Trait(receiver,index) : null); }
    public ushort? ReadPersonTraitMask1BC(int receiver) { PersonSkillIndex(receiver, 0, 1); return _personTraitMasks1BC.TryGetValue(receiver,out var value) ? value : _initialPersonActivation?.Mask(receiver); }
    public void WritePersonTraitMask1BC(int receiver, ushort value) => SetPersonTraitMask1BC(receiver, value);
    public void SetPersonTraitMask1BC(int receiver, ushort? value) { PersonSkillIndex(receiver, 0, 1); StorePersonSkillOverride(_personTraitMasks1BC,receiver,value,_initialPersonActivation is not null,_initialPersonActivation?.Mask(receiver)); }
    public ushort? ReadPersonTraitExtra1BE(int receiver, int index) { PersonSkillIndex(receiver, index, 9); return _personTraitExtras1BE.TryGetValue((receiver,index),out var value) ? value : _initialPersonActivation?.Extra(receiver,index); }
    public void WritePersonTraitExtra1BE(int receiver, int index, ushort value) => SetPersonTraitExtra1BE(receiver, index, value);
    public void SetPersonTraitExtra1BE(int receiver, int index, ushort? value) { PersonSkillIndex(receiver, index, 9); StorePersonSkillOverride(_personTraitExtras1BE,(receiver,index),value,_initialPersonActivation is not null,_initialPersonActivation?.Extra(receiver,index)); }
    public byte? ReadPersonTacticSlot168(int receiver, int index) { PersonSkillIndex(receiver, index, 10); return _personTacticSlots168.TryGetValue((receiver,index),out var value) ? value : PersonSkillProjected(_personTacticHiddenReceivers,receiver) ? _initialPersonTactics?.Tactic(receiver,index) : null; }
    public void WritePersonTacticSlot168(int receiver, int index, byte value) => SetPersonTacticSlot168(receiver, index, value);
    public void SetPersonTacticSlot168(int receiver, int index, byte? value) { PersonSkillIndex(receiver, index, 10); StorePersonSkillOverride(_personTacticSlots168,(receiver,index),value,_initialPersonTactics is not null || !PersonSkillProjected(_personTacticHiddenReceivers,receiver),PersonSkillProjected(_personTacticHiddenReceivers,receiver) ? _initialPersonTactics?.Tactic(receiver,index) : null); }

    /// <summary>Only226AA0's proved trait150/mask1BC/extra1BE zero windows.
    /// Static loading does not reset these omitted auxiliary fields.</summary>
    public void ResetPersonTraitFields(int receiver)
    {
        for (int index = 0; index < 9; index++) SetPersonTraitSlot150(receiver, index, 0);
        SetPersonTraitMask1BC(receiver, 0);
        for (int index = 0; index < 9; index++) SetPersonTraitExtra1BE(receiver, index, 0);
    }
    public bool? ReadSkillPersonalityValidity(PersonalityReference reference)
    {
        if (!reference.PhysicalSlot.HasValue) return false;
        int physical = MapPhysical(reference.PhysicalSlot, 356, "skill personality receiver");
        if (physical == 0) return false;
        if (physical is < 256 or > 355) return true;
        byte? value = ReadPersonalityByte(reference, 0xB6);
        return value.HasValue ? value.Value != 0 : null;
    }
    public byte? ReadSkillPersonalityFlags(PersonalityReference reference)
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
    public TacticReference? ReadSkillTacticReference(int slot)
    { var reference = ReadStaticTableReference(0x774D0, slot); return reference is null ? null : new(reference.PhysicalSlot); }
    public bool? ReadSkillTacticValidity(TacticReference reference)
        => reference.PhysicalSlot.HasValue && MapPhysical(reference.PhysicalSlot, 201, "skill tactic receiver") != 0;
    public int? ReadSkillTacticPhysicalSlot(TacticReference reference) => MapPhysical(reference.PhysicalSlot, 201, "skill tactic receiver");
    public byte? ReadSkillTacticFlags(TacticReference reference)
    {
        int physical = MapPhysical(reference.PhysicalSlot, 201, "skill tactic receiver");
        if (CurrentTacticQualificationFlagsReader is { } reader) return reader(physical);
        var values = ReadSkillTacticCodes(physical);
        if (values is null) return null;
        if (values.Count != 10) return ReadSkillTacticSourceFlags(physical);
        return DecodeSkillTacticFlags(physical, values[0]);
    }
    internal static byte DecodeSkillTacticFlags(int physical, int encoded)
    {
        byte value = unchecked((byte)encoded);
        return (byte)(((value << 1) | (value >> 7)) ^ (byte)physical);
    }
    public void SetSkillTacticFlags44(int physical, byte? value) { Check(physical, 201, "tactic receiver"); _tacticFlags44[physical] = value; }
    public void SetSkillTacticEncodedValues(int physical, IReadOnlyList<int>? values)
    { Check(physical, 201, "tactic receiver"); _tacticEncodedValues70[physical] = values?.ToArray(); }
    public GemReference? ReadSkillGemReference(int slot)
    { var reference = ReadStaticTableReference(0x79118, slot); return reference is null ? null : new(reference.PhysicalSlot); }
    public bool? ReadSkillGemValidity(GemReference reference)
    {
        if (!reference.PhysicalSlot.HasValue) return false;
        int physical = MapPhysical(reference.PhysicalSlot, 31, "gem receiver");
        if (physical == 0) return false;
        byte? flag = ReadSkillGemSourceCode(physical, 0x53);
        return flag.HasValue ? flag.Value != 0 : null;
    }
    public byte? ReadSkillGemTactic52(GemReference reference) => ReadSkillGemSourceCode(MapPhysical(reference.PhysicalSlot, 31, "gem receiver"), 0x52);
    public void SetSkillGemFields(int physical, byte? tactic52, byte? validity53)
    { Check(physical, 31, "gem receiver"); _gemTacticCodes52[physical] = tactic52; _gemValidityCodes53[physical] = validity53; }

    public HexListNodeReference? ReadSkillTemporaryHead(HexListHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!handle.Identity.HasValue || !KnownPoolPresent()) return new(null);
        //224A1D reads the saved descriptor's current key before the head table.
        int key = RegistryKey(new(handle.Identity));
        bool heads = (RegistryHeadTablePresenceReader is null ? RegistryHeadTablePresent : RegistryHeadTablePresenceReader())
            ?? throw new InvalidDataException("Current personality temporary head table is unknown.");
        return heads && NativeListKeyInRange(key) ? ReadHexListHead(new(key)) : new(null);
    }
    public PersonalityReference? ReadSkillTemporaryPayload(HexListNodeReference node)
        => ReadNativeListPayload<PersonalityReference>(NodeIdentity(node));
    public bool AppendSkillTemporaryPersonality(HexListHandle handle, PersonalityReference reference)
    {
        int key = RegistryKey(new(handle.Identity));
        HexListNodeReference node = AllocateNativeListNode(key);
        if (!node.Identity.HasValue) return false;
        WriteCurrentListPayload(NodeIdentity(node), reference);
        return true;
    }
    public PersonRoleAndPositionResult PostProcessPerson(int receiver)
    {
        InitializePersonAttributeCaps(receiver);
        HexListHandle temporary = PersonSkillPostLoad.Apply(receiver, this);
        NormalizePersonAttributeValues(receiver);
        PersonRoleAndPositionResult result = RepairPersonRoleAndLocations(receiver);
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
