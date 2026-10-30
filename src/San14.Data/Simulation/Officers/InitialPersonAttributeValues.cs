namespace San14.Data;

/// <summary>Six original attribute fields belonging to one loaded scenario.
/// Current normalization, caps and edits remain private context writes.</summary>
internal sealed class InitialPersonAttributeValues
{
    private readonly LoadedScenario owner;
    private readonly Row[] rows;

    internal InitialPersonAttributeValues(LoadedScenario source)
    {
        owner = source;
        var people = source.Snapshot.Characters;
        if (people.Count != 1851)
            throw new InvalidDataException("Initial Person attributes require the complete loaded receiver table.");
        rows = new Row[people.Count];
        for (int slot = 0; slot < people.Count; slot++)
        {
            var person = people[slot];
            if (person.Slot != slot || person.CharacterId is < 0 or > ushort.MaxValue)
                throw new InvalidDataException("Initial Person attributes have an invalid loaded receiver.");
            rows[slot] = new(person.Leadership, person.Strength, person.Intelligence,
                person.Politics, person.Charm, person.RawPolicyBaseCost);
        }
    }

    internal bool BelongsTo(LoadedScenario source) => ReferenceEquals(owner, source);
    internal byte? Read(int physical, int offset)
    {
        var row = rows[physical];
        return offset switch
        {
            0x124 => row.Leadership, 0x125 => row.Strength, 0x126 => row.Intelligence,
            0x127 => row.Politics, 0x128 => row.Charm, 0x1BA => row.PolicyBaseCost,
            _ => null
        };
    }

    private readonly record struct Row(byte? Leadership, byte? Strength, byte? Intelligence,
        byte? Politics, byte? Charm, byte? PolicyBaseCost);
}
