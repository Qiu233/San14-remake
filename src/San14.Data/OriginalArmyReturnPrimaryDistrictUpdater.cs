using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace San14.Data;

public sealed record OriginalArmyReturnPrimaryDistrictResolution(OriginalArmyReturnPrimaryDistrictPacket? Packet, string? UnresolvedOwner);
public sealed class OriginalArmyReturnPrimaryDistrictPacket
{
    public OriginalDistrictReference District { get; }
    public IReadOnlyList<OriginalArmyReturnGovernorPacket> GovernorUpdates { get; }
    internal OriginalEventInitializationContext Owner { get; }
    internal IOriginalArmyReturnGovernorCurrentQueries Queries { get; }
    internal string Fingerprint { get; }
    internal OriginalArmyReturnPrimaryDistrictPacket(OriginalDistrictReference district,
        OriginalArmyReturnGovernorPacket[] updates, OriginalEventInitializationContext owner,
        IOriginalArmyReturnGovernorCurrentQueries queries, string fingerprint)
    { District = district; GovernorUpdates = Array.AsReadOnly(updates); Owner = owner; Queries = queries; Fingerprint = fingerprint; }
}

/// <summary>2E9140(RDX=0,R8=0) ordinary existing-leader District branch.
/// Its actual City pointer table and manager+A0 Gate chain determine order.
/// All governor/status effects are staged and committed together. Other
/// District reselection branches, whole29F220 and temporary allocation failure
/// branches remain explicit unresolved results.</summary>
public static class OriginalArmyReturnPrimaryDistrictUpdater
{
    public const string Revision = "san14:ce-army-return-primary-district-20261003";
    public static OriginalArmyReturnPrimaryDistrictResolution Prepare(OriginalDistrictReference district,
        OriginalEventInitializationContext context, IOriginalArmyReturnGovernorCurrentQueries queries)
        => PrepareCore(district, context, queries, true);

    internal static OriginalArmyReturnPrimaryDistrictResolution PrepareCurrent(OriginalDistrictReference district,
        OriginalEventInitializationContext context, IOriginalArmyReturnGovernorCurrentQueries queries)
        => PrepareCore(district, context, queries, false);

