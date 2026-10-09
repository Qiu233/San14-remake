namespace San14.Data;

/// <summary>Only current comparator/getter fields absent from the shared
/// Context. TC offsets are named explicitly; SC Person offsets add38. Office
///22E0D0 is its actual current getter result, not a Core office rank guess.</summary>
public sealed record ArmyReturnGovernorComparatorFields(byte? PersonFlags160,
    byte? PersonOfficeE7, byte? OfficeFlags3D, byte? OfficeOrder37, int? OfficeOrder22E0D0,
    ushort? PersonWord166, byte? PersonByteE8, byte? PersonByteEC);
public interface IArmyReturnGovernorCurrentQueries
{
    ArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int personPhysical);
    int? ReadCurrentAssociatedVirtual98(PersonLinkedEntityReference reference) => null;
}
public enum ArmyReturnGovernorWritePhase { FormerGovernor, SelectedGovernor }
public sealed record ArmyReturnGovernorPersonWrite(int PersonPhysical, byte StatusAfter, ArmyReturnGovernorWritePhase Phase);
public sealed record ArmyReturnGovernorResolution(ArmyReturnGovernorPacket? Packet, string? UnresolvedOwner);

/// <summary>Selected governor and ordered role changes for the current operation.</summary>
public sealed class ArmyReturnGovernorPacket
{
    public BaseReference Base { get; }
    public ushort FormerGovernorWord2E { get; }
    public ushort GovernorWord2EAfter { get; }
    public int? SelectedPersonPhysical { get; }
    public IReadOnlyList<ArmyReturnGovernorPersonWrite> PersonWrites { get; }
    internal ArmyReturnGovernorPacket(BaseReference target, ushort former, ushort after, int? selected,
        ArmyReturnGovernorPersonWrite[] writes)
    {
        Base = target; FormerGovernorWord2E = former; GovernorWord2EAfter = after; SelectedPersonPhysical = selected;
        PersonWrites = Array.AsReadOnly(writes);
    }
}

