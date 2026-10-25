using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyNormalTurnAction31To38Current(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int action)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw NormalMaintenanceMissing("shared_current_pool");
        return action switch
        {
            31 => ApplyNormalTurnSpecialPersonnelReleasesCurrent(state, current, pool),
            32 => RestoreNormalTurnDuelReadinessCurrent(state, current),
            33 => ConsumeNormalTurnEncirclementCurrent(state, current),
            34 => ApplyNormalTurnArmyPassiveTraitsCurrent(state, current, pool),
            35 => MaintainNormalTurnAppointmentsCurrent(state, current),
            36 => ApplyNormalTurnTerritoryMaintenanceCurrent(state, current, pool),
            37 => RefreshNormalTurnRegionSnapshotsCurrent(state, current),
            38 => state with { FacilityFoodReduction = new(ArmyFoodTerrainModifiers.Capture(state, current).ToImmutableArray()) },
            _ => throw NormalMaintenanceMissing("action_out_of_range")
        };
    }

    //42F0B0→2E7800: native Person100 order and validity precede the F1
    //filter. The direct2E7730 writer already owns health caps/strength/rounding.
    private GameState RestoreNormalTurnDuelReadinessCurrent(GameState state, SourceWorldContext current)
    {
        foreach (int physical in NormalMaintenanceRegistry(current, RegistryKind.Person))
        {
            if (!NormalMaintenanceNeed(current.ReadPersonNativeValidity(physical), "readiness_Person_validity")) continue;
            int id = NormalMaintenanceNeed(current.ReadPersonIdentity(physical), "readiness_Person_ID");
            byte readiness = state.Officers.GetValueOrDefault(id)?.DuelCondition?.Readiness
                ?? throw NormalMaintenanceMissing("readiness_F1");
            if (readiness < 100) state = RestoreDuelReadinessDuringArmyOperation(state, current, id);
        }
        return state;
    }

    //428340→2758F0→2755F0. Use the same complete appointment-maintenance
    //kernel as formal district creation, without changing unrelated people.
    private GameState MaintainNormalTurnAppointmentsCurrent(GameState state, SourceWorldContext current)
    {
        //This always-reached Turn action also runs outside the monthly branch.
        //Bind its latest saved Area/City appointments and Person vectors here;
        //action6's monthly binding is neither guaranteed nor a current owner.
        //The existing binder preserves unknown legacy fields and physical0
        //through the saved assignment bank, without a scenario fallback.
        _personAssignmentPrecallInputs.BindCurrent(state, current);
        var beforeAreas = NormalMaintenanceRegistry(current, RegistryKind.Area).ToDictionary(id => id,
            id => NormalMaintenanceNeed(current.ReadAssignmentArea36(id), "appointment_Area36"));
        var touchedCities = new List<int>();
        foreach (int physical in NormalMaintenanceRegistry(current, RegistryKind.City))
        {
            var city = current.ReadCityTableReference(physical) ?? throw NormalMaintenanceMissing("appointment_City_pointer");
            ushort recruiter = NormalMaintenanceNeed(current.ReadAssignmentCityCA(physical), "appointment_CityCA");
            ushort trainer = NormalMaintenanceNeed(current.ReadAssignmentCityCC(physical), "appointment_CityCC");
            MaintainDistrictAdministrationAppointmentsCurrent(current, city);
            if (recruiter != NormalMaintenanceNeed(current.ReadAssignmentCityCA(physical), "appointment_CityCA_after") ||
                trainer != NormalMaintenanceNeed(current.ReadAssignmentCityCC(physical), "appointment_CityCC_after"))
                touchedCities.Add(physical);
        }
        var touchedAreas = beforeAreas.Where(row => row.Value !=
            NormalMaintenanceNeed(current.ReadAssignmentArea36(row.Key), "appointment_Area36_after")).ToArray();
        if (touchedCities.Count != 0 || touchedAreas.Length != 0)
        {
            state = PersonAssignmentPrecallInputs.PublishAreaAppointments(state, current, touchedCities,
                touchedAreas.Select(row => row.Key), []);
            var people = new PersonAssignmentPrecallInputs.AreaAppointmentDraft(state);
            foreach (int id in touchedAreas.Select(row => (int)row.Value).Distinct())
            {
                int person = (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
                    ?? throw NormalMaintenanceMissing("appointment_removed_Person_pointer");
                people.Write(person, current.ReadAssignmentPersonAreaVector(person)
                    ?? throw NormalMaintenanceMissing("appointment_removed_Person_vector"));
            }
            state = people.Publish(state);
            if (touchedAreas.Length != 0)
            {
                var simulation = state.NativeArmySimulation ?? throw NormalMaintenanceMissing("appointment_parameter_owner");
                var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
                foreach (var row in touchedAreas)
                    values[PersonAssignmentPrecallInputs.AreaFieldKey(row.Key, 0x6D)] =
                        NormalMaintenanceNeed(current.ReadAssignmentArea6D(row.Key), "appointment_bonus_after");
                state = state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
            }
        }
        return BaseCaptureCurrentInputs.PublishCurrent(state, current);
    }

    //42F780→33E2E0 then2EC4C0. These are scheduled gameplay snapshots,
    //not a correctness scan or a getter-time refresh of regional factors.
    private static GameState RefreshNormalTurnRegionSnapshotsCurrent(GameState state, SourceWorldContext current)
    {
        var counts = new int[501];
        for (int slot = 0; slot < 48400; slot++)
        {
            var hex = current.ReadHexTableReference(slot) ?? throw NormalMaintenanceMissing("statistics_Hex_pointer");
            int raw = NormalMaintenanceNeed(current.ReadHexAreaSlot(hex), "statistics_Hex12");
            var area = current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw NormalMaintenanceMissing("statistics_Area_pointer");
            if (!NormalMaintenanceNeed(current.ReadAreaNativeValidity(area), "statistics_Area_validity")) continue;
            int force = NormalMaintenanceNeed(current.ReadAreaCurrentForceSlot(area), "statistics_Area_Force");
            if (force is < 1 or > 51 || NormalMaintenanceNeed(current.ReadHexForceSlot(hex), "statistics_Hex14") != force) continue;
            int physical = NormalMaintenanceNeed(area.PhysicalSlot, "statistics_Area_physical");
            counts[physical] = unchecked(counts[physical] + 1);
        }
        var saved = state.RegionStatistics ?? throw NormalMaintenanceMissing("statistics_saved_owner");
        var entries = new Dictionary<int, AreaMapStatisticsEntry>(saved.Entries);
        foreach (int physical in NormalMaintenanceRegistry(current, RegistryKind.Area))
        {
            var area = new AreaReference(physical);
            ushort count = checked((ushort)Math.Clamp(counts[physical], 0, ushort.MaxValue));
            int force = Math.Clamp((int)NormalMaintenanceNeed(current.ReadAreaCurrentForceSlot(area), "statistics_snapshot_Force"), 0, 51);
            current.WriteAreaRawField68(area, count);
            current.WriteAreaRawField78(area, (byte)force);
            var previous = entries.GetValueOrDefault(physical) ?? throw NormalMaintenanceMissing("statistics_saved_Area");
            //Null is the domain encoding of native no-Force0; the independent
            //current flag records that this is a known scheduled snapshot.
            entries[physical] = previous with { OwnedCellCount = count, ForceId = force == 0 ? null : force,
                IsOwnedCountCurrent = true };
        }
        state = state with { RegionStatistics = saved with { Entries = entries } };
        for (int slot = 0; slot <= 500; slot++)
        {
            var army = current.ReadArmyUnitTableReference(slot) ?? throw NormalMaintenanceMissing("region_Army_pointer");
            if (!NormalMaintenanceNeed(current.ReadArmyUnitNativeValidity(army), "region_Army_validity")) continue;
            var force = current.ReadArmyUnitForceReference(army) ?? throw NormalMaintenanceMissing("region_Army_Force");
            int owner = NormalMaintenanceNeed(current.ReadForcePhysicalSlot(force), "region_Army_Force_physical");
            if (owner is >= 46 and <= 51) continue;
            int physical = NormalMaintenanceNeed(army.PhysicalSlot, "region_Army_physical");
            ushort rawHex = NormalMaintenanceNeed(current.ReadArmyUnitRawHex2A(army), "region_Army2A");
            var image = NotificationArmy(state, physical) with { NativeSavedRegionHex152 = (ushort)Math.Min((int)rawHex, 48399) };
            var lifecycle = state.ReadArmyLifecycle() ?? throw NormalMaintenanceMissing("region_lifecycle");
            var receiver = lifecycle.ReceiverSlots()[physical];
            state = receiver.ActiveUnitId is int id
                ? state with { Units = new(state.Units) { [id] = image } }
                : state.WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.With(receiver.Receiver with { PendingImage = image }) });
        }
        return state;
    }

    private static IEnumerable<int> NormalMaintenanceRegistry(SourceWorldContext current, RegistryKind kind)
    {
        foreach (var row in current.ReadRegistryReferences(kind) ?? throw NormalMaintenanceMissing("registry_" + kind))
        {
            if (row is null || row.Kind != kind) throw NormalMaintenanceMissing("registry_payload_" + kind);
            yield return NormalMaintenanceNeed(row.PhysicalSlot, "registry_pointer");
        }
    }
    private static T NormalMaintenanceNeed<T>(T? value, string owner) where T : struct => value ?? throw NormalMaintenanceMissing(owner);
    private static InvalidDataException NormalMaintenanceMissing(string owner) => new("normal.turn.maintenance." + owner);
}
