using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace San14.Data;

public sealed partial class OriginalEventInitializationContext
{
    private readonly Dictionary<OriginalBaseReference, OriginalRegistryListHandle?> _armyReturnBasePerson88 = [];
    public byte? ReadArmyReturnUnitStatus10(OriginalArmyUnitReference army) =>
        _armyStatusFlags.GetValueOrDefault(MapPhysical(army.PhysicalSlot, 501, "army unit"));
    public void SetArmyReturnBasePerson88Descriptor(OriginalBaseReference target, OriginalRegistryListHandle? descriptor)
    { _ = BasePhysical(target); _armyReturnBasePerson88[target] = descriptor; }
    public OriginalRegistryListHandle? ReadArmyReturnBasePerson88Descriptor(OriginalBaseReference target)
    { _ = BasePhysical(target); return _armyReturnBasePerson88.GetValueOrDefault(target); }

    internal string ArmyReturnSharedListFingerprint() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        Heads = _hexListHeads.OrderBy(x => x.Key), Tails = _hexListTails.OrderBy(x => x.Key), Counts = _hexListCounts.OrderBy(x => x.Key),
        Next = _hexListNext.OrderBy(x => x.Key), Previous = _hexListPrevious.OrderBy(x => x.Key),
        Payloads = _nativeListPayloads.OrderBy(x => x.Key).Select(x => new
        { x.Key, Kind = x.Value?.GetType().FullName, Value = x.Value is null ? null : JsonSerializer.Serialize(x.Value, x.Value.GetType()) }),
        Base88 = _armyReturnBasePerson88.OrderBy(x => x.Key.Kind).ThenBy(x => x.Key.PhysicalSlot)
    }))));

    internal OriginalEventInitializationContext CreateArmyReturnHomeDraft()
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
    internal void CommitArmyReturnSharedListFields(OriginalEventInitializationContext source)
    {
        Copy(source._hexListHeads, _hexListHeads); Copy(source._hexListTails, _hexListTails); Copy(source._hexListCounts, _hexListCounts);
        Copy(source._hexListNext, _hexListNext); Copy(source._hexListPrevious, _hexListPrevious);
        Copy(source._nativeListPayloads, _nativeListPayloads); Copy(source._armyReturnBasePerson88, _armyReturnBasePerson88);
        static void Copy<TKey, TValue>(Dictionary<TKey, TValue> from, Dictionary<TKey, TValue> to) where TKey : notnull
        { to.Clear(); foreach (var field in from) to.Add(field.Key, field.Value); }
    }
}
