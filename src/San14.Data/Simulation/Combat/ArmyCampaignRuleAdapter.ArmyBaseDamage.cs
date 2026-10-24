using San14.Core;

namespace San14.Data;

/// <summary>The four real stage2..4 values consumed by Army-to-Base16A260.
/// This is not a six-slot frame filled with invented unused values.</summary>
internal sealed record ArmyBaseNormalDamageAttributes(float ArmyCommon,
    float ArmySiegeTroopAttack, float CityCommon, float CityDefense);
internal sealed record ArmyBaseNormalDamageReceipt(
    BattleRemakeCurrentDamageReceipt Damage,
    BattleRemakeCurrentArmySiegeReceipt ArmyAttributes,
    BattleRemakeCurrentBaseAttributeReceipt CityAttributes,
    ArmyBaseNormalDamageAttributes CurveAttributes,
    bool CurvePermitted, bool AttackerNotPlayer, bool TargetSpecialGovernor);
internal sealed record ArmyBaseNormalDamageResult(
    ArmyBaseNormalDamageReceipt? Receipt, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Army-origin/Base-target ordinary troop damage over the caller's
    /// current and cycle sampler. Both divisor bytes are the actual final mixed
    /// descriptor/cache/effect55 outputs, never an Army-only rescan or default.
    /// No wall application, reward, queue, world publication or RNG reseed occurs.
    /// Caller must discard its unpublished operation when a reader is unresolved.</summary>
    internal ArmyBaseNormalDamageResult ResolveBorrowedArmyBaseDamage(
        GameState state, SourceWorldContext current, int attackerArmyId,
        BaseReference target, byte? armyFinalDivisor14, byte? baseFinalDivisor14,
        NativeArmyCombatCycleSampler? sampler = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(target);
        try
        {
            bool ownedTarget = target.Kind == BaseKind.City && target.PhysicalSlot is >= 1 and <= 51 &&
                    state.Cities.ContainsKey(target.PhysicalSlot.Value) ||
                target.Kind == BaseKind.Gate && target.PhysicalSlot is >= 1 and <= 10 &&
                    state.Gates.ContainsKey(target.PhysicalSlot.Value + 51);
            if (!ownedTarget || current.ReadBaseNativeValidity(target) != true)
                return Missing("actual_target_Base_receiver");
            if (armyFinalDivisor14 is not > 0 || baseFinalDivisor14 is not > 0)
                return Missing("final_mixed_descriptor14");
            if (!state.Units.TryGetValue(attackerArmyId, out var attacker) ||
                attacker.NativeArmyOrigin?.NativeSlotIndex is not >= 1 or > 500 ||
                attacker.NativeTacticRuntime is not { } native ||
                attacker.Troops is < 0 or > ushort.MaxValue)
                return Missing("actual_attacker_Army");
            if (native.Status10 != 1) return Missing("ordinary_type1_siege_attributes");
            if (native.Abnormal25 != 0) return Missing("Army_provocation_stage3_4");

            var abilities = new TacticRemakeCurrentAbilities(current, _scenario,
                Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            var context = new TacticRemakeCurrentContext(current, _catalog, _map,
                _terrain, _usage, Revision, abilities, operationSampler: sampler);
            if (context.ArmyValid(state, attacker) != true) return Missing("attacker_Army_validity");
            var queries = new TacticRemakeCurrentBattleQueries(context, current, Revision,
                new TacticRemakeCurrentBattleFactors(context, current, _regionSeed, Revision));
            var common = queries.ReadCommonQueriesForDescriptor(state, attacker, armyFinalDivisor14.Value);
            if (common is null) return Missing("attacker_Common");
            var attributeQueries = new BattleRemakeCurrentArmyAttributes(_battleSeed, current,
                abilities, queries, Revision);
            var army = attributeQueries.ResolveSiegeOffense(state, attacker, common);
            if (army is null) return Missing("attacker_siege_attributes");
            var region = new BattleRemakeCurrentRegionFactor(_regionSeed, context, current, Revision);
            var city = new BattleRemakeCurrentBaseAttributes(_battleSeed, current,
                abilities, region, Revision).Resolve(state, target, baseFinalDivisor14.Value);
            if (city is null) return Missing("target_Base_attributes");
            var attributes = new ArmyBaseNormalDamageAttributes(army.Common,
                army.SiegeTroopAttack, city.Common, city.Defense);
            if (!float.IsFinite(attributes.ArmyCommon) || !float.IsFinite(attributes.ArmySiegeTroopAttack) ||
                !float.IsFinite(attributes.CityCommon) || !float.IsFinite(attributes.CityDefense))
                return Missing("finite_current_attributes");

            //16A260 first reads current-player validity. It compares v60 with
            //Army22CD80 only when that player is valid; no Force validity gate
            //is inserted for the attacking Army's returned raw District10.
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            if (player is null) return Missing("current_player_Force_pointer");
            if (current.ReadForceNativeValidity(player) is not bool playerValid)
                return Missing("current_player_Force_validity");
            bool attackerNotPlayer = true;
            if (playerValid)
            {
                if (current.ReadForcePhysicalSlot(player) is not int playerIdentity)
                    return Missing("current_player_Force_identity");
                byte actorForce = current.ReadArmyUnitRawForceSlot(new(attacker.NativeArmyOrigin.NativeSlotIndex));
                attackerNotPlayer = actorForce != playerIdentity;
            }
            //16A260 reaches232950 only for target kind5, including a player
            //attacker. Kind6 keeps the source special-governor selector false;
            //it must not query a City-only property on the Gate receiver.
            bool special = false;
            if (target.Kind == BaseKind.City)
            {
                if (current.ReadConditionBaseSpecialGovernor(target) is not bool citySpecial)
                    return Missing("target_City_special_Governor232950");
                special = citySpecial;
            }
            bool permit = !attackerNotPlayer || !special;
            if (!permit)
                return Done(new(0, state.RandomState, null,
                    $"{Revision}:Army-City-special-player-curve-skip"), false);

            if (city.ReadyTroops3C < 0) return Missing("target_Base_signed_negative_troops3C");
            var normalQueries = new BattleRemakeCurrentArmyDamageQueries(attributeQueries, queries, Revision);
            var parameters = new BattleRemakeCurrentDamage(_battleSeed.DamageSource,
                normalQueries, Revision).CurrentParameters(state);
            if (parameters is null) return Missing("current_damage_parameters");
            //Actor kind27 leaves the attacking-Base pointer null. Target5/6
            //leaves the target-Army pointer null. Therefore the City-origin
            //modifier and target-Army effect56 branches are actual source skips.
            var strike = new TroopDamageSourceInput(attacker.Troops, city.ReadyTroops3C,
                army.SiegeTroopAttack, city.Defense, false, null, false);
            int amount = BattleDamageSource.CalculateTroopDamage(parameters, strike);
            return Done(new(amount, state.RandomState, null,
                $"{Revision}:{parameters.CurrentRevision}:Army-{target.Kind}"), true);

            ArmyBaseNormalDamageResult Done(BattleRemakeCurrentDamageReceipt damage, bool curve) =>
                new(new(damage, army, city, attributes, curve, attackerNotPlayer, special), null);
        }
        catch (InvalidDataException error) { return Missing("reader:" + error.Message); }

        static ArmyBaseNormalDamageResult Missing(string owner) => new(null, "normal.Army_Base." + owner);
    }
}
