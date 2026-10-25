namespace San14.Data;

public enum RegistryKind
{
    Army, City, Gate, Force, Area, District, Object, Person, Item, Scenery,
    CautionTroops, Table81B50,
    // Used only when comparing an opaque shared-node payload, not a registration table.
    Hex,
    // An opaque shared temporary-list payload, not a registration table.
    Kingdom
}

// Kind and physical receiver identify a current pointer in the normal Data profile.
// A raw character ID, lookup-table slot or Core ID is not pointer identity.
public sealed record RegistryObjectReference(RegistryKind Kind, int? PhysicalSlot);
public sealed record RegistryTableRange(RegistryKind Kind, int Count);

public interface IObjectRegistrationState
{
    RegistryObjectReference? ReadRegistryTableReference(RegistryKind kind, int slot);
    bool? ReadRegistryObjectNativeValidity(RegistryObjectReference reference);
    byte? ReadRegistryPersonStatus(RegistryObjectReference reference);
    byte? ReadRegistryItemRawField50(RegistryObjectReference reference);
    bool? ReadRegistryItemListEligibility(RegistryObjectReference reference) =>
        ReadRegistryItemRawField50(reference) is byte code ? code == 1 : null;
    RegistryListHandle? ReadRegistryDescriptor(RegistryKind kind);
    RegistryListNodeReference? ReadRegistryHead(RegistryListHandle handle);
    RegistryObjectReference? ReadRegistryNodePayload(RegistryListNodeReference node);
    RegistryListNodeReference? ReadRegistryNodeNext(RegistryListNodeReference node);
    bool? ReadRegistryLockEnabled();
    void EnterRegistryLock();
    void ExitRegistryLock();
    RegistryListNodeReference? AllocateRegistryNode(RegistryListHandle handle);
    void WriteRegistryNodePayload(RegistryListNodeReference node, RegistryObjectReference reference);
}

/// <summary>The twelve current registration loops inside32F910, after its reset
/// and manager stages. This does not implement those preceding or later stages.</summary>
public static class ObjectRegistration
{
    public static IReadOnlyList<RegistryTableRange> Ranges { get; } = Array.AsReadOnly(new[]
    {
        new RegistryTableRange(RegistryKind.Army,501),
        new RegistryTableRange(RegistryKind.City,52),
        new RegistryTableRange(RegistryKind.Gate,11),
        new RegistryTableRange(RegistryKind.Force,52),
        new RegistryTableRange(RegistryKind.Area,501),
        new RegistryTableRange(RegistryKind.District,52),
        new RegistryTableRange(RegistryKind.Object,3001),
        new RegistryTableRange(RegistryKind.Person,1851),
        new RegistryTableRange(RegistryKind.Item,202),
        new RegistryTableRange(RegistryKind.Scenery,101),
        new RegistryTableRange(RegistryKind.CautionTroops,501),
        new RegistryTableRange(RegistryKind.Table81B50,1001)
    });

    public static void Apply(IObjectRegistrationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (RegistryTableRange range in Ranges)
            for (int slot=0;slot<range.Count;slot++)
            {
                RegistryObjectReference? reference=state.ReadRegistryTableReference(range.Kind,slot);
                if (reference?.PhysicalSlot.HasValue==true && reference.Kind!=range.Kind)
                    throw new InvalidDataException("Current registration table contains a different receiver type outside its supported profile.");
                _ = Register(reference,state);
            }
    }

    public static bool Register(RegistryObjectReference? reference, IObjectRegistrationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (reference is null) throw new InvalidDataException("Current registration payload pointer is unknown.");
        if (!reference.PhysicalSlot.HasValue) return false;
        if (!Ranges.Any(range=>range.Kind==reference.Kind))
            throw new InvalidDataException("Current payload kind has no native registration helper.");
        if (!Known(state.ReadRegistryObjectNativeValidity(reference),"payload native validity")) return false;
        if (reference.Kind==RegistryKind.Person)
        {
            byte status=Known(state.ReadRegistryPersonStatus(reference),"person status11E");
            if (status is not (>=1 and <=6) and not 8) return false;
        }
        if (reference.Kind==RegistryKind.Item &&
            !Known(state.ReadRegistryItemListEligibility(reference),"item list eligibility")) return false;
        if (reference.Kind is RegistryKind.Army or RegistryKind.Person or
            RegistryKind.CautionTroops or RegistryKind.Table81B50)
        {
            RegistryListHandle scan=Descriptor(reference.Kind,state);
            RegistryListNodeReference node=Pointer(state.ReadRegistryHead(scan),"deduplication head");
            while (node.Identity.HasValue)
            {
                RegistryObjectReference payload=Pointer(state.ReadRegistryNodePayload(node),"deduplication payload");
                if (payload==reference) return false;
                node=Pointer(state.ReadRegistryNodeNext(node),"deduplication next");
            }
        }
        if (reference.Kind is RegistryKind.CautionTroops or RegistryKind.Table81B50)
            AppendContainer(reference.Kind,()=>reference,state);
        else AppendDirect(reference,state);
        // A known allocation failure still returns native1. No finally/rollback.
        return true;
    }

    private static void AppendDirect(RegistryObjectReference reference,IObjectRegistrationState state)
    {
        if (Known(state.ReadRegistryLockEnabled(),"append lock flag")) state.EnterRegistryLock();
        RegistryListHandle append=Descriptor(reference.Kind,state);
        RegistryListNodeReference allocated=Pointer(state.AllocateRegistryNode(append),"allocation result");
        if (allocated.Identity.HasValue) state.WriteRegistryNodePayload(allocated,reference);
        if (Known(state.ReadRegistryLockEnabled(),"append unlock flag")) state.ExitRegistryLock();
    }
    private static void AppendContainer(RegistryKind kind,Func<RegistryObjectReference> savedPayloadAddress,
        IObjectRegistrationState state)
    {
        //9104D0 owns the lock. It saves container+8 descriptor before reading the
        // caller's saved pointer slot, and reads that descriptor's key afterward.
        if (Known(state.ReadRegistryLockEnabled(),"container lock flag")) state.EnterRegistryLock();
        RegistryListHandle append=Descriptor(kind,state);
        RegistryObjectReference payload=savedPayloadAddress();
        RegistryListNodeReference allocated=Pointer(state.AllocateRegistryNode(append),"container allocation result");
        if (allocated.Identity.HasValue) state.WriteRegistryNodePayload(allocated,payload);
        if (Known(state.ReadRegistryLockEnabled(),"container unlock flag")) state.ExitRegistryLock();
    }

    private static RegistryListHandle Descriptor(RegistryKind kind,IObjectRegistrationState state)
        => Pointer(state.ReadRegistryDescriptor(kind),"current descriptor");
    private static T Known<T>(T? value,string field) where T:struct => value
        ?? throw new InvalidDataException($"Current registration {field} is unknown.");
    private static T Pointer<T>(T? value,string field) where T:class => value
        ?? throw new InvalidDataException($"Current registration {field} is unknown.");
}
