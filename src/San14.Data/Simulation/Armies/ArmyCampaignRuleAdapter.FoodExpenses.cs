using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableArmyFoodExpenseRules
{
    public PlayableArmyFoodExpenseBatch CaptureArmyFoodExpenses(GameState state)
    {
        // No SC default coefficients have been established for this profile.
        if (state.NativeArmySimulation?.Parameters.Language != "TC") return new(state, null);
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        IReadOnlyList<byte>? reductions = null;
        string? reductionFailure = null;
        var result = new Dictionary<int, PlayableArmyFoodExpense>();
        var order = new List<PlayableArmyFoodExpenseEntry>();
        var lifecycle = state.ReadArmyLifecycle() ?? throw new InvalidDataException("food_upkeep.lifecycle");
        var slots = lifecycle.ReceiverSlots();
        foreach (var pointer in ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
        {
            int receiver = pointer.PhysicalSlot ?? throw new InvalidDataException("food_upkeep.registry_army_pointer");
            if ((uint)receiver >= slots.Length) throw new InvalidDataException("food_upkeep.registry_army_receiver");
            if (slots[receiver].ActiveUnitId is not int unitId || !state.Units.TryGetValue(unitId, out var army)) continue;
            PlayableArmyFoodExpense quote;
            try
            {
                int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw new InvalidDataException("food_upkeep.army_receiver");
                bool excluded = OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical)
                    ?? throw new InvalidDataException("food_upkeep.army_exclusion");
                byte flags = army.NativeTacticRuntime?.Flags26 ?? throw new InvalidDataException("food_upkeep.army_logistics");
                if (excluded || (flags & 2) == 0) quote = new(0) { Admitted = false };
                else
                {
                    //A missing producer is also one batch result. Do not scan
                    //every facility again for each remaining Army on failure.
                    if (reductionFailure is not null) throw new InvalidDataException(reductionFailure);
                    if (reductions is null)
                        try { reductions = ArmyFoodTerrainModifiers.ReadScheduled(state); }
                        catch (InvalidDataException missing) { reductionFailure = missing.Message; throw; }
                    var home = current.FindArmyUnitCurrentBase(new(physical));
                    var collector = CurrentGateMapQueries.FindCity(home, current);
                    var payer = home.Kind == BaseKind.Gate ? CurrentGateMapQueries.FindCity(collector, current) : collector;
                    bool validPayer = current.ReadBaseNativeValidity(payer) ?? throw new InvalidDataException("food_upkeep.payer_validity");
                    quote = new(ArmyFoodExpenseSource.Read(state, current, army, reductions, out _))
                    { FoodCityId = validPayer ? payer.PhysicalSlot : null };
                }
            }
            catch (InvalidDataException missing) { quote = new(null, missing.Message); }
            order.Add(new(army.Id, quote));
            if (result.TryGetValue(army.Id, out var prior))
                result[army.Id] = prior.Amount is int before && quote.Amount is int after
                    ? quote with { Amount = unchecked(before + after) } : prior.Amount is null ? prior : quote;
            else result.Add(army.Id, quote);
        }
        foreach (int id in state.Units.Keys)
            result.TryAdd(id, new(0) { Admitted = false }); // not admitted to the actual manager roster.
        var pool = current.ReadOwnedCurrentListPool() ?? throw new InvalidDataException("food_upkeep.query_pool");
        return new(state.WithArmyLifecycle(lifecycle with { Indexes = pool.ProjectOwnedRoles() }), result)
        { OrderedExpenses = order.ToArray() };
    }
}
