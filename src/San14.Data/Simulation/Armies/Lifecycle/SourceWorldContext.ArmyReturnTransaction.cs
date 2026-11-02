namespace San14.Data;

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, San14.Core.OfficerHealth?> _officerHealth = [];
    private readonly Dictionary<int, int> _healthSourceIdentities = [];
    private RegistryListHandle? _armyReturnPending70;
    /// <summary>Actual current external service-mode owner; absent whole value is unknown.</summary>
    public TransferModeState? ArmyReturnTransferMode { get; set; }
    public void SetOfficerHealth(int officerId, San14.Core.OfficerHealth? value) => _officerHealth[officerId] = value;
    public San14.Core.OfficerHealth? ReadOfficerHealth(int officerId) => _officerHealth.GetValueOrDefault(officerId);
    internal void BindOfficerHealthSource(int physical, int officerId, San14.Core.OfficerHealth? value)
    {
        _healthSourceIdentities[physical] = officerId;
        SetOfficerHealth(officerId, value);
    }
    internal San14.Core.OfficerHealth? ReadBoundOfficerHealth(int physical) =>
        _healthSourceIdentities.TryGetValue(physical, out int identity) && ReadPersonIdentity(physical) == identity
            ? ReadOfficerHealth(identity) : null;
    public void SetArmyReturnPending70Descriptor(RegistryListHandle? value) => _armyReturnPending70 = value;
    public RegistryListHandle? ReadArmyReturnPending70Descriptor() => _armyReturnPending70;
    internal void WriteArmyReturnPersonFlags196(int person, ushort flags)
    {
        Check(person, 1851, "return person flags196");
        var old = _persons.GetValueOrDefault(person) ?? throw new InvalidDataException("Current return person fields are unknown.");
        _persons[person] = old with { Behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(old.Behavior, flags) };
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
        // Freedom's real cause4 writes this saved Army lifecycle byte bank.
        // Keep its known/unknown entries independent of the live current.
        Copy(_armyUnitFlags150, draft._armyUnitFlags150);
        //Type2/bit20 reaches the actual Person Return mode8 search inside
        //this same transaction. Keep its immutable terrain inputs present.
        CopyTransactionTerrainDefinitionsTo(draft);
        Copy(_hexSurfaceCodes, draft._hexSurfaceCodes);
        Copy(_surfaceTypePointers, draft._surfaceTypePointers); Copy(_surfaceTypeFields2A, draft._surfaceTypeFields2A);
        //Only movement costs were copied here, not terrain combat inputs.
        draft._initialSurfaceMovementRules = _initialSurfaceMovementRules;
        // Freedom placement reads current Area38, without admitting Area74,
        // neighbors or other initial Area channels to this return draft.
        Copy(_areaFlags38, draft._areaFlags38);
        draft._armyReturnAreaFlags38Source = _initialAreaScenarioValues ?? _armyReturnAreaFlags38Source;
        CopyArmyMapOccupancyTo(draft);
        draft._initialPersonTraits = _initialPersonTraits;
        draft._personTraitHiddenReceivers = _personTraitHiddenReceivers;
        Copy(_personTraitSlots150, draft._personTraitSlots150);
        draft._genericStaticPointerDefinitions = _genericStaticPointerDefinitions;
        Copy(_staticTablePointers, draft._staticTablePointers);
        Copy(_officerHealth, draft._officerHealth);
        Copy(_healthSourceIdentities, draft._healthSourceIdentities);
        Copy(_personPortraitAppearance, draft._personPortraitAppearance);
        Copy(_captureItemOwners4A, draft._captureItemOwners4A);
        Copy(_personDestructionLoyaltyE8, draft._personDestructionLoyaltyE8);
        Copy(rulerReleaseRelations13, draft.rulerReleaseRelations13);
        Copy(rulerReleasePopularity1BE, draft.rulerReleasePopularity1BE);
        Copy(_forceRanks47, draft._forceRanks47);
        Copy(_registryItemPointers, draft._registryItemPointers);
        Copy(_captureItemSubtypes47, draft._captureItemSubtypes47);
        Copy(_moraleWavePersonPostE7, draft._moraleWavePersonPostE7);
        draft._initialPersonAttributes = _initialPersonAttributes;
        Copy(_personAttributeBytes, draft._personAttributeBytes);
        Copy(_registryItemStatuses, draft._registryItemStatuses);
        Copy(_forceReportColors, draft._forceReportColors);
        Copy(_forceKingdomNames, draft._forceKingdomNames);
        draft._initialPersonalities = _initialPersonalities;
        foreach (var row in _personalityFields) draft._personalityFields[row.Key] = row.Value.ToArray();
        foreach (var row in _personalityVectors)
            draft._personalityVectors[row.Key] = new() { Values = row.Value.Values.ToList(), Capacity = row.Value.Capacity };
        draft._armyReturnPending70 = _armyReturnPending70; draft.ArmyReturnTransferMode = ArmyReturnTransferMode;
        draft.SetOwnedPersonCommandManager(ReadOwnedPersonCommandManager());
        CopyOwnedDestructionQueueViewTo(draft);
        // Service bitmaps/dates belong to this return transaction. External
        // clock/platform bindings are retained by the service's own Copy contract.
        draft.TransferService = TransferService?.Copy();
        static void Copy<K,V>(Dictionary<K,V> source, Dictionary<K,V> target) where K : notnull
        { foreach (var row in source) target[row.Key] = row.Value; }
    }
    internal void CommitArmyReturnTransactionFields(SourceWorldContext draft)
    {
        Copy(draft._persons, _persons); Copy(draft._personFirstLocations, _personFirstLocations);
        draft.CopyOfficerLocationsTo(this); draft.CopyArmyTargetsTo(this);
        Copy(draft._personDistrictSlots, _personDistrictSlots); Copy(draft._cityDistricts, _cityDistricts);
        Copy(draft._armyReturnGovernorWords2E, _armyReturnGovernorWords2E);
        Copy(draft._baseResources, _baseResources); Copy(draft._baseMorale44, _baseMorale44);
        Copy(draft._armyReturnBaseWounded40, _armyReturnBaseWounded40); CommitArmyMapOccupancyFrom(draft);
        Copy(draft._armyReturnResourceFields, _armyReturnResourceFields);
        // Accepted cause4 flags must survive the later return image publisher.
        Copy(draft._armyUnitFlags150, _armyUnitFlags150);
        _armyReturnImageChanges = draft._armyReturnImageChanges?.Copy();
        CommitArmyReturnTraitFields(draft);
        CommitAssignmentCurrentFields(draft);
        CommitBaseCaptureCurrentFields(draft);
        CommitBaseReturnCaptivesFields(draft);
        Copy(draft._captureItemOwners4A, _captureItemOwners4A);
        Copy(draft._personDestructionLoyaltyE8, _personDestructionLoyaltyE8);
        Copy(draft.rulerReleaseRelations13, rulerReleaseRelations13);
        Copy(draft.rulerReleasePopularity1BE, rulerReleasePopularity1BE);
        Copy(draft._officerHealth, _officerHealth);
        Copy(draft._healthSourceIdentities, _healthSourceIdentities);
        Copy(draft._moraleWavePersonPostE7, _moraleWavePersonPostE7);
        Copy(draft._personAttributeBytes, _personAttributeBytes);
        Copy(draft._registryItemStatuses, _registryItemStatuses);
        draft.CopyOfficerAftermathTo(this);
        // Publish only the accepted transaction, without retaining its mutable
        // service owner as an alias into the already prepared draft.
        TransferService = draft.TransferService?.Copy();
        static void Copy<K,V>(Dictionary<K,V> source, Dictionary<K,V> target) where K : notnull
        { target.Clear(); foreach (var row in source) target.Add(row.Key, row.Value); }
    }
}
