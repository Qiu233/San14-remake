using San14.Core;

namespace San14.Data;

internal enum StoryForceRelationOutcome { AllianceCreated, AllianceExtended, AllianceDestroyed }

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary122..126 in the single real Story invocation. Manager
    /// facts are emitted only at the reached source notification; the caller
    /// owns their saved classifications and eventual result-page consumer.</summary>
    internal (GameState State, int NativeReturn) ApplyStoryForceRelationsCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments, ForceReference first,
        ForceReference? second, Action<ForceReference, ForceReference, StoryForceRelationOutcome> recordOutcome)
    {
        if (arguments.Count != 8) throw StoryForceMissing("arguments");
        ArgumentNullException.ThrowIfNull(recordOutcome);
        _baseCapacityInputs.BindCurrent(state, current);
        var operation = new StoryForceRelationsCurrent(this, state, current, recordOutcome);
        operation.Execute(code, arguments, first, second);
        // All five original outer handlers return1, even when their actual
        // validity-gated mutation was skipped. No presentation ACK is implied.
        return (operation.State, 1);
    }

    private sealed class StoryForceRelationsCurrent(ArmyCampaignRuleAdapter rules, GameState state,
        SourceWorldContext current, Action<ForceReference, ForceReference, StoryForceRelationOutcome> recordOutcome)
    {
        internal GameState State { get; private set; } = state;
        private WorldIndexContext Pool => current.ReadOwnedCurrentListPool() ?? throw StoryForceMissing("current_pool");
        private bool Valid(ForceReference force) => Need(current.ReadForceNativeValidity(force), "Force_validity");
        private int Physical(ForceReference force) => Need(current.ReadForcePhysicalSlot(force), "Force_identity");

        internal void Execute(int code, IReadOnlyList<int> a, ForceReference first, ForceReference? second)
        {
            if (code == 122) { if (Valid(first)) AddMorale(first, a[1]); return; }
            if (code is not (123 or 124 or 125 or 126)) throw StoryForceMissing("opcode");
            var other = second ?? throw StoryForceMissing("second_Force");
            if (Valid(first) && Valid(other))
            {
                switch (code)
                {
                    case 123:
                    case 124:
                        int adjustment = a[2];
                        if (code == 123)
                        {
                            int opposite = Physical(other);
                            int before = opposite < 52 ? rules.ReadEditedEventFriendship(State, current, first, opposite) : 0;
                            adjustment = unchecked(adjustment - before);
                        }
                        // This is2704F0 notify0 including its ordinary special
                        // person tail, not the editor's self/player admission.
                        State = rules.ApplyNormalTurnForceRelationCurrent(State, current, Pool, first, other, adjustment);
                        State = rules.SynchronizeDiplomacyProjection(State);
                        break;
                    case 125:
                        int old = ReadReportForceRelationByte(State, first, Physical(other), StoryForceMissing) & 127;
                        int requested = a[2];
                        // a3==0 writes even for shortening/zero. Notification
                        // uses the requested signed amount, not clamped after.
                        if (a[3] == 0 || requested > old)
                            State = WriteEditedEventAlliance(State, current, Physical(first), Physical(other), requested);
                        if (requested > old)
                            RecordOutcome(first, other, old == 0 ? StoryForceRelationOutcome.AllianceCreated
                                : StoryForceRelationOutcome.AllianceExtended);
                        break;
                    case 126:
                        if ((ReadReportForceRelationByte(State, first, Physical(other), StoryForceMissing) & 127) != 0)
                        {
                            State = new MonthlyCampaignMaintenanceSource(rules, State, current, Pool)
                                .ApplyStoryAllianceEndCurrent(first, other);
                            RecordOutcome(first, other, StoryForceRelationOutcome.AllianceDestroyed);
                        }
                        break;
                }
            }
            //125 always calls2758F0;126 always calls33E2E0 then2758F0,
            //including the outer invalid/no-existing-alliance branches.
            if (code == 126) State = RefreshEditedEventRegionStatistics(State, current);
            if (code is 125 or 126)
            {
                State = rules.MaintainEditedEventAppointments(State, current);
                State = rules.SynchronizeDiplomacyProjection(State);
                if (code == 126) State = PublishNormalTurnCurrent(State, current, Pool);
            }
        }

        private void RecordOutcome(ForceReference first, ForceReference second, StoryForceRelationOutcome outcome)
        {
            //3D4990/4A70/4B50 recheck their actual pair after the world writer.
            if (Need(current.ReadEventManagerPresent(), "event_manager_presence") && Valid(first) && Valid(second))
                recordOutcome(first, second, outcome);
        }

        private void AddMorale(ForceReference selected, int amount)
        {
            int key = Physical(selected);
            foreach (var entry in current.ReadRegisteredBases() ?? throw StoryForceMissing("Base_registry"))
            {
                var receiver = entry ?? throw StoryForceMissing("Base_payload");
                if (key is < 1 or > 51 || Need(current.ReadBaseForceSlot(receiver), "Base_Force") != key) continue;
                var force = BaseForce(receiver);
                int requested = Bound(unchecked(Need(current.ReadBaseMorale44(receiver), "Base_morale") + amount),
                    BasePostLoad.ReadMoraleUpper(force, current));
                if (!BasePostLoad.WriteMoraleCurrent(receiver, requested, current)) continue;
                byte after = Need(current.ReadBaseMorale44(receiver), "Base_morale_after");
                PublishBaseMorale(receiver, after);
            }
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw StoryForceMissing("Army_registry"))
            {
                if (entry is null || entry.Kind != RegistryKind.Army) throw StoryForceMissing("Army_payload");
                int physical = Need(entry.PhysicalSlot, "Army_pointer");
                var receiver = new ArmyUnitReference(physical);
                if (key is < 1 or > 51 || current.ReadArmyUnitRawForceSlot(receiver) != key) continue;
                if (Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(State, current, physical), "Army233560")) continue;
                var force = current.ReadArmyUnitForceReference(receiver);
                int clamp = BasePostLoad.ReadMoraleUpper(force, current);
                int getter = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(receiver), current);
                byte raw = Need(current.ReadArmyReturnResourceFields(receiver)?.Morale1A, "Army_morale");
                int requested = Bound(unchecked(Math.Min((int)raw, getter) + amount), clamp);
                byte after = ArmyMoraleSource.SetByte2419A0(requested,
                    BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(receiver), current));
                current.WriteArmyReturnMorale(physical, after);
                State = rules.WriteEditedEventArmyMorale(State, physical, after);
            }
        }

        private ForceReference BaseForce(BaseReference receiver)
        {
            int slot = Need(current.ReadBaseForceSlot(receiver), "Base_morale_Force");
            if (slot < 0) throw StoryForceMissing("negative_Base_Force");
            return current.ReadForceTableReference(slot < 52 ? slot : 0) ?? throw StoryForceMissing("Base_Force_pointer");
        }
        private void PublishBaseMorale(BaseReference receiver, byte morale)
        {
            int physical = Need(receiver.PhysicalSlot, "morale_Base_pointer");
            if (receiver.Kind == BaseKind.City)
            {
                var city = State.Cities.GetValueOrDefault(physical) ?? throw StoryForceMissing("morale_City_owner");
                var runtime = city.NativeReturnRuntime ?? throw StoryForceMissing("morale_City_runtime");
                State = State with { Cities = new(State.Cities) { [physical] = city with { Morale = morale,
                    NativeReturnRuntime = runtime with { Morale44 = morale } } } };
            }
            else
            {
                int location = checked(physical + 51);
                var gate = State.Gates.GetValueOrDefault(location) ?? throw StoryForceMissing("morale_Gate_owner");
                var runtime = gate.NativeCombatRuntime ?? throw StoryForceMissing("morale_Gate_runtime");
                State = State with { Gates = new(State.Gates) { [location] = gate with
                    { NativeCombatRuntime = runtime with { Morale44 = morale } } } };
                var native = State.NativeArmySimulation ?? throw StoryForceMissing("morale_parameters");
                State = State with { NativeArmySimulation = native with { Parameters = native.Parameters with
                    { Int32Values = new(native.Parameters.Int32Values) { [ArmyMoraleSource.BaseMorale44Key(receiver)] = morale } } } };
            }
        }
        private static int Bound(int value, int cap) => value < 0 ? 0 : Math.Min(value, cap);
        private static T Need<T>(T? value, string name) where T : struct => value ?? throw StoryForceMissing(name);
    }

    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        /// <summary>The existing292BA0 world mutation and its param3=1 tail,
        /// without the edited-entry gates or the monthly-expiry narration.</summary>
        internal GameState ApplyStoryAllianceEndCurrent(ForceReference first, ForceReference second)
        {
            bool mayClear = (ForceFlags(first) & 2) == 0 || (ForceFlags(second) & 2) == 0;
            bool crossed = mayClear &&
                (ForceArmies(first).Any(army => Need(current.ReadHexForceSlot(ArmyHex(army)), "story_crossed_Hex") == Physical(second)) ||
                 ForceArmies(second).Any(army => Need(current.ReadHexForceSlot(ArmyHex(army)), "story_crossed_Hex") == Physical(first)));
            EndAlliance(first, second);
            if (crossed)
                state = new NormalTurnTerritoryMaintenanceSource(package, state, current, pool)
                    .ApplyEditedEventLogistics(ForceArmies(first).Concat(ForceArmies(second)).ToArray());
            return state;
        }
    }
    private static InvalidDataException StoryForceMissing(string field) => new("story.force_relations." + field);
}
