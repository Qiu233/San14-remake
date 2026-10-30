using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

internal sealed record OriginalAiTransportCommand(DispatchTransportArmyOrder Command, int DestinationLocationId);
internal sealed record OriginalAiTransportPreparation(bool Success, IReadOnlyList<OriginalAiTransportCommand> Commands);

public sealed partial class ArmyCampaignRuleAdapter
{
    //Shared normal command boundary for an explicitly selected paying District.
    //It never opens a deployment page or applies its primary-District filters.
    private bool ValidateDistrictTransportPacketCurrent(GameState state, SourceWorldContext current,
        DispatchTransportArmyOrder order, int district)
    {
        if (Seed.Language != "TC") throw AiTransportMissing("SC_source_contract");
        if (!AiTransportKnown(current.ReadDistrictNativeValidity(district), "command_District_validity") ||
            AiTransportKnown(current.ReadDistrictForceSlot(new(district)), "command_District10") != order.ForceId) return false;
        int person = AiTransportKnown(current.ReadPersonLookupReference(order.OfficerId)?.PhysicalSlot, "command_Person_pointer");
        if (!AiTransportKnown(current.ReadPersonNativeValidity(person), "command_Person_validity") ||
            AiTransportKnown(current.ReadPersonDistrictSlot(person), "command_PersonE0") != district) return false;
        var source = current.ReadCityTableReference(order.SourceCityId) ?? throw AiTransportMissing("command_source_City");
        int home = AiTransportKnown(current.ReadBaseObjectLink(source), "command_source4E");
        if (!AiTransportKnown(current.ReadBaseNativeValidity(source), "command_source_validity") ||
            AiTransportKnown(current.ReadBaseForceSlot(source), "command_source_Force") != order.ForceId ||
            AiTransportKnown(current.ReadPersonFirstLocation(person), "command_PersonE2") != home) return false;
        var query = new OriginalTransportSelection(this, state, current, district);
        if (!query.People(source).Contains(person)) return false;
        var limits = query.PersonLimits(source, person);
        if (order.Troops > limits.Troops || order.Gold > limits.Gold || order.Supplies > limits.Food) return false;
        int start = current.ReadBaseCurrentHexIndex(source), goal;
        if (order.Action is { ActionKey: "enter", Target.Kind: DeploymentTargetKind.Location })
        {
            var target = query.Base(order.Action.Target.Id);
            if (!AiTransportKnown(current.ReadBaseNativeValidity(target), "command_target_validity") || query.ForceOf(target) != order.ForceId) return false;
            goal = current.ReadBaseCurrentHexIndex(target);
        }
        else if (order.Action is { ActionKey: "move", Target.Kind: DeploymentTargetKind.Hex }) goal = order.Action.Target.Id;
        else return false;
        foreach (int next in order.WaypointHexIds.Append(goal))
        {
            if (PersonReturnPathSource.Search(current, start, next).Length == 0) return false;
            start = next;
        }
        return true;
    }
    internal bool ReadOriginalAiTransportDistrictGateCurrent(GameState state, SourceWorldContext current,
        int district, bool front)
    {
        if (Seed.Language != "TC") throw AiTransportMissing("SC_source_contract");
        if (AiTransportKnown(current.ReadDistrictRawByte(district, 0x14), "District14") == 0) return false;
        return !front || !ReadOriginalAiDistrictIsCurrentPlayer(state, current, district) ||
            (AiTransportKnown(current.ReadDistrictRawByte(district, 0x1B), "District1B") & 0x10) != 0;
    }

