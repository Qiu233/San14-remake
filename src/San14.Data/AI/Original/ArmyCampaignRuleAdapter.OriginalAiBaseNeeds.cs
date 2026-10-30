using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IAiStrategyMemoryExecution
{
    public CommandResult EstimateFoodNeeds(GameState state, ForceState force,
        EstimateAiFoodNeedsOrder order, CommandContext context)
    {
        try
        {
            var memory = state.OriginalAiStrategyMemory ?? throw AiBaseNeedsMissing("saved_memory");
            var inputs = new AiBaseNeedsOperation(this, state, force.Id);
            var rows = memory.Footholds.ToBuilder();
            foreach (var at in inputs.OwnedBases)
            {
                int location = inputs.Location(at);
                if (!rows.TryGetValue(location, out var row)) throw AiBaseNeedsMissing("foothold_" + location);
                if (location >= 62)
                {
                    //09E160 has an explicit early zero for unsupported Base
                    //categories. This is not a fallback for an unknown input.
                    rows[location] = row with { FoodNeed = 0 };
                    continue;
                }
                int forecast = row.FoodForecast ?? throw AiBaseNeedsMissing("food_forecast_" + location);
                var incoming = inputs.Incoming(at, food: true);
                var values = new OriginalAiFoodNeedInputs(inputs.UsableTroops(at), incoming.Troops,
                    forecast, inputs.CurrentFood(at), incoming.Cargo);
                rows[location] = row with { FoodNeed = OriginalAiBaseNeedsMath.CalculateFood(values,
                    order.TroopConsumptionPercent, order.SupplyHorizon, order.IncomeHorizon) };
            }
            return CommandResult.Accept(state with { OriginalAiStrategyMemory = memory with { Footholds = rows.ToImmutable() } });
        }
        catch (InvalidDataException error)
        { return CommandResult.RuleUnavailable(error.Message, "Current food-needs inputs are unavailable."); }
    }

    public CommandResult EstimateGoldNeeds(GameState state, ForceState force,
        EstimateAiGoldNeedsOrder order, CommandContext context)
    {
        try
        {
            var memory = state.OriginalAiStrategyMemory ?? throw AiBaseNeedsMissing("saved_memory");
            var inputs = new AiBaseNeedsOperation(this, state, force.Id);
            var rows = memory.Footholds.ToBuilder();
            foreach (var at in inputs.OwnedBases)
            {
                int location = inputs.Location(at);
                if (!rows.TryGetValue(location, out var row)) throw AiBaseNeedsMissing("foothold_" + location);
                if (location >= 62) throw AiBaseNeedsMissing("gold_Base_category");
                int forecast = row.GoldForecast ?? throw AiBaseNeedsMissing("gold_forecast_" + location);
                var incoming = inputs.Incoming(at, food: false);
                var values = new OriginalAiGoldNeedInputs(forecast, inputs.CurrentGold(at), incoming.Cargo);
                rows[location] = row with { GoldNeed = OriginalAiBaseNeedsMath.CalculateGold(values, order.IncomeHorizon) };
            }
            return CommandResult.Accept(state with { OriginalAiStrategyMemory = memory with { Footholds = rows.ToImmutable() } });
        }
        catch (InvalidDataException error)
        { return CommandResult.RuleUnavailable(error.Message, "Current gold-needs inputs are unavailable."); }
    }

    /// <summary>One actual estimate command's read view. Ordered registrations
    /// are retained; no information query refreshes the saved planning owner.</summary>
    private sealed class AiBaseNeedsOperation
    {
        private readonly ArmyCampaignRuleAdapter adapter;
        private readonly GameState state;
        private readonly Dictionary<BaseReference, int> usable = [];
        private readonly List<int> armyOrder = [];
        internal SourceWorldContext Current { get; }
        internal IReadOnlyList<BaseReference> OwnedBases { get; }

        internal AiBaseNeedsOperation(ArmyCampaignRuleAdapter adapter, GameState state, int forceId,
            SourceWorldContext? operationCurrent = null)
        {
            if (adapter.Seed.Language != "TC") throw AiBaseNeedsMissing("SC_source_contract");
            this.adapter = adapter; this.state = state;
            Current = operationCurrent ?? adapter.CaptureOwnedArmyReturnWorld(state).Context;
            var bases = new List<BaseReference>();
            foreach (var entry in Current.ReadRegisteredBases() ?? throw AiBaseNeedsMissing("World80"))
            {
                var at = entry ?? throw AiBaseNeedsMissing("World80_pointer");
                // The Force Foothold iterator has no additional validity,
                // treasury, district or deployment-eligibility filter.
                if (forceId is >= 1 and <= 51 && AiBaseNeedsKnown(Current.ReadBaseForceSlot(at), "Base_Force") == forceId)
                    bases.Add(at);
            }
            OwnedBases = bases;
            // The source does not read the Army iterator when there are no
            // owned bases. Do not demand an unrelated owner in that case.
            if (bases.Count == 0) return;
            foreach (var entry in Current.ReadRegistryReferences(RegistryKind.Army) ?? throw AiBaseNeedsMissing("World_Army"))
            {
                if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical })
                    throw AiBaseNeedsMissing("World_Army_pointer");
                int owner = AiBaseNeedsKnown(Current.ReadForcePhysicalSlot(Current.ReadArmyUnitForceReference(new(physical))), "Army_Force");
                if (owner != forceId) continue;
                //334740's ForceArmy iterator filters233560 after the
                //Force comparison; the underlying World60 chain is unfiltered.
                if (AiBaseNeedsKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, Current, physical), "Army233560")) continue;
                armyOrder.Add(physical);
            }
        }

        internal int Location(BaseReference at) => AiBaseNeedsKnown(Current.ReadBaseObjectLink(at), "Base4E");

        internal int CurrentFood(BaseReference at)
        {
            var city = CurrentGateMapQueries.FindCity(at, Current);
            return AiBaseNeedsKnown(Current.ReadBaseResource(city, BaseResourceKind.Supplies), "City_virtualA0_food");
        }

        internal int CurrentGold(BaseReference at)
        {
            //09E3C0 explicitly resolves the City's22A940 receiver; Gate's
            //virtual90 (22EB10) resolves the same current City before reading.
            var city = CurrentGateMapQueries.FindCity(at, Current);
            return AiBaseNeedsKnown(Current.ReadBaseResource(city, BaseResourceKind.Gold), "City_virtual90_gold");
        }

        internal int UsableTroops(BaseReference at)
        {
            int location = Location(at);
            if (location is >= 62) return 0; // Explicit09E160 unsupported-Base branch.
            int total = SingleUsable(at);
            if (location >= 52) return total;
            var city = CurrentGateMapQueries.FindCity(at, Current);
            int physicalCity = AiBaseNeedsKnown(city.PhysicalSlot, "related_City");
            if (physicalCity is < 1 or > 51) return total; //334500 iterator predicate.
            // CCityAreaIterator follows World+B0. A gate-capital Hex17 is
            // resolved through its actual Hex1A, with the source lookup's
            // normal head fallback. Duplicate entries are not collapsed.
            foreach (var entry in Current.ReadRegisteredAreaReferences() ?? throw AiBaseNeedsMissing("WorldB0"))
            {
                var area = entry ?? throw AiBaseNeedsMissing("WorldB0_pointer");
                if (AiBaseNeedsKnown(Current.ReadAreaCitySlot(area), "Area35") != physicalCity) continue;
                ushort rawHex = AiBaseNeedsKnown(Current.ReadAreaHexIndex(area), "Area3A");
                var hex = Current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw AiBaseNeedsMissing("Area_Hex_pointer");
                if (AiBaseNeedsKnown(Current.ReadHexSurfaceCode(hex), "Hex10") != 17) continue;
                int link = AiBaseNeedsKnown(Current.ReadHexObjectLink(hex), "Hex1A");
                var gate = BaseLookup.Find(link < 62 ? link : 0, Current);
                total = unchecked(total + SingleUsable(gate));
            }
            return total;
        }

        private int SingleUsable(BaseReference at)
        {
            if (usable.TryGetValue(at, out int value)) return value;
            var operation = new AutomaticDraftOperation(adapter, state, Location(at), Current);
            value = operation.ReadUsableTroops(operation.Candidates(),
                adapter.GeneralTargetParameter(state, "adjacent-enemy-reserve-troops", 5000));
            usable.Add(at, value);
            return value;
        }

        internal (int Troops, int Cargo) Incoming(BaseReference at, bool food)
        {
            int location = Location(at), troops = 0, cargo = 0;
            var arena = state.ReadArmyLifecycle() ?? throw AiBaseNeedsMissing("Army_receivers");
            var slots = arena.ReceiverSlots();
            foreach (int physical in armyOrder)
            {
                if ((uint)physical >= (uint)slots.Count) throw AiBaseNeedsMissing("Army_receiver");
                var slot = slots[physical];
                if (AiBaseNeedsKnown(slot.Status10, "Army10") != 2) continue;
                var target = OwnedArmyLifecycleTargets.Read(state, slot.ReceiverId) ?? throw AiBaseNeedsMissing("Army_target");
                int destination = OriginalAiBaseNeedsMath.TransportTargetLocation(
                    AiBaseNeedsKnown(target.Kind38, "Army38"), AiBaseNeedsKnown(target.Identity3A, "Army3A"));
                if (destination != location) continue;
                if (food)
                {
                    var image = slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id)
                        ?? throw AiBaseNeedsMissing("active_Army") : slot.PendingImage ?? throw AiBaseNeedsMissing("retained_Army");
                    if (image.Troops is < 0 or > ushort.MaxValue) throw AiBaseNeedsMissing("Army16_range");
                    troops = unchecked(troops + image.Troops);
                }
                // Read only the requested source cargo field. Missing gold
                // must not block an otherwise known food estimate or vice versa.
                int amount = food ? AiBaseNeedsKnown(slot.StoredResources.Food, "Army60_food")
                    : AiBaseNeedsKnown(slot.StoredResources.Gold, "Army5C_gold");
                cargo = unchecked(cargo + amount);
            }
            return (troops, cargo);
        }
    }

    private static T AiBaseNeedsKnown<T>(T? value, string field) where T : struct => value ?? throw AiBaseNeedsMissing(field);
    private static InvalidDataException AiBaseNeedsMissing(string field) => new("original_ai.base_needs." + field);
}
