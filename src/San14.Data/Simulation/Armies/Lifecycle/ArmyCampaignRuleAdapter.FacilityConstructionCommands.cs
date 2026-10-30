using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed record FacilityConstructionChoice(string DefinitionKey, string Name, byte? Category = null);

public sealed record FacilityConstructionQuote(bool? Allowed, int? GoldCost, int? PayerLocationId,
    int? CurrentGold, string? Reason, string? UnresolvedInput)
{
    public bool Available => UnresolvedInput is null;
}

public sealed partial class ArmyCampaignRuleAdapter : IPlayableArmyConstructionCommandRules, IPlayableArmyContinuousConstructionCommandRules
{
    public IReadOnlyList<FacilityConstructionChoice> QueryFacilityConstructionChoices()
    {
        var definitions = _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath).Catalog.Definitions;
        return Array.AsReadOnly(SupportedKinds.Select(slot => new FacilityConstructionChoice(
            MapObjectSourceDirectory.DefinitionKey(slot), definitions[slot].Name)).ToArray());
    }

    public int? QueryFacilityRepairCapacity(GameState state, int structureId)
    {
        if (state.Structures?.ContainsKey(structureId) != true || state.MapObjects is not { } objects)
            return null;
        var owner = objects.Receivers.FirstOrDefault(row => row.Subject?.StructureId == structureId);
        if (owner is null) return null;
        try
        {
            var current = CaptureFacilityConstructionCurrent(state);
            var directory = current.CurrentMapObjectDirectory ?? throw Missing("capacity_Object_directory");
            var obj = new ObjectReference(directory.PhysicalOf(owner.Id));
            if (current.ReadObjectNativeValidity(obj) != true) return null;
            return FacilityRepairCapacity.ReadCapacity(state, current, obj, _baseCapacityInputs);
        }
        catch (InvalidDataException) { return null; }
    }

    /// <summary>One selected target quote; borrows the committed world and
    /// captures current inputs once. No order, state clone or resource writer.</summary>
    public FacilityConstructionQuote QueryFacilityConstructionQuote(GameState state, int unitId,
        int hexId, string definitionKey)
    {
        if (!state.Units.TryGetValue(unitId, out var army) || !FacilityConstructionIntent.IsDefinitionKey(definitionKey) ||
            hexId is < 0 or >= 48400)
            return new(false, null, null, null, "The current army, definition or site is invalid.", null);
        try
        {
            var current = CaptureFacilityConstructionCurrent(state);
            int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("quote_Army_receiver");
            if (!K(current.ReadArmyUnitNativeValidity(new(physical)), "quote_Army_validity") ||
                army.NativeTacticRuntime is not { Status10: 1 })
                return new(false, null, null, null, "Only an ordinary field army can construct.", null);
            if (K(ArmySavedCommandKnowledge.ReadControl(state, army), "quote_control140") != 0)
                throw Missing("quote_saved_order_replacement");
            var reference = current.ReadObjectDefinitionReference(MapObjectSourceDirectory.DefinitionSlot(definitionKey))
                ?? throw Missing("quote_definition_pointer");
            if (!K(current.ReadObjectDefinitionValidity(reference), "quote_definition_validity"))
                return new(false, null, null, null, "The definition is unavailable.", null);
            if (!IsSupported(reference.PhysicalSlot)) throw Missing("quote_definition_consumer_outside_supported_facilities");
            var site = current.ReadHexTableReference(hexId) ?? throw Missing("quote_Hex_pointer");
            int person = current.ResolveOfficerSource(army.OfficerId) ?? throw Missing("quote_Person_pointer");
            string? rejected = null;
            bool legal = FacilityConstructionLegality.CheckCommander(state, current, person, site, reference, reason => rejected = reason);
            if (!legal) return new(false, null, null, null, rejected ?? "structure.site.unavailable", null);
            var home = current.FindArmyUnitCurrentBase(new(physical));
            int cost = FacilityConstructionCost.Read(state, current, current.ReadArmyUnitForceReference(new(physical)),
                Read(current, reference), _baseCapacityInputs);
            int gold = BaseCurrencyAdditionSource.ReadCurrentGold(home, current);
            int payer = K(BaseCurrencyAdditionSource.CurrencyCity(home, current).PhysicalSlot, "quote_payer_City_identity");
            return new(true, cost, payer, gold, null, null);
        }
        catch (InvalidDataException error) { return new(null, null, null, null, null, error.Message); }
    }

    public CommandResult IssueFacilityConstruction(GameState state, UnitState army, int hexId,
        string definitionKey, IReadOnlyList<int> waypointHexIds)
        => IssueFacilityConstructionCore(state, army, hexId, definitionKey, waypointHexIds, null);

    /// <summary>The two actual placement endpoints of a continuous obstacle.
    /// Arrival pays for and constructs each reached site separately.</summary>
    public CommandResult IssueContinuousFacilityConstruction(GameState state, UnitState army,
        int initialHexId, int finalHexId, string definitionKey, IReadOnlyList<int> waypointHexIds)
        => IssueFacilityConstructionCore(state, army, initialHexId, definitionKey, waypointHexIds, finalHexId);

    private CommandResult IssueFacilityConstructionCore(GameState state, UnitState army, int hexId,
        string definitionKey, IReadOnlyList<int> waypointHexIds, int? finalHexId)
    {
        if (!state.Units.TryGetValue(army.Id, out var actual) || actual != army ||
            !FacilityConstructionIntent.IsDefinitionKey(definitionKey) || hexId is < 0 or >= 48400 ||
            waypointHexIds is null || waypointHexIds.Count > 5 || waypointHexIds.Any(hex => hex is < 0 or >= 48400) ||
            finalHexId is < 0 or >= 48400)
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
            if (!IsSupported(reference.PhysicalSlot)) throw Missing("command_definition_consumer_outside_supported_facilities");
            var definition = Read(current, reference);
            if (finalHexId.HasValue && definition.Category != 3)
                return CommandResult.Reject("Only a continuous obstacle accepts two construction endpoints.", "structure.target");
            var target = current.ReadHexTableReference(hexId) ?? throw Missing("command_site_pointer");
            int person = current.ResolveOfficerSource(army.OfficerId) ?? throw Missing("command_commander_pointer");
            if (!FacilityConstructionLegality.CheckCommander(state, current, person, target, reference))
                return CommandResult.Reject("The selected army cannot construct this facility at this site.", "structure.target");
            if(finalHexId is int end && !ContinuousConstructionAllows(state,current,hexId,end,reference,
                site=>FacilityConstructionLegality.CheckCommander(state,current,person,site,reference)))
                return CommandResult.Reject("The continuous construction route contains an unavailable site.","structure.target");

            return SubmitCurrentConstructionCommand(state, current, army, receiver, 8, hexId,
                waypointHexIds, new(definitionKey, ArmyMarchDestination.AtHex(hexId),
                    definition.Category == 3 ? ArmyMarchDestination.AtHex(finalHexId ?? hexId) : ArmyMarchDestination.None),
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
            var intent = receiver.FacilityConstruction;
            if (intent?.DefinitionKey is null && ArmyConfigurationSourceAdapter.ReadAction(state, army) != 6)
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
        FacilityConstructionIntent? intent, UnitMission mission, DeploymentAction? projection,
        (byte Kind, ushort Identity)? selectedTarget = null, bool borrowCurrentPool = false)
    {
        byte formerAction = K(ArmyConfigurationSourceAdapter.ReadAction(state, army), "command_previous_action");
        var runtime = army.NativeTacticRuntime ?? throw Missing("command_Army_runtime");
        var waypoints = waypointHexIds.Select(ArmyMarchDestination.AtHex).ToArray();
        var march = army.March ?? throw Missing("command_march");
        var changed = army with
        {
            DestinationHexId = hexId, FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(action),
            Mission = mission, MilitaryPlan = projection is null ? null : new(waypointHexIds.ToArray(), 0, projection),
            March = march with { Target = waypoints.FirstOrDefault() ?? ArmyMarchDestination.None,
                FollowingWaypoints = new(waypoints.Skip(1).Cast<ArmyMarchDestination?>().ToArray()), NextStep = ArmyMarchDestination.None },
            OverrideTarget = runtime.Abnormal25 == 0 ? ArmyOverrideTargetState.Cleared : army.OverrideTarget
        };
        var target = selectedTarget ?? (Kind: (byte)9, Identity: (ushort)hexId);
        var working = OwnedArmyLifecycleTargets.Write(state, receiver.ReceiverId, new(target.Kind, target.Identity));
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
        var appended = borrowCurrentPool
            ? ArmyChangedListsSource.AppendCurrent(working, army.NativeArmyOrigin!.NativeSlotIndex!.Value, current)
            : ArmyChangedListsSource.Append(working, army.NativeArmyOrigin!.NativeSlotIndex!.Value, current);
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
        current.ConstructionTutorialQualification = () => ReadConstructionTutorialQualification(state);
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        current.MaterializeFixedScenery(_landmarkDefinitions);
        //44 placement is fixed;47 flags are current saved knowledge. A missing
        //owner or row must not borrow its opening flags from the static source.
        for (int i = 0; i < 101; i++)
            current.SetSceneryFlags47(i, state.LandmarkInvestments?.GetValueOrDefault(i)?.Flags);
        current.BindFixedConstructionSceneryLinks();
        _baseCapacityInputs.BindCurrent(state, current);
    }

    private bool? ReadConstructionTutorialQualification(GameState state) => ReadApplicationTutorialActive() switch
    {
        false => false,
        true => TacticRemakeCurrentBattleControl.Read(ReadTutorialControlState(state), 51),
        _ => null
    };
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
