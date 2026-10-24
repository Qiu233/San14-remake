using San14.Core;

namespace San14.Data;

internal sealed record BaseOriginNormalDamageReceipt(
    BattleRemakeCurrentDamageReceipt Damage,
    BattleRemakeCurrentBaseAttributeReceipt CityAttributes,
    BattleRemakeCurrentFieldDefenseReceipt ArmyAttributes,
    BattleRemakeCurrentAttackingCityReceipt? CityModifiers);
internal sealed record BaseOriginNormalDamageResult(
    BaseOriginNormalDamageReceipt? Receipt, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Base-origin ordinary stage5 over the caller's same current.
    /// Both divisors are final descriptor/cache/effect55 results from the real
    /// mixed phase. This entry never rescans Army-only candidates, constructs
    /// an Army for the City, copies the world, or fills missing parameters.
    /// City modifier queries retain their reached current/pool prefix, so an
    /// unresolved result must discard the enclosing unpublished operation.</summary>
    internal BaseOriginNormalDamageResult ResolveBorrowedBaseOriginDamage(
        GameState state, SourceWorldContext current, BaseReference origin,
        int targetArmyId, byte? baseFinalDivisor14, byte? targetFinalDivisor14,
        NativeArmyCombatCycleSampler? sampler = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(current);
        bool ownedOrigin = origin.Kind == BaseKind.City && origin.PhysicalSlot is >= 1 and <= 51 &&
                state.Cities.ContainsKey(origin.PhysicalSlot.Value) ||
            origin.Kind == BaseKind.Gate && origin.PhysicalSlot is >= 1 and <= 10 &&
                state.Gates.ContainsKey(origin.PhysicalSlot.Value + 51);
        if (!ownedOrigin || current.ReadBaseNativeValidity(origin) != true)
            return Missing("normal.Base_origin.actual_Base_receiver");
        if (baseFinalDivisor14 is not > 0 || targetFinalDivisor14 is not > 0)
            return Missing("normal.Base_origin.final_mixed_descriptor14");
        if (!state.Units.TryGetValue(targetArmyId, out var target) ||
            target.NativeTacticRuntime is not { } native ||
            target.Troops is < 0 or > ushort.MaxValue)
            return Missing("normal.Base_origin.actual_target_Army");

        var abilities = new TacticRemakeCurrentAbilities(current, _scenario,
            Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        var context = new TacticRemakeCurrentContext(current, _catalog, _map,
            _terrain, _usage, Revision, abilities, operationSampler: sampler);
        if (context.ArmyValid(state, target) != true)
            return Missing("normal.Base_origin.target_Army_validity");
        var region = new BattleRemakeCurrentRegionFactor(_regionSeed, context, current, Revision);
        var city = new BattleRemakeCurrentBaseAttributes(_battleSeed, current,
            abilities, region, Revision).Resolve(state, origin, baseFinalDivisor14.Value);
        if (city is null) return Missing("normal.Base_origin.Base_attributes");
        var queries = new TacticRemakeCurrentBattleQueries(context, current, Revision,
            new TacticRemakeCurrentBattleFactors(context, current, _regionSeed, Revision));
        var reached = queries.ReadCommonQueriesForDescriptor(state, target, targetFinalDivisor14.Value);
        if (reached is null) return Missing("normal.Base_origin.target_Common");
        var attributes = new BattleRemakeCurrentArmyAttributes(_battleSeed, current,
            abilities, queries, Revision);
        var army = attributes.ResolveFieldDefenseForDescriptor(state, target, reached);
        if (army is null) return Missing("normal.Base_origin.target_field_attributes");
        var slots = new BattleRemakeCurrentDamageAttributes(city.Common,
            city.AttackAgainstArmy, city.Defense, army.Common, army.Attack, army.Defense);
        slots.Validate();
        var normalQueries = new BattleRemakeCurrentArmyDamageQueries(attributes, queries, Revision);
        var parameters = new BattleRemakeCurrentDamage(_battleSeed.DamageSource,
            normalQueries, Revision).CurrentParameters(state);
        if (parameters is null) return Missing("normal.Base_origin.current_damage_parameters");

        //16A260 admits the attacking-City policy/person/advantage branch
        //only when the actual Base4E is below52. A Gate's52..61 link skips
        //that whole source branch; null here means that reached absence,
        //never an unresolved City modifier result or a zero-filled input.
        BattleRemakeCurrentAttackingCityReceipt? modifier = null;
        if (city.ObjectLink4E < 52)
        {
            var modifiers = BattleRemakeCurrentAttackingCityModifiers.Resolve(state, current, origin, Revision);
            if (modifiers.Receipt is not { } cityModifier)
                return Missing(modifiers.UnresolvedOwner ?? "normal.Base_origin.City_modifiers");
            modifier = cityModifier;
        }
        var strike = new TroopDamageSourceInput(city.ReadyTroops3C, target.Troops,
            city.AttackAgainstArmy, army.Defense, native.Status10 == 2, modifier?.Input, false);
        //16A4E3 reaches the commander effect only after City modifiers and
        //only for a positive pre-cap amount. The private parameter view changes
        //neither the saved globals nor the actual final signed cap.
        var uncapped = parameters.WithCurrentValues(parameters.CurrentValues with { DamageCap = int.MaxValue },
            $"{Revision}:Base-origin-pre-half");
        int gross = BattleDamageSource.CalculateTroopDamage(uncapped, strike);
        int amount;
        if (gross > 0)
        {
            if (queries.ReadTargetHalfDamageTrait(state, target) is not bool half)
                return Missing("normal.Base_origin.target_effect56");
            amount = BattleDamageSource.CalculateTroopDamage(parameters,
                strike with { TargetHalfDamageTrait = half });
        }
        else amount = Math.Min(gross, parameters.DamageCap);
        var damage = new BattleRemakeCurrentDamageReceipt(amount, state.RandomState, slots,
            $"{Revision}:{parameters.CurrentRevision}:{modifier?.Revision ?? "no-attacking-City-branch"}:Base-origin");
        return new(new(damage, city, army, modifier), null);

        static BaseOriginNormalDamageResult Missing(string owner) => new(null, owner);
    }
}
