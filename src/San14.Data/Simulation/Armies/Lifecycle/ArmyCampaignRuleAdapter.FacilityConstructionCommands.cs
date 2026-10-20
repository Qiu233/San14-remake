using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableArmyConstructionCommandRules
{
    public CommandResult IssueFacilityConstruction(GameState state, UnitState army, int hexId,
        string definitionKey, IReadOnlyList<int> waypointHexIds)
    {
        if (!state.Units.TryGetValue(army.Id, out var actual) || actual != army ||
            !FacilityConstructionIntent.IsDefinitionKey(definitionKey) || hexId is < 0 or >= 48400 ||
            waypointHexIds is null || waypointHexIds.Count > 5 || waypointHexIds.Any(hex => hex is < 0 or >= 48400))
            return CommandResult.Reject("The current army, facility definition or site is invalid.", "structure.target");
        try
        {
            var current = CaptureFacilityConstructionCurrent(state);
            int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("command_Army_receiver");
            if (!K(current.ReadArmyUnitNativeValidity(new(physical)), "command_Army_validity") ||
                army.NativeTacticRuntime is not { Status10: 1 } runtime)
                return CommandResult.Reject("Only a valid ordinary field army may construct this facility.", "structure.army");
            if (K(ArmySavedCommandKnowledge.ReadControl(state, army), "command_control140") != 0)
                throw Missing("command_saved_order_replacement");
            var lifecycle = state.ReadArmyLifecycle() ?? throw Missing("command_lifecycle");
            var receiver = lifecycle.Receivers.FindUnit(army.Id) ?? throw Missing("command_Army_owner");
            var reference = current.ReadObjectDefinitionReference(MapObjectSourceDirectory.DefinitionSlot(definitionKey))
                ?? throw Missing("command_definition_pointer");
            if (!K(current.ReadObjectDefinitionValidity(reference), "command_definition_validity"))
                return CommandResult.Reject("The selected facility definition is unavailable.", "structure.definition");
            if (reference.PhysicalSlot != 7) throw Missing("command_definition_consumer_outside_kind7");
            var target = current.ReadHexTableReference(hexId) ?? throw Missing("command_site_pointer");
            int person = current.ResolveOfficerSource(army.OfficerId) ?? throw Missing("command_commander_pointer");
            if (!FacilityConstructionLegality.CheckCommander(state, current, person, target, reference))
                return CommandResult.Reject("The selected army cannot construct this facility at this site.", "structure.target");

            return SubmitCurrentConstructionCommand(state, current, army, receiver, 8, hexId,
                waypointHexIds, new(definitionKey, ArmyMarchDestination.AtHex(hexId), ArmyMarchDestination.None),
                new(UnitMissionKind.BuildStructure, StructureKindId: definitionKey),
                new("build", new(DeploymentTargetKind.Hex, hexId), definitionKey));
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable("rules.army_construction", error.Message); }
    }

    public CommandResult IssueMovementAfterConstruction(GameState state, UnitState army,
        DeploymentAction action, IReadOnlyList<int> waypointHexIds)
    {
        if (!state.Units.TryGetValue(army.Id, out var actual) || actual != army ||
            action is not { ActionKey: "move" or "stay", Target.Kind: DeploymentTargetKind.Hex } ||
            action.Target.Id is < 0 or >= 48400 || waypointHexIds is null || waypointHexIds.Count > 5 ||
            waypointHexIds.Any(hex => hex is < 0 or >= 48400))
            return CommandResult.Reject("The current army or movement target is invalid.", "unit.action");
        try
        {
            var current = CaptureFacilityConstructionCurrent(state);
            int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("command_Army_receiver");
            if (!K(current.ReadArmyUnitNativeValidity(new(physical)), "command_Army_validity") ||
                army.NativeTacticRuntime is not { Status10: 1 })
                return CommandResult.Reject("Only a valid ordinary field army may replace this order.", "structure.army");
            if (K(ArmySavedCommandKnowledge.ReadControl(state, army), "command_control140") != 0)
                throw Missing("command_saved_order_replacement");
            var receiver = state.ReadArmyLifecycle()?.Receivers.FindUnit(army.Id) ?? throw Missing("command_Army_owner");
            if (receiver.FacilityConstruction is not { DefinitionKey: not null } intent)
                throw Missing("command_construction_selection");
            _ = current.ReadHexTableReference(action.Target.Id) ?? throw Missing("command_site_pointer");
            return SubmitCurrentConstructionCommand(state, current, army, receiver,
                action.ActionKey == "move" ? (byte)1 : (byte)13, action.Target.Id, waypointHexIds,
                intent, new(UnitMissionKind.Combat), action);
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable("rules.army_construction", error.Message); }
    }

    private static CommandResult SubmitCurrentConstructionCommand(GameState state, SourceWorldContext current,
        UnitState army, ArmyReceiverState receiver, byte action, int hexId, IReadOnlyList<int> waypointHexIds,
        FacilityConstructionIntent intent, UnitMission mission, DeploymentAction projection)
    {
        byte formerAction = K(ArmyConfigurationSourceAdapter.ReadAction(state, army), "command_previous_action");
        var runtime = army.NativeTacticRuntime ?? throw Missing("command_Army_runtime");
        var waypoints = waypointHexIds.Select(ArmyMarchDestination.AtHex).ToArray();
        var march = army.March ?? throw Missing("command_march");
        var changed = army with
        {
            DestinationHexId = hexId, FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(action),
            Mission = mission, MilitaryPlan = new(waypointHexIds.ToArray(), 0, projection),
            March = march with { Target = waypoints.FirstOrDefault() ?? ArmyMarchDestination.None,
                FollowingWaypoints = new(waypoints.Skip(1).Cast<ArmyMarchDestination?>().ToArray()), NextStep = ArmyMarchDestination.None },
            OverrideTarget = runtime.Abnormal25 == 0 ? ArmyOverrideTargetState.Cleared : army.OverrideTarget
        };
        var working = OwnedArmyLifecycleTargets.Write(state, receiver.ReceiverId, new(9, (ushort)hexId));
        var lifecycle = working.ReadArmyLifecycle()!;
        receiver = receiver with { FacilityConstruction = intent,
            FalseReportDestination = formerAction == 17 ? ArmyMarchDestination.None : receiver.FalseReportDestination,
            OverrideTarget = changed.OverrideTarget };
        //7F6870 submits current action/target/waypoints together.7E8D80 preserves
        //the selected definition and initial/continuation sites for move/stay.
        working = (working with { Units = new(working.Units) { [army.Id] = changed },
            ResourceState = ArmyConfigurationSourceAdapter.WriteAction(working.ResourceState, army.Id, action) })
            .WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.With(receiver),
                Indexes = current.ReadOwnedCurrentListPool()!.ExportOwnedState() });
        working = ArmyOverrideTargetSourceAdapter.ReconcileOwner(working, changed);
        var appended = ArmyChangedListsSource.Append(working, army.NativeArmyOrigin!.NativeSlotIndex!.Value, current);
        if (appended.State is not { } result) throw Missing(appended.UnresolvedOwner ?? "command_changed_list");
        return CommandResult.Accept(result);
    }

    private SourceWorldContext CaptureFacilityConstructionCurrent(GameState state)
    {
        var current = CaptureWritableTransportRouteCurrent(state);
        BindFacilityConstructionInputs(state, current);
        return current;
    }

    private void BindFacilityConstructionInputs(GameState state, SourceWorldContext current)
    {
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        current.MaterializeFixedScenery(_landmarkDefinitions);
        //44 placement is fixed;47 flags are current saved knowledge. A missing
        //owner or row must not borrow its opening flags from the static source.
        for (int i = 0; i < 101; i++)
            current.SetSceneryFlags47(i, state.LandmarkInvestments?.GetValueOrDefault(i)?.Flags);
        current.BindFixedConstructionSceneryLinks();
        _baseCapacityInputs.BindCurrent(state, current);
    }
}

public sealed partial class SourceWorldContext
{
    private bool _constructionSceneryLinksKnown;
    internal void BindFixedConstructionSceneryLinks()
    {
        //44 placement is an immutable map resource.47 flags are overlaid from
        //the saved current landmark owner independently; this creates no registry.
        if (SceneryDefinitions is not { } definitions) throw FacilityConstructionDefinitions.Missing("fixed_scenery_definitions");
        _hexScenerySlots.Clear();
        _constructionSceneryLinksKnown = true;
        foreach (var row in definitions.Sceneries)
            if (ReadSceneryNativeValidity(new(row.Slot)) is true)
            {
                var hex = ReadHexTableReference(row.RawHex44 < 48400 ? row.RawHex44 : 0)
                    ?? throw FacilityConstructionDefinitions.Missing("fixed_scenery_Hex_pointer");
                _ = AssignHexScenery(hex, row.Slot, 1);
            }
    }
}
