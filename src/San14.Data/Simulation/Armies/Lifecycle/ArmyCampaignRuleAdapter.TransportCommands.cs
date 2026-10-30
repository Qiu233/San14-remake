using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableTransportArmyCommandRules
{
    /// <summary>7FA520 writes only the two selected World preferences. A
    /// partial old bank remains partial; this never runs the fresh installer.</summary>
    public CommandResult SaveTransportDefaults(GameState state, SaveTransportDefaultsOrder order)
    {
        if (order.SourceBehaviorChoice is < 0 or > 2)
            return CommandResult.Reject("Transport behavior default is invalid.", "transport.defaults_behavior");
        try
        {
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            if (player?.PhysicalSlot is not int playerPhysical)
                return CommandResult.RuleUnavailable("current.transport_defaults.player_Force3A", "Current player identity is unresolved.");
            if (playerPhysical != order.ForceId)
                return CommandResult.Reject("Transport defaults belong to the current player.", "transport.defaults_scope");
        }
        catch (InvalidDataException error)
        {
            return CommandResult.RuleUnavailable(error.Message, "Current player permission is unresolved.");
        }
        if (state.NativeArmySimulation is not { } native)
            return CommandResult.RuleUnavailable("current.transport_defaults.parameters", "Transport preferences have no saved owner.");
        var values = native.Parameters.Int32Values;
        if (values.TryGetValue(TransportSetupCurrentInputs.VersionKey, out int version) && version != 1)
            return CommandResult.RuleUnavailable("transport.setup.version", "Transport preference version is unsupported.");
        var changed = new Dictionary<string, int>(values)
        {
            [TransportSetupCurrentInputs.VersionKey] = 1,
            [TransportSetupCurrentInputs.ReturnOfficerPreferenceKey] = order.ReturnOfficerToSource ? 1 : 0,
            [TransportSetupCurrentInputs.BehaviorPreferenceKey] = order.SourceBehaviorChoice
        };
        return CommandResult.Accept(state with { NativeArmySimulation = native with
            { Parameters = native.Parameters with { Int32Values = changed } } });
    }

    /// <summary>The real creator consumes all explicit Make/Target values.
    /// Command books and stock publication stay in the shared source writer.</summary>
    public CommandResult IssueTransportArmy(GameState state, DispatchTransportArmyOrder order)
    {
        if (order.GroupPurpose is { } purpose && (order.DistrictId is null ||
            purpose.Kind != ArmyGroupPurposeKind.TransportToFoothold ||
            purpose.Target.Kind != ArmyGroupPurposeTargetKind.Foothold ||
            order.Action is not { ActionKey: "enter", Target.Kind: DeploymentTargetKind.Location } ||
            purpose.Target.Id != order.Action.Target.Id))
            return CommandResult.Reject("Transport group purpose must name its selected entry destination.", "transport.group_purpose");
        if (order.DistrictId is int district)
        {
            try
            {
                var current = CaptureDeploymentPersonSelectionCurrent(state);
                if (!ValidateDistrictTransportPacketCurrent(state, current, order, district))
                    return CommandResult.Reject("Transport packet no longer satisfies its current source capability.", "transport.district_packet");
            }
            catch (InvalidDataException error)
            { return CommandResult.RuleUnavailable(error.Message, "Current district transport inputs are unresolved."); }
        }
        else
        {
        //The same source quantity capability used by Make is authoritative at
        //submission too. Its UI min operation must never alter this command.
        var quantities = QueryTransportQuantities(state, order.SourceCityId, order.OfficerId,
            order.Troops, order.Gold, order.Supplies);
        if (quantities.CanDecide is null || quantities.Troops is null ||
            quantities.Gold is null || quantities.Supplies is null)
            return CommandResult.RuleUnavailable(quantities.UnresolvedInputs.FirstOrDefault() ??
                "transport.setup.quantity-capacity", "Current transport quantity limits are unresolved.");
        if (quantities.CanDecide != true || quantities.Troops != order.Troops ||
            quantities.Gold != order.Gold || quantities.Supplies != order.Supplies)
            return CommandResult.Reject("Transport quantities exceed their current capacity or are empty.",
                "transport.quantity_capacity");
        }
        state = ArmyIdentityAllocation.Initialize(state);
        if (!state.Cities.TryGetValue(order.SourceCityId, out var source) || source.HexId is not int sourceHex ||
            source.Morale is not int morale)
            return CommandResult.RuleUnavailable("current.transport_source_City", "Current transport source fields are unresolved.");
        int destinationHex;
        int? destinationCity = null;
        if (order.Action is { ActionKey: "move", Target.Kind: DeploymentTargetKind.Hex })
            destinationHex = order.Action.Target.Id;
        else if (order.Action is { ActionKey: "enter", Target.Kind: DeploymentTargetKind.Location } &&
            state.Cities.TryGetValue(order.Action.Target.Id, out var city) && city.HexId is int cityHex)
        { destinationHex = cityHex; destinationCity = city.Id; }
        else if (order.Action is { ActionKey: "enter", Target.Kind: DeploymentTargetKind.Location } &&
            state.Gates.TryGetValue(order.Action.Target.Id, out var gate) && gate.HexId is int gateHex)
            destinationHex = gateHex;
        else return CommandResult.Reject("Transport target is invalid.", "transport.destination");
        int id = state.ArmyIdentities!.Candidate;
        var projection = new UnitState(id, order.ForceId, order.OfficerId, sourceHex, destinationHex,
            order.Troops, order.Supplies, new(morale, null))
        {
            SourceLocationId = source.Id,
            //The legacy Arrival flag is not an interpretation of option41 or
            //the officer-return bit. Those two selections use their own owners.
            Mission = new(UnitMissionKind.Transport, destinationCity, order.Gold)
        };
        string? unresolved = null;
        var result = CreateTransportCore(state, order.OfficerId, order.SourceCityId, projection,
            current => TransportArmyPacketSource.TryComposeCommand(state, current, order),
            reason => unresolved = reason);
        if (result is null && unresolved == "transport.insufficient_orders")
            return CommandResult.Reject("District lacks the required uncommitted command books.", unresolved);
        return result is null
            ? CommandResult.RuleUnavailable(unresolved ?? "transport.explicit_source_creation", "Current transport creation or its required source tail is unresolved.")
            : CommandResult.Accept(result.State);
    }
}
