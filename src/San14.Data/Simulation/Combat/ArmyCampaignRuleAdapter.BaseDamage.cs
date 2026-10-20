using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState InstallFreshBaseDamageTiers(GameState state)
    {
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Cities.Values.Any(city => city.DamageTier is not null) || state.Gates.Values.Any(gate => gate.DamageTier is not null))
            throw new InvalidDataException("Base damage tiers require their fresh post-load boundary.");
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        //City/Gate slot7 precedes the Person policy-cost clamp. Reuse the
        //already source-verified opening contribution calculation, rather
        //than treating the later current policy snapshot as this earlier phase.
        var openingEffects = CityOpeningPostLoadSource.ReadSourceCreatedForceEffects32(_scenario);
        var currentEffects = current.BaseForcePolicyEffectReader;
        current.BaseForcePolicyEffectReader = (force, effect) => effect == 32
            ? force.PhysicalSlot is int physical && physical is >= 0 and < 52 ? openingEffects[physical] : null
            : currentEffects?.Invoke(force, effect);
        var cities = new Dictionary<int, CityState>(state.Cities);
        var gates = new Dictionary<int, GateState>(state.Gates);
        foreach (var city in state.Cities.Values)
        {
            var target = new BaseReference(BaseKind.City, city.Id);
            var result = Initialize(target);
            cities[city.Id] = city with { Durability = result.Wall, DamageTier = result.Tier };
        }
        foreach (var gate in state.Gates.Values)
        {
            var target = new BaseReference(BaseKind.Gate, gate.Id - 51);
            var result = Initialize(target);
            gates[gate.Id] = gate with { Durability = result.Wall, DamageTier = result.Tier };
        }
        return state with { Cities = cities, Gates = gates };

        (int? Wall, byte Tier) Initialize(BaseReference target)
        {
            if (!NormalCombatMixedProducer.Need(current.ReadBaseNativeValidity(target), "fresh_Base_validity"))
                return (target.Kind == BaseKind.City ? cities[target.PhysicalSlot!.Value].Durability : gates[target.PhysicalSlot!.Value + 51].Durability, 0);
            //2250E0/225450: the existing linked wall setter precedes the
            //damage-band writer. No live getter repeats this fresh operation.
            BasePostLoad.WriteLinkedField14(target, BasePostLoad.ReadLinkedField14(target, current), current);
            return (BasePostLoad.ReadLinkedField14(target, current), BasePostLoad.ReadFlag66(target, current));
        }
    }
}
