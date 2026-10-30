using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed record GeneralTarget(DeploymentTarget Target, DeploymentAction Action, int Hex,
        DeploymentAutomaticStyle Style, int TroopBudget, string Name, int Troops);
    private sealed record GeneralSource(BaseReference Base, int Location, int Troops, ushort Link);

    /// <summary>The ordinary target-first entry. It visits the actual primary
    /// District Footholds and prepares real new packets, without allocating an
    /// Army or reserving resources. Called once on entry, never from redraw.</summary>
    public GeneralUnitTargetPreparation PrepareGeneralUnitTarget(GameState state, DeploymentTarget target)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(target);
        int force = 0, district = 0, hex = -1;
        try
        {
            if (state.Phase != TurnPhase.Strategy) throw GeneralTargetMissing("strategy_phase");
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state,current);
            current.RestoreOwnedArmyReturnSlots(state,state.ReadArmyLifecycle() ?? throw GeneralTargetMissing("Army_lifecycle"));
            _moraleWaveInputs.BindCurrent(state,current); _baseCapacityInputs.BindCurrent(state,current);
            DeploymentTacticCurrentInputs.BindCurrent(state,current);
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state,current) ?? throw GeneralTargetMissing("current_player");
            force = GeneralTargetNeed(player.PhysicalSlot,"player_Force");
            var primary = ForcePrimaryDistrictSource.Resolve(current,player);
            district = GeneralTargetNeed(primary.PhysicalSlot,"primary_District");
            int books = state.Districts.GetValueOrDefault(district)?.OrdersRemaining ?? throw GeneralTargetMissing("District_books");
            var selected = ReadGeneralTargetCurrent(state,current,target);
            hex = selected.Hex;
            var armyDescriptor = current.ReadRegistryDescriptor(RegistryKind.Army) ?? throw GeneralTargetMissing("Army_registry");
            int armyCount = GeneralTargetNeed(current.ReadTemporaryHexListCount(new(armyDescriptor.Identity)),"Army_count");
            var eligible = new List<GeneralSource>();
            foreach (var entry in current.ReadRegisteredBases() ?? throw GeneralTargetMissing("Foothold_registry"))
            {
                var source = entry ?? throw GeneralTargetMissing("Foothold_pointer");
                //334590 filters the live Foothold registry by its actual
                //virtual78 District; it never visits the Army list here.
                if (district is < 1 or > 51 || GeneralTargetNeed(current.ReadBaseDistrictSlot(source),"Foothold_District") != district) continue;
                if (!GeneralTargetNeed(current.ReadBaseNativeValidity(source),"Foothold_validity")) continue;
                if (!GeneralTargetNeed(current.ReadDistrictNativeValidity(district),"District_validity") || books == 0) continue;
                int troops = GeneralTargetNeed(current.ReadBaseResource(source,BaseResourceKind.Troops),"Foothold_troops");
                if (troops < GeneralTargetParameter(state,"minimum-deployment-troops",1000)) continue;
                int physical = GeneralTargetNeed(source.PhysicalSlot,"Foothold_identity");
                int location = physical + (source.Kind == BaseKind.Gate ? 51 : 0);
                var operation = new AutomaticDraftOperation(this,state,location,current);
                var candidates = operation.Candidates();
                if (candidates.Count == 0 || armyCount >= 500) continue;
                bool nearby = source.Kind == BaseKind.City
                    ? state.Cities.GetValueOrDefault(location)?.HasNearbyEnemyArmy ?? throw GeneralTargetMissing("City_nearby_enemy")
                    : state.Gates.GetValueOrDefault(location)?.HasNearbyEnemyArmy ?? throw GeneralTargetMissing("Gate_nearby_enemy");
                if (nearby) continue;
                int usable = operation.ReadUsableTroops(candidates,GeneralTargetParameter(state,"adjacent-enemy-reserve-troops",5000));
                if (usable <= GeneralTargetParameter(state,"usable-troops-threshold",3000)) continue;
                var foodBase = source.Kind == BaseKind.City ? source : CurrentGateMapQueries.FindCity(source,current);
                int food = GeneralTargetNeed(current.ReadBaseResource(foodBase,BaseResourceKind.Supplies),"Foothold_supplies");
                if (food <= GeneralTargetParameter(state,"supplies-threshold",10000)) continue;
                int start = CurrentGateMapQueries.FindHex(source,current).PhysicalSlot ?? throw GeneralTargetMissing("Foothold_Hex");
                int cost = ArmyRouteSearch.CampaignTargetCost(state,current,force,start,hex);
                if (cost >= GeneralTargetParameter(state,"source-route-cost-limit",10000)) continue;
                eligible.Add(new(source,location,troops,GeneralTargetNeed(current.ReadBaseObjectLink(source),"Foothold_link")));
            }
            var sources = eligible.ToArray();
            UnitSelectionGroupSourceSort.Sort(sources,(a,b) => a.Troops != b.Troops ? a.Troops > b.Troops : a.Link < b.Link);
            var counts = new int[21];
            var rows = new List<GeneralUnitTargetArmy>();
            int emittedTroops = 0;
            var defaults = state.DefaultDeploymentBehavior ?? throw GeneralTargetMissing("World_default_behavior");
            foreach (var source in sources)
            {
                var packets = new AutomaticDraftOperation(this,state,source.Location,current).Create(selected.Style,counts,rows.Count);
                foreach (var packet in packets)
                {
                    //The native budget compares the previous sum. Equality
                    //still appends one complete packet, without clipping it.
                    if (emittedTroops > selected.TroopBudget) return Complete();
                    var request = packet.Request with { Actions = Array.AsReadOnly(new[]{selected.Action}),
                        Behavior = defaults, NavalFormationId = packet.NavalFormationId };
                    rows.Add(new(source.Location,packet with { Request = request },hex));
                    emittedTroops = unchecked(emittedTroops + request.Troops);
                }
            }
            return Complete();

            GeneralUnitTargetPreparation Complete()
            {
                //7DDE10 mode1 stable-sorts540 by7CDD60(packet).Base4E
                //before building the Person selector.7ED5B0 later commits
                //that sorted vector; producer formation counts above retain
                //their independent troop-descending construction order.
                var links = sources.ToDictionary(source => source.Location,source => source.Link);
                var displayed = rows.OrderBy(army => links[army.SourceLocationId]).ToArray();
                var preparation = new GeneralUnitTargetPreparation(force,district,selected.Target,hex,Array.AsReadOnly(displayed),
                    new(0,0,0,selected.Name,selected.Troops,books,0,books),null);
                return preparation with { Header = QueryGeneralUnitTargetHeader(preparation,preparation.Armies) };
            }
        }
        catch (InvalidDataException error) { return new(force,district,target,hex,Array.Empty<GeneralUnitTargetArmy>(),null,error.Message); }
    }

    /// <summary>Refresh only the changed local rows' dispatch aggregates.
    /// The entry retains its actual District budget and target metadata.</summary>
    public GeneralUnitTargetHeader QueryGeneralUnitTargetHeader(GeneralUnitTargetPreparation preparation,
        IReadOnlyList<GeneralUnitTargetArmy> orderedArmies)
    {
        ArgumentNullException.ThrowIfNull(preparation); ArgumentNullException.ThrowIfNull(orderedArmies);
        var header = preparation.Header ?? throw GeneralTargetMissing(preparation.UnresolvedOwner ?? "header_entry");
        int troops = 0;
        var sources = new HashSet<int>();
        foreach (var army in orderedArmies) { troops = unchecked(troops + army.Row.Request.Troops); sources.Add(army.SourceLocationId); }
        return header with { ArmyCount = orderedArmies.Count, TotalTroops = troops, DistinctSourceCount = sources.Count,
            CommandBookCost = orderedArmies.Count, CommandBooksAfter = unchecked(header.CommandBooks - orderedArmies.Count) };
    }

    private GeneralTarget ReadGeneralTargetCurrent(GameState state,SourceWorldContext current,DeploymentTarget target)
    {
        if (target.Kind == DeploymentTargetKind.Hex && target.Id is >= 0 and < 48400)
        {
            var hex = current.ReadHexTableReference(target.Id) ?? throw GeneralTargetMissing("target_Hex_pointer");
            int physical = GeneralTargetNeed(hex.PhysicalSlot,"target_Hex");
            int rawArea = GeneralTargetNeed(current.ReadHexAreaSlot(hex),"target_Area_link");
            var area = current.ReadAreaTableReference(rawArea <= 500 ? rawArea : 0) ?? throw GeneralTargetMissing("target_Area_pointer");
            int areaId = GeneralTargetNeed(area.PhysicalSlot,"target_Area");
            string name = state.Areas.GetValueOrDefault(areaId)?.Name ?? throw GeneralTargetMissing("target_Area_name");
            return new(target,new("occupy",target),physical,DeploymentAutomaticStyle.Occupation,int.MaxValue,name,0);
        }
        if (target.Kind == DeploymentTargetKind.Location && target.Id is >= 1 and <= 61)
        {
            var receiver = BaseLookup.Find(target.Id,current);
            if (!GeneralTargetNeed(current.ReadBaseNativeValidity(receiver),"target_Foothold_validity")) throw GeneralTargetMissing("target_Foothold_invalid");
            int goal = CurrentGateMapQueries.FindHex(receiver,current).PhysicalSlot ?? throw GeneralTargetMissing("target_Foothold_Hex");
            string name = target.Id <= 51 ? state.Cities.GetValueOrDefault(target.Id)?.Name ?? throw GeneralTargetMissing("target_City_name")
                : state.Gates.GetValueOrDefault(target.Id)?.Name ?? throw GeneralTargetMissing("target_Gate_name");
            int troops = GeneralTargetNeed(current.ReadBaseResource(receiver,BaseResourceKind.Troops),"target_Foothold_troops");
            return new(target,new("attack",target),goal,DeploymentAutomaticStyle.SiegeTroops,int.MaxValue,name,troops);
        }
        if (target.Kind == DeploymentTargetKind.Unit && state.Units.TryGetValue(target.Id,out var unit))
        {
            int physical = unit.NativeArmyOrigin?.NativeSlotIndex ?? throw GeneralTargetMissing("target_Army_receiver");
            var army = current.ReadArmyUnitTableReference(physical) ?? throw GeneralTargetMissing("target_Army_pointer");
            if (!GeneralTargetNeed(current.ReadArmyUnitNativeValidity(army),"target_Army_validity")) throw GeneralTargetMissing("target_Army_invalid");
            int raw = GeneralTargetNeed(current.ReadArmyUnitRawHex2A(army),"target_Army_Hex");
            int goal = (current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw GeneralTargetMissing("target_Army_Hex_pointer"))
                .PhysicalSlot ?? throw GeneralTargetMissing("target_Army_Hex_identity");
            int budget = unchecked(unit.Troops * GeneralTargetParameter(state,"target-Army-troops-percent",150)) / 100;
            return new(target,new("attack",target),goal,DeploymentAutomaticStyle.FieldAttack,budget,ReadHistoryArmyName(state,current,army),unit.Troops);
        }
        throw GeneralTargetMissing("target_kind_or_receiver");
    }

    private int GeneralTargetParameter(GameState state,string name,int extractedValue)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw GeneralTargetMissing("parameter_owner");
        if (bank.Language != Seed.Language || bank.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language)) throw GeneralTargetMissing("parameter_profile");
        string key = "general-unit-target:" + name;
        if (bank.Float32Values.ContainsKey(key)) throw GeneralTargetMissing("parameter_type_"+name);
        return bank.Int32Values.TryGetValue(key,out int value) ? value : extractedValue;
    }
    private static T GeneralTargetNeed<T>(T? value,string name) where T:struct => value ?? throw GeneralTargetMissing(name);
    private static InvalidDataException GeneralTargetMissing(string name) => new("deployment.general_target."+name);
}
