using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace San14.Data;

/// <summary>Only current comparator/getter fields absent from the shared
/// Context. TC offsets are named explicitly; SC Person offsets add38. Office
///22E0D0 is its actual current getter result, not a Core office rank guess.</summary>
public sealed record OriginalArmyReturnGovernorComparatorFields(byte? PersonFlags160,
    byte? PersonOfficeE7, byte? OfficeFlags3D, byte? OfficeOrder37, int? OfficeOrder22E0D0,
    ushort? PersonWord166, byte? PersonByteE8, byte? PersonByteEC);
public sealed record OriginalArmyReturnGovernorPoolCapacity(int FreeNodeCount, int AvailableEmptyDescriptorCount);
public interface IOriginalArmyReturnGovernorCurrentQueries
{
    OriginalArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int personPhysical);
    int? ReadCurrentAssociatedVirtual98(OriginalPersonLinkedEntityReference reference) => null;
    OriginalArmyReturnGovernorPoolCapacity? ReadCurrentTemporaryPoolCapacity() => null;
}
public sealed record OriginalArmyReturnGovernorPersonWrite(int PersonPhysical, byte StatusAfter, string SourceStep);
public sealed record OriginalArmyReturnGovernorResolution(OriginalArmyReturnGovernorPacket? Packet, string? UnresolvedOwner);

/// <summary>A packet can only be created by the source selector/setter. It is
/// bound to its exact owned Context and current query owner, not a callback
/// claiming the whole return or District update already finished.</summary>
public sealed class OriginalArmyReturnGovernorPacket
{
    public OriginalBaseReference Base { get; }
    public ushort FormerGovernorWord2E { get; }
    public ushort GovernorWord2EAfter { get; }
    public int? SelectedPersonPhysical { get; }
    public IReadOnlyList<OriginalArmyReturnGovernorPersonWrite> PersonWrites { get; }
    internal OriginalEventInitializationContext Owner { get; }
    internal IOriginalArmyReturnGovernorCurrentQueries? Queries { get; }
    internal string Fingerprint { get; }
    internal IReadOnlyList<int> CandidateOrderBeforeSort { get; }
    internal IReadOnlyList<int> CandidateOrderAfterSort { get; }
    internal OriginalArmyReturnGovernorPacket(OriginalBaseReference target, ushort former, ushort after, int? selected,
        OriginalArmyReturnGovernorPersonWrite[] writes, OriginalEventInitializationContext owner,
        IOriginalArmyReturnGovernorCurrentQueries? queries, string fingerprint, int[] beforeSort, int[] afterSort)
    {
        Base = target; FormerGovernorWord2E = former; GovernorWord2EAfter = after; SelectedPersonPhysical = selected;
        PersonWrites = Array.AsReadOnly(writes); Owner = owner; Queries = queries; Fingerprint = fingerprint;
        CandidateOrderBeforeSort = Array.AsReadOnly(beforeSort); CandidateOrderAfterSort = Array.AsReadOnly(afterSort);
    }
}

/// <summary>Ordinary282C10 candidate filtering/stable254440 order followed by
///26D090 governor assignment and241D30 status writes. Reuses actual registered
///Person order and existing position/associated-owner primitives. Source
///temporary descriptor/node allocation failure is not guessed: this retained
///packet requires current owned sufficient pool capacity. Whole29F220 and its
///2E9140 District caller remain separate until their other effects close.</summary>
public static class OriginalArmyReturnGovernorUpdater
{
    public const string Revision = "san14:ce-ordinary-return-governor-update-20261003";
    public static OriginalArmyReturnGovernorResolution Prepare(OriginalBaseReference target,
        OriginalEventInitializationContext context, IOriginalArmyReturnGovernorCurrentQueries? queries = null)
        => PrepareCore(target, context, queries, true);

    internal static OriginalArmyReturnGovernorResolution PrepareCurrent(OriginalBaseReference target,
        OriginalEventInitializationContext context, IOriginalArmyReturnGovernorCurrentQueries? queries = null)
        => PrepareCore(target, context, queries, false);

