using San14.Core;

namespace San14.Data;

public sealed record ArmyDemolitionResult(GameState? State, IReadOnlyList<int> EligibleReceiverSlots,
    string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The real10h demolition manager. A reached positive removal
    /// requires its complete map-object lifecycle and report transaction.</summary>
    public ArmyDemolitionResult TryAdvanceArmyDemolition(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Demolition ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Army demolition requires reached stage12.");
        if (moment.PreWriterThirdDate.Hour / 2 != 5) return new(state, [], null);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            var queries = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
                Seed.Language, SimulationRuleIdentity.Version);
            var order = current.ReadRegistryReferences(RegistryKind.Army) ?? throw DemolitionMissing("complete_army_manager");
            var eligible = new List<int>();
            GameState working = state;
            var requests = new List<CampaignEffectRequest>();
            foreach (var entry in order)
            {
                int physical = entry is { Kind: RegistryKind.Army, PhysicalSlot: int at } ? at : throw DemolitionMissing("army_pointer");
                var army = current.ReadArmyUnitTableReference(physical) ?? throw DemolitionMissing("army_table");
                if (!DemolitionKnown(current.ReadArmyUnitNativeValidity(army), "army_validity")) continue;
                if (DemolitionKnown(queries.IsAtHome2C96E0(physical), "at_home")) continue;
                if (DemolitionKnown(queries.IsExcluded233560(physical), "excluded")) continue;
                var image = NotificationArmy(working, physical);
                if (DemolitionKnown(ArmyConfigurationSourceAdapter.ReadAction(state, image), "action") != 10) continue;
                var runtime = image.NativeTacticRuntime ?? throw DemolitionMissing("army_runtime");
                if (runtime.Abnormal23 != 0 || runtime.Abnormal25 != 0 || runtime.Abnormal24 != 0 || (runtime.Flags26 & 1) != 0) continue;
                int ownHex = DemolitionKnown(current.ReadArmyUnitRawHex2A(army), "army_hex");
                var target = current.ReadArmyLifecycleTargetFields(physical) ?? throw DemolitionMissing("target");
                int goal = DemolitionGoal(current, DemolitionKnown(target.Kind38, "target_kind"), DemolitionKnown(target.Identity3A, "target_identity"));
                bool adjacent = false;
                for (int direction = 1; direction <= 6; direction++)
                {
                    int neighbor = DemolitionNeighbor(ownHex, direction);
                    if ((uint)neighbor >= 48400) continue;
                    var hex = current.ReadHexTableReference(neighbor) ?? throw DemolitionMissing("neighbor_pointer");
                    if (hex.PhysicalSlot is not int actual) continue;
                    if (actual == goal) { adjacent = true; break; }
                }
                if (!adjacent) continue;
                ushort first = DemolitionKnown(ArmyMarchSourceAdapter.EncodeDestination(image.March?.Target), "first_waypoint");
                if (first <= 48399) continue;
                eligible.Add(physical);
                // The actor consumer tests type11 only after the full predicate.
                if (target.Kind38 != 11) continue;
                var obj = current.ReadObjectTableReference(target.Identity3A!.Value <= 3000 ? target.Identity3A.Value : 0)
                    ?? throw DemolitionMissing("object_pointer");
                if (!DemolitionKnown(current.ReadObjectNativeValidity(obj), "object_validity")) continue;
                var removal = ApplyDemolitionNumbers(working, current, obj, army);
                if (removal is not null)
                {
                    working = removal.State;
                    working = AppendDemolitionEffects(working, current, army, image, removal, moment, requests);
                }
                // The caller still revalidates and clears a valid Object
                // target when its invalid definition made the remover return.
                if (DemolitionKnown(current.ReadArmyUnitNativeValidity(army), "tail_army_validity") &&
                    DemolitionKnown(current.ReadArmyUnitNativeValidity(army), "tail_army_revalidation"))
                {
                    byte control = ArmySavedCommandKnowledge.ReadControl(working, image) ?? throw DemolitionMissing("tail_control");
                    if (control == 0) current.SetArmyLifecycleTargetFields(physical, new(11, 0));
                }
            }
            working = OwnedArmyLifecycleTargets.PublishCurrent(working, current);
            working = working.WithArmyLifecycle(working.ReadArmyLifecycle()! with
                { Indexes = current.ReadOwnedCurrentListPool()!.ExportOwnedState() });
            if (requests.Count != 0) working = working with
                { CampaignEffectRequests = (working.CampaignEffectRequests ?? throw DemolitionMissing("effect_owner"))
                    .Append(CampaignEffectSource.ArmyDemolition, moment.CalendarCycleOrdinal, requests) };
            return new(working, eligible.AsReadOnly(), null);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    private static int DemolitionGoal(SourceWorldContext current, byte kind, ushort identity)
    {
        if (kind == 9) return identity;
        if (kind == 27)
        {
            var army = current.ReadArmyUnitTableReference(identity <= 500 ? identity : 0) ?? throw DemolitionMissing("goal_army_pointer");
            return DemolitionKnown(current.ReadArmyUnitRawHex2A(army), "goal_army_position");
        }
        if (kind is 5 or 6)
        {
            var location = kind == 5 ? current.ReadCityTableReference(identity <= 51 ? identity : 0)
                : current.ReadGateTableReference(identity <= 10 ? identity : 0);
            identity = DemolitionKnown(current.ReadBaseObjectLink(location ?? throw DemolitionMissing("goal_base_pointer")), "goal_base_object");
        }
        else if (kind != 11) return 48400;
        var obj = current.ReadObjectTableReference(identity <= 3000 ? identity : 0) ?? throw DemolitionMissing("goal_object_pointer");
        // The native goal getter deliberately reads even an invalid object.
        return DemolitionKnown(current.ReadObjectHexIndex(obj), "goal_object_position");
    }

    private static int DemolitionNeighbor(int hex, int direction)
    {
        int x = hex % 220, y = hex / 220;
        bool odd = (x & 1) != 0;
        return direction switch
        {
            1 when x > 0 && (!odd || y > 0) => hex - (odd ? 221 : 1),
            2 when y > 0 => hex - 220,
            3 when x < 219 && (!odd || y > 0) => hex + (odd ? -219 : 1),
            4 when x > 0 => hex + (!odd && y < 219 ? 219 : -1),
            5 when y < 219 => hex + 220,
            6 when x < 219 => hex + (!odd && y < 219 ? 221 : 1),
            _ => 48400
        };
    }
    private static T DemolitionKnown<T>(T? value, string field) where T : struct => value ?? throw DemolitionMissing(field);
    private static InvalidDataException DemolitionMissing(string field) => new("army.progress.demolition." + field);
}
