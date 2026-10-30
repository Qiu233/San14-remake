using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //48D080 reaches2EA480(Army,0,0,1): move to the commander's real home.
    //Its final1 preserves the existing override and selects move2/3 rather
    //than withdrawal11/12. The normal route, hold and no-route report remain.
    private GameState ReturnSeventhTutorialAttackerCurrent(GameState state)
    {
        var operation = CaptureOwnedArmyReturnWorld(state);
        var current = operation.Context;
        int person = SeventhPerson(current, 656);
        var actor = SeventhNeed(current.ReadPersonNativeValidity(person), "return_actor_validity")
            ? PersonArmyLocationSource.ReadCommanderArmy(current, person) ?? throw SeventhMissing("return_actor_Army") : new ArmyUnitReference(0);
        if (!SeventhNeed(current.ReadArmyUnitNativeValidity(actor), "return_actor_Army_validity")) return state;
        int physical = SeventhNeed(actor.PhysicalSlot, "return_actor_physical");
        var army = FArmy(state, physical);
        var runtime = army.NativeTacticRuntime ?? throw SeventhMissing("return_actor_runtime");
        byte control = SeventhNeed(ArmySavedCommandKnowledge.ReadControl(state, army), "return_control140");
        var march = army.March ?? throw SeventhMissing("return_March");
        var changed = army with
        {
            MilitaryPlan = army.MilitaryPlan is { } prior ? prior with
                { WaypointHexIds = [], NextWaypointIndex = 0, IncidentalEnemyId = null } : null,
            March = march with { Target = ArmyMarchDestination.None, FollowingWaypoints = new([]), NextStep = ArmyMarchDestination.None },
        };
        if (control == 0)
        {
            ushort home = SeventhNeed(current.ReadPersonFirstLocation(person), "return_home");
            byte kind = home is >= 52 and <= 61 ? (byte)6 : (byte)5;
            ushort identity = kind == 6 ? (ushort)(home - 51) : home <= 51 ? home : (ushort)0;
            byte action = kind == 6 ? (byte)3 : (byte)2;
            var receiver = state.ReadArmyLifecycle()?.Receivers.FindUnit(army.Id) ?? throw SeventhMissing("return_receiver");
            state = OwnedArmyLifecycleTargets.Write(state, receiver.ReceiverId, new(kind, identity));
            current.SetArmyLifecycleTargetFields(physical, new(kind, identity));
            changed = changed with { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(action), MilitaryPlan = null,
                Mission = new(UnitMissionKind.Combat) };
            state = state with { ResourceState = ArmyConfigurationSourceAdapter.WriteAction(state.ResourceState, army.Id, action) };
        }
        if (runtime.Status10 == 2)
        {
            byte choice = SeventhNeed(TransportRouteSourceAdapter.ReadChoice(state, army), "return_transport_choice");
            state = state with { ResourceState = TransportRouteSourceAdapter.Write(state.ResourceState, army.Id,
                Enumerable.Repeat((ushort)48400, 5).ToArray(), choice) };
            changed = changed with { TransportRoute = TransportRouteSourceAdapter.Decode(Enumerable.Repeat((ushort)48400, 5).ToArray()) };
        }
        state = state with { Units = new(state.Units) { [army.Id] = changed } };
        state = FRoute(state, army.Id, true, current, false);
        changed = state.Units[army.Id];
        var effects = FacilityOperation.Playable(state);
        if (changed.March?.NextStep == ArmyMarchDestination.None)
        {
            state = HoldArmyPositionAfterCommand(state, current, changed, effects);
            changed = state.Units[army.Id];
            ushort at = SeventhNeed(ArmyMarchSourceAdapter.ReadCurrentHex(state, changed), "return_hold_Hex");
            state = state with { Units = new(state.Units) { [army.Id] = changed with { DestinationHexId = at,
                Mission = new(UnitMissionKind.Combat), MilitaryPlan = new([], 0,
                    new("stay", new(DeploymentTargetKind.Hex, at)), Completed: true, RestingHexId: at) } } };
            if (IsPlayerRulerLedDistrict(state, current, changed))
                state = AppendAutomaticWithdrawalReport(state, current, changed, effects, 0x3231, null);
        }
        else
        {
            ushort goal = SeventhNeed(OrdinaryArmyRouteSource.ReadCurrentTarget(state, changed, current), "return_goal");
            state = state with { Units = new(state.Units) { [army.Id] = changed with { DestinationHexId = goal } } };
        }
        return state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = operation.Pool.ExportOwnedState() });
    }
}
