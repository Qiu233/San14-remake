namespace San14.Data;

public interface IOriginalPersonLinkedEntityLookupState
{
    IReadOnlyList<OriginalPersonLinkedEntityReference?>? ReadCurrentPersonLinkedEntities();
    bool? ReadLinkedEntityNativeValidity(OriginalPersonLinkedEntityReference reference);
    IReadOnlyList<uint?>? ReadLinkedEntityMemberIds(OriginalPersonLinkedEntityReference reference);
    ushort? ReadPersonIdentity(int physicalSlot);
}

/// <summary>229ED0 ordered payload/member search. The caller performs its own later validity query.</summary>
public static class OriginalPersonLinkedEntityLookup
{
    public static OriginalPersonLinkedEntityReference Find(int personPhysicalSlot, IOriginalPersonLinkedEntityLookupState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (personPhysicalSlot is < 0 or >= 1851) throw new InvalidDataException("Linked-entity person is outside supported CE profile.");
        IReadOnlyList<OriginalPersonLinkedEntityReference?> sequence = state.ReadCurrentPersonLinkedEntities()
            ?? throw new InvalidDataException("Current person-linked payload sequence is unknown.");
        foreach (OriginalPersonLinkedEntityReference? reference in sequence)
        {
            if (reference is null) throw new InvalidDataException("Current person-linked payload pointer is unknown.");
            bool valid = state.ReadLinkedEntityNativeValidity(reference)
                ?? throw new InvalidDataException("Current person-linked payload validity is unknown.");
            if (!valid) continue;
            IReadOnlyList<uint?> members = state.ReadLinkedEntityMemberIds(reference)
                ?? throw new InvalidDataException("Current person-linked payload member chain is unknown.");
            if (members.Count == 0) continue;
            ushort id = state.ReadPersonIdentity(personPhysicalSlot)
                ?? throw new InvalidDataException("Current person-linked lookup ID is unknown.");
            foreach (uint? member in members)
            {
                uint value = member ?? throw new InvalidDataException("Current person-linked member ID is unknown.");
                if (value == id) return reference;
            }
        }
        return new(null);
    }
}