    internal OriginalAiTransportPreparation PrepareOriginalAiFrontTransportCurrent(GameState state,
        SourceWorldContext current, int district)
    {
        var query = new OriginalTransportSelection(this, state, current, district);
        var saved = state.OriginalAiStrategyMemory?.Forces.GetValueOrDefault(query.Force)
            ?? throw AiTransportMissing("Force_classification_owner");
        if (saved.FrontierLocationIds.IsDefault || saved.RearLocationIds.IsDefault)
            throw AiTransportMissing("Force_classification_lists");
        var frontier = saved.FrontierLocationIds.ToHashSet();
        var rear = saved.RearLocationIds.ToHashSet();
        var sources = saved.FrontierLocationIds.Select(query.Base)
            .Where(at => AiTransportKnown(current.ReadBaseDistrictSlot(at), "Base_District") == district).ToArray();
        if (sources.Length == 0) return new(false, []);
        var commands = new List<OriginalAiTransportCommand>();
        foreach (var source in sources)
        {
            if (query.Special(source) || query.Location(source) is >= 52 and <= 61 || query.EnemyNear(source, 15)) continue;
            var own = query.Resources(source, incoming: true, homeHalf: false);
            var stock = query.Resources(source, incoming: false, homeHalf: false);
            var needs = query.Memory(source);
            int gold = Math.Min(unchecked(stock.Gold - 3000), unchecked(-AiTransportKnown(needs.GoldNeed, "Base98_GoldNeed") - 3000));
            int food = Math.Min(unchecked(stock.Food - 10000), unchecked(-AiTransportKnown(needs.FoodNeed, "Base94_FoodNeed") - 10000));
            int troops = Math.Min(unchecked(stock.Troops - 5000), unchecked(stock.Troops - query.Capacity(source).Troops + 30000) / 2);
            if (food < 10000) food = 0;
            if (gold < 3000) gold = 0;
            if (troops < 1000) troops = 0;
            if (food < 1 && gold < 1) { if (troops < 1) continue; }
            else if (troops < 1) { if (stock.Troops < 5001) continue; troops = 1000; }
            var targets = new List<BaseReference>();
            foreach (var target in query.Neighbors(source))
            {
                if (!frontier.Contains(query.Location(target)) || query.ForceOf(target) != query.Force ||
                    query.Location(target) is >= 52 and <= 61 || query.Special(target) || !query.Reachable(source, target)) continue;
                if (query.Neighbors(target).Any(at => rear.Contains(query.Location(at)))) continue;
                if (query.HasHomeTransport(source, target) || query.HasHomeTransport(target, source)) continue;
                targets.Add(target);
            }
            if (targets.Count == 0) continue;
            var people = query.People(source);
            foreach (var target in targets)
            {
                if (people.Count == 0) break;
                var other = query.Resources(target, incoming: true, homeHalf: true);
                var cap = query.Capacity(target);
                int sendFood = Math.Min(cap.Food - other.Food, Math.Min(food, unchecked(own.Food - other.Food) / 2));
                int sendGold = Math.Min(cap.Gold - other.Gold, Math.Min(gold, unchecked(own.Gold - other.Gold) / 2));
                int sendTroops = Math.Min(cap.Troops - other.Troops, Math.Min(troops, unchecked(own.Troops - other.Troops) / 2));
                if (sendFood < 10000) sendFood = 0;
                if (sendGold < 3000) sendGold = 0;
                if (sendTroops < 5000) sendTroops = 0;
                if (sendFood <= 0 && sendGold <= 0 && sendTroops <= 1000) continue;
                sendTroops = Math.Max(1000, sendTroops);
                int person = people[0]; people.RemoveAt(0);
                var limit = query.PersonLimits(source, person);
                sendFood = Math.Min(sendFood, limit.Food); sendGold = Math.Min(sendGold, limit.Gold);
                sendTroops = Math.Min(sendTroops, limit.Troops);
                commands.Add(query.Command(source, target, person, sendTroops, sendGold, sendFood));
                query.RecordTransport(source, target, sendTroops, sendGold, sendFood);
                food = unchecked(food - sendFood); gold = unchecked(gold - sendGold); troops = unchecked(troops - sendTroops);
                own = (unchecked(own.Troops - sendTroops), unchecked(own.Gold - sendGold), unchecked(own.Food - sendFood));
                if (query.BooksAfter(commands.Count) == 0) break;
            }
            if (query.BooksAfter(commands.Count) == 0) break;
        }
        return new(true, commands);
    }

