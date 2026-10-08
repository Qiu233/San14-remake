namespace San14.Data;

// A missing reference is unknown; an absent identity is an observed null pointer.
public sealed record OriginalRegistryListHandle(int? Identity);
public sealed record OriginalRegistryListNodeReference(int? Identity);

public interface IOriginalBaseRegistrationState
{
    OriginalBaseReference? ReadCityTableReference(int slot);
    OriginalBaseReference? ReadGateTableReference(int slot);
    OriginalRegistryListHandle? ReadBaseRegistryHandle();
    OriginalRegistryListNodeReference? AllocateBaseRegistryNode(OriginalRegistryListHandle handle);
    void WriteBaseRegistryNodePayload(OriginalRegistryListNodeReference node, OriginalBaseReference reference);
}

/// <summary>
/// Complete31ECD0 business operation and its52-city/10-gate call sequence in32F910.
/// The shared018C90 allocation effects precede the payload write. Earlier reset,
/// other registrations and later initialization are required separate stages.
/// </summary>
public static class OriginalBaseRegistration
{
    public static bool Register(OriginalBaseReference? reference, IOriginalBaseRegistrationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (reference is null) throw new InvalidDataException("Current base registration payload is unknown.");
        if (!reference.PhysicalSlot.HasValue) return false;
        OriginalRegistryListHandle handle = state.ReadBaseRegistryHandle()
            ?? throw new InvalidDataException("Current base registry descriptor is unknown.");
        OriginalRegistryListNodeReference node = state.AllocateBaseRegistryNode(handle)
            ?? throw new InvalidDataException("Current base registry allocation result is unknown.");
        if (node.Identity.HasValue) state.WriteBaseRegistryNodePayload(node, reference);
        // Allocation failure does not change the original helper's success result.
        return true;
    }

    public static void Apply(IOriginalBaseRegistrationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        // City0 is included; gate0 is excluded. No validity filter or deduplication.
        for (int slot = 0; slot < 52; slot++) _ = Register(state.ReadCityTableReference(slot), state);
        for (int slot = 1; slot < 11; slot++) _ = Register(state.ReadGateTableReference(slot), state);
    }
}
