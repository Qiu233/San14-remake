namespace San14.Data;

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<BaseReference, RegistryListHandle?> _armyReturnBasePerson88 = [];
    public byte? ReadArmyReturnUnitStatus10(ArmyUnitReference army) =>
        _armyStatusFlags.GetValueOrDefault(MapPhysical(army.PhysicalSlot, 501, "army unit"));
    public void SetArmyReturnBasePerson88Descriptor(BaseReference target, RegistryListHandle? descriptor)
    { _ = BasePhysical(target); _armyReturnBasePerson88[target] = descriptor; }
    public RegistryListHandle? ReadArmyReturnBasePerson88Descriptor(BaseReference target)
    { _ = BasePhysical(target); return _armyReturnBasePerson88.GetValueOrDefault(target); }

    internal SourceWorldContext CreateArmyReturnHomeDraft()
    {
        var draft = CreateArmyReturnGovernorDraft();
        Copy(_cityPointers, draft._cityPointers); Copy(_gatePointers, draft._gatePointers);
        Copy(_objectPointers, draft._objectPointers); Copy(_objects, draft._objects);
        Copy(_cityFlags, draft._cityFlags); Copy(_gateFlags, draft._gateFlags); Copy(_cityDistricts, draft._cityDistricts);
        Copy(_hexPointers, draft._hexPointers); Copy(_areaPointers, draft._areaPointers);
        Copy(_hexAreas, draft._hexAreas); Copy(_hexForces, draft._hexForces); Copy(_areaValidityCodes, draft._areaValidityCodes);
        Copy(_areaCities, draft._areaCities); Copy(_areaHexes, draft._areaHexes);
        draft.SetRegisteredBases(ReadRegisteredBases()?.ToArray());
        draft.SetArmyReturnRegisteredGates(ReadArmyReturnRegisteredGates()?.ToArray());
        draft.SetArmyReturnRegisteredDistricts(ReadArmyReturnRegisteredDistricts()?.ToArray());
        //Changed-City29C430 consumes the actual City90/AreaB0 managers.
        //Keep their saved descriptor identities over this same detached
        //pool, instead of replacing current manager readers with arrays.
        Copy(_registryDescriptors, draft._registryDescriptors);
        draft.RawTransferServiceFlags16A8 = RawTransferServiceFlags16A8; draft.CurrentPlayerForce = CurrentPlayerForce;
        draft.RegistryHeadTablePresent = RegistryHeadTablePresent; draft.RegistryLockEnabled = RegistryLockEnabled;
        draft.CommitArmyReturnSharedListFields(this);
        CopyArmyReturnResourceInputsTo(draft);
        CopyArmyReturnTraitInputsTo(draft);
        CopyOwnedDestructionQueueViewTo(draft);
        CopyAssignmentInputsTo(draft);
        CopyBaseCaptureCurrentTo(draft);
        CopyBaseReturnCaptivesInputsTo(draft);
        return draft;
        static void Copy<TKey, TValue>(Dictionary<TKey, TValue> from, Dictionary<TKey, TValue> to) where TKey : notnull
        { foreach (var field in from) to.Add(field.Key, field.Value); }
    }
    internal void CommitArmyReturnSharedListFields(SourceWorldContext source)
    {
        _orderedLists = source._orderedLists.Copy();
        Copy(source._armyReturnBasePerson88, _armyReturnBasePerson88);
        Copy(source._baseHexListHandles, _baseHexListHandles);
        Copy(source._registryDescriptors, _registryDescriptors);
        _baseRegistryConfigured = source._baseRegistryConfigured; _baseRegistryHandle = source._baseRegistryHandle;
        Copy(source._associatedChainDescriptors, _associatedChainDescriptors);
        _currentAssociatedContainer = source._currentAssociatedContainer;
        static void Copy<TKey, TValue>(Dictionary<TKey, TValue> from, Dictionary<TKey, TValue> to) where TKey : notnull
        { to.Clear(); foreach (var field in from) to.Add(field.Key, field.Value); }
    }
}
