namespace San14.Data;

// A missing reference is unknown; an absent identity is an observed null pointer.
public sealed record RegistryListHandle(int? Identity);
public sealed record RegistryListNodeReference(int? Identity);

public interface IBaseRegistrationState
{
    BaseReference? ReadCityTableReference(int slot);
    BaseReference? ReadGateTableReference(int slot);
    RegistryListHandle? ReadBaseRegistryHandle();
    RegistryListNodeReference? AllocateBaseRegistryNode(RegistryListHandle handle);
    void WriteBaseRegistryNodePayload(RegistryListNodeReference node, BaseReference reference);
}

/// <summary>
/// Complete31ECD0 business operation and its52-city/10-gate call sequence in32F910.
/// The shared018C90 allocation effects precede the payload write. Earlier reset,
/// other registrations and later initialization are required separate stages.
/// </summary>
public static class BaseRegistration
{
    public static bool Register(BaseReference? reference, IBaseRegistrationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (reference is null) throw new InvalidDataException("Current base registration payload is unknown.");
        if (!reference.PhysicalSlot.HasValue) return false;
        RegistryListHandle handle = state.ReadBaseRegistryHandle()
            ?? throw new InvalidDataException("Current base registry descriptor is unknown.");
        RegistryListNodeReference node = state.AllocateBaseRegistryNode(handle)
            ?? throw new InvalidDataException("Current base registry allocation result is unknown.");
        if (node.Identity.HasValue) state.WriteBaseRegistryNodePayload(node, reference);
        // Allocation failure does not change the original helper's success result.
        return true;
    }

    public static void Apply(IBaseRegistrationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        // City0 is included; gate0 is excluded. No validity filter or deduplication.
        for (int slot = 0; slot < 52; slot++) _ = Register(state.ReadCityTableReference(slot), state);
        for (int slot = 1; slot < 11; slot++) _ = Register(state.ReadGateTableReference(slot), state);
    }
}
