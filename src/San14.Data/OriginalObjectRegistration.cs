namespace San14.Data;

public enum OriginalRegistryKind
{
    Army, City, Gate, Force, Area, District, Object, Person, Item, Scenery,
    CautionTroops, Table81B50,
    // Used only when comparing an opaque shared-node payload, not a registration table.
    Hex
}

// Kind and physical receiver identify a current pointer in the normal Data profile.
// A raw character ID, lookup-table slot or Core ID is not pointer identity.
public sealed record OriginalRegistryObjectReference(OriginalRegistryKind Kind, int? PhysicalSlot);
public sealed record OriginalRegistryTableRange(OriginalRegistryKind Kind, int Count);

public interface IOriginalObjectRegistrationState
{
    OriginalRegistryObjectReference? ReadRegistryTableReference(OriginalRegistryKind kind, int slot);
    bool? ReadRegistryObjectNativeValidity(OriginalRegistryObjectReference reference);
    byte? ReadRegistryPersonStatus(OriginalRegistryObjectReference reference);
    byte? ReadRegistryItemRawField50(OriginalRegistryObjectReference reference);
    OriginalRegistryListHandle? ReadRegistryDescriptor(OriginalRegistryKind kind);
    OriginalRegistryListNodeReference? ReadRegistryHead(OriginalRegistryListHandle handle);
    OriginalRegistryObjectReference? ReadRegistryNodePayload(OriginalRegistryListNodeReference node);
    OriginalRegistryListNodeReference? ReadRegistryNodeNext(OriginalRegistryListNodeReference node);
    bool? ReadRegistryLockEnabled();
    void EnterRegistryLock();
    void ExitRegistryLock();
    OriginalRegistryListNodeReference? AllocateRegistryNode(OriginalRegistryListHandle handle);
    void WriteRegistryNodePayload(OriginalRegistryListNodeReference node, OriginalRegistryObjectReference reference);
}

/// <summary>The twelve current registration loops inside32F910, after its reset
/// and manager stages. This does not implement those preceding or later stages.</summary>
public static class OriginalObjectRegistration
{
    public static IReadOnlyList<OriginalRegistryTableRange> Ranges { get; } = Array.AsReadOnly(new[]
    {
        new OriginalRegistryTableRange(OriginalRegistryKind.Army,501),
        new OriginalRegistryTableRange(OriginalRegistryKind.City,52),
        new OriginalRegistryTableRange(OriginalRegistryKind.Gate,11),
        new OriginalRegistryTableRange(OriginalRegistryKind.Force,52),
        new OriginalRegistryTableRange(OriginalRegistryKind.Area,501),
        new OriginalRegistryTableRange(OriginalRegistryKind.District,52),
        new OriginalRegistryTableRange(OriginalRegistryKind.Object,3001),
        new OriginalRegistryTableRange(OriginalRegistryKind.Person,1851),
        new OriginalRegistryTableRange(OriginalRegistryKind.Item,202),
        new OriginalRegistryTableRange(OriginalRegistryKind.Scenery,101),
        new OriginalRegistryTableRange(OriginalRegistryKind.CautionTroops,501),
        new OriginalRegistryTableRange(OriginalRegistryKind.Table81B50,1001)
    });

    public static void Apply(IOriginalObjectRegistrationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (OriginalRegistryTableRange range in Ranges)
            for (int slot=0;slot<range.Count;slot++)
            {
                OriginalRegistryObjectReference? reference=state.ReadRegistryTableReference(range.Kind,slot);
                if (reference?.PhysicalSlot.HasValue==true && reference.Kind!=range.Kind)
                    throw new InvalidDataException("Current registration table contains a different receiver type outside its supported profile.");
                _ = Register(reference,state);
            }
    }

    public static bool Register(OriginalRegistryObjectReference? reference, IOriginalObjectRegistrationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (reference is null) throw new InvalidDataException("Current registration payload pointer is unknown.");
        if (!reference.PhysicalSlot.HasValue) return false;
        if (!Ranges.Any(range=>range.Kind==reference.Kind))
            throw new InvalidDataException("Current payload kind has no native registration helper.");
        if (!Known(state.ReadRegistryObjectNativeValidity(reference),"payload native validity")) return false;
        if (reference.Kind==OriginalRegistryKind.Person)
        {
            byte status=Known(state.ReadRegistryPersonStatus(reference),"person status11E");
            if (status is not (>=1 and <=6) and not 8) return false;
        }
        if (reference.Kind==OriginalRegistryKind.Item &&
            Known(state.ReadRegistryItemRawField50(reference),"item byte50")!=1) return false;
        if (reference.Kind is OriginalRegistryKind.Army or OriginalRegistryKind.Person or
            OriginalRegistryKind.CautionTroops or OriginalRegistryKind.Table81B50)
        {
            OriginalRegistryListHandle scan=Descriptor(reference.Kind,state);
            OriginalRegistryListNodeReference node=Pointer(state.ReadRegistryHead(scan),"deduplication head");
            while (node.Identity.HasValue)
            {
                OriginalRegistryObjectReference payload=Pointer(state.ReadRegistryNodePayload(node),"deduplication payload");
                if (payload==reference) return false;
                node=Pointer(state.ReadRegistryNodeNext(node),"deduplication next");
            }
        }
        if (reference.Kind is OriginalRegistryKind.CautionTroops or OriginalRegistryKind.Table81B50)
            AppendContainer(reference.Kind,()=>reference,state);
        else AppendDirect(reference,state);
        // A known allocation failure still returns native1. No finally/rollback.
        return true;
    }

    private static void AppendDirect(OriginalRegistryObjectReference reference,IOriginalObjectRegistrationState state)
    {
        if (Known(state.ReadRegistryLockEnabled(),"append lock flag")) state.EnterRegistryLock();
        OriginalRegistryListHandle append=Descriptor(reference.Kind,state);
        OriginalRegistryListNodeReference allocated=Pointer(state.AllocateRegistryNode(append),"allocation result");
        if (allocated.Identity.HasValue) state.WriteRegistryNodePayload(allocated,reference);
        if (Known(state.ReadRegistryLockEnabled(),"append unlock flag")) state.ExitRegistryLock();
    }
    private static void AppendContainer(OriginalRegistryKind kind,Func<OriginalRegistryObjectReference> savedPayloadAddress,
        IOriginalObjectRegistrationState state)
    {
        //9104D0 owns the lock. It saves container+8 descriptor before reading the
        // caller's saved pointer slot, and reads that descriptor's key afterward.
        if (Known(state.ReadRegistryLockEnabled(),"container lock flag")) state.EnterRegistryLock();
        OriginalRegistryListHandle append=Descriptor(kind,state);
        OriginalRegistryObjectReference payload=savedPayloadAddress();
        OriginalRegistryListNodeReference allocated=Pointer(state.AllocateRegistryNode(append),"container allocation result");
        if (allocated.Identity.HasValue) state.WriteRegistryNodePayload(allocated,payload);
        if (Known(state.ReadRegistryLockEnabled(),"container unlock flag")) state.ExitRegistryLock();
    }

    private static OriginalRegistryListHandle Descriptor(OriginalRegistryKind kind,IOriginalObjectRegistrationState state)
        => Pointer(state.ReadRegistryDescriptor(kind),"current descriptor");
    private static T Known<T>(T? value,string field) where T:struct => value
        ?? throw new InvalidDataException($"Current registration {field} is unknown.");
    private static T Pointer<T>(T? value,string field) where T:class => value
        ?? throw new InvalidDataException($"Current registration {field} is unknown.");
}
