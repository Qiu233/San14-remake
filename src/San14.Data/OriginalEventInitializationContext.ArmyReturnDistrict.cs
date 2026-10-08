namespace San14.Data;

public sealed partial class OriginalEventInitializationContext
{
    private IReadOnlyList<OriginalBaseReference?>? _armyReturnRegisteredGates;
    private IReadOnlyList<OriginalDistrictReference?>? _armyReturnRegisteredDistricts;

    // Explicit resolved current manager+A0/+D0 chains. These setters preserve
    // native pointer order and duplicates; they never reconstruct from IDs.
    public void SetArmyReturnRegisteredGates(IReadOnlyList<OriginalBaseReference?>? values)
    {
        if (values is not null) foreach (var value in values)
            if (value is not null && value.Kind != OriginalBaseKind.Gate)
                throw new InvalidDataException("Army return manager+A0 payload is not a Gate.");
        UseResolvedRegistrySequence(OriginalRegistryKind.Gate);
        _armyReturnRegisteredGates = values?.ToArray();
    }
    public IReadOnlyList<OriginalBaseReference?>? ReadArmyReturnRegisteredGates() => RegistryConfigured(OriginalRegistryKind.Gate)
        ? ProjectRegistry<OriginalBaseReference?>(OriginalRegistryKind.Gate, value => value is null ? null : new(OriginalBaseKind.Gate, value.PhysicalSlot))
        : _armyReturnRegisteredGates is null ? null : Array.AsReadOnly(_armyReturnRegisteredGates.ToArray());
    public void SetArmyReturnRegisteredDistricts(IReadOnlyList<OriginalDistrictReference?>? values)
    {
        UseResolvedRegistrySequence(OriginalRegistryKind.District);
        _armyReturnRegisteredDistricts = values?.ToArray();
    }
    public IReadOnlyList<OriginalDistrictReference?>? ReadArmyReturnRegisteredDistricts() => RegistryConfigured(OriginalRegistryKind.District)
        ? ProjectRegistry<OriginalDistrictReference?>(OriginalRegistryKind.District, value => value is null ? null : new(value.PhysicalSlot))
        : _armyReturnRegisteredDistricts is null ? null : Array.AsReadOnly(_armyReturnRegisteredDistricts.ToArray());

    // Deliberately a narrow governor-read draft, not a world clone or save.
    // Every mutable collection consumed by the selector is detached. Resolved
    // manager/association chains are captured in their owned current order.
    internal OriginalEventInitializationContext CreateArmyReturnGovernorDraft()
    {
        var draft = new OriginalEventInitializationContext();
        Copy(_persons, draft._persons); Copy(_personFirstLocations, draft._personFirstLocations);
        Copy(_personDistrictSlots, draft._personDistrictSlots); Copy(_districtPointers, draft._districtPointers);
        Copy(_forcePointers, draft._forcePointers); Copy(_forceRulers, draft._forceRulers);
        foreach (var field in _districtFields) draft._districtFields[field.Key] = field.Value.ToArray();
        Copy(_personPointers, draft._personPointers); draft._personLookupCleared = _personLookupCleared;
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
