namespace San14.Core;

/// <summary>Provisional deployed-unit destination change; authority uses GameEngine.Apply.</summary>
public sealed record RetargetUnitOrder(int ForceId, int UnitId, int DestinationHexId) : GameOrder(ForceId);
/// <summary>Retarget cargo or an already-empty returning convoy to an owned
/// city. Arrival disposition and the original food payer remain unchanged.</summary>
public sealed record RetargetTransportOrder(int ForceId, int UnitId, int DestinationCityId) : GameOrder(ForceId);
public sealed record WithdrawUnitOrder(int ForceId, int UnitId) : GameOrder(ForceId);
public sealed record OccupyAreaOrder(int ForceId, int UnitId, int AreaId) : GameOrder(ForceId);
public sealed record DisbandUnitOrder(int ForceId, int UnitId, int CityId) : GameOrder(ForceId);
public sealed record BuildStructureOrder(int ForceId, int UnitId, int HexId,
    string KindId) : GameOrder(ForceId);
public sealed record RepairStructureOrder(int ForceId, int UnitId,
    int StructureId) : GameOrder(ForceId);
public sealed record DemolishStructureOrder(int ForceId, int UnitId,
    int StructureId) : GameOrder(ForceId);
public sealed record DispatchTransportOrder(int ForceId, int SourceCityId, int DestinationCityId,
    int OfficerId, int Troops, int Supplies, int Gold,
    TransportArrivalDisposition Arrival = TransportArrivalDisposition.EnterCity) : GameOrder(ForceId);

