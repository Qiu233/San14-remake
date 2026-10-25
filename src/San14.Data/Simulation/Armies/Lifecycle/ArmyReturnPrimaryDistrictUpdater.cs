using San14.Core;
namespace San14.Data;

public sealed record ArmyReturnPrimaryDistrictResolution(ArmyReturnPrimaryDistrictPacket? Packet, string? UnresolvedOwner);
public sealed class ArmyReturnPrimaryDistrictPacket
{
    public DistrictReference District { get; }
    public IReadOnlyList<ArmyReturnGovernorPacket> GovernorUpdates { get; }
    internal ArmyReturnPrimaryDistrictPacket(DistrictReference district,
        ArmyReturnGovernorPacket[] updates)
    { District = district; GovernorUpdates = Array.AsReadOnly(updates); }
}

/// <summary>2E9140 ordinary District maintenance and actual leader selection.
/// Its actual City pointer table and manager+A0 Gate chain determine order.
/// Executes governor/status effects on the caller owned operation context.
/// The enclosing operation supplies state ownership for reached reorganization.</summary>
public static class ArmyReturnPrimaryDistrictUpdater
{
    internal static ArmyReturnPrimaryDistrictResolution ApplyCurrent(DistrictReference district,
        SourceWorldContext context, IArmyReturnGovernorCurrentQueries queries)
    {
        ArgumentNullException.ThrowIfNull(district); ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(queries);
        try
        {
            int physical = district.PhysicalSlot ?? throw Missing("current.District_pointer");
            if (!Get(context.ReadDistrictNativeValidity(physical), "current.District_validity"))
                return new(new(district, []), null);
            byte forceSlot = Get(context.ReadDistrictForceId(physical), "current.District_force10");
            byte number = Get(context.ReadDistrictNumber(physical), "current.District_number11");
            ushort leaderId = Get(context.ReadDistrictLeader(physical), "current.District_leader12");
            var force = context.ReadForceTableReference(forceSlot <= 51 ? forceSlot : 0) ?? throw Missing("current.Force_table");
            int forcePhysical = Get(context.ReadForcePhysicalSlot(force), "current.Force_physical60");
            if (forcePhysical is < 1 or > 45) throw Missing("branch.2E9140_nonordinary_force");
            int? leader = (context.ReadPersonLookupReference(leaderId <= 6000 ? leaderId : 0) ?? throw Missing("current.District_leader_lookup")).PhysicalSlot;
            if (!leader.HasValue) throw Missing("current.District_leader_pointer");

            //22AB60/22AAC0 count iterator matches, including currently invalid
            //Cities. Only the later governor loop tests Base native validity.
            var cities = new List<BaseReference>();
            int forceCityCount = 0;
            for (int slot = 0; slot < 52; slot++)
            {
                var city = context.ReadCityTableReference(slot) ?? throw Missing($"current.City_table[{slot}]");
                int cityForce = Get(context.ReadBaseForceSlot(city), $"current.City[{slot}].virtualB0");
                if (cityForce == forcePhysical) forceCityCount++;
                int cityDistrict = Get(context.ReadBaseDistrictSlot(city), $"current.City[{slot}].virtual78");
                if (cityDistrict == physical) cities.Add(city);
            }
            if (forceCityCount == 0) return UnregisterForce();
            var persons = context.ReadRegisteredPersonPhysicalSlots()?.ToArray() ?? throw Missing("current.Person_manager100_complete_chain");
            int forcePersonCount = 0, districtPersonCount = 0;
            foreach (int? entry in persons)
            {
                int person = entry ?? throw Missing("current.Person_manager100_payload");
                byte personDistrict = Get(context.ReadPersonDistrictSlot(person), $"current.Person[{person}].districtE0");
                byte personForce = Get(context.ReadPersonOldForceSlot(person), $"current.Person[{person}].force22CF60");
                if (personForce != forcePhysical && personDistrict != physical) continue;
                byte status = Get(context.ReadPersonStatus(person), $"current.Person[{person}].statusE6");
                if (status is < 1 or > 4) continue;
                ushort id = Get(context.ReadPersonIdentity(person), $"current.Person[{person}].id10");
                ushort first = Get(context.ReadPersonFirstLocation(person), $"current.Person[{person}].firstE2");
                bool ordinaryPerson = Get(OfficerBehaviorRules.UsesOrdinaryPersonRules(context.ReadPersonBehavior(person)), $"current.Person[{person}].flags15E");
                //230350/230570(mode1,flag1): special synthetic IDs at a Base
                //are excluded for ordinary Forces; flags bit14 also excludes.
                if (SpecialId(id) && first is >= 1 and <= 61)
                {
                    var currentForce = context.ReadPersonForce(person) ?? throw Missing("current.Person_force22CB40");
                    int currentIndex = Get(context.ReadForcePhysicalSlot(currentForce), "current.Person_force_virtual60");
                    if (currentIndex is < 46 or > 50) continue;
                }
                if (first is < 1 or > 61)
                {
                    var currentForce = context.ReadPersonForce(person) ?? throw Missing("current.Person_force22CB40");
                    int currentIndex = Get(context.ReadForcePhysicalSlot(currentForce), "current.Person_force_virtual60");
                    if (currentIndex is >= 46 and <= 50) continue;
                }
                if (!ordinaryPerson) continue;
                if (personForce == forcePhysical) forcePersonCount++;
                if (personDistrict == physical) districtPersonCount++;
            }
            if (forcePersonCount == 0) return UnregisterForce();
            //22E930 walks registered District manager+D0, not its pointer table.
            var primary = ForcePrimaryDistrictSource.Resolve(context, force);
            if (cities.Count == 0 || districtPersonCount == 0)
            {
                var operation = queries as ArmyReturnDistrictOperationQueries
                    ?? throw Missing("current.District_reorganization_state_owner");
                if (leader is int leaderAt && Get(context.ReadPersonNativeValidity(leaderAt), "empty_District_leader_validity") &&
                    Get(context.ReadPersonStatus(leaderAt), "empty_District_leaderE6") == 1)
                {
                    var inherited = PersonReturnDistrictReorganizationSource.InheritPrimaryCurrent(context, district, force, operation);
                    return new(new(district, inherited.ToArray()), null);
                }
                var merged = PersonReturnDistrictReorganizationSource.MergeCurrent(context, district, primary, operation);
                return new(new(district, merged.ToArray()), null);
            }
            byte leaderDistrict = Get(context.ReadPersonDistrictSlot(leader.Value), "current.District_leader_districtE0");
            byte leaderStatus = Get(context.ReadPersonStatus(leader.Value), "current.District_leader_statusE6");
            //2E923A..9284: same rawE0 and (number1 OR leader status1/2) reaches
            //9494 directly. An ordinary secondary District with its actual
            //status2 leader does not run the reselection body.
            if (leaderDistrict != physical || number != 1 && leaderStatus is not (1 or 2))
                PersonReturnDistrictReorganizationSource.ReselectLeader(context, district, queries);
            var gates = context.ReadArmyReturnRegisteredGates() ?? throw Missing("current.Gate_managerA0_complete_chain");
            var updates = new List<ArmyReturnGovernorPacket>();
            foreach (var city in cities)
            {
                UpdateIfValid(city);
                // Retain the manager order, duplicates and City association before
                // running Gate governors. This list belongs only to this operation.
                var associatedGates = new List<BaseReference>();
                foreach (var gate in gates)
                {
                    if (gate is null || gate.Kind != BaseKind.Gate)
                        throw Missing("current.Gate_managerA0_payload");
                    if (CurrentGateMapQueries.FindCity(gate, context) == city) associatedGates.Add(gate);
                }
                foreach (var gate in associatedGates) UpdateIfValid(gate);
            }
            return new(new(district, updates.ToArray()), null);

            ArmyReturnPrimaryDistrictResolution UnregisterForce()
            {
                var operation = queries as ArmyReturnDistrictOperationQueries
                    ?? throw Missing("current.Force_unregistration_state_owner");
                if (operation.ForceRetirementHandler is { } retire)
                {
                    operation.Publish(retire(forcePhysical));
                    return new(new(district, []), null);
                }
                var package = operation.Package ?? throw Missing("current.Force_unregistration_rules");
                var result = new ForceUnregisterSource(package).TryApplyBorrowedCurrent(operation.Latest(), forcePhysical, 0,
                    context, context.ReadOwnedCurrentListPool() ?? throw Missing("current.Force_unregistration_pool"));
                operation.Publish(result.State ?? throw Missing(result.UnresolvedOwner ?? "current.293BD0"));
                return new(new(district, result.Receipt?.GovernorUpdates ?? []), null);
            }

            void UpdateIfValid(BaseReference target)
            {
                if (!Get(context.ReadBaseNativeValidity(target), "current.District_Base_validity")) return;
                var selected = ArmyReturnGovernorUpdater.PrepareCurrent(target, context, queries);
                var update = selected.Packet ?? throw Missing(selected.UnresolvedOwner ?? "current.Governor_unknown");
                ArmyReturnGovernorUpdater.ApplyCurrentWrites(context, update);
                updates.Add(update);
            }
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.District_input: " + error.Message); }
    }

    private static bool SpecialId(ushort id) => Enumerable.Range(0, 5).Any(i => id >= 4111 + 20 * i && id <= 4120 + 20 * i);
    private static T Get<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static MissingOwner Missing(string owner) => new(owner);
    private sealed class MissingOwner(string owner) : Exception(owner);
}
