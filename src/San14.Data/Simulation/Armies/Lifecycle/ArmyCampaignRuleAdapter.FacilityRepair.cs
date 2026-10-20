using San14.Core;

namespace San14.Data;

public sealed record ArmyFacilityRepairResult(GameState? State,
    IReadOnlyList<int> EligibleReceiverSlots, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordered hour10 repair manager. Every successful numerical,
    /// activation, reward and pending-effect write belongs to one candidate.</summary>
    public ArmyFacilityRepairResult TryAdvanceArmyFacilityRepair(GameState state,
        NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.FacilityRepair ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Facility repair requires reached stage16 entry.");
        if (moment.PreWriterThirdDate.Hour / 2 != 5) return new(state, [], null);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            var order = current.ReadRegistryReferences(RegistryKind.Army) ?? throw RepairMissing("complete_manager60");
            var requests = new List<CampaignEffectRequest>();
            var eligible = new List<int>();
            GameState working = state;
            foreach (var entry in order)
            {
                int physical = entry is { Kind: RegistryKind.Army, PhysicalSlot: int at } ? at : throw RepairMissing("army_entry");
                var army = current.ReadArmyUnitTableReference(physical) ?? throw RepairMissing("army_pointer");
                var queries = new ArmyMapGroupingCurrentQueries(working, current, _scenario,
                    _governorInputs, Seed.Language, SimulationRuleIdentity.Version);
                // The predicate's initial native-validity call has no branch.
                // Do not add demolition's pre-predicate Army validity gate.
                if (RepairNeed(queries.IsAtHome2C96E0(physical), "at_home")) continue;
                if (RepairNeed(queries.IsExcluded233560(physical), "excluded")) continue;
                var image = NotificationArmy(working, physical);
                if (RepairNeed(ArmyConfigurationSourceAdapter.ReadAction(working, image), "action") != 9) continue;
                var runtime = image.NativeTacticRuntime ?? throw RepairMissing("army_runtime");
                if (runtime.Abnormal23 != 0 || runtime.Abnormal25 != 0 || runtime.Abnormal24 != 0 || (runtime.Flags26 & 1) != 0) continue;
                int own = RepairNeed(current.ReadArmyUnitRawHex2A(army), "army_hex");
                var target = current.ReadArmyLifecycleTargetFields(physical) ?? throw RepairMissing("target");
                byte kind = RepairNeed(target.Kind38, "target_kind");
                ushort identity = RepairNeed(target.Identity3A, "target_identity");
                int goal = DemolitionGoal(current, kind, identity);
                bool adjacent = false;
                for (int direction = 1; direction <= 6; direction++)
                {
                    int neighbor = DemolitionNeighbor(own, direction);
                    if ((uint)neighbor >= 48400) continue;
                    var hex = current.ReadHexTableReference(neighbor) ?? throw RepairMissing("neighbor_pointer");
                    if (hex.PhysicalSlot == goal) { adjacent = true; break; }
                }
                if (!adjacent) continue;
                var obj = current.ReadObjectTableReference(identity <= 3000 ? identity : 0) ?? throw RepairMissing("object_pointer");
                if (!RepairNeed(current.ReadObjectNativeValidity(obj), "object_validity")) continue;
                if (kind != 11) continue;
                if (RepairNeed(ArmyMarchSourceAdapter.EncodeDestination(image.March?.Target), "first_waypoint") < 48400) continue;
                var record = current.ReadPhysicalObject(RepairNeed(obj.PhysicalSlot, "object_physical")) ?? throw RepairMissing("object_fields");
                var definition = current.ReadObjectDefinitionReference(record.RawField12 <= 30 ? record.RawField12 : 0)
                    ?? throw RepairMissing("definition_pointer");
                if (!RepairNeed(current.ReadObjectDefinitionValidity(definition), "definition_validity")) continue;
                int before = RepairNeed(current.ReadObjectRawField14(obj), "predicate_durability");
                int capacity = RepairNeed(FacilityRepairCapacity.ReadCapacity(working, current, obj, _baseCapacityInputs), "predicate_capacity");
                if (before >= capacity) continue;
                eligible.Add(physical);
                // Only the actor leaf now tests Army validity.
                if (!RepairNeed(current.ReadArmyUnitNativeValidity(army), "leaf_army_validity") ||
                    !RepairNeed(current.ReadObjectNativeValidity(obj), "leaf_object_validity")) continue;
                ushort commander = RepairNeed(current.ReadArmyUnitCommanderId(army), "leaf_commander");
                var person = (commander <= 6000 ? current.ReadPersonLookupReference(commander) : current.ReadTransferPersonTableReference(0))
                    ?? throw RepairMissing("leaf_person_pointer");
                int personPhysical = RepairNeed(person.PhysicalSlot, "leaf_person_identity");
                if (!RepairNeed(current.ReadPersonNativeValidity(personPhysical), "leaf_person_validity") ||
                    !RepairNeed(current.ReadObjectDefinitionValidity(definition), "leaf_definition_validity")) continue;
                if (RepairNeed(definition.PhysicalSlot, "leaf_definition_identity") != 7)
                    throw RepairMissing("definition_consumer_outside_kind7_slice");
                _baseCapacityInputs.BindCurrent(working, current);
                var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.Version);
                int raw = FacilityRepairRate.Read(working, current, obj, army, abilities, _baseCapacityInputs);
                var numbers = ApplyFacilityRepairNumbers(working, current, obj, definition, raw);
                working = numbers.State;
                working = AppendFacilityRepairEffects(working, current, army, image, numbers, commander, moment, requests);
                working = ApplyFacilityRepairRewards(working, current, army, obj, personPhysical, numbers, raw);
            }
            if (requests.Count != 0)
                working = AppendFacilityRepairBatch(working, moment, requests);
            return new(working, eligible.AsReadOnly(), null);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    private static T RepairNeed<T>(T? value, string field) where T : struct => value ?? throw RepairMissing(field);
    private static InvalidDataException RepairMissing(string field) => FacilityRepairParameters.Missing(field);
}
