namespace San14.Data;

public enum BaseKind { City, Gate }
public sealed record BaseReference(BaseKind Kind, int? PhysicalSlot);

public interface IBaseLookupState
{
    BaseReference? ReadCityZeroReference();
    IReadOnlyList<BaseReference?>? ReadRegisteredBases();
    ushort? ReadBaseObjectLink(BaseReference reference);
}

/// <summary>32BCE0 registered-base lookup; never filters by validity or substitutes a direct index.</summary>
public static class BaseLookup
{
    public static BaseReference Find(int requested, IBaseLookupState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (requested == 0) return state.ReadCityZeroReference()
            ?? throw new InvalidDataException("Current city table0 pointer is unknown.");
        IReadOnlyList<BaseReference?> bases = Read(state);
        if (requested <= 61)
        {
            foreach (BaseReference? entry in bases)
            {
                BaseReference reference = entry
                    ?? throw new InvalidDataException("Current registered base pointer is unknown.");
                ushort link = state.ReadBaseObjectLink(reference)
                    ?? throw new InvalidDataException("Current registered base word4E is unknown.");
                if (link == requested) return reference;
            }
            // Native fallback reacquires the current registered head.
            bases = Read(state);
        }
        // Native fallback consumes only the fresh head payload. Counting a live
        // chain here would read later nodes that this branch never accesses.
        foreach (BaseReference? entry in bases)
            return entry ?? throw new InvalidDataException("Current first registered base pointer is unknown.");
        throw new InvalidDataException("Current registered base list is empty; native fallback would dereference a missing node.");
    }

    private static IReadOnlyList<BaseReference?> Read(IBaseLookupState state) => state.ReadRegisteredBases()
        ?? throw new InvalidDataException("Current registered base list is unknown.");
}