    private static OriginalArmyReturnGovernorResolution PrepareCore(OriginalBaseReference target,
        OriginalEventInitializationContext context, IOriginalArmyReturnGovernorCurrentQueries? queries, bool createFingerprint)
    {
        ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(context);
        var reads = new List<object>();
        try
        {
            ushort location = Get(context.ReadBaseObjectLink(target), "current.Base_word4E");
            ushort former = Get(context.ReadArmyReturnBaseGovernor2E(target), "current.Base_governor_word2E");
            reads.Add(new { location, former });
            var chain = context.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("current.Person_manager100_complete_chain");
            reads.Add(chain.ToArray());
            var candidates = new List<int>();
            if (location is >= 1 and <= 61)
            {
                foreach (int? entry in chain)
                {
                    int physical = entry ?? throw Missing("current.Person_manager100_payload");
                    ushort first = Get(context.ReadPersonFirstLocation(physical), $"current.Person[{physical}].first_E2");
                    reads.Add(new { physical, first });
                    if (first != location) continue;
                    byte status = Status(physical);
                    if (status is < 1 or > 4) continue; //3346B0→232F10(flag0).
                    ushort second = Get(context.ReadPersonSecondLocation(physical), $"current.Person[{physical}].second_E4");
                    reads.Add(new { physical, second });
                    var expected = OriginalPersonPositionRepair.DecodeFirstLocation(first);
                    var actual = OriginalPersonPositionRepair.DecodeSecondLocation(second);
                    if (actual != expected) continue; //232370's current position mismatch.
                    //TC22AC00/current SC22AE90 uses the existing ordered
                    //associated payload/member semantics. The older helper
                    //comment's SC229ED0 is not used as this profile's anchor.
                    var associated = context.ReadPersonLinkedEntity(physical)
                        ?? throw Missing("current.associated22AC00_owner");
                    reads.Add(new { physical, associated.Identity });
                    if (associated.Identity.HasValue)
                    {
                        bool valid = Get(context.ReadLinkedEntityNativeValidity(associated), "current.associated_validity");
                        reads.Add(new { associated.Identity, valid });
                        if (valid)
                        {
                            int kind = Get(queries?.ReadCurrentAssociatedVirtual98(associated), "current.associated_virtual98");
                            reads.Add(new { associated.Identity, kind });
                            if (kind == 16) continue;
                        }
                    }
                    ushort id = Identity(physical);
                    if (SpecialId(id)) //233240,mode0. Ordinary IDs short-circuit.
                    {
                        var force = context.ReadPersonForce(physical) ?? throw Missing("current.Person_force22CB40");
                        int forceIndex = Get(context.ReadForcePhysicalSlot(force), "current.Force_virtual60");
                        reads.Add(new { physical, forceIndex });
                        if (forceIndex is < 46 or > 50) continue;
                    }
                    ushort flags = Get(context.ReadArmyReturnPersonFlags196(physical), $"current.Person[{physical}].flags15E");
                    reads.Add(new { physical, flags });
                    if ((flags & 0x4000) == 0) candidates.Add(physical);
                }
            }
            var capacity = queries?.ReadCurrentTemporaryPoolCapacity()
                ?? throw Missing("current.Governor_temporary_pool_capacity");
            if (capacity.FreeNodeCount < 0 || capacity.AvailableEmptyDescriptorCount < 0)
                throw new InvalidDataException("Current governor temporary pool capacity is invalid.");
            reads.Add(capacity);
            if (capacity.AvailableEmptyDescriptorCount < 1 || capacity.FreeNodeCount < candidates.Count)
                throw Missing("current.Governor_temporary_allocation_failure_branch");
            //24C840 moves only when254440(current,previous) is true.
            //Equal keys retain actual registered-pointer order and duplicates.
            int[] beforeSort = candidates.ToArray();
            for (int i = 1; i < candidates.Count; i++)
            {
                int item = candidates[i], j = i;
                while (j > 0 && Preferred(item, candidates[j - 1]))
                { candidates[j] = candidates[j - 1]; j--; }
                candidates[j] = item;
            }
            int? selected = candidates.Count == 0 ? Lookup(0) : candidates[0];
            reads.Add(new { selected, candidates });
            var writes = new List<OriginalArmyReturnGovernorPersonWrite>();
            int? old = Lookup(former <= 6000 ? former : 0);
            if (Valid(old) && Status(old!.Value) is >= 3 and <= 4)
                writes.Add(new(old.Value, 4, "26D0DE→241D30.old-governor"));
            ushort after = 0;
            if (Valid(selected))
            {
                after = (ushort)Math.Min((int)Identity(selected!.Value), 6000);
                //The prior write is visible when old and new are the same.
                byte current = writes.LastOrDefault(w => w.PersonPhysical == selected.Value)?.StatusAfter ?? Status(selected.Value);
                if (current is not (1 or 2)) writes.Add(new(selected.Value, 3, "26D11C→241D30.new-governor"));
            }
            string fingerprint = createFingerprint
                ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reads)))) : "";
            return new(new(target, former, after, selected, writes.ToArray(), context, queries, fingerprint, beforeSort, candidates.ToArray()), null);

            byte Status(int physical)
            { byte value = Get(context.ReadPersonStatus(physical), $"current.Person[{physical}].status_E6"); reads.Add(new { physical, status = value }); return value; }
            ushort Identity(int physical)
            { ushort value = Get(context.ReadPersonIdentity(physical), $"current.Person[{physical}].identity10"); reads.Add(new { physical, id = value }); return value; }
            int? Lookup(int id)
            {
                var pointer = context.ReadPersonLookupReference(id) ?? throw Missing($"current.Person_lookup[{id}]");
                reads.Add(new { lookupId = id, pointer.PhysicalSlot }); return pointer.PhysicalSlot;
            }
            bool Valid(int? physical)
            {
                if (!physical.HasValue) return false;
                bool value = Get(context.ReadPersonNativeValidity(physical.Value), $"current.Person[{physical}].native_validity");
                reads.Add(new { physical, valid = value }); return value;
            }
            bool Preferred(int left, int right)
            {
                byte a = Status(left), b = Status(right);
                if (a == 1 && b != 1) return true; if (a != 1 && b == 1) return false;
                if (a == 2 && b != 2) return true; if (a != 2 && b == 2) return false;
                var l = queries?.ReadCurrentComparatorFields(left) ?? throw Missing($"current.Person[{left}].governor_comparator");
                var r = queries?.ReadCurrentComparatorFields(right) ?? throw Missing($"current.Person[{right}].governor_comparator");
                reads.Add(new { left, l, right, r });
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

    /// <summary>Re-prepare against the same live owner before the first write.
    /// All writes use validated pure Context dictionary setters. A stale/missing
    /// source read produces no partial governor or person-role mutation.</summary>
    public static bool ApplyPrepared(OriginalArmyReturnGovernorPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var current = Prepare(packet.Base, packet.Owner, packet.Queries).Packet;
        if (current is null || current.Fingerprint != packet.Fingerprint) return false;
        ApplyCurrentWrites(packet);
        return true;
    }

    //The caller owns this private operation context. Prepare once and write in
    //source order; its enclosing operation discards the context on unknown.
    internal static void ApplyCurrentWrites(OriginalArmyReturnGovernorPacket packet)
    {
        foreach (var write in packet.PersonWrites.Where(w => w.SourceStep.StartsWith("26D0DE", StringComparison.Ordinal)))
            packet.Owner.WritePersonStatus(write.PersonPhysical, write.StatusAfter);
        packet.Owner.SetArmyReturnBaseGovernor2E(packet.Base, packet.GovernorWord2EAfter);
        foreach (var write in packet.PersonWrites.Where(w => w.SourceStep.StartsWith("26D11C", StringComparison.Ordinal)))
            packet.Owner.WritePersonStatus(write.PersonPhysical, write.StatusAfter);
    }
    private static int OfficeOrder(OriginalArmyReturnGovernorComparatorFields fields)
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
