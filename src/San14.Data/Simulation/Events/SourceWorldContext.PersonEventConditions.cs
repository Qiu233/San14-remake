using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext : IS14PlayerEventConditionQueries
{
    private readonly Dictionary<int, int?> _conditionSelectedUserPersons = [];
    public bool? ConditionApplicationPresent { get; set; }
    public int? ConditionCurrentUserIndex { get; set; }

    public S14EventConditionPersonPointer? ReadConditionPersonTableSlot(int slot)
    {
        TransferPersonReference? value = ReadPersonLookupReference(slot);
        return value is null ? null : new(value.PhysicalSlot is int physical ? new(physical) : null);
    }

    public S14EventConditionPersonPointer? ReadConditionBasePerson() => _officerSources.Placeholder is not { } reference
        ? null : new(reference.Record is int physical ? new(physical) : null);

    public byte? ReadConditionPersonStatus(S14EventConditionPersonReference person) => ReadPersonStatus(person.PhysicalSlot);
    public bool? ReadConditionPersonValidity(S14EventConditionPersonReference person) => ReadPersonNativeValidity(person.PhysicalSlot);

    public IReadOnlyList<S14EventConditionForceReference>? ReadRegisteredConditionForcesInNativeOrder()
    {
        IReadOnlyList<RegistryObjectReference?>? values = ReadRegistryReferences(RegistryKind.Force);
        return values is null ? null : new ConditionForceList(values);
    }

    public ushort? ReadConditionForceRulerPersonId(S14EventConditionForceReference force)
    {
        Check(force.PhysicalSlot, 52, "condition force receiver");
        return _forceRulers.GetValueOrDefault(force.PhysicalSlot);
    }

    public S14EventConditionForcePointer? ReadConditionForceTableSlot(int slot)
    {
        ForceReference? value = ReadForceTableReference(slot);
        return value is null ? null : new(value.PhysicalSlot is int physical ? new(physical) : null);
    }
    public S14EventConditionForcePointer? ReadConditionPersonForce(S14EventConditionPersonReference person)
    {
        ForceReference? value = ReadPersonForce(person.PhysicalSlot);
        return value is null ? null : new(value.PhysicalSlot is int physical ? new(physical) : null);
    }
    public bool? ReadConditionForceValidity(S14EventConditionForceReference force) =>
        ReadForceNativeValidity(new(force.PhysicalSlot));
    public int? ReadConditionCurrentPlayerForcePhysicalSlot() => CurrentPlayerForce?.PhysicalSlot;
    public bool? ReadConditionApplicationPresent() => ConditionApplicationPresent;
    public int? ReadConditionCurrentUserIndex() => ConditionCurrentUserIndex;
    public ushort? ReadConditionPersonCharacterId(S14EventConditionPersonReference person) =>
        ReadPersonIdentity(person.PhysicalSlot);
    public int? ReadConditionSelectedUserPersonId(int index)
    {
        if (index < 0) throw new InvalidDataException("Selected-user ID query requires a nonnegative current index.");
        return _conditionSelectedUserPersons.GetValueOrDefault(index);
    }
    public void SetConditionSelectedUserPersonId(int index, int? value)
    {
        if (index < 0) throw new InvalidDataException("Selected-user ID write requires a nonnegative current index.");
        _conditionSelectedUserPersons[index] = value;
    }

    // Keep the native traversal lazy: a matched ruler must not consume later nodes.
    private sealed class ConditionForceList(IReadOnlyList<RegistryObjectReference?> values)
        : IReadOnlyList<S14EventConditionForceReference>
    {
        private static S14EventConditionForceReference Project(RegistryObjectReference? value) =>
            value is { Kind: RegistryKind.Force, PhysicalSlot: int physical } ? new(physical)
                : throw new InvalidDataException("Current condition force list contains an unknown or null receiver.");
        public int Count => values.Count;
        public S14EventConditionForceReference this[int index] => Project(values[index]);
        public IEnumerator<S14EventConditionForceReference> GetEnumerator()
        { foreach (RegistryObjectReference? value in values) yield return Project(value); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