    internal OriginalAiTransportPreparation PrepareOriginalAiTransportCurrent(GameState state,
        SourceWorldContext current, int district, IReadOnlyList<OriginalAiEntity> sources,
        IReadOnlyList<OriginalAiEntity> targets, int minimumFood, int minimumGold, int minimumTroops)
    {
        if (Seed.Language != "TC") throw AiTransportMissing("SC_source_contract");
        bool advanced = AiTransportKnown(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "SettingsE58");
        int foodFloor = advanced ? unchecked(AiTransportKnown(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 41), "advanced41") * 7500) / 100 + 7500 : 15000;
        minimumFood = Math.Max(minimumFood, foodFloor); minimumGold = Math.Max(minimumGold, 8000);
        if (sources.Count == 0 || targets.Count == 0) return new(false, []);
        var query = new OriginalTransportSelection(this, state, current, district);
        var destinations = new List<OriginalTransportDemand>();
        foreach (var entity in targets)
        {
            var at = query.Base(entity.Id);
            if (query.ForceOf(at) != query.Force || query.IncomingCount(at) >= 5) continue;
            var stock = query.Resources(at, true, true); var cap = query.Capacity(at);
            int food = at.Kind == BaseKind.Gate ? 0 : Math.Max(0, unchecked(cap.Food - stock.Food));
            int gold = at.Kind == BaseKind.Gate ? 0 : Math.Max(0, unchecked(cap.Gold - stock.Gold));
            int troops = Math.Max(0, unchecked(cap.Troops - stock.Troops));
            if (food <= 10000) food = 0; if (gold <= 10000) gold = 0; if (troops <= 5000) troops = 0;
            if (food != 0 || gold != 0 || troops != 0) destinations.Add(new(at, troops, gold, food));
        }
        if (destinations.Count == 0) return new(false, []);
        //The native list is stably sorted twice before the first nine entries
        //become the admissible target list. Neither sort invents an ID tie break.
        StableTransportSort(destinations, (a, b) => query.Urgency(a.At) > query.Urgency(b.At));
        StableTransportSort(destinations, (a, b) =>
        {
            int first = query.Resources(a.At, false, false).Food, second = query.Resources(b.At, false, false).Food;
            return (first < 20000 && first < second) != (second < 20000 && second < first) && first < second;
        });
        var selectedTargets = destinations.Take(9).Select(row => row.At).ToHashSet();
        var budgets = new List<OriginalTransportBudget>();
        foreach (var entity in sources)
        {
            var at = query.Base(entity.Id); var stock = query.Resources(at, false, false);
            int food = 0, gold = 0, troops = unchecked(stock.Troops - minimumTroops);
            if (at.Kind != BaseKind.Gate)
            {
                var memory = query.Memory(at);
                int goldNeed = AiTransportKnown(memory.GoldNeed, "source_Base98"), foodNeed = AiTransportKnown(memory.FoodNeed, "source_Base94");
                if (goldNeed < 1)
                {
                    gold = Math.Min(unchecked(stock.Gold - minimumGold), unchecked(-goldNeed - minimumGold));
                    int homeDistrict = AiTransportKnown(current.ReadBaseDistrictSlot(at), "source_District");
                    int leaderId = AiTransportKnown(current.ReadDistrictLeader(homeDistrict), "source_District12");
                    int leader = AiTransportKnown(current.ReadPersonLookupReference(leaderId <= 6000 ? leaderId : 0)?.PhysicalSlot, "source_leader_pointer");
                    if (AiTransportKnown(current.ReadPersonNativeValidity(leader), "source_leader_validity") &&
                        AiTransportKnown(current.ReadPersonFirstLocation(leader), "source_leaderE2") == query.Location(at))
                    {
                        int reserve = unchecked((AiTransportKnown(RulerCaptureReleaseInputs.ReadCurrentWord(state, leader, 0x170), "source_leader170") + 24) * 500);
                        if (reserve > 0) gold = unchecked(gold - reserve);
                    }
                }
                if (foodNeed < 1) food = Math.Min(unchecked(stock.Food - minimumFood), unchecked(-foodNeed - minimumFood));
            }
            if (at.Kind != BaseKind.Gate && (food > 5000 || gold > 3000) || troops > 3000)
                budgets.Add(new(at, stock.Troops, stock.Gold, stock.Food, troops, gold, food));
        }
        var usedPeople = new HashSet<int>();
        foreach (var budget in budgets)
        {
            var ordered = destinations.ToList();
            StableTransportSort(ordered, (a, b) => query.Distance(budget.At, a.At) < query.Distance(budget.At, b.At));
            foreach (var demand in ordered)
            {
                if (!selectedTargets.Contains(demand.At) || budget.At == demand.At ||
                    budget.At.Kind == BaseKind.Gate && (demand.Gold > 0 || demand.Food > 0)) continue;
                var route = query.Intermediate(budget.At, demand.At);
                if (route is null || query.ForceOf(route.Value.Target) != query.Force ||
                    query.HasTransport(budget.At, route.Value.Target) || query.HasTransport(route.Value.Target, budget.At) ||
                    query.EnemyNear(route.Value.Target, 5) || !query.Reachable(budget.At, route.Value.Target)) continue;
                var personCandidates = query.People(budget.At);
                foreach (int person in personCandidates)
                {
                    if (usedPeople.Contains(person)) continue;
                    var limits = query.PersonLimits(budget.At, person);
                    int roomTroops = demand.Troops, roomGold = demand.Gold, roomFood = demand.Food;
                    if (route.Value.Target != demand.At)
                    {
                        var cap = query.Capacity(route.Value.Target); var stock = query.Resources(route.Value.Target, true, false);
                        roomTroops = unchecked(cap.Troops - stock.Troops); roomGold = unchecked(cap.Gold - stock.Gold); roomFood = unchecked(cap.Food - stock.Food);
                    }
                    int troops = Math.Min(roomTroops, Math.Min(limits.Troops, budget.AvailableTroops));
                    int gold = Math.Min(roomGold, Math.Min(limits.Gold, budget.AvailableGold));
                    int food = Math.Min(roomFood, Math.Min(limits.Food, budget.AvailableFood));
                    if (troops < 3000) troops = 0;
                    if (gold < 3000) gold = 0; else gold = Math.Min(gold, 100000);
                    if (food < 10000) food = 0; else food = Math.Min(food, 5000000);
                    if (troops <= 0 && gold <= 0 && food <= 0) continue;
                    var destination = route.Value.Target;
                    if (troops < 1000)
                    {
                        troops = 1000;
                        if (budget.StockTroops - 1000 < minimumTroops) continue;
                        if (route.Value.EscortGate is { } gate && AiTransportKnown(current.ReadBaseNativeValidity(gate), "escort_Gate_validity")) destination = gate;
                    }
                    if (budget.At.Kind == BaseKind.Gate && (gold != 0 || food != 0)) continue;
                    usedPeople.Add(person);
                    if (destination == demand.At)
                    { demand.Troops = unchecked(demand.Troops - troops); demand.Gold = unchecked(demand.Gold - gold); demand.Food = unchecked(demand.Food - food); }
                    budget.StockTroops = unchecked(budget.StockTroops - troops); budget.StockGold = unchecked(budget.StockGold - gold); budget.StockFood = unchecked(budget.StockFood - food);
                    budget.AvailableTroops = unchecked(budget.AvailableTroops - troops); budget.AvailableGold = unchecked(budget.AvailableGold - gold); budget.AvailableFood = unchecked(budget.AvailableFood - food);
                    budget.Shipments.Add((person, destination, troops, gold, food));
                    break;
                }
            }
        }
        var commands = new List<OriginalAiTransportCommand>();
        foreach (var budget in budgets)
        {
            var visited = new HashSet<BaseReference>();
            foreach (var shipment in budget.Shipments)
            {
                if (!visited.Add(shipment.Target)) continue;
                int troops = 0, gold = 0, food = 0;
                foreach (var row in budget.Shipments)
                {
                    if (row.Target != shipment.Target) continue;
                    if (row.Troops > 1000) troops = unchecked(troops + row.Troops);
                    gold = unchecked(gold + row.Gold); food = unchecked(food + row.Food);
                }
                var stock = query.Resources(shipment.Target, true, true); var cap = query.Capacity(shipment.Target);
                troops = Math.Min(troops, unchecked(cap.Troops - stock.Troops));
                gold = Math.Min(gold, unchecked(cap.Gold - stock.Gold)); food = Math.Min(food, unchecked(cap.Food - stock.Food));
                troops = Math.Max(1000, troops); if (gold < 3000) gold = 0; if (food < 10000) food = 0;
                var limits = query.PersonLimits(budget.At, shipment.Person);
                troops = Math.Min(troops, limits.Troops); gold = Math.Min(gold, limits.Gold); food = Math.Min(food, limits.Food);
                if (food < 1 && gold < 1 && troops < 1001) continue;
                commands.Add(query.Command(budget.At, shipment.Target, shipment.Person, troops, gold, food));
                query.RecordTransport(budget.At, shipment.Target, troops, gold, food);
                if (AiTransportKnown(current.ReadPersonDistrictSlot(shipment.Person), "command_PersonE0") == district &&
                    query.ActualBooksAfter(commands.Count(command => command.Command.DistrictId == district)) == 0) return new(true, commands);
            }
        }
        return new(true, commands);
    }

    private sealed class OriginalTransportDemand(BaseReference at, int troops, int gold, int food)
    {
        internal readonly BaseReference At = at;
        internal int Troops = troops, Gold = gold, Food = food;
    }
    private sealed class OriginalTransportBudget(BaseReference at, int stockTroops, int stockGold, int stockFood,
        int availableTroops, int availableGold, int availableFood)
    {
        internal readonly BaseReference At = at;
        internal int StockTroops = stockTroops, StockGold = stockGold, StockFood = stockFood;
        internal int AvailableTroops = availableTroops, AvailableGold = availableGold, AvailableFood = availableFood;
        internal readonly List<(int Person, BaseReference Target, int Troops, int Gold, int Food)> Shipments = [];
    }
    private static void StableTransportSort<T>(List<T> rows, Func<T, T, bool> before)
    {
        for (int i = 1; i < rows.Count; i++)
        {
            T value = rows[i]; int at = i;
            while (at > 0 && before(value, rows[at - 1])) { rows[at] = rows[at - 1]; at--; }
            rows[at] = value;
        }
    }

    private sealed class OriginalTransportSelection
    {
        private readonly ArmyCampaignRuleAdapter owner;
        private readonly GameState state;
        internal readonly SourceWorldContext Current;
        internal readonly int District, Force;
        private readonly List<(int Source, int Target, int Troops, int Gold, int Food)> planned = [];
        private readonly List<(int Physical, int Status, int Target)> armies = [];
        private List<int>? homeArmies;
        private readonly Dictionary<BaseReference, List<BaseReference>> neighborCache = [];
        private bool armiesRead;

        internal OriginalTransportSelection(ArmyCampaignRuleAdapter owner, GameState state, SourceWorldContext current, int district)
        {
            this.owner = owner; this.state = state; Current = current; District = district;
            Force = AiTransportKnown(current.ReadDistrictForceSlot(new(district)), "District10");
            owner._baseCapacityInputs.BindCurrent(state, current);
        }

        private void EnsureArmies()
        {
            if (armiesRead) return;
            var current = Current;
            var arena = state.ReadArmyLifecycle() ?? throw AiTransportMissing("Army_lifecycle");
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw AiTransportMissing("Army_registry"))
            {
                int physical = entry?.PhysicalSlot ?? throw AiTransportMissing("Army_registry_pointer");
                var at = new ArmyUnitReference(physical);
                if (AiTransportKnown(current.ReadArmyUnitForceReference(at).PhysicalSlot, "Army_Force") != Force ||
                    AiTransportKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "Army233560")) continue;
                var receiver = arena.ReceiverSlots()[physical];
                int status = AiTransportKnown(receiver.Status10, "Army10");
                int target = -1;
                if (status == 2)
                {
                    var to = OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId) ?? throw AiTransportMissing("Army_target");
                    target = OriginalAiBaseNeedsMath.TransportTargetLocation(AiTransportKnown(to.Kind38, "Army38"), AiTransportKnown(to.Identity3A, "Army3A"));
                }
                armies.Add((physical, status, target));
            }
            armiesRead = true;
        }

        internal BaseReference Base(int location) => BaseLookup.Find(location, Current);
        internal int Location(BaseReference at) => AiTransportKnown(Current.ReadBaseObjectLink(at), "Base4E");
        internal int ForceOf(BaseReference at) => AiTransportKnown(Current.ReadBaseForceSlot(at), "Base_Force");
        internal bool Special(BaseReference at) => AiTransportKnown(Current.ReadConditionBaseSpecialGovernor(at), "Base_virtual68");
        internal OriginalAiFootholdMemory Memory(BaseReference at) => state.OriginalAiStrategyMemory?.Footholds.GetValueOrDefault(Location(at))
            ?? throw AiTransportMissing("Base_planning_owner." + Location(at));
        internal (int Troops, int Gold, int Food) Capacity(BaseReference at)
        {
            var c = BasePostLoad.ReadResourceCapacities(at, Current); return (c.Troops, c.Gold, c.Supplies);
        }
        internal (int Troops, int Gold, int Food) Resources(BaseReference at, bool incoming, bool homeHalf)
        {
            if (incoming) EnsureArmies();
            int location = Location(at), troops = AiTransportKnown(Current.ReadBaseResource(at, BaseResourceKind.Troops), "Base3C"),
                gold = AiTransportKnown(Current.ReadBaseResource(at, BaseResourceKind.Gold), "Base_virtual90"),
                food = AiTransportKnown(Current.ReadBaseResource(at, BaseResourceKind.Supplies), "Base_virtualA0");
            foreach (var row in armies)
            {
                if (!incoming || row.Status != 2 || row.Target != location) continue;
                var receiver = state.ReadArmyLifecycle()!.ReceiverSlots()[row.Physical];
                troops = unchecked(troops + ArmyTroops(row.Physical));
                gold = unchecked(gold + AiTransportKnown(receiver.StoredResources.Gold, "Army5C"));
                food = unchecked(food + AiTransportKnown(receiver.StoredResources.Food, "Army60"));
            }
            if (homeHalf && location is >= 1 and <= 61)
                foreach (int physical in HomeArmies())
                    if (ArmyHome(physical) == location && AiTransportKnown(Current.ReadArmyReturnUnitStatus10(new(physical)), "home_Army10") == 1)
                        troops = unchecked(troops + (ArmyTroops(physical) >> 1));
            foreach (var row in planned)
            {
                if (row.Source == location) { troops = unchecked(troops - row.Troops); gold = unchecked(gold - row.Gold); food = unchecked(food - row.Food); }
                if (incoming && row.Target == location) { troops = unchecked(troops + row.Troops); gold = unchecked(gold + row.Gold); food = unchecked(food + row.Food); }
            }
            return (troops, gold, food);
        }
        internal bool HasTransport(BaseReference from, BaseReference to)
        {
            EnsureArmies();
            return armies.Any(a => a.Status == 2 && a.Target == Location(to) && ArmyHome(a.Physical) == Location(from)) ||
                planned.Any(a => a.Source == Location(from) && a.Target == Location(to));
        }
        internal int IncomingCount(BaseReference at) { EnsureArmies(); return armies.Count(a => a.Status == 2 && a.Target == Location(at)); }
        private IReadOnlyList<int> HomeArmies()
        {
            if (homeArmies is not null) return homeArmies;
            homeArmies = [];
            foreach (var entry in Current.ReadRegistryReferences(RegistryKind.Army) ?? throw AiTransportMissing("home_Army_registry"))
            {
                int physical = entry?.PhysicalSlot ?? throw AiTransportMissing("home_Army_pointer");
                if (!AiTransportKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, Current, physical), "home_Army233560")) homeArmies.Add(physical);
            }
            return homeArmies;
        }
        internal bool HasHomeTransport(BaseReference from, BaseReference to)
        {
            int home = Location(from), target = Location(to);
            if (home is < 1 or > 61) return false;
            foreach (int physical in HomeArmies())
            {
                if (ArmyHome(physical) != home || AiTransportKnown(Current.ReadArmyReturnUnitStatus10(new(physical)), "home_Army10") != 2) continue;
                var receiver = state.ReadArmyLifecycle()!.ReceiverSlots()[physical];
                var fields = OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId) ?? throw AiTransportMissing("home_Army_target");
                int destination = OriginalAiBaseNeedsMath.TransportTargetLocation(AiTransportKnown(fields.Kind38, "home_Army38"), AiTransportKnown(fields.Identity3A, "home_Army3A"));
                if (destination == target) return true;
            }
            return planned.Any(row => row.Source == home && row.Target == target);
        }
        private int ArmyHome(int physical)
        {
            int commander = AiTransportKnown(Current.ReadArmyUnitCommanderId(new(physical)), "Army12");
            var person = commander <= 6000 ? Current.ReadPersonLookupReference(commander) : Current.ReadTransferPersonTableReference(0);
            int p = AiTransportKnown(person?.PhysicalSlot, "Army_Person");
            return AiTransportKnown(Current.ReadPersonNativeValidity(p), "Army_Person_validity")
                ? AiTransportKnown(Current.ReadPersonFirstLocation(p), "Army_PersonE2") : 0;
        }
        private int ArmyTroops(int physical) => AiTransportKnown(
            (Current.ReadArmyReturnResourceFields(new(physical)) ?? throw AiTransportMissing("Army_resources")).Ready16, "Army16");
        internal void RecordTransport(BaseReference from, BaseReference to, int troops, int gold, int food) => planned.Add((Location(from), Location(to), troops, gold, food));
        internal bool Reachable(BaseReference from, BaseReference to) => PersonReturnPathSource.Search(Current,
            Current.ReadBaseCurrentHexIndex(from), Current.ReadBaseCurrentHexIndex(to)).Length != 0;
        internal int Distance(BaseReference from, BaseReference to) => ArmyRouteSearch.Distance(
            Current.ReadBaseCurrentHexIndex(from), Current.ReadBaseCurrentHexIndex(to));
        internal int Urgency(BaseReference at)
        {
            var memory = Memory(at); var stock = Resources(at, false, false); var capacity = Capacity(at);
            var city = CurrentGateMapQueries.FindCity(at, Current);
            int size = AiTransportKnown(Current.ReadCitySizeB8(city), "target_CityB8");
            if (size == 0) throw AiTransportMissing("target_CityB8_divisor");
            return unchecked(Math.Max(0, AiTransportKnown(memory.FoodNeed, "target_Base94")) * 5 +
                Math.Max(0, AiTransportKnown(memory.GoldNeed, "target_Base98")) + (capacity.Troops - stock.Troops) * 3) / size;
        }
        internal (BaseReference Target, BaseReference? EscortGate)? Intermediate(BaseReference source, BaseReference target)
        {
            var neighbors = Neighbors(source);
            BaseReference selected = target;
            if (!neighbors.Contains(CurrentGateMapQueries.FindCity(target, Current)))
            {
                if (source.Kind != BaseKind.City) return null;
                int first = Area(source), last = Area(target);
                var path = OriginalAiTransportAreaRoute.Resolve(Current, first, last, true, Neighbors);
                if (!path.Found) return null;
                BaseReference? middle = null;
                foreach (int raw in path.Areas)
                {
                    var area = Current.ReadAreaTableReference(raw is >= 0 and < 501 ? raw : 0) ?? throw AiTransportMissing("route_Area");
                    if (!AiTransportKnown(Current.ReadAreaNativeValidity(area), "route_Area_validity") || area.PhysicalSlot == first) continue;
                    int city = AiTransportKnown(Current.ReadAreaCitySlot(area), "route_Area35");
                    middle = Current.ReadCityTableReference(city < 52 ? city : 0) ?? throw AiTransportMissing("route_City");
                    break;
                }
                if (middle is null || middle == source || middle == target) return null;
                selected = middle;
            }
            if (!neighbors.Contains(CurrentGateMapQueries.FindCity(selected, Current))) return null;
            BaseReference? escort = null;
            if (selected.Kind == BaseKind.City)
            {
                int minimum = AreaDistance(source, selected);
                foreach (var entry in Current.ReadArmyReturnRegisteredGates() ?? throw AiTransportMissing("escort_Gate_registry"))
                {
                    var gate = entry ?? throw AiTransportMissing("escort_Gate_pointer");
                    if (CurrentGateMapQueries.FindCity(gate, Current) != selected || ForceOf(gate) != Force) continue;
                    int distance = AreaDistance(source, gate);
                    if (distance >= minimum) continue;
                    minimum = distance; escort = gate;
                }
            }
            return (selected, escort);
        }
        private int Area(BaseReference at) => AiTransportKnown(CurrentGateMapQueries.FindArea(at, Current).PhysicalSlot, "Base_Area");
        private int AreaDistance(BaseReference from, BaseReference to)
        {
            int first = Area(from), last = Area(to);
            if (first == last) return 0;
            var path = OriginalAiTransportAreaRoute.Resolve(Current, first, last, false);
            return path.Found ? path.Cost : int.MaxValue;
        }
        internal List<BaseReference> Neighbors(BaseReference at)
        {
            if (neighborCache.TryGetValue(at, out var cached)) return cached;
            var temporary = Current.CreateTemporaryHexList() ?? throw AiTransportMissing("neighbor_descriptor");
            try
            {
                new PersonFreedomHomeSource(owner._scenario).FillCities22F430(Current, at, temporary);
                var values = new List<BaseReference>();
                var node = Current.ReadRegistryHead(new(temporary.Identity)) ?? throw AiTransportMissing("neighbor_head");
                while (node.Identity.HasValue)
                {
                    var entry = Current.ReadRegistryNodePayload(node) ?? throw AiTransportMissing("neighbor_payload");
                    if (entry.Kind != RegistryKind.City) throw AiTransportMissing("neighbor_City_kind");
                    values.Add(new(BaseKind.City, entry.PhysicalSlot));
                    node = Current.ReadRegistryNodeNext(node) ?? throw AiTransportMissing("neighbor_next");
                }
                neighborCache.Add(at, values);
                return values;
            }
            finally { Current.ClearTemporaryHexList(temporary); Current.ReleaseTemporaryHexList(temporary); }
        }
        internal bool EnemyNear(BaseReference at, int radius)
        {
            //0B8620 reads the real Hex occupancy bindings, not Army position projections.
            var occupancy = state.ArmyMapOccupancy ?? throw AiTransportMissing("Hex_Army_occupancy");
            int center = Current.ReadBaseCurrentHexIndex(at);
            var visited = new HashSet<int> { center }; var queue = new Queue<(int Hex, int Distance)>(); queue.Enqueue((center, 0));
            while (queue.TryDequeue(out var row))
            {
                if (row.Distance != 0)
                {
                    var cell = occupancy.ReadCell(row.Hex);
                    if (cell.Kind == ArmyMapCellKind.Unknown) throw AiTransportMissing("Hex_occupancy_cell");
                    if (cell.Kind == ArmyMapCellKind.Binding)
                    {
                        var binding = occupancy.GetBinding(cell.BindingId ?? throw AiTransportMissing("Hex_binding"));
                        if (binding.Kind == ArmyOccupancyUnitKind.Unknown) throw AiTransportMissing("Hex_binding_Army");
                        if (binding.Kind == ArmyOccupancyUnitKind.Unit)
                        {
                            int unitId = binding.UnitId ?? throw AiTransportMissing("Hex_Army_identity");
                            var army = state.Units.GetValueOrDefault(unitId) ?? state.ReadArmyLifecycle()?.Receivers.FindUnit(unitId)?.PendingImage
                                ?? throw AiTransportMissing("Hex_Army_owner");
                            int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw AiTransportMissing("Hex_Army_physical");
                            if (AiTransportKnown(Current.ReadArmyUnitNativeValidity(new(physical)), "near_Army_validity"))
                            {
                                var first = Current.ReadForceTableReference(ForceOf(at)) ?? throw AiTransportMissing("near_Base_Force");
                                var second = Current.ReadArmyUnitForceReference(new(physical));
                                if (!AiTransportKnown(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, first, second), "near_Force_friendly")) return true;
                            }
                        }
                    }
                }
                if (row.Distance == radius) continue;
                for (int direction = 1; direction <= 6; direction++)
                {
                    int hex = PersonReturnPathSource.Neighbor(row.Hex, direction);
                    if (hex < 48400 && visited.Add(hex)) queue.Enqueue((hex, row.Distance + 1));
                }
            }
            return false;
        }
        internal List<int> People(BaseReference at)
        {
            int home = Location(at);
            int ruler = AiTransportKnown(Current.ReadForceRecord(Force)?.RawRuler10, "Force_ruler10");
            var people = new List<int>();
            foreach (int p in EnumerateBaseResidents(Current, (ushort)home, AiTransportMissing))
            {
                if (AiTransportKnown(Current.ReadPersonResetWord1FF(p), "Person1C7") % 256 != 0 ||
                    AiTransportKnown(Current.ReadPersonSecondLocation(p), "PersonE4") != home ||
                    (AiTransportKnown(Current.ReadArmyReturnPersonFlags196(p), "Person15E") & 1) != 0 ||
                    (Current.ReadPersonLinkedEntity(p) ?? throw AiTransportMissing("Person_linked_command")).Identity.HasValue ||
                    AiTransportKnown(Current.ReadPersonIdentity(p), "Person10") == ruler ||
                    AiTransportKnown(OfficerHealthSourceAdapter.ReadByte(Current, p), "Person161") != 0) continue;
                people.Add(p);
            }
            //023D40/0973A0's actual comparator: trait32 presence first,
            //otherwise the smaller full strength+leadership sum comes first.
            for (int i = 1; i < people.Count; i++)
            {
                int p = people[i], position = i;
                while (position > 0 && Before(p, people[position - 1])) { people[position] = people[position - 1]; position--; }
                people[position] = p;
            }
            return people;
            bool Before(int a, int b)
            {
                if (AiTransportKnown(TacticRemakeCurrentTraits.CommanderHasEffect(Current, a, 32), "Person_trait32")) return true;
                return Score(a) < Score(b);
            }
            int Score(int p)
            {
                int id = AiTransportKnown(Current.ReadPersonIdentity(p), "Person10");
                return unchecked(owner.ReadOriginalAiCityDutyAbility(state, Current, id, 0) + owner.ReadOriginalAiCityDutyAbility(state, Current, id, 1));
            }
        }
        internal (int Troops, int Gold, int Food) PersonLimits(BaseReference source, int physical)
        {
            int id = AiTransportKnown(Current.ReadPersonIdentity(physical), "quantity_Person10");
            var limits = ReadTransportQuantities(state, Current, source, id, 0, 0, 0);
            var stock = Resources(source, false, false);
            return (Math.Min(stock.Troops, limits.MaximumTroops ?? throw AiTransportMissing("quantity_Troops")),
                Math.Min(stock.Gold, limits.MaximumGold ?? throw AiTransportMissing("quantity_Gold")),
                Math.Min(stock.Food, limits.MaximumSupplies ?? throw AiTransportMissing("quantity_Food")));
        }
        internal int BooksAfter(int count)
        {
            int before = AiTransportKnown(Current.ReadDistrictRawByte(District, 0x14), "District14");
            //0302E0 has its own local byte14 counter and subtracts exactly
            //one after each successful creation, separately from2009B0 fees.
            return Math.Max(0, unchecked(before - count));
        }
        internal int ActualBooksAfter(int count)
        {
            int remaining = AiTransportKnown(Current.ReadDistrictRawByte(District, 0x14), "District14");
            int cost = ArmyDestructionParameterSeed.ReadCurrent(state, SimulationParameterKeys.ArmyDestruction.TransportCommandCost)
                ?? throw AiTransportMissing("command_book_cost");
            for (int i = 0; i < count; i++) remaining = Math.Clamp(unchecked(remaining - cost), 0, 200);
            return remaining;
        }
        internal OriginalAiTransportCommand Command(BaseReference from, BaseReference to, int person, int troops, int gold, int food)
        {
            int id = AiTransportKnown(Current.ReadPersonIdentity(person), "command_Person10");
            int district = AiTransportKnown(Current.ReadPersonDistrictSlot(person), "command_PersonE0");
            if (from.Kind != BaseKind.City) throw AiTransportMissing("Gate_source_creation_contract");
            var force = Current.ReadPersonForce(person) ?? throw AiTransportMissing("command_Person_Force");
            if (AiTransportKnown(force.PhysicalSlot, "command_Person_Force_identity") != Force || ForceOf(from) != Force)
                throw AiTransportMissing("source_executor_Force_authority_contract");
            int naval = TransportArmyPacketSource.ReadDefaultNaval(state, Current, force) ?? throw AiTransportMissing("command_Force15E");
            return new(new(Force, AiTransportKnown(from.PhysicalSlot, "command_City"), id, troops, food, gold, naval,
                new("enter", new(DeploymentTargetKind.Location, Location(to))), [], true, 1)
                {
                    DistrictId = district,
                    GroupPurpose = new(ArmyGroupPurposeKind.TransportToFoothold,
                        new(ArmyGroupPurposeTargetKind.Foothold, Location(to)))
                }, Location(to));
        }
    }

    private static T AiTransportKnown<T>(T? value, string field) where T : struct => value ?? throw AiTransportMissing(field);
    private static InvalidDataException AiTransportMissing(string field) => new("original_ai.transport." + field);
}
