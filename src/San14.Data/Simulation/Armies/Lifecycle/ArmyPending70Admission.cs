namespace San14.Data;

/// <summary>Actual current pending+70 container, distinct from manager+60 and
/// waiting+80. Append allocates a shared node at the current descriptor's tail
/// with018CC0 flag1 and owns head/tail/link/count changes. Known-null node is
/// allocation failure; null return means unknown. No Army validity is queried.</summary>
public interface IArmyPending70AdmissionState
{
    RegistryListHandle? ReadPending70Descriptor();
    RegistryListNodeReference? ReadPending70Head(RegistryListHandle descriptor);
    RegistryObjectReference? ReadPending70Payload(RegistryListNodeReference node);
    RegistryListNodeReference? ReadPending70Next(RegistryListNodeReference node);
    bool? ReadPending70LockEnabled();
    void EnterPending70Lock();
    void ExitPending70Lock();
    RegistryListNodeReference? AppendPending70Node(RegistryListHandle descriptor);
    void WritePending70Payload(RegistryListNodeReference node, RegistryObjectReference army);
}

public enum ArmyPending70AdmissionOutcome { AlreadyPending, Appended, KnownAllocationFailure }

/// <summary>TC31F370 callback: pointer deduplication followed by conditional
/// lock, fresh descriptor, shared-pool tail append and saved pointer write.
/// Native callback has no meaningful return value. These outcomes describe
/// only actual container work. They do not prove a caller lifecycle reached
/// this callback, authorize a Core delete or execute the pending consumer.</summary>
public static class ArmyPending70Admission
{
    public const string Revision = "san14:ce-army-pending70-source-admission-20261003";

    /// <summary>Invoke only where an actual lifecycle owner reaches31F370:
    ///259360 at25C180 or29F220 at29F809. Source entry eligibility alone cannot
    ///skip their preceding person/resource/report/current-field operations.</summary>
    public static ArmyPending70AdmissionOutcome EnqueueCurrentPointer(RegistryObjectReference? army,
        IArmyPending70AdmissionState state)
    {
        RequireArmy(army); ArgumentNullException.ThrowIfNull(state);
        if (ContainsCurrentPointer(army, state)) return ArmyPending70AdmissionOutcome.AlreadyPending;
        if (LockFlag(state)) state.EnterPending70Lock();
        var descriptor = Known(state.ReadPending70Descriptor(), "fresh append descriptor");
        var node = Known(state.AppendPending70Node(descriptor), "append allocation result");
        if (node.Identity.HasValue) state.WritePending70Payload(node, army!);
        if (LockFlag(state)) state.ExitPending70Lock();
        return node.Identity.HasValue ? ArmyPending70AdmissionOutcome.Appended
            : ArmyPending70AdmissionOutcome.KnownAllocationFailure;
    }

    /// <summary>Actual primary pending membership, also the first early-return
    ///gate in259360 and first component of233560. It does not substitute for
    ///233560's independent registry140 association checks.</summary>
    public static bool ContainsCurrentPointer(RegistryObjectReference? army,
        IArmyPending70AdmissionState state)
    {
        RequireArmy(army); ArgumentNullException.ThrowIfNull(state);
        var descriptor = Known(state.ReadPending70Descriptor(), "membership descriptor");
        var node = Known(state.ReadPending70Head(descriptor), "membership head");
        var visited = new HashSet<int>();
        while (node.Identity is int identity)
        {
            if (!visited.Add(identity)) throw new InvalidDataException("Current pending70 chain contains a node cycle.");
            if (Known(state.ReadPending70Payload(node), "membership payload") == army) return true;
            node = Known(state.ReadPending70Next(node), "membership next");
        }
        return false;
    }
    private static bool LockFlag(IArmyPending70AdmissionState state)
        => state.ReadPending70LockEnabled() ?? throw new InvalidDataException("Current pending70 lock flag is unknown.");
    private static T Known<T>(T? value, string field) where T : class
        => value ?? throw new InvalidDataException($"Current pending70 {field} is unknown.");
    private static void RequireArmy(RegistryObjectReference? army)
    {
        if (army is null) throw new InvalidDataException("Current pending70 Army pointer is unknown.");
        if (army.Kind != RegistryKind.Army) throw new InvalidDataException("Pending70 admission requires an Army receiver.");
        //31F370 itself does not reject known-null receiver or read its header.
        //Its two normal callers qualify the pointer before reaching this seam.
    }
}
