namespace San14.Data;

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, byte?> _armyReturnPersonByte199 = [];
    private RegistryListHandle? _armyReturnPending70;
    /// <summary>Actual current external service-mode owner; absent whole value is unknown.</summary>
    public TransferModeState? ArmyReturnTransferMode { get; set; }
    public void SetArmyReturnPersonByte199(int person, byte? value)
    { Check(person, 1851, "return person byte199"); _armyReturnPersonByte199[person] = value; }
    public byte? ReadArmyReturnPersonByte199(int person)
    { Check(person, 1851, "return person byte199"); return _armyReturnPersonByte199.GetValueOrDefault(person); }
    public void SetArmyReturnPending70Descriptor(RegistryListHandle? value) => _armyReturnPending70 = value;
    public RegistryListHandle? ReadArmyReturnPending70Descriptor() => _armyReturnPending70;
    internal void WriteArmyReturnPersonFlags196(int person, ushort flags)
    {
        Check(person, 1851, "return person flags196");
        var old = _persons.GetValueOrDefault(person) ?? throw new InvalidDataException("Current return person fields are unknown.");
        _persons[person] = old with { Flags = flags };
    }
    //Mutable Home/trait/resource fields are already detached by HomeDraft.
    //These additional actual lookup/input windows let each source component
    //consume that SAME staged world rather than redirecting queries to live state.
    internal SourceWorldContext CreateArmyReturnTransactionDraft()
    {
        var draft = CreateArmyReturnHomeDraft(); CopyArmyReturnTransactionInputsTo(draft); return draft;
    }
    internal void CopyArmyReturnTransactionInputsTo(SourceWorldContext draft)
    {
        draft.StaticSource = StaticSource;
        Copy(_armyPointers, draft._armyPointers); Copy(_armyCommanderIds, draft._armyCommanderIds);
        Copy(_armyStatusFlags, draft._armyStatusFlags); Copy(_armyUnitHexes2A, draft._armyUnitHexes2A);
        //Type2/bit20 reaches the actual Person Return mode8 search inside
        //this same transaction. Keep its immutable terrain inputs present.
        CopyTransactionTerrainDefinitionsTo(draft);
        Copy(_hexSurfaceCodes, draft._hexSurfaceCodes);
        Copy(_surfaceTypePointers, draft._surfaceTypePointers); Copy(_surfaceTypeFields2A, draft._surfaceTypeFields2A);
        CopyArmyMapOccupancyTo(draft); Copy(_personTraitSlots150, draft._personTraitSlots150);
        Copy(_staticTablePointers, draft._staticTablePointers); Copy(_transferPersonPointers, draft._transferPersonPointers);
        Copy(_armyReturnPersonByte199, draft._armyReturnPersonByte199);
        Copy(_captureItemOwners4A, draft._captureItemOwners4A);
        foreach (var row in _personalityFields) draft._personalityFields[row.Key] = row.Value.ToArray();
        foreach (var row in _personalityVectors)
            draft._personalityVectors[row.Key] = new() { Values = row.Value.Values.ToList(), Capacity = row.Value.Capacity };
        draft._armyReturnPending70 = _armyReturnPending70; draft.ArmyReturnTransferMode = ArmyReturnTransferMode;
        CopyOwnedDestructionQueueViewTo(draft);
        draft.TransferService = TransferService; //Only proved no-service-write branch is admitted.
        static void Copy<K,V>(Dictionary<K,V> source, Dictionary<K,V> target) where K : notnull
        { foreach (var row in source) target[row.Key] = row.Value; }
    }
    internal void CommitArmyReturnTransactionFields(SourceWorldContext draft)
    {
        Copy(draft._persons, _persons); Copy(draft._personFirstLocations, _personFirstLocations);
        Copy(draft._personDistrictSlots, _personDistrictSlots); Copy(draft._cityDistricts, _cityDistricts);
        Copy(draft._armyReturnGovernorWords2E, _armyReturnGovernorWords2E);
        Copy(draft._baseResources, _baseResources); Copy(draft._baseMorale44, _baseMorale44);
        Copy(draft._armyReturnBaseWounded40, _armyReturnBaseWounded40); CommitArmyMapOccupancyFrom(draft);
        CommitArmyReturnTraitFields(draft);
        CommitAssignmentCurrentFields(draft);
        CommitBaseCaptureCurrentFields(draft);
        CommitBaseReturnCaptivesFields(draft);
        static void Copy<K,V>(Dictionary<K,V> source, Dictionary<K,V> target) where K : notnull
        { target.Clear(); foreach (var row in source) target.Add(row.Key, row.Value); }
    }
}
