namespace San14.Data;

public sealed partial class SourceWorldContext
{
    private IReadOnlyList<BaseReference?>? _armyReturnRegisteredGates;
    private IReadOnlyList<DistrictReference?>? _armyReturnRegisteredDistricts;

    // Explicit resolved current manager+A0/+D0 chains. These setters preserve
    // native pointer order and duplicates; they never reconstruct from IDs.
    public void SetArmyReturnRegisteredGates(IReadOnlyList<BaseReference?>? values)
    {
        if (values is not null) foreach (var value in values)
            if (value is not null && value.Kind != BaseKind.Gate)
                throw new InvalidDataException("Army return manager+A0 payload is not a Gate.");
        UseResolvedRegistrySequence(RegistryKind.Gate);
        _armyReturnRegisteredGates = values?.ToArray();
    }
    public IReadOnlyList<BaseReference?>? ReadArmyReturnRegisteredGates() => RegistryConfigured(RegistryKind.Gate)
        ? ProjectRegistry<BaseReference?>(RegistryKind.Gate, value => value is null ? null : new(BaseKind.Gate, value.PhysicalSlot))
        : _armyReturnRegisteredGates is null ? null : Array.AsReadOnly(_armyReturnRegisteredGates.ToArray());
    public void SetArmyReturnRegisteredDistricts(IReadOnlyList<DistrictReference?>? values)
    {
        UseResolvedRegistrySequence(RegistryKind.District);
        _armyReturnRegisteredDistricts = values?.ToArray();
    }
    public IReadOnlyList<DistrictReference?>? ReadArmyReturnRegisteredDistricts() => RegistryConfigured(RegistryKind.District)
        ? ProjectRegistry<DistrictReference?>(RegistryKind.District, value => value is null ? null : new(value.PhysicalSlot))
        : _armyReturnRegisteredDistricts is null ? null : Array.AsReadOnly(_armyReturnRegisteredDistricts.ToArray());

    // Deliberately a narrow governor-read draft, not a world clone or save.
    // Every mutable collection consumed by the selector is detached. Resolved
    // manager/association chains are captured in their owned current order.
    internal SourceWorldContext CreateArmyReturnGovernorDraft()
    {
        var draft = new SourceWorldContext();
        Copy(_persons, draft._persons); Copy(_personFirstLocations, draft._personFirstLocations);
        CopyOfficerLocationsTo(draft); CopyArmyTargetsTo(draft);
        Copy(_personDistrictSlots, draft._personDistrictSlots); Copy(_districtPointers, draft._districtPointers);
        Copy(_forcePointers, draft._forcePointers); Copy(_forceRulers, draft._forceRulers);
        _districtIdentities.CopyTo(draft._districtIdentities);
        foreach (var field in _districtSourceExtensions) draft._districtSourceExtensions[field.Key] = field.Value.ToArray();
        _officerSources.CopyTo(draft._officerSources);
        Copy(_cityLinks, draft._cityLinks); Copy(_gateLinks, draft._gateLinks);
        Copy(_armyReturnGovernorWords2E, draft._armyReturnGovernorWords2E);
        Copy(_linkedEntityStates, draft._linkedEntityStates);
        foreach (var field in _linkedEntityMembers) draft._linkedEntityMembers[field.Key] = field.Value?.ToArray();
        draft.SetRegisteredPersonPhysicalSlots(ReadRegisteredPersonPhysicalSlots()?.ToArray());
        draft.SetCurrentPersonLinkedEntities(ReadCurrentPersonLinkedEntities()?.ToArray());
        return draft;
        static void Copy<TKey, TValue>(Dictionary<TKey, TValue> from, Dictionary<TKey, TValue> to) where TKey : notnull
        { foreach (var field in from) to.Add(field.Key, field.Value); }
    }
}