/// <summary>Ordinary282C10 candidate filtering/stable254440 order followed by
///26D090 governor assignment and241D30 status writes. Reuses actual registered
///Person order and existing position/associated-owner primitives. Source
///temporary candidates use ordinary managed storage. Whole return and
///District branch selection remain with their callers.</summary>
public static class ArmyReturnGovernorUpdater
{
    internal static ArmyReturnGovernorResolution PrepareCurrent(BaseReference target,
        SourceWorldContext context, IArmyReturnGovernorCurrentQueries? queries = null)
    {
        ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(context);
        try
        {
            ushort location = Get(context.ReadBaseObjectLink(target), "current.Base_word4E");
            ushort former = Get(context.ReadArmyReturnBaseGovernor2E(target), "current.Base_governor_word2E");
            var chain = context.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("current.Person_manager100_complete_chain");
            var candidates = new List<int>();
            if (location is >= 1 and <= 61)
            {
                foreach (int? entry in chain)
                {
                    int physical = entry ?? throw Missing("current.Person_manager100_payload");
                    ushort first = Get(context.ReadPersonFirstLocation(physical), $"current.Person[{physical}].first_E2");
                    if (first != location) continue;
                    byte status = Status(physical);
                    if (status is < 1 or > 4) continue; //3346B0→232F10(flag0).
                    ushort second = Get(context.ReadPersonSecondLocation(physical), $"current.Person[{physical}].second_E4");
                    var expected = PersonPositionRepair.DecodeFirstLocation(first);
                    var actual = PersonPositionRepair.DecodeSecondLocation(second);
                    if (actual != expected) continue; //232370's current position mismatch.
                    //TC22AC00/current SC22AE90 uses the existing ordered
                    //associated payload/member semantics. The older helper
                    //comment's SC229ED0 is not used as this profile's anchor.
                    var associated = context.ReadPersonLinkedEntity(physical)
                        ?? throw Missing("current.associated22AC00_owner");
                    if (associated.Identity.HasValue)
                    {
                        bool valid = Get(context.ReadLinkedEntityNativeValidity(associated), "current.associated_validity");
                        if (valid)
                        {
                            int kind = Get(queries?.ReadCurrentAssociatedVirtual98(associated), "current.associated_virtual98");
                            if (kind == 16) continue;
                        }
                    }
                    ushort id = Identity(physical);
                    if (SpecialId(id)) //233240,mode0. Ordinary IDs short-circuit.
                    {
                        var force = context.ReadPersonForce(physical) ?? throw Missing("current.Person_force22CB40");
                        int forceIndex = Get(context.ReadForcePhysicalSlot(force), "current.Force_virtual60");
                        if (forceIndex is < 46 or > 50) continue;
                    }
                    ushort flags = Get(context.ReadArmyReturnPersonFlags196(physical), $"current.Person[{physical}].flags15E");
                    if ((flags & 0x4000) == 0) candidates.Add(physical);
                }
            }
            //24C840 moves only when254440(current,previous) is true.
            //Equal keys retain actual registered-pointer order and duplicates.
            for (int i = 1; i < candidates.Count; i++)
            {
                int item = candidates[i], j = i;
                while (j > 0 && Preferred(item, candidates[j - 1]))
                { candidates[j] = candidates[j - 1]; j--; }
                candidates[j] = item;
            }
            int? selected = candidates.Count == 0 ? Lookup(0) : candidates[0];
            var writes = new List<ArmyReturnGovernorPersonWrite>();
            int? old = Lookup(former <= 6000 ? former : 0);
            if (Valid(old) && Status(old!.Value) is >= 3 and <= 4)
                writes.Add(new(old.Value, 4, ArmyReturnGovernorWritePhase.FormerGovernor));
            ushort after = 0;
            if (Valid(selected))
            {
                after = (ushort)Math.Min((int)Identity(selected!.Value), 6000);
                //The prior write is visible when old and new are the same.
                byte current = writes.LastOrDefault(w => w.PersonPhysical == selected.Value)?.StatusAfter ?? Status(selected.Value);
                if (current is not (1 or 2)) writes.Add(new(selected.Value, 3, ArmyReturnGovernorWritePhase.SelectedGovernor));
            }
            return new(new(target, former, after, selected, writes.ToArray()), null);

            byte Status(int physical)
            { byte value = Get(context.ReadPersonStatus(physical), $"current.Person[{physical}].status_E6"); return value; }
            ushort Identity(int physical)
            { ushort value = Get(context.ReadPersonIdentity(physical), $"current.Person[{physical}].identity10"); return value; }
            int? Lookup(int id)
            {
                var pointer = context.ReadPersonLookupReference(id) ?? throw Missing($"current.Person_lookup[{id}]");
                return pointer.PhysicalSlot;
            }
            bool Valid(int? physical)
            {
                if (!physical.HasValue) return false;
                return Get(context.ReadPersonNativeValidity(physical.Value), $"current.Person[{physical}].native_validity");
            }
            bool Preferred(int left, int right)
            {
                byte a = Status(left), b = Status(right);
                if (a == 1 && b != 1) return true; if (a != 1 && b == 1) return false;
                if (a == 2 && b != 2) return true; if (a != 2 && b == 2) return false;
                var l = queries?.ReadCurrentComparatorFields(left) ?? throw Missing($"current.Person[{left}].governor_comparator");
                var r = queries?.ReadCurrentComparatorFields(right) ?? throw Missing($"current.Person[{right}].governor_comparator");
                bool lf = (Get(l.PersonFlags160, "current.Person_flags160") & 1) != 0;
                bool rf = (Get(r.PersonFlags160, "current.Person_flags160") & 1) != 0;
                if (lf != rf) return lf;
                int lo = OfficeOrder(l), ro = OfficeOrder(r); if (lo != ro) return lo < ro;
                ushort lw = Get(l.PersonWord166, "current.Person_word166"), rw = Get(r.PersonWord166, "current.Person_word166");
                if (lw != rw) return lw < rw;
                byte le = Get(l.PersonByteE8, "current.Person_byteE8"), re = Get(r.PersonByteE8, "current.Person_byteE8");
                if (le != re) return le > re;
                byte lc = Get(l.PersonByteEC, "current.Person_byteEC"), rc = Get(r.PersonByteEC, "current.Person_byteEC");
                if (lc != rc) return lc > rc;
                return Identity(left) < Identity(right);
            }
        }
        catch (MissingOwner error) { return new(null, error.Message); }
    }

    // The enclosing operation owns the context and publishes only on success.
    internal static void ApplyCurrentWrites(SourceWorldContext context, ArmyReturnGovernorPacket packet)
    {
        foreach (var write in packet.PersonWrites.Where(w => w.Phase == ArmyReturnGovernorWritePhase.FormerGovernor))
            context.WritePersonStatus(write.PersonPhysical, write.StatusAfter);
        context.SetArmyReturnBaseGovernor2E(packet.Base, packet.GovernorWord2EAfter);
        foreach (var write in packet.PersonWrites.Where(w => w.Phase == ArmyReturnGovernorWritePhase.SelectedGovernor))
            context.WritePersonStatus(write.PersonPhysical, write.StatusAfter);
    }
    private static int OfficeOrder(ArmyReturnGovernorComparatorFields fields)
    {
        _ = Get(fields.PersonOfficeE7, "current.Person_officeE7");
        byte flags = Get(fields.OfficeFlags3D, "current.Office_flags3D");
        return (flags & 7) != 0 ? Get(fields.OfficeOrder37, "current.Office_order37")
            : Get(fields.OfficeOrder22E0D0, "current.Office_order22E0D0");
    }
    private static bool SpecialId(ushort id) => Enumerable.Range(0, 5).Any(i => id >= 4111 + 20 * i && id <= 4120 + 20 * i);
    private static T Get<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static MissingOwner Missing(string owner) => new(owner);
    private sealed class MissingOwner(string owner) : Exception(owner);
}
