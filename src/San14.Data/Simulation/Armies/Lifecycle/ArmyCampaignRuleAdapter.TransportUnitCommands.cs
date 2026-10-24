using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableTransportUnitCommandRules
{
    public CommandResult IssueTransportArmyAction(GameState state, UnitState army,
        DeploymentAction action, IReadOnlyList<int> waypointHexIds)
    {
        if (action is not { ActionKey: "stay" or "move" or "enter", Target: { }, StructureKindId: null } ||
            waypointHexIds is null || waypointHexIds.Count > 5 || waypointHexIds.Any(hex => hex is < 0 or >= 48400))
            return CommandResult.Reject("Transport action or intermediate route is invalid.", "transport.action");
        if (action.ActionKey == "enter" ? action.Target is not { Kind: DeploymentTargetKind.Location, Id: >= 1 and <= 61 }
            : action.Target is not { Kind: DeploymentTargetKind.Hex, Id: >= 0 and < 48400 })
            return CommandResult.Reject("The transport target shape is invalid.", "transport.destination");
        try
        {
            var (physical, receiver, runtime, control) = ReadTransportUnitCommandOwner(state, army);
            byte previous = TransportUnitNeed(ArmyConfigurationSourceAdapter.ReadAction(state, army), "previous_action37");
            var current = CaptureWritableTransportRouteCurrent(state);
            byte nativeAction = previous, kind = 0;
            ushort identity = 0;
            int destination = army.DestinationHexId;
            int? cityId = army.Mission!.DestinationCityId;
            if (control == 0)
            {
                if (action.ActionKey is "move" or "stay")
                {
                    if (action.Target is not { Kind: DeploymentTargetKind.Hex, Id: >= 0 and < 48400 })
                        return CommandResult.Reject("Transport movement must select a map Hex.", "transport.destination");
                    destination = action.Target.Id;
                    if (action.ActionKey == "stay" &&
                        ReadTransportUnitTargetPosition(current, army.OfficerId) != destination)
                        return CommandResult.Reject("Transport holding must select its actual current position.", "transport.destination");
                    //7EB310 item8 first chooses13;7E8D80 tag1 normalizes it
                    //to1/9 just like movement. It never writes ordinary13.
                    nativeAction = 1; kind = 9; identity = (ushort)destination; cityId = null;
                }
                else
                {
                    if (action.Target is not { Kind: DeploymentTargetKind.Location, Id: >= 1 and <= 61 })
                        return CommandResult.Reject("Transport entry must select a friendly Base.", "transport.destination");
                    int location = action.Target.Id;
                    //The menu returns the selected receiver's physical ordinal.
                    //Do not run that already resolved identity through a second lookup.
                    var target = new BaseReference(location <= 51 ? BaseKind.City : BaseKind.Gate,
                        location <= 51 ? location : location - 51);
                    if (!TransportUnitNeed(current.ReadBaseNativeValidity(target), "target_Base_validity"))
                        return CommandResult.Reject("The selected transport Base is no longer valid.", "transport.destination");
                    int rawForce = TransportUnitNeed(current.ReadBaseForceSlot(target), "target_Base_force");
                    var targetForce = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
                        ?? throw TransportCommandMissing("target_Force_pointer");
                    var actorForce = current.ReadArmyUnitForceReference(new(physical))
                        ?? throw TransportCommandMissing("actor_Force_pointer");
                    if (!TransportUnitNeed(current.ReadForceNativeValidity(targetForce), "target_Force_validity") || targetForce != actorForce)
                        return CommandResult.Reject("Transport may enter only a Base owned by its actual force.", "transport.destination");
                    destination = TransportUnitNeed(CurrentGateMapQueries.FindHex(target, current).PhysicalSlot, "target_Base_Hex");
                    int? projected = location <= 51 ? state.Cities.GetValueOrDefault(location)?.HexId
                        : state.Gates.GetValueOrDefault(location)?.HexId;
                    if (destination is < 0 or >= 48400 || projected != destination) throw TransportCommandMissing("target_Base_projection");
                    nativeAction = location <= 51 ? (byte)2 : (byte)3;
                    kind = location <= 51 ? (byte)5 : (byte)6;
                    identity = (ushort)target.PhysicalSlot!.Value;
                    cityId = location <= 51 ? location : null;
                }
            }

            return WriteTransportUnitCommand(state, army, current, physical, receiver, runtime, control,
                nativeAction, kind, identity, destination, cityId, waypointHexIds);
        }
        catch (InvalidDataException error)
        {
            return CommandResult.RuleUnavailable(error.Message, "The current transport action is unresolved.");
        }
    }

    private CommandResult WriteTransportUnitCommand(GameState state, UnitState army,
        SourceWorldContext current, int physical, ArmyReceiverState receiver,
        NativeArmyTacticRuntime runtime, byte control, byte nativeAction, byte kind, ushort identity,
        int destination, int? cityId, IReadOnlyList<int> waypointHexIds)
    {
        byte previous = TransportUnitNeed(ArmyConfigurationSourceAdapter.ReadAction(state, army), "previous_action37");
        var march = army.March ?? new(null, null, null);
        //A locked receiver skips action/target setters, but2EE4B0 clears
        //all five words. The caller still resets48 and appends Changed18.
        ushort[] points = Enumerable.Range(0, 5).Select(index => control == 0 && index < waypointHexIds.Count
            ? (ushort)waypointHexIds[index] : (ushort)48400).ToArray();
        var changed = army with
        {
            DestinationHexId = destination,
            FieldAttackPolicy = control == 0 ? ArmyConfigurationSourceAdapter.DecodePolicy(nativeAction) : army.FieldAttackPolicy,
            Mission = control == 0 ? army.Mission! with { DestinationCityId = cityId } : army.Mission,
            TransportRoute = TransportRouteSourceAdapter.Decode(points),
            March = march with { NextStep = ArmyMarchDestination.None },
            OverrideTarget = runtime.Abnormal25 == 0 ? ArmyOverrideTargetState.Cleared : army.OverrideTarget
        };
        var working = control == 0 ? OwnedArmyLifecycleTargets.Write(state, receiver.ReceiverId, new(kind, identity)) : state;
        var lifecycle = working.ReadArmyLifecycle()!;
        receiver = receiver with { FalseReportDestination = previous == 17 ? ArmyMarchDestination.None : receiver.FalseReportDestination,
            OverrideTarget = changed.OverrideTarget };
        var resource = control == 0 ? ArmyConfigurationSourceAdapter.WriteAction(working.ResourceState, army.Id, nativeAction)
            : working.ResourceState;
        //Changing the explicit points replaces only their compatibility;
        //the independent41 owner is retained, including unknown knowledge.
        var knowledge = CampaignSourceKnowledge.From(resource);
        if (knowledge.FindTransportRoute(army.Id) is { } route)
            resource = knowledge.WithTransportRoute(new(route.Owner, route.Choice41, []));
        working = (working with { Units = new(working.Units) { [army.Id] = changed }, ResourceState = resource })
            .WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.With(receiver) });
        working = ArmyOverrideTargetSourceAdapter.ReconcileOwner(working, changed);
        var appended = ArmyChangedListsSource.Append(working, physical, current);
        return appended.State is { } result ? CommandResult.Accept(result)
            : CommandResult.RuleUnavailable(appended.UnresolvedOwner ?? "current.transport_unit.changed_list",
                "The transport command could not register its current route change.");
    }

    private CommandResult IssueTransportUnitWithdrawal(GameState state, UnitState army, bool finalizeAbnormal = false,
        WithdrawalSavedProjection? projectedGroup = null)
    {
        try
        {
            var (physical, receiver, runtime, control) = ReadTransportUnitCommandOwner(state, army);
            bool abnormal = runtime.Abnormal23 != 0 || runtime.Abnormal24 != 0 || runtime.Abnormal25 != 0;
            if (abnormal && !finalizeAbnormal)
                return CommandResult.Reject("This transport's current abnormal state prevents withdrawal.", "unit.withdraw_abnormal");
            projectedGroup ??= ProjectWithdrawalSavedGroups(state, [army], null)[army.Id];
            var current = CaptureWritableTransportRouteCurrent(state);
            byte action = TransportUnitNeed(ArmyConfigurationSourceAdapter.ReadAction(state, army), "previous_action37");
            byte kind = 0;
            ushort identity = 0;
            int destination = army.DestinationHexId;
            int? cityId = army.Mission!.DestinationCityId;
            if (control == 0 && abnormal)
            {
                //7C47D0 skips the withdrawal packet edit. DA2B0 still
                //projects its saved group, including a reset caused by that
                //group's selected and eligible first row.
                var target = OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId)
                    ?? throw TransportCommandMissing("withdraw_retained_target");
                kind = TransportUnitNeed(target.Kind38, "withdraw_retained_kind");
                identity = TransportUnitNeed(target.Identity3A, "withdraw_retained_identity");
                if (action == 1)
                {
                    int here = projectedGroup.LastPointPhysical
                        ?? throw TransportCommandMissing("withdraw_saved_group_final_pointer");
                    if (here is < 0 or >= 48400) throw TransportCommandMissing("withdraw_saved_group_final_pointer");
                    identity = (ushort)here;
                    destination = here;
                    cityId = null;
                }
            }
            else if (control == 0)
            {
                //22A630 ->22A6C0 follows the actual commander's E2 through
                //the Base tables; the deployment source is not a substitute.
                ushort commander = TransportUnitNeed(current.ReadArmyUnitCommanderId(new(physical)), "withdraw_Commander12");
                var person = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
                    ?? throw TransportCommandMissing("withdraw_Commander_pointer");
                ushort home = person.PhysicalSlot is int slot &&
                    TransportUnitNeed(current.ReadPersonNativeValidity(slot), "withdraw_Commander_validity")
                    ? TransportUnitNeed(current.ReadPersonFirstLocation(slot), "withdraw_Commander_E2") : (ushort)0;
                kind = home is >= 52 and <= 61 ? (byte)6 : (byte)5;
                identity = kind == 6 ? (ushort)(home - 51) : home <= 51 ? home : (ushort)0;
                var target = kind == 5 ? current.ReadCityTableReference(identity) : current.ReadGateTableReference(identity);
                if (target is null) throw TransportCommandMissing("withdraw_Base_pointer");
                ushort link = TransportUnitNeed(current.ReadBaseObjectLink(target), "withdraw_Base4E");
                var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0)
                    ?? throw TransportCommandMissing("withdraw_Base_Object");
                destination = TransportUnitNeed(current.ReadObjectHexIndex(obj), "withdraw_Base_Hex10");
                int location = kind == 6 ? identity + 51 : identity;
                int? projected = kind == 5 ? state.Cities.GetValueOrDefault(location)?.HexId : state.Gates.GetValueOrDefault(location)?.HexId;
                if (identity == 0 || destination is < 0 or >= 48400 || projected != destination)
                    throw TransportCommandMissing("withdraw_exact_Base_projection");
                action = kind == 6 ? (byte)12 : (byte)11;
                cityId = kind == 5 ? location : null;
            }
            //This remains a transport carrying its original stores and options.
            //Only native11/12 identifies withdrawal; Mission.Returning belongs
            //to the legacy coarse delivery model and is left untouched.
            return WriteTransportUnitCommand(state, army, current, physical, receiver, runtime, control,
                action, kind, identity, destination, cityId, projectedGroup.WaypointHexIds);
        }
        catch (InvalidDataException error)
        {
            return CommandResult.RuleUnavailable(error.Message, "The current transport withdrawal is unresolved.");
        }
    }

    public CommandResult IssueTransportArmiesWithdrawal(GameState state, IReadOnlyList<UnitState> armies,
        IReadOnlyList<ArmyWithdrawalSavedGroup>? savedGroups = null)
    {
        if (armies is null || armies.Count is < 1 or > 30 || armies.Select(army => army.Id).Distinct().Count() != armies.Count)
            return CommandResult.Reject("Select distinct deployed transports.", "unit.batch");
        try
        {
            bool eligible = false;
            foreach (var army in armies)
            {
                var (_, _, runtime, _) = ReadTransportUnitCommandOwner(state, army);
                eligible |= runtime.Abnormal23 == 0 && runtime.Abnormal24 == 0 && runtime.Abnormal25 == 0;
            }
            if (!eligible)
                return CommandResult.Reject("No selected transport can withdraw in its current abnormal state.", "unit.withdraw_abnormal");
            var projectedGroups = ProjectWithdrawalSavedGroups(state, armies, savedGroups);
            var candidate = state;
            foreach (var army in armies)
            {
                var result = IssueTransportUnitWithdrawal(candidate, candidate.Units[army.Id], finalizeAbnormal: true, projectedGroup: projectedGroups[army.Id]);
                if (!result.Accepted) return result;
                candidate = result.State!;
            }
            return CommandResult.Accept(candidate);
        }
        catch (InvalidDataException error)
        {
            return CommandResult.RuleUnavailable(error.Message, "The current transport withdrawal selection is unresolved.");
        }
    }

    public CommandResult SetTransportArmyOptions(GameState state, UnitState army,
        int? sourceBehaviorChoice, bool? returnOfficerToSource)
    {
        if (sourceBehaviorChoice is < 0 or > 2 || sourceBehaviorChoice is null && returnOfficerToSource is null)
            return CommandResult.Reject("Select a valid transport option.", "transport.options");
        try
        {
            var (_, _, runtime, control) = ReadTransportUnitCommandOwner(state, army);
            //2EE2E0's known lock branch is a real no-write return. It does not
            //invalidate48, enqueue a route, or consume any resources.
            if (control != 0) return CommandResult.Accept(state);
            var resource = state.ResourceState;
            if (sourceBehaviorChoice is int choice)
            {
                var knowledge = CampaignSourceKnowledge.From(resource);
                var previous = knowledge.FindTransportRoute(army.Id);
                resource = knowledge.WithTransportRoute(new(new(army.Id), (byte)choice,
                    previous is null ? [] : previous.UnmappedWaypoints));
            }
            if (returnOfficerToSource is bool returns)
            {
                byte flags = (byte)(runtime.Flags26 & 0xDF);
                if (returns) flags |= 0x20;
                return CommandResult.Accept(state with { ResourceState = resource,
                    Units = new(state.Units) { [army.Id] = army with { NativeTacticRuntime = runtime with { Flags26 = flags } } } });
            }
            return CommandResult.Accept(state with { ResourceState = resource });
        }
        catch (InvalidDataException error)
        {
            return CommandResult.RuleUnavailable(error.Message, "The current transport option is unresolved.");
        }
    }

    private static (int Physical, ArmyReceiverState Receiver, NativeArmyTacticRuntime Runtime, byte Control)
        ReadTransportUnitCommandOwner(GameState state, UnitState army)
    {
        if (state.Units.GetValueOrDefault(army.Id) != army || army.Mission?.Kind != UnitMissionKind.Transport ||
            army.NativeArmyOrigin?.NativeSlotIndex is not int physical || physical is < 1 or > 500 ||
            army.NativeTacticRuntime is not { Status10: 2 } runtime)
            throw TransportCommandMissing("active_type2_receiver");
        var arena = state.ReadArmyLifecycle() ?? throw TransportCommandMissing("lifecycle");
        var slots = arena.ReceiverSlots();
        if ((uint)physical >= (uint)slots.Length) throw TransportCommandMissing("receiver_index");
        var slot = slots[physical];
        if (slot.Receiver is not { Phase: ArmyReceiverPhase.Active } receiver ||
            slot.ActiveUnitId != army.Id || slot.Commander12 == 0 || slot.Commander12 != army.OfficerId || slot.Status10 != 2)
            throw TransportCommandMissing("receiver_unit_ownership");
        return (physical, receiver, runtime,
            TransportUnitNeed(ArmySavedCommandKnowledge.ReadControl(state, army), "saved_control140"));
    }

    private static T TransportUnitNeed<T>(T? value, string field) where T : struct => value ?? throw TransportCommandMissing(field);
    private static InvalidDataException TransportCommandMissing(string field) => new("current.transport_unit." + field);
}
