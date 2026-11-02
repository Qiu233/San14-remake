using System.Collections.Frozen;

namespace San14.Data;

/// <summary>The complete opening identity/lookup definition of one admitted
/// scenario generation. Current overrides and callbacks are absent.</summary>
internal sealed class InitialOfficerSourceDefinitions
{
    private readonly LoadedScenario owner;
    private readonly int[] identities;
    private readonly FrozenDictionary<int,int?> logicalReferences;
    internal int RecordCount => identities.Length;

    internal InitialOfficerSourceDefinitions(LoadedScenario source)
    {
        owner = source;
        var people = source.Snapshot.Characters;
        if (people.Count != 1851)
            throw new InvalidDataException("Initial Officer identities require the complete loaded receiver table.");
        identities = new int[people.Count];
        var lookups = new Dictionary<int,int?>();
        for (int slot = 0; slot < people.Count; slot++)
        {
            var person = people[slot];
            if (person.Slot != slot || person.CharacterId is < 0 or > ushort.MaxValue)
                throw new InvalidDataException("Initial Officer identities contain an invalid loaded receiver.");
            if (person.CharacterId > 6000)
                throw new InvalidDataException("Initial Officer identity exceeds the source logical lookup table.");
            identities[slot] = person.CharacterId;
            // Exact ordered RebuildPersonIds: receiver0 participates even for
            // identity0; later duplicate identities retain the last writer.
            if (slot == 0 || person.CharacterId != 0) lookups[person.CharacterId] = slot;
        }
        // The independent reserved lookup survives an empty embedded ID.
        for (int id = 0; id < 6001; id++)
            if (PostRegistrationLookups.HirePersonPhysicalSlot(id) is int physical) lookups[id] = physical;
        logicalReferences = lookups.ToFrozenDictionary();
    }

    internal bool BelongsTo(LoadedScenario source) => ReferenceEquals(owner, source);
    internal bool ContainsRecord(int physical) => (uint)physical < (uint)identities.Length;
    internal int? IdentityAt(int physical) => ContainsRecord(physical) ? identities[physical] : null;
    internal bool TryLogicalReference(int id,out int? physical) => logicalReferences.TryGetValue(id,out physical);
}
