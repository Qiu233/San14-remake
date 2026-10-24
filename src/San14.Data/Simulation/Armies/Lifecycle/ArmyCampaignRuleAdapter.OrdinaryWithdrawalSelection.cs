using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public CommandResult IssueOrdinaryArmiesWithdrawal(GameState state, IReadOnlyList<UnitState> armies,
        IReadOnlyList<ArmyWithdrawalSavedGroup>? savedGroups = null)
    {
        if (armies is null || armies.Count is < 1 or > 30 || armies.Select(army => army.Id).Distinct().Count() != armies.Count)
            return CommandResult.Reject("Select distinct deployed ordinary armies.", "unit.batch");
        try
        {
            bool eligible = false;
            foreach (var army in armies)
            {
                var (_, runtime) = ReadOrdinaryWithdrawalOwner(state, army);
                eligible |= CanWriteOrdinaryWithdrawal(runtime);
            }
            if (!eligible)
                return CommandResult.Reject("No selected army can withdraw in its current abnormal state.", "unit.withdraw_abnormal");
            var projectedGroups = ProjectWithdrawalSavedGroups(state, armies, savedGroups);
            var candidate = state;
            foreach (var army in armies)
            {
                var result = FinalizeOrdinaryWithdrawalRow(candidate, candidate.Units[army.Id], projectedGroups[army.Id]);
                if (!result.Accepted) return result;
                candidate = result.State!;
            }
            return CommandResult.Accept(candidate);
        }
        catch (InvalidDataException error)
        {
            return CommandResult.RuleUnavailable("rules.army_withdrawal", error.Message);
        }
    }

    private CommandResult FinalizeOrdinaryWithdrawalRow(GameState state, UnitState army, WithdrawalSavedProjection projectedGroup)
    {
        var (receiver, runtime) = ReadOrdinaryWithdrawalOwner(state, army);
        var current = CaptureWritableTransportRouteCurrent(state);
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        byte control = K(ArmySavedCommandKnowledge.ReadControl(state, army), "withdraw_saved_control");
        byte action = K(ArmyConfigurationSourceAdapter.ReadAction(state, army), "withdraw_current_action");
        var priorTarget = OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId)
            ?? throw Missing("withdraw_current_target");
        byte kind = K(priorTarget.Kind38, "withdraw_current_target_kind");
        ushort identity = K(priorTarget.Identity3A, "withdraw_current_target_identity");
        int destination = army.DestinationHexId;
        var mission = army.Mission!;
        var projection = army.MilitaryPlan?.Action;
        var intent = receiver.FacilityConstruction;
        bool eligible = CanWriteOrdinaryWithdrawal(runtime);
        if (eligible)
        {
            //Tag0 row24 and row22 become no definition/no initial site.
            //The continuation site and all four independent policies remain.
            intent = (intent ?? new(null, null, null)) with
            { DefinitionKey = null, InitialSite = ArmyMarchDestination.None };
            if (control == 0)
            {
                ushort commander = K(current.ReadArmyUnitCommanderId(new(army.NativeArmyOrigin!.NativeSlotIndex)), "withdraw_Commander12");
                var person = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
                    ?? throw Missing("withdraw_Commander_pointer");
                ushort home = person.PhysicalSlot is int slot && K(current.ReadPersonNativeValidity(slot), "withdraw_Commander_validity")
                    ? K(current.ReadPersonFirstLocation(slot), "withdraw_Commander_E2") : (ushort)0;
                kind = home is >= 52 and <= 61 ? (byte)6 : (byte)5;
                identity = kind == 6 ? (ushort)(home - 51) : home <= 51 ? home : (ushort)0;
                var target = kind == 5 ? current.ReadCityTableReference(identity) : current.ReadGateTableReference(identity);
                if (target is null) throw Missing("withdraw_Base_pointer");
                ushort link = K(current.ReadBaseObjectLink(target), "withdraw_Base4E");
                var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw Missing("withdraw_Base_Object");
                destination = K(current.ReadObjectHexIndex(obj), "withdraw_Base_Hex10");
                int location = kind == 6 ? identity + 51 : identity;
                int? projected = kind == 5 ? state.Cities.GetValueOrDefault(location)?.HexId : state.Gates.GetValueOrDefault(location)?.HexId;
                if (identity == 0 || destination is < 0 or >= 48400 || projected != destination)
                    throw Missing("withdraw_exact_home_projection");
                action = kind == 6 ? (byte)12 : (byte)11;
                mission = new(UnitMissionKind.Retreat);
                projection = null;
            }
        }
        else if (control == 0 && action == 1)
        {
            //DA2B0 reads the retained saved group, whose first row alone
            //decides whether the whole group was reset to its Home Hex.
            destination = projectedGroup.LastPointPhysical
                ?? throw Missing("withdraw_saved_group_final_pointer");
            if (destination is < 0 or >= 48400) throw Missing("withdraw_saved_group_final_pointer");
            identity = (ushort)destination;
            projection = new("move", new(DeploymentTargetKind.Hex, destination));
        }

        //The shared ordinary setter preserves +25-gated override, projects the
        //saved group into all five slots, clears Next and enqueues every
        //selected receiver.
        //A locked receiver keeps its action/target while the other native tail
        //writes, including construction selection, still take place.
        var result = SubmitCurrentConstructionCommand(state, current, army, receiver, action,
            destination, control == 0 ? projectedGroup.WaypointHexIds : [], intent, mission, projection, (kind, identity));
        if (!result.Accepted || eligible || action != 17) return result;

        //Tag0 keeps the row32 Hex pointer captured from the old13E. F6870
        //clears the old action17 word first, then restores that valid pointer's
        //physical identity when the submitted packet still has action17.
        ushort raw = ArmyMarchSourceAdapter.EncodeDestination(receiver.FalseReportDestination)
            ?? throw Missing("withdraw_false_report13E");
        var savedHex = current.ReadHexTableReference(raw < 48400 ? raw : 0)
            ?? throw Missing("withdraw_false_report_Hex_pointer");
        var falseReport = savedHex.PhysicalSlot is int physical
            ? ArmyMarchDestination.AtHex(physical) : ArmyMarchDestination.None;
        var changed = result.State!;
        var lifecycle = changed.ReadArmyLifecycle()!;
        var committed = lifecycle.Receivers.Find(receiver.ReceiverId)!;
        return CommandResult.Accept(changed.WithArmyLifecycle(lifecycle with
        { Receivers = lifecycle.Receivers.With(committed with { FalseReportDestination = falseReport }) }));
    }

    private static bool CanWriteOrdinaryWithdrawal(NativeArmyTacticRuntime runtime) =>
        runtime.Abnormal23 == 0 && runtime.Abnormal24 == 0 && runtime.Abnormal25 == 0;

    private static (ArmyReceiverState Receiver, NativeArmyTacticRuntime Runtime)
        ReadOrdinaryWithdrawalOwner(GameState state, UnitState army)
    {
        if (state.Units.GetValueOrDefault(army.Id) != army || army.Mission is null or { Kind: UnitMissionKind.Transport } ||
            army.NativeArmyOrigin?.NativeSlotIndex is not int physical || physical is < 1 or > 500 ||
            army.NativeTacticRuntime is not { Status10: 1 } runtime)
            throw Missing("withdraw_active_ordinary_receiver");
        var slots = (state.ReadArmyLifecycle() ?? throw Missing("withdraw_lifecycle")).ReceiverSlots();
        if ((uint)physical >= (uint)slots.Length || slots[physical] is not
            { Receiver: { Phase: ArmyReceiverPhase.Active } receiver, Status10: 1 } ||
            slots[physical].ActiveUnitId != army.Id || slots[physical].Commander12 == 0 || slots[physical].Commander12 != army.OfficerId)
            throw Missing("withdraw_receiver_ownership");
        return (receiver, runtime);
    }
}
