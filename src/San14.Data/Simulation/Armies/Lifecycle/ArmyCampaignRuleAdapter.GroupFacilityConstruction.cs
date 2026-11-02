using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableArmyGroupConstructionCommandRules
{
    /// <summary>7CC4C0/7DA2B0/7F6870 mode3: qualify the whole selected
    /// People list, then replace real Army rows in retained packet order.
    /// Core has checked each row's ordinary player/AI command authority.
    /// There is no command-book or gold debit at this retarget writer.</summary>
    public CommandResult IssueArmyGroupFacilityConstruction(GameState state, ForceState force,
        IssueArmiesFacilityConstructionOrder order, CommandContext context)
    {
        if (order.ForceId != force.Id || order.Armies is null ||
            order.Armies.Count < 1 ||
            !FacilityConstructionIntent.IsDefinitionKey(order.DefinitionKey) ||
            order.InitialHexId is < 0 or >= 48400 || order.FinalHexId is < 0 or >= 48400)
            return CommandResult.Reject("Select ordinary armies, a facility and its actual construction sites.", "structure.selection");
        // Copy only this selected command input. Each row owns its route.
        var rows = new List<ArmyFacilityConstructionSelection>(order.Armies.Count);
        var ids = new HashSet<int>();
        foreach (var row in order.Armies)
        {
            if (row is null || !ids.Add(row.UnitId) || row.WaypointHexIds is null ||
                row.WaypointHexIds.Count > 5 || row.WaypointHexIds.Any(hex => hex is < 0 or >= 48400))
                return CommandResult.Reject("Select distinct armies with up to five intermediate destinations each.", "structure.selection");
            rows.Add(new(row.UnitId, row.WaypointHexIds.ToArray()));
        }
        try
        {
            var current = CaptureFacilityConstructionCurrent(state);
            var lifecycle = state.ReadArmyLifecycle() ?? throw Missing("group_command_lifecycle");
            var people = new List<int>(rows.Count);
            foreach (var row in rows)
            {
                if (!state.Units.TryGetValue(row.UnitId, out var army) || army.ForceId != force.Id ||
                    army.NativeTacticRuntime is { Status10: not 1 } || army.Mission?.Kind == UnitMissionKind.Transport)
                    return CommandResult.Reject("Only current ordinary armies of this force may construct.", "structure.army");
                if (army.NativeTacticRuntime is null) throw Missing("group_command_Army_runtime");
                int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("group_command_Army_receiver");
                var reference = new ArmyUnitReference(physical);
                if (!K(current.ReadArmyUnitNativeValidity(reference), "group_command_Army_validity"))
                    return CommandResult.Reject("A selected army is no longer active.", "structure.army");
                if (K(current.ReadArmyUnitCommanderId(reference), "group_command_Army_commander") != army.OfficerId)
                    throw Missing("group_command_Army_commander_identity");
                if (K(ArmySavedCommandKnowledge.ReadControl(state, army), "group_command_control140") != 0)
                    throw Missing("group_command_saved_order_replacement");
                var receiver = lifecycle.Receivers.FindUnit(army.Id) ?? throw Missing("group_command_Army_owner");
                if (receiver.Phase != ArmyReceiverPhase.Active || receiver.ActiveUnitId != army.Id)
                    throw Missing("group_command_Army_owner_identity");
                _ = army.March ?? throw Missing("group_command_march");
                _ = K(ArmyConfigurationSourceAdapter.ReadAction(state, army), "group_command_previous_action");
                int person = current.ResolveOfficerSource(army.OfficerId) ?? throw Missing("group_command_Person_pointer");
                if (!K(current.ReadPersonNativeValidity(person), "group_command_Person_validity"))
                    return CommandResult.Reject("A selected commander is no longer valid.", "structure.army");
                people.Add(person);
            }
            var definitionReference = current.ReadObjectDefinitionReference(MapObjectSourceDirectory.DefinitionSlot(order.DefinitionKey))
                ?? throw Missing("group_command_definition_pointer");
            if (!K(current.ReadObjectDefinitionValidity(definitionReference), "group_command_definition_validity"))
                return CommandResult.Reject("The selected facility definition is unavailable.", "structure.definition");
            if (!IsSupported(definitionReference.PhysicalSlot)) throw Missing("group_command_definition_consumer");
            var definition = Read(current, definitionReference);
            if (order.FinalHexId.HasValue && definition.Category != 3)
                return CommandResult.Reject("Only continuous obstacles accept two endpoints.", "structure.target");
            var target = current.ReadHexTableReference(order.InitialHexId) ?? throw Missing("group_command_site_pointer");
            var selectedPeople = current.CreateNearbyTemporary();
            try
            {
                foreach (int person in people) current.AppendConstructionPersonTemporary(selectedPeople, person);
                string? reason = null;
                bool Allowed(HexReference hex) => FacilityConstructionLegality.Check(state, current,
                    selectedPeople, hex, definitionReference, rejected => reason = rejected);
                if (!Allowed(target))
                    return CommandResult.Reject("The selected group cannot construct at this site.", reason ?? "structure.target");
                if (order.FinalHexId is int last && !ContinuousConstructionAllows(state, current,
                    order.InitialHexId, last, definitionReference, Allowed))
                    return CommandResult.Reject("The continuous construction route contains an unavailable site.", "structure.continuation");
            }
            finally { current.ClearNearbyTemporary(selectedPeople, true); }

            var working = state;
            foreach (var row in rows)
            {
                var army = working.Units[row.UnitId];
                var receiver = working.ReadArmyLifecycle()!.Receivers.FindUnit(army.Id)
                    ?? throw Missing("group_command_current_Army_owner");
                // No singleton CheckCommander here:7E17B0/2793F0 have
                // already admitted the actual whole People list. Arrival-time
                // maintenance still rechecks each real arriving commander.
                var result = SubmitCurrentConstructionCommand(working, current, army, receiver, 8,
                    order.InitialHexId, row.WaypointHexIds,
                    new(order.DefinitionKey, ArmyMarchDestination.AtHex(order.InitialHexId),
                        definition.Category == 3 ? ArmyMarchDestination.AtHex(order.FinalHexId ?? order.InitialHexId)
                            : ArmyMarchDestination.None),
                    new(UnitMissionKind.BuildStructure, StructureKindId: order.DefinitionKey),
                    new("build", new(DeploymentTargetKind.Hex, order.InitialHexId), order.DefinitionKey),
                    borrowCurrentPool: true);
                if (result.State is not { } candidate) return result;
                working = candidate;
            }
            return CommandResult.Accept(working);
        }
        catch (InvalidDataException error)
        { return CommandResult.RuleUnavailable("rules.army_group_construction", error.Message); }
    }
}