public sealed class PlayableUnitOrders(IOrderRules originalCommands, PlayableLandRoutes routes,
    IPlayableCityEntryResourceRules? cityEntryResources = null,
    IPlayableCityEntryStateRules? cityEntryState = null,
    IPlayableTransportCreationRules? transportCreation = null,
    IPlayableArmyConstructionCommandRules? construction = null,
    IPlayableArmyWithdrawalCommandRules? withdrawalRules = null) : IOrderRules
{
    public const string ImmediateBaseEntryReportRuleId = "san14:playable-immediate-base-entry-report-v1";
    public const string TransportCityRetargetRuleId = "san14:provisional-transport-city-retarget-v1";

    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        //Legacy unit commands bypass the waypoint adapter's typed entry points.
        //Check the same current district before any retirement, task or cargo
        //change. Transport dispatch has its own source-district authorization.
        int? controlledUnitId = order switch
        {
            RetargetUnitOrder scopedRedirect => scopedRedirect.UnitId,
            RetargetTransportOrder scopedConvoy => scopedConvoy.UnitId,
            WithdrawUnitOrder scopedWithdrawal => scopedWithdrawal.UnitId,
            OccupyAreaOrder scopedOccupation => scopedOccupation.UnitId,
            DisbandUnitOrder scopedDisband => scopedDisband.UnitId,
            BuildStructureOrder scopedBuild => scopedBuild.UnitId,
            RepairStructureOrder scopedRepair => scopedRepair.UnitId,
            DemolishStructureOrder scopedDemolish => scopedDemolish.UnitId,
            _ => (int?)null
        };
        if (context.DelegatedDistrictId is not null && controlledUnitId is int unitId &&
            !PlayableUnitControl.TryResolve(state, force, unitId, context, out _, out CommandResult? denied))
            return denied!;
        if (order is DispatchTransportOrder transport)
            return DispatchTransport(state, force, transport, context);
        if (order is DispatchTransportArmyOrder transportArmy)
            return DispatchTransportArmy(state, force, transportArmy, context);
        if (order is PrepareTransportFoodWarningOrder foodWarning)
        {
            if (!state.Cities.TryGetValue(foodWarning.SourceCityId, out var warningCity) ||
                warningCity.ForceId != force.Id || warningCity.DistrictId is not int warningDistrictId ||
                !state.Districts.TryGetValue(warningDistrictId, out var warningDistrict) || warningDistrict.ForceId != force.Id)
                return CommandResult.Reject("Transport source city is unavailable.", "transport.source");
            if (context.DelegatedDistrictId is int warningDelegated ? warningDelegated != warningDistrict.Id : warningDistrict.Number != 1)
                return CommandResult.Reject("Transport source is outside the authorized district.", "transport.district_scope");
            if (foodWarning.CargoSupplies is < 0 or > 5000000)
                return CommandResult.Reject("Transport cargo supplies are invalid.", "transport.selection");
            return transportCreation is IPlayableTransportFoodWarningRules warningRules
                ? warningRules.PrepareTransportFoodWarning(state, foodWarning)
                : CommandResult.RuleUnavailable("rules.transport_food_warning", "Current transport food warning rules are unavailable.");
        }
        if (order is SaveTransportDefaultsOrder transportDefaults)
        {
            if (context.DelegatedDistrictId is not null)
                return CommandResult.Reject("Transport defaults require direct player control.", "transport.defaults_scope");
            if (transportDefaults.SourceBehaviorChoice is < 0 or > 2)
                return CommandResult.Reject("Transport behavior default is invalid.", "transport.defaults_behavior");
            return transportCreation is IPlayableTransportArmyCommandRules defaults
                ? defaults.SaveTransportDefaults(state, transportDefaults)
                : CommandResult.RuleUnavailable("rules.transport_defaults", "Current transport preference rules are unavailable.");
        }
        if (order is RetargetTransportOrder convoy)
        {
            if (!PlayableUnitControl.TryResolve(state, force, convoy.UnitId, context,
                    out UnitState? active, out CommandResult? transportDenied)) return transportDenied!;
            if (active!.Mission?.Kind != UnitMissionKind.Transport)
                return CommandResult.Reject("Only a transport unit may receive a city transport objective.", "transport.unit_mission");
            if (!state.Cities.TryGetValue(convoy.DestinationCityId, out CityState? destination) || destination.ForceId != force.Id)
                return CommandResult.Reject("Transport must target a friendly city.", "transport.destination");
            return RetargetTransport(state, active, destination);
        }
        if (order is BuildStructureOrder build)
        {
            if (!PlayableUnitControl.TryResolve(state, force, build.UnitId, context,
                out var builder, out var buildDenied)) return buildDenied!;
            return construction?.IssueFacilityConstruction(state, builder!, build.HexId, build.KindId, []) ??
                CommandResult.RuleUnavailable("rules.army_construction", "Current facility construction command rules are unavailable.");
        }
        if ((order is RepairStructureOrder or DemolishStructureOrder) && construction is IPlayableArmyFacilityWorkCommandRules facilityWork)
        {
            int selectedUnit = order is RepairStructureOrder repairOrder ? repairOrder.UnitId : ((DemolishStructureOrder)order).UnitId;
            int selectedStructure = order is RepairStructureOrder repairTarget ? repairTarget.StructureId : ((DemolishStructureOrder)order).StructureId;
            if (!PlayableUnitControl.TryResolve(state, force, selectedUnit, context,
                out var demolisher, out var demolitionDenied)) return demolitionDenied!;
            if (state.Structures is not { } targetStructures ||
                !targetStructures.TryGetValue(selectedStructure, out var target) ||
                order is DemolishStructureOrder && target.ForceId != force.Id)
                return CommandResult.Reject("The selected facility is unavailable.", "structure.target");
            int? approach = PlayableWaypointMarch.FacilityApproach(routes, demolisher!.HexId, target.HexId);
            return approach is int at ? order is RepairStructureOrder
                ? facilityWork.IssueFacilityRepair(state, demolisher, target.Id, at, [])
                : facilityWork.IssueFacilityDemolition(state, demolisher, target.Id, at, []) :
                CommandResult.Reject("The facility is unreachable.", "structure.destination");
        }
        if (order is WithdrawUnitOrder nativeWithdrawal)
            return Withdraw(state, force, nativeWithdrawal);
        if (controlledUnitId is int constructionArmy && order is not RetargetUnitOrder &&
            state.ArmyLifecycle?.Receivers.FindUnit(constructionArmy)?.FacilityConstruction?.DefinitionKey is not null)
            return CommandResult.RuleUnavailable("rules.army_construction_replacement",
                "Replacement of this current construction command is unresolved.");
        if (order is RepairStructureOrder repair)
            return TargetExistingStructure(state, force, repair.UnitId,
                repair.StructureId, UnitMissionKind.RepairStructure);
        if (order is DemolishStructureOrder demolish)
            return TargetExistingStructure(state, force, demolish.UnitId,
                demolish.StructureId, UnitMissionKind.DemolishStructure);
        if (order is OccupyAreaOrder occupation)
            return OccupyArea(state, force, occupation);
        if (order is DisbandUnitOrder disband)
        {
            if (!state.Units.TryGetValue(disband.UnitId, out UnitState? active) || active.ForceId != force.Id)
                return CommandResult.Reject("Unit does not belong to this force.", "unit.control");
            if (!state.Cities.TryGetValue(disband.CityId, out CityState? city) ||
                city.ForceId != force.Id || city.HexId != active.HexId)
                return CommandResult.Reject("Unit must be inside a friendly city to disband.", "unit.disband_location");
            if (PlayableCityEntryStates.Return(state, active, city, cityEntryState) is { } returned)
            {
                if (returned.State.ArmyReturnCaptiveContinuation is { } captive)
                    return CommandResult.Accept(returned.State with { ArmyReturnCaptiveContinuation = captive with
                        { ImmediateEntryReportRuleId = returned.RuleId } });
                var settled = PlayableOfficerArrivalAftermath.Settle(returned.State, [active.OfficerId]);
                var completedReports = settled.Reports.Append(new PeriodReportEntry("unit_entered", active.ForceId,
                    city.Id, active.OfficerId, active.Troops, active.HexId, active.HexId)
                { LocationId = city.Id, RuleId = returned.RuleId }).ToArray();
                return CommandResult.Accept(settled.State with {
                    PeriodReports = settled.State.PeriodReports.Concat(completedReports).ToArray(),
                    PendingCommandReports = settled.State.PendingCommandReports.Concat(completedReports).ToArray() });
            }
            if (PlayableCityEntryStates.RequiresCurrentState(state, active, city, cityEntryState))
                return CommandResult.RuleUnavailable(PlayableCityEntryStates.UnresolvedRuleId,
                    "The current Army could not complete its city return.");
            PlayableCityEntryResourceQuote? sourceResources =
                PlayableCityEntryResources.Quote(state, active, city, cityEntryResources);
            if (active.WoundedTroops is > 0 && sourceResources is null)
                return CommandResult.RuleUnavailable("unit.wounded_base_transfer",
                    "Transfer of known wounded troops into a base is unresolved.");
            var unitsAfter = new Dictionary<int, UnitState>(state.Units);
            unitsAfter.Remove(active.Id);
            var citiesAfter = new Dictionary<int, CityState>(state.Cities)
            {
                [city.Id] = sourceResources?.City ?? city with
                {
                    Troops = checked(city.Troops + active.Troops),
                    Supplies = active.Mission?.Kind == UnitMissionKind.Transport
                        ? checked(city.Supplies + active.Supplies) : city.Supplies,
                    Gold = checked(city.Gold + (active.Mission?.CargoGold ?? 0))
                }
            };
            OfficerState officer = state.Officers[active.OfficerId];
            var officersAfter = new Dictionary<int, OfficerState>(state.Officers)
            {
                [officer.Id] = officer with { Deployed = false, LocationId = city.Id, DistrictId = city.DistrictId,
                    LocationState = officer.LocationState is { } location
                        ? location with { Position = null } : null }
            };
            GameState retired = ArmyRetirement.PreservePendingImage(state with
            {
                Cities = citiesAfter, Officers = officersAfter, Units = unitsAfter
            }, active);
            PlayableOfficerArrivalAftermathResult arrival = PlayableOfficerArrivalAftermath.Settle(retired, [officer.Id]);
            //The actual ready/cargo transfer is reportable even when no old
            //appointment was cleared. Preserve it independently of the next
            //deployment or report save, using the existing period event kinds.
            var entryReports = new List<PeriodReportEntry>(arrival.Reports);
            if (active.Mission is { Kind: UnitMissionKind.Transport } transportMission)
            {
                AddEntry("transport_troops", active.Troops);
                if (transportMission.Returning) AddEntry("transport_returned", 0);
                else
                {
                    AddEntry("transport_supplies", active.Supplies);
                    AddEntry("transport_gold", transportMission.CargoGold);
                }
            }
            else AddEntry("unit_entered", active.Troops);
            void AddEntry(string kind, int amount) => entryReports.Add(new(kind, active.ForceId,
                city.Id, active.OfficerId, amount, active.HexId, active.HexId)
            { LocationId = city.Id, RuleId = sourceResources?.RuleId ?? ImmediateBaseEntryReportRuleId });
            return CommandResult.Accept(arrival.State with
            {
                //Keep immediate command feedback compatible with the existing
                //view, and separately queue the newly created events. Period
                //advancement must never infer unread events from old reports.
                PeriodReports = arrival.State.PeriodReports.Concat(entryReports).ToArray(),
                PendingCommandReports = arrival.State.PendingCommandReports.Concat(entryReports).ToArray()
            });
        }
        if (order is not RetargetUnitOrder redirect)
            return originalCommands.Apply(state, force, order, context);
        routes.ValidateWorld(state);
        if (!state.Units.TryGetValue(redirect.UnitId, out UnitState? unit) || unit.ForceId != force.Id)
            return CommandResult.Reject("Unit does not belong to this force.", "unit.control");
        UnitMission? mission = unit.Mission;
        if (mission?.Kind == UnitMissionKind.Transport)
        {
            CityState? destination = state.Cities.Values.FirstOrDefault(city =>
                city.ForceId == force.Id && city.HexId == redirect.DestinationHexId);
            if (destination is null)
                return CommandResult.Reject("Transport must target a friendly city.",
                    "transport.destination");
            return RetargetTransport(state, unit, destination);
        }
        if (unit.HexId == redirect.DestinationHexId ||
            routes.Route(unit.HexId, redirect.DestinationHexId).Length == 0)
            return CommandResult.Reject("Unit destination is unreachable under the provisional route rules.",
                "unit.destination");
        if (mission?.Kind is UnitMissionKind.OccupyArea or UnitMissionKind.Retreat or
                 UnitMissionKind.BuildStructure or UnitMissionKind.RepairStructure or
                 UnitMissionKind.DemolishStructure)
            mission = new UnitMission(UnitMissionKind.Combat);
        var units = new Dictionary<int, UnitState>(state.Units)
        {
            [unit.Id] = unit with { DestinationHexId = redirect.DestinationHexId, Mission = mission }
        };
        return CommandResult.Accept(state with { Units = units });
    }

    private CommandResult RetargetTransport(GameState state, UnitState unit, CityState destination)
    {
        routes.ValidateWorld(state);
        if (destination.HexId is not int hex || !routes.CanEnter(hex) ||
            unit.HexId != hex && routes.Route(unit.HexId, hex).Length == 0)
            return CommandResult.Reject("Transport city is unreachable under the provisional route rules.", "unit.destination");
        if (unit.NativeArmyOrigin is not null)
        {
            if (transportCreation is not IPlayableTransportRetargetRules nativeRetarget ||
                nativeRetarget.RetargetTransport(state, unit, destination) is not { } written)
                return CommandResult.RuleUnavailable("rules.transport_native_retarget",
                    "Current source transport target or changed-list owner is unavailable.");
            return CommandResult.Accept(written.State);
        }
        //A returning convoy already has zero cargo. Its flag is an accounting
        //fact, not a request to deliver its old load at the new city again.
        return CommandResult.Accept(state with
        {
            Units = new(state.Units) { [unit.Id] = unit with
                { DestinationHexId = hex, Mission = unit.Mission! with { DestinationCityId = destination.Id } } }
        });
    }

    private CommandResult TargetExistingStructure(GameState state, ForceState force,
        int unitId, int structureId, UnitMissionKind kind)
    {
        routes.ValidateWorld(state);
        if (!state.Units.TryGetValue(unitId, out UnitState? unit) || unit.ForceId != force.Id)
            return CommandResult.Reject("Unit does not belong to this force.", "unit.control");
        if (unit.Mission is null or { Kind: UnitMissionKind.Transport or UnitMissionKind.Retreat })
            return CommandResult.Reject("Unit cannot target a facility in its current mission.",
                "structure.unit_mission");
        if (state.Structures is null ||
            !state.Structures.TryGetValue(structureId, out StructureState? structure) ||
            structure.ForceId != force.Id ||
            !PlayableConstructionRules.IsSupportedKind(structure.KindId))
            return CommandResult.Reject("The selected facility is unavailable.",
                "structure.target");
        if (kind == UnitMissionKind.RepairStructure && structure.MaxDurability is null)
            return CommandResult.Reject("The facility's current durability capacity is unknown.", "structure.capacity_unknown");
        if (kind == UnitMissionKind.RepairStructure &&
            (!structure.Completed || structure.MaxDurability == 0 ||
             structure.Durability >= structure.MaxDurability))
            return CommandResult.Reject("Only a completed, damaged facility can be repaired.",
                "structure.full_durability");
        if (unit.HexId != structure.HexId &&
            routes.Route(unit.HexId, structure.HexId).Length == 0)
            return CommandResult.Reject("The facility is unreachable.",
                "structure.destination");
        return CommandResult.Accept(state with
        {
            Units = new Dictionary<int, UnitState>(state.Units)
            {
                [unit.Id] = unit with
                {
                    DestinationHexId = structure.HexId,
                    Mission = new UnitMission(kind, TargetStructureId: structure.Id)
                }
            }
        });
    }

    private CommandResult OccupyArea(GameState state, ForceState force, OccupyAreaOrder order)
    {
        routes.ValidateWorld(state);
        if (!state.Units.TryGetValue(order.UnitId, out UnitState? unit) || unit.ForceId != force.Id)
            return CommandResult.Reject("Unit does not belong to this force.", "unit.control");
        if (unit.Mission is { Kind: UnitMissionKind.Transport or UnitMissionKind.Retreat })
            return CommandResult.Reject("This unit cannot receive an occupation objective now.",
                "unit.occupation_mission");
        if (!state.Areas.ContainsKey(order.AreaId))
            return CommandResult.Reject("Occupation area does not exist.", "unit.occupation_area");
        int[] route = routes.RouteToNearest(unit.HexId, hex =>
            state.Territory[hex].AreaId == order.AreaId &&
            state.Territory[hex].ForceId != force.Id);
        bool currentTarget = state.Territory[unit.HexId].AreaId == order.AreaId &&
                             state.Territory[unit.HexId].ForceId != force.Id;
        if (!currentTarget && route.Length == 0)
            return CommandResult.Reject("Occupation area has no reachable unowned land.",
                "unit.occupation_complete");
        int destination = currentTarget ? unit.HexId : route[^1];
        var units = new Dictionary<int, UnitState>(state.Units)
        {
            [unit.Id] = unit with
            {
                DestinationHexId = destination,
                Mission = new UnitMission(UnitMissionKind.OccupyArea, TargetAreaId: order.AreaId)
            }
        };
        return CommandResult.Accept(state with { Units = units });
    }

    private CommandResult Withdraw(GameState state, ForceState force, WithdrawUnitOrder order)
    {
        routes.ValidateWorld(state);
        if (!state.Units.TryGetValue(order.UnitId, out UnitState? unit) || unit.ForceId != force.Id)
            return CommandResult.Reject("Unit does not belong to this force.", "unit.control");
        if (unit.Mission is null or { Kind: UnitMissionKind.Transport })
            return CommandResult.Reject("Only a known combat mission can withdraw.",
                "unit.withdraw_mission");
        if (unit.NativeArmyOrigin is not null)
            return withdrawalRules?.IssueWithdrawal(state, unit) ?? CommandResult.RuleUnavailable(
                "rules.army_withdrawal", "Current commander-home withdrawal rules are unavailable.");
        if (PlayableUnitRetreat.Destination(unit, state.Cities, state.Gates,
                state.Officers, routes, state.MapWidth) is not { } destination)
            return CommandResult.Reject("No reachable friendly base is available for retreat.",
                "unit.withdraw_destination");
        var units = new Dictionary<int, UnitState>(state.Units)
        {
            [unit.Id] = unit with
            {
                DestinationHexId = destination.HexId,
                Mission = new UnitMission(UnitMissionKind.Retreat)
            }
        };
        return CommandResult.Accept(state with { Units = units });
    }

    private CommandResult DispatchTransportArmy(GameState state, ForceState force,
        DispatchTransportArmyOrder order, CommandContext context)
    {
        if (order.GroupPurpose is { } groupPurpose && (order.DistrictId is null ||
            groupPurpose.Kind != ArmyGroupPurposeKind.TransportToFoothold ||
            groupPurpose.Target is not { Kind: ArmyGroupPurposeTargetKind.Foothold, Id: >= 0 and <= 61 } destination ||
            order.Action is not { ActionKey: "enter", Target.Kind: DeploymentTargetKind.Location } ||
            destination.Id != order.Action.Target.Id))
            return CommandResult.Reject("Transport group purpose must match its actual base destination.", "transport.group_purpose");
        if (order.DistrictId is int explicitDistrictId)
            return DispatchTransportArmyInDistrict(state, force, order, explicitDistrictId, context);
        if (!state.Cities.TryGetValue(order.SourceCityId, out var source) || source.ForceId != force.Id ||
            source.HexId is not int sourceHex || source.DistrictId is not int districtId ||
            !state.Districts.TryGetValue(districtId, out var district))
            return CommandResult.Reject("Transport source city is unavailable.", "transport.source");
        if (context.DelegatedDistrictId is int delegated ? delegated != district.Id : district.Number != 1)
            return CommandResult.Reject("Transport source is outside the authorized district.", "transport.district_scope");
        if (!state.Officers.TryGetValue(order.OfficerId, out var officer) || officer.ForceId != force.Id ||
            officer.LocationId != source.Id || officer.Presence != OfficerPresence.Present || officer.Deployed ||
            PlayableOfficerAvailability.IsSourceBusy(state, officer) ||
            state.PreparedOfficerMoves?.ContainsKey(officer.Id) == true ||
            state.PreparedTalentOrders?.ContainsKey(officer.Id) == true ||
            state.PreparedDeployments.Values.Any(plan => plan.OfficerId == officer.Id))
            return CommandResult.Reject("Transport officer is unavailable.", "transport.officer");
        if (order.Troops is < 0 or > ushort.MaxValue || order.Supplies is < 0 or > 5000000 ||
            order.Gold is < 0 or > 100000 ||
            order.Troops == 0 && order.Supplies == 0 && order.Gold == 0 ||
            order.Troops > source.Troops - DeploymentAvailability.ReservedTroops(state, source.Id) ||
            order.Supplies > source.Supplies || order.Gold > source.Gold)
            return CommandResult.Reject("Transport cargo exceeds available city resources.", "transport.resources");
        if (order.NavalFormationId is < 0 or > 20 || order.SourceBehaviorChoice is < 0 or > 2 ||
            order.Action is not { Target: { } target, StructureKindId: null } || order.WaypointHexIds is null ||
            order.WaypointHexIds.Count > PlayableWaypointMarch.MaximumWaypoints)
            return CommandResult.Reject("Transport selections are invalid.", "transport.selection");
        int targetHex;
        if (order.Action is { ActionKey: "move", Target.Kind: DeploymentTargetKind.Hex })
            targetHex = target.Id;
        else if (order.Action is { ActionKey: "enter", Target.Kind: DeploymentTargetKind.Location })
        {
            if (state.Cities.TryGetValue(target.Id, out var city) && city.ForceId == force.Id && city.HexId is int cityHex)
                targetHex = cityHex;
            else if (state.Gates.TryGetValue(target.Id, out var gate) && gate.ForceId == force.Id && gate.HexId is int gateHex)
                targetHex = gateHex;
            else return CommandResult.Reject("Transport must enter a friendly base.", "transport.destination");
            if (target.Id == source.Id)
                return CommandResult.Reject("Transport must select a different destination.", "transport.destination");
        }
        else return CommandResult.Reject("Transport supports movement or entry into a friendly base.", "transport.action");
        int cursor = sourceHex;
        foreach (int hex in order.WaypointHexIds.Append(targetHex))
        {
            if (!routes.CanEnter(hex) || cursor != hex && routes.Route(cursor, hex).Length == 0)
                return CommandResult.Reject("The transport route is unreachable.", "transport.destination");
            cursor = hex;
        }
        return transportCreation is IPlayableTransportArmyCommandRules commands
            ? commands.IssueTransportArmy(state, order)
            : CommandResult.RuleUnavailable("rules.transport_army_command", "Current transport command rules are unavailable.");
    }

    private CommandResult DispatchTransportArmyInDistrict(GameState state, ForceState force,
        DispatchTransportArmyOrder order, int districtId, CommandContext context)
    {
        if (!state.Districts.TryGetValue(districtId, out var district) || district.ForceId != force.Id ||
            context.DelegatedDistrictId is int delegated && delegated != districtId)
            return CommandResult.Reject("Transport execution is outside the authorized district.", "transport.district_scope");
        if (!state.Cities.TryGetValue(order.SourceCityId, out var source) || source.ForceId != force.Id ||
            !state.Officers.TryGetValue(order.OfficerId, out var officer) || officer.ForceId != force.Id ||
            officer.LocationId != source.Id)
            return CommandResult.Reject("Transport source references do not belong to this force.", "transport.source");
        if (order.Troops is < 0 or > ushort.MaxValue || order.Supplies is < 0 or > 5000000 ||
            order.Gold is < 0 or > 100000 || order.Troops == 0 && order.Supplies == 0 && order.Gold == 0 ||
            order.NavalFormationId is < 0 or > 20 || order.SourceBehaviorChoice is < 0 or > 2 ||
            order.Action is not { Target: { } target, StructureKindId: null } || order.WaypointHexIds is null ||
            order.WaypointHexIds.Count > PlayableWaypointMarch.MaximumWaypoints)
            return CommandResult.Reject("Transport packet is invalid.", "transport.selection");
        int hexCount = checked(state.MapWidth * state.MapHeight);
        if (order.WaypointHexIds.Any(hex => hex < 0 || hex >= hexCount))
            return CommandResult.Reject("Transport intermediate coordinates are invalid.", "transport.selection");
        if (order.Action is { ActionKey: "move", Target.Kind: DeploymentTargetKind.Hex })
        {
            if (target.Id < 0 || target.Id >= hexCount)
                return CommandResult.Reject("Transport destination coordinate is invalid.", "transport.destination");
        }
        else if (order.Action is { ActionKey: "enter", Target.Kind: DeploymentTargetKind.Location })
        {
            bool friendly = state.Cities.TryGetValue(target.Id, out var city) ? city.ForceId == force.Id :
                state.Gates.TryGetValue(target.Id, out var gate) && gate.ForceId == force.Id;
            if (!friendly || target.Id == source.Id)
                return CommandResult.Reject("Transport must enter a different friendly base.", "transport.destination");
        }
        else return CommandResult.Reject("Transport supports movement or entry into a friendly base.", "transport.action");

        // Commander validity and status, current capacities, stock clamps,
        // mode-specific routing and the actual district debit form one provider
        // transaction. Page reservations and generic routes are different rules.
        return transportCreation is IPlayableTransportArmyCommandRules commands
            ? commands.IssueTransportArmy(state, order)
            : CommandResult.RuleUnavailable("rules.transport_army_command", "Current transport command rules are unavailable.");
    }

    private CommandResult DispatchTransport(GameState state, ForceState force,
        DispatchTransportOrder order, CommandContext context)
    {
        routes.ValidateWorld(state);
        if (!Enum.IsDefined(order.Arrival))
            return CommandResult.Reject("Transport arrival choice is invalid.", "transport.arrival");
        if (!state.Cities.TryGetValue(order.SourceCityId, out CityState? source) ||
            source.ForceId != force.Id || source.HexId is not int sourceHex ||
            source.DistrictId is not int districtId ||
            !state.Districts.TryGetValue(districtId, out DistrictState? district))
            return CommandResult.Reject("Transport source city is unavailable.", "transport.source");
        if (context.DelegatedDistrictId is int delegated ? delegated != district.Id : district.Number != 1)
            return CommandResult.Reject("Transport source is outside the authorized district.",
                "transport.district_scope");
        if (!state.Cities.TryGetValue(order.DestinationCityId, out CityState? destination) ||
            destination.ForceId != force.Id || destination.Id == source.Id ||
            destination.HexId is not int destinationHex ||
            routes.Route(sourceHex, destinationHex).Length == 0)
            return CommandResult.Reject("Transport destination must be a reachable friendly city.",
                "transport.destination");
        if (!state.Officers.TryGetValue(order.OfficerId, out OfficerState? officer) ||
            officer.ForceId != force.Id || officer.LocationId != source.Id ||
            officer.Presence != OfficerPresence.Present || officer.Deployed || PlayableOfficerAvailability.IsSourceBusy(state, officer) ||
            state.PreparedOfficerMoves?.ContainsKey(officer.Id) == true ||
            state.PreparedTalentOrders?.ContainsKey(officer.Id) == true ||
            state.PreparedDeployments.Values.Any(plan => plan.OfficerId == officer.Id))
            return CommandResult.Reject("Transport officer is unavailable.", "transport.officer");
        if (order.Troops < 1 || order.Supplies < 0 || order.Gold < 0 ||
            order.Troops > source.Troops - DeploymentAvailability.ReservedTroops(state, source.Id) ||
            order.Supplies > source.Supplies || order.Gold > source.Gold)
            return CommandResult.Reject("Transport cargo exceeds available city resources.",
                "transport.resources");
        if (district.OrdersRemaining is not int remaining)
            return CommandResult.RuleUnavailable("rules.command_budget", "District command books are unresolved.");
        if (remaining - CommandBookAvailability.Reserved(state, district.Id) < 1)
            return CommandResult.Reject("District lacks an uncommitted command book.",
                "transport.insufficient_orders");
        state = ArmyIdentityAllocation.Initialize(state);
        int id = state.ArmyIdentities!.Candidate;
        var unit = new UnitState(id, force.Id, officer.Id, sourceHex, destinationHex,
            order.Troops, order.Supplies, new(source.Morale ?? 100, null))
        {
            SourceLocationId = source.Id,
            Mission = new UnitMission(UnitMissionKind.Transport, destination.Id, order.Gold,
                order.Arrival)
        };
        if (transportCreation is not null)
        {
            var created = transportCreation.CreateTransport(state, order, unit);
            return created is null
                ? CommandResult.RuleUnavailable("transport.source_creation",
                    "Current source transport construction is unavailable.")
                : CommandResult.Accept(created.State with { ArmyIdentities = state.ArmyIdentities.Advance() });
        }
        return CommandResult.Accept(state with
        {
            Cities = new Dictionary<int, CityState>(state.Cities)
            {
                [source.Id] = source with
                {
                    Troops = source.Troops - order.Troops,
                    Supplies = source.Supplies - order.Supplies,
                    Gold = source.Gold - order.Gold
                }
            },
            Officers = new Dictionary<int, OfficerState>(state.Officers)
            {
                [officer.Id] = officer with
                {
                    Deployed = true,
                    //This provisional convoy has no native allocation receipt.
                    //Its independent second-location owner cannot retain the
                    //former in-base pointer or be inferred from a logical ID.
                    LocationState = officer.LocationState is { } location
                        ? location with { Position = null } : null
                }
            },
            Districts = new Dictionary<int, DistrictState>(state.Districts)
            {
                [district.Id] = district with { OrdersRemaining = remaining - 1 }
            },
            Units = new Dictionary<int, UnitState>(state.Units) { [id] = unit },
            ArmyIdentities = state.ArmyIdentities.Advance()
        });
    }
}