    private static OriginalArmyReturnPrimaryDistrictResolution PrepareCore(OriginalDistrictReference district,
        OriginalEventInitializationContext context, IOriginalArmyReturnGovernorCurrentQueries queries, bool createFingerprint)
    {
        ArgumentNullException.ThrowIfNull(district); ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(queries);
        var reads = new List<object>();
        try
        {
            int physical = district.PhysicalSlot ?? throw Missing("current.District_pointer");
            if (!Get(context.ReadDistrictNativeValidity(physical), "current.District_validity"))
                throw Missing("branch.2E9140_invalid_district");
            byte forceSlot = Get(context.ReadDistrictRawByte(physical, 0x10), "current.District_force10");
            byte number = Get(context.ReadDistrictRawByte(physical, 0x11), "current.District_number11");
            ushort leaderId = Get(context.ReadDistrictLeader(physical), "current.District_leader12");
            var force = context.ReadForceTableReference(forceSlot <= 51 ? forceSlot : 0) ?? throw Missing("current.Force_table");
            int forcePhysical = Get(context.ReadForcePhysicalSlot(force), "current.Force_physical60");
            if (forcePhysical is < 1 or > 45) throw Missing("branch.2E9140_nonordinary_force");
            reads.Add(new { physical, forceSlot, number, leaderId, forcePhysical });
            int? leader = (context.ReadPersonLookupReference(leaderId <= 6000 ? leaderId : 0) ?? throw Missing("current.District_leader_lookup")).PhysicalSlot;
            if (!leader.HasValue) throw Missing("current.District_leader_pointer");

            //22AB60/22AAC0 count iterator matches, including currently invalid
            //Cities. Only the later governor loop tests Base native validity.
            var cities = new List<OriginalBaseReference>();
            int forceCityCount = 0;
            for (int slot = 0; slot < 52; slot++)
            {
                var city = context.ReadCityTableReference(slot) ?? throw Missing($"current.City_table[{slot}]");
                int cityForce = Get(context.ReadBaseForceSlot(city), $"current.City[{slot}].virtualB0");
                reads.Add(new { slot, city, cityForce });
                if (cityForce == forcePhysical) forceCityCount++;
                int cityDistrict = Get(context.ReadBaseDistrictSlot(city), $"current.City[{slot}].virtual78");
                reads.Add(new { slot, cityDistrict });
                if (cityDistrict == physical) cities.Add(city);
            }
            if (forceCityCount == 0) throw Missing("branch.2E9140_force_no_city_293BD0");
            var persons = context.ReadRegisteredPersonPhysicalSlots()?.ToArray() ?? throw Missing("current.Person_manager100_complete_chain");
            reads.Add(persons);
            int forcePersonCount = 0, districtPersonCount = 0;
            foreach (int? entry in persons)
            {
                int person = entry ?? throw Missing("current.Person_manager100_payload");
                byte personDistrict = Get(context.ReadPersonDistrictSlot(person), $"current.Person[{person}].districtE0");
                byte personForce = Get(context.ReadPersonOldForceSlot(person), $"current.Person[{person}].force22CF60");
                reads.Add(new { person, personDistrict, personForce });
                if (personForce != forcePhysical && personDistrict != physical) continue;
                byte status = Get(context.ReadPersonStatus(person), $"current.Person[{person}].statusE6");
                reads.Add(new { person, status });
                if (status is < 1 or > 4) continue;
                ushort id = Get(context.ReadPersonIdentity(person), $"current.Person[{person}].id10");
                ushort first = Get(context.ReadPersonFirstLocation(person), $"current.Person[{person}].firstE2");
                ushort flags = Get(context.ReadArmyReturnPersonFlags196(person), $"current.Person[{person}].flags15E");
                reads.Add(new { person, id, first, flags });
                //230350/230570(mode1,flag1): special synthetic IDs at a Base
                //are excluded for ordinary Forces; flags bit14 also excludes.
                if (SpecialId(id) && first is >= 1 and <= 61)
                {
                    var currentForce = context.ReadPersonForce(person) ?? throw Missing("current.Person_force22CB40");
                    int currentIndex = Get(context.ReadForcePhysicalSlot(currentForce), "current.Person_force_virtual60");
                    reads.Add(new { person, specialCurrentIndex = currentIndex });
                    if (currentIndex is < 46 or > 50) continue;
                }
                if (first is < 1 or > 61)
                {
                    var currentForce = context.ReadPersonForce(person) ?? throw Missing("current.Person_force22CB40");
                    int currentIndex = Get(context.ReadForcePhysicalSlot(currentForce), "current.Person_force_virtual60");
                    reads.Add(new { person, currentIndex });
                    if (currentIndex is >= 46 and <= 50) continue;
                }
                if ((flags & 0x4000) != 0) continue;
                if (personForce == forcePhysical) forcePersonCount++;
                if (personDistrict == physical) districtPersonCount++;
            }
            if (forcePersonCount == 0) throw Missing("branch.2E9140_force_no_person_293BD0");
            //22E930 walks registered District manager+D0, not its pointer table.
            var districts = context.ReadArmyReturnRegisteredDistricts() ?? throw Missing("current.District_managerD0_complete_chain");
            reads.Add(districts.ToArray());
            ushort ruler = Get(context.ReadForceRecord(forcePhysical)?.RawRuler10, "current.Force_ruler10");
            reads.Add(new { ruler });
            bool primaryFound = false;
            foreach (var candidate in districts)
            {
                int at = candidate?.PhysicalSlot ?? throw Missing("current.District_managerD0_payload");
                byte currentForce = Get(context.ReadDistrictRawByte(at, 0x10), "current.registered_District_force10");
                reads.Add(new { at, currentForce });
                if (currentForce != forcePhysical) continue;
                ushort currentLeader = Get(context.ReadDistrictLeader(at), "current.registered_District_leader12");
                reads.Add(new { at, currentLeader });
                if (currentLeader == ruler) { primaryFound = true; break; }
                byte currentNumber = Get(context.ReadDistrictRawByte(at, 0x11), "current.registered_District_number11");
                reads.Add(new { at, currentNumber });
                if (currentNumber == 1) { primaryFound = true; break; }
            }
            if (!primaryFound)
            {
                var fallback = context.ReadDistrictTableReference(0) ?? throw Missing("current.PrimaryDistrict_fallback_table0");
                reads.Add(new { fallback });
            }
            if (cities.Count == 0 || districtPersonCount == 0)
                throw Missing("branch.2E9140_district_empty_reselection");
            byte leaderDistrict = Get(context.ReadPersonDistrictSlot(leader.Value), "current.District_leader_districtE0");
            reads.Add(new { leader, leaderDistrict });
            byte leaderStatus = Get(context.ReadPersonStatus(leader.Value), "current.District_leader_statusE6");
            reads.Add(new { leaderStatus });
            //2E923A..9284: same rawE0 and (number1 OR leader status1/2) reaches
            //9494 directly. An ordinary secondary District with its actual
            //status2 leader does not run the reselection body.
            if (leaderDistrict != physical || number != 1 && leaderStatus is not (1 or 2))
                throw Missing("branch.2E9140_leader_reselection");
            var capacity = queries.ReadCurrentTemporaryPoolCapacity() ?? throw Missing("current.District_temporary_pool_capacity");
            if (capacity.FreeNodeCount < 0 || capacity.AvailableEmptyDescriptorCount < 0)
                throw new InvalidDataException("Current temporary pool capacity is invalid.");
            reads.Add(capacity);
            var gates = context.ReadArmyReturnRegisteredGates()?.ToArray() ?? throw Missing("current.Gate_managerA0_complete_chain");
            reads.Add(gates);
            //The operation-local caller now executes the real temporary lists
            //and governors in order on this same context. Only select the
            //ordinary existing-leader branch here; do not rehearse its body a second
            //time in a detached governor draft.
            if (!createFingerprint) return new(new(district, [], context, queries, ""), null);
            var draft = context.CreateArmyReturnGovernorDraft();
            var updates = new List<OriginalArmyReturnGovernorPacket>();
            var scopedQueries = new ScopedQueries(queries, capacity);
            foreach (var city in cities)
            {
                UpdateIfValid(city, scopedQueries);
                //2E94EC allocates the Gate result descriptor even for an empty
                //result;22D110 tail-appends every owner-City pointer match.
                if (capacity.AvailableEmptyDescriptorCount < 1)
                    throw Missing("current.District_gate_descriptor_allocation_failure");
                var associatedGates = new List<OriginalBaseReference>();
                foreach (var gate in gates)
                {
                    if (gate is null || gate.Kind != OriginalBaseKind.Gate)
                        throw Missing("current.Gate_managerA0_payload");
                    var ownerCity = OriginalCurrentGateMapQueries.FindCity(gate, context);
                    reads.Add(new { gate, ownerCity });
                    if (ownerCity == city) associatedGates.Add(gate);
                }
                if (capacity.FreeNodeCount < associatedGates.Count)
                    throw Missing("current.District_gate_node_allocation_failure");
                var retainedGateCapacity = new OriginalArmyReturnGovernorPoolCapacity(capacity.FreeNodeCount - associatedGates.Count,
                    capacity.AvailableEmptyDescriptorCount - 1);
                var gateQueries = new ScopedQueries(queries, retainedGateCapacity);
                foreach (var gate in associatedGates) UpdateIfValid(gate, gateQueries);
            }
            string fingerprint = createFingerprint
                ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reads)))) : "";
            return new(new(district, updates.ToArray(), context, queries, fingerprint), null);

            void UpdateIfValid(OriginalBaseReference target, IOriginalArmyReturnGovernorCurrentQueries scoped)
            {
                bool valid = Get(context.ReadBaseNativeValidity(target), "current.District_Base_validity");
                reads.Add(new { target, valid });
                if (!valid) return;
                var result = createFingerprint ? OriginalArmyReturnGovernorUpdater.Prepare(target, draft, scoped)
                    : OriginalArmyReturnGovernorUpdater.PrepareCurrent(target, draft, scoped);
                var update = result.Packet ?? throw Missing(result.UnresolvedOwner ?? "current.Governor_unknown");
                reads.Add(new { target, update.Fingerprint });
                if (createFingerprint)
                {
                    if (!OriginalArmyReturnGovernorUpdater.ApplyPrepared(update)) throw Missing("current.Governor_query_changed_during_prepare");
                }
                else OriginalArmyReturnGovernorUpdater.ApplyCurrentWrites(update);
                updates.Add(update);
            }
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.District_input: " + error.Message); }
    }

    /// <summary>Revalidates the complete current recipe before any live write;
    /// then pure dictionary writes follow the original City/Gate order.</summary>
    public static bool ApplyPrepared(OriginalArmyReturnPrimaryDistrictPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var current = Prepare(packet.District, packet.Owner, packet.Queries).Packet;
        if (current is null || current.Fingerprint != packet.Fingerprint) return false;
        foreach (var update in current.GovernorUpdates)
        {
            foreach (var write in update.PersonWrites.Where(w => w.SourceStep.StartsWith("26D0DE", StringComparison.Ordinal)))
                packet.Owner.WritePersonStatus(write.PersonPhysical, write.StatusAfter);
            packet.Owner.SetArmyReturnBaseGovernor2E(update.Base, update.GovernorWord2EAfter);
            foreach (var write in update.PersonWrites.Where(w => w.SourceStep.StartsWith("26D11C", StringComparison.Ordinal)))
                packet.Owner.WritePersonStatus(write.PersonPhysical, write.StatusAfter);
        }
        return true;
    }
    private sealed class ScopedQueries(IOriginalArmyReturnGovernorCurrentQueries owner,
        OriginalArmyReturnGovernorPoolCapacity capacity) : IOriginalArmyReturnGovernorCurrentQueries
    {
        public OriginalArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int person) => owner.ReadCurrentComparatorFields(person);
        public int? ReadCurrentAssociatedVirtual98(OriginalPersonLinkedEntityReference reference) => owner.ReadCurrentAssociatedVirtual98(reference);
        public OriginalArmyReturnGovernorPoolCapacity? ReadCurrentTemporaryPoolCapacity() => capacity;
    }
    private static bool SpecialId(ushort id) => Enumerable.Range(0, 5).Any(i => id >= 4111 + 20 * i && id <= 4120 + 20 * i);
    private static T Get<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static MissingOwner Missing(string owner) => new(owner);
    private sealed class MissingOwner(string owner) : Exception(owner);
}
