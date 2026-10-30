using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record AutomaticArmyTroopDamageAmount(bool TargetValid, int? Amount, string? SkipReason);

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>176C10's actual Army recipient branch. Reuse the instruction's
    /// existing Army-origin prefix, captured before its ordered Hex vector.
    /// The caller owns immediate/deferred178EC0, captured markers, the B8 tree,
    /// shared current/pool/sampler and publication. This query draws no RNG and
    /// does not use ordinary attack/defense damage or debit any recipient.</summary>
    internal AutomaticArmyTroopDamageAmount ResolveAutomaticArmyTroopDamage(GameState state,
        UnitState caster, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction, AutomaticArmyBaseTroopDamagePrefix prefix)
        => ResolveAutomaticArmyTroopDamageCore(state, caster, target, source, instruction, prefix);

    internal AutomaticArmyTroopDamageAmount ResolveRetiredEnteredAutomaticArmyTroopDamageCurrent(GameState state,
        SourceWorldContext current, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction, AutomaticArmyBaseTroopDamagePrefix prefix)
    {
        if (!ReferenceEquals(current, _definitions)) throw ArmyTroopDamageMissing("retired_current");
        ArmyCampaignRuleAdapter.RequireRetiredAutomaticActorCurrent(state, current, prefix.SourceArmyPhysicalSlot);
        return ResolveAutomaticArmyTroopDamageCore(state, null, target, source, instruction, prefix);
    }

    private AutomaticArmyTroopDamageAmount ResolveAutomaticArmyTroopDamageCore(GameState state,
        UnitState? caster, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction, AutomaticArmyBaseTroopDamagePrefix prefix)
    {
        if (instruction.Opcode != 1 || caster is not null && prefix.CasterId != caster.Id)
            throw ArmyTroopDamageMissing("instruction_or_prefix_owner");
        int targetSlot = K(target.NativeArmyOrigin?.NativeSlotIndex, "target_Army_physical");
        var targetArmy = _definitions.ReadArmyUnitTableReference(targetSlot)
            ?? throw ArmyTroopDamageMissing("target_Army_pointer");
        if (!K(_definitions.ReadArmyUnitNativeValidity(targetArmy), "target_Army_native_validity"))
            return new(false, null, "invalid_Army");
        if (state.NativeArmySimulation?.Parameters.Language is not ("TC" or "SC"))
            throw ArmyTroopDamageMissing("numeric_source_not_qualified");
        int casterSlot = prefix.SourceArmyPhysicalSlot;
        var casterArmy = _definitions.ReadArmyUnitTableReference(casterSlot)
            ?? throw ArmyTroopDamageMissing("caster_Army_pointer");
        int targetNativePhysical = K(targetArmy.PhysicalSlot, "target_native_physical");
        bool sourceValid = K(_definitions.ReadArmyUnitNativeValidity(casterArmy), "caster_Army_validity");
        var runtime = sourceValid ? caster?.NativeTacticRuntime ?? throw ArmyTroopDamageMissing("caster_runtime") : null;
        float waiting = runtime is null || runtime.Waiting21 != 0 ? 1f : (float)P(runtime.Waiting22 == 0
            ? SimulationParameterKeys.Tactics.Waiting0SharedFactorPercent
            : SimulationParameterKeys.Tactics.Waiting1SharedFactorPercent) / 100f;
        int ability = SourceAbility();
        int baseline = P(AutomaticTacticDamageCurrentParameters.AbilityBaseline);
        int divisor = P(AutomaticTacticDamageCurrentParameters.TroopDivisor);
        if (divisor == 0) throw ArmyTroopDamageMissing("zero_troop_divisor");
        int rootOffset = P(AutomaticTacticDamageCurrentParameters.SquareRootOffset);
        //16B820 independently calls16D840, then16E820 and selector6A.
        int factorSource = SourceAbility();
        int targetDimension = Selector() == 1 ? 1 : 2;
        int factorTarget = K(_definitions.ReadArmyUnitNativeValidity(targetArmy), "factor_target_Army_validity")
            ? ArmyAbility(targetArmy, targetDimension) : 0;
        byte factorSelector = Selector();
        int factorDivisor = P(factorSelector == 1 ? SimulationParameterKeys.Tactics.StrengthDifferenceDivisor
            : SimulationParameterKeys.Tactics.IntelligenceDifferenceDivisor);
        if (factorDivisor == 0) throw ArmyTroopDamageMissing("zero_ability_factor_divisor");
        float factor = (float)unchecked(factorSource - factorTarget) / (float)factorDivisor;
        factor += 1f;
        float floor = (float)P(SimulationParameterKeys.Tactics.EffectFactorFloorPercent) / 100f;
        factor = factor > floor ? factor : floor;

        float provocation = 1f;
        if (K(_definitions.ReadArmyUnitNativeValidity(targetArmy), "provocation_target_Army_validity"))
        {
            var receiver = new RegistryObjectReference(RegistryKind.Army, targetNativePhysical);
            byte status = K(_definitions.ReadTacticClearByte(receiver, 0x25), "target_abnormal25");
            if (status != 0)
            {
                byte kind = K(_definitions.ReadTacticClearByte(receiver, 0x3C), "target_override3C");
                //The source compares captured Army kind27 and its real native
                //physical ID. A different kind does not consume word3E.
                bool same = kind == 27 && K(_definitions.ReadTacticArmyTarget3E(targetArmy),
                    "target_override3E") == prefix.SourceArmyPhysicalSlot;
                if (!same) provocation = (float)P(AutomaticTacticDamageCurrentParameters.ProvokedOtherSourcePercent) / 100f;
            }
        }
        float casterModifier = 1f;
        if (runtime is not null && runtime.StatDurations50 is not { Length: 6 }) throw ArmyTroopDamageMissing("caster_duration53");
        if (runtime is not null && runtime.StatDurations50![3] != 0)
        {
            if (runtime.StatModifiers4A is not { Length: 6 }) throw ArmyTroopDamageMissing("caster_signed_modifier4D");
            casterModifier = 1f - ((float)runtime.StatModifiers4A[3] *
                (float)P(AutomaticTacticDamageCurrentParameters.CasterModifierScale)) / 100f;
        }
        int forceSlot = prefix.SourceForceSlot <= 51 ? prefix.SourceForceSlot : 0;
        var force = _definitions.ReadForceTableReference(forceSlot) ?? throw ArmyTroopDamageMissing("caster_Force_pointer");
        float panel = 1f;
        if (K(_definitions.ReadForceNativeValidity(force), "caster_Force_validity"))
        {
            var tactic = _definitions.ReadSkillTacticReference(source.Slot) ?? throw ArmyTroopDamageMissing("Tactic_pointer");
            byte category = K(_definitions.ReadSkillTacticRawByte(tactic, 0x40), "Tactic_category40");
            if (category is 1 or 2)
                panel = (float)K(_definitions.ReadBaseForceModifier(force, category == 1 ? 32 : 33),
                    "caster_Force_panel32_33") / 100f + 1f;
        }
        byte forceFlags = state.NativeArmySimulation!.Forces.GetValueOrDefault(forceSlot)?.Flags193
            ?? throw ArmyTroopDamageMissing("caster_Force193");
        int forcePercent = (forceFlags & 16) != 0 ? P(AutomaticTacticDamageCurrentParameters.SpecialForcePercent) : 100;
        int amount = TacticArmyBaseTroopDamageMath.BeforeSettings(new(instruction.EffectValue,
            ability, baseline, prefix.StageProduct, prefix.SourceTroops, divisor, rootOffset,
            factor, casterModifier, waiting, forcePercent, panel), provocation, sourceValid ? 1f : 0f);
        //176C10 queries trait56 even for a nonpositive truncated amount;
        //ordinary16A260's separate positive-only gate does not belong here.
        if (K(_definitions.ReadArmyUnitNativeValidity(targetArmy), "half_target_Army_validity"))
        {
            var commander = Commander(targetArmy);
            if (commander.Valid && K(BattleRemakeCurrentDamageBodyQueries.ReadFirstMatchingPermanentEffect(
                _definitions, commander.Physical, 56), "target_first_permanent_effect56"))
            {
                int halfDivisor = P(SimulationParameterKeys.BattleDamage.HalfDamageDivisor);
                if (halfDivisor == 0 || amount == int.MinValue && halfDivisor == -1)
                    throw ArmyTroopDamageMissing("target_effect56_divisor");
                amount = unchecked(amount - amount / halfDivisor);
            }
        }
        int setting = K(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selectorE58");
        if (setting == 0)
        {
            if (K(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "settings_advanced"))
                amount = TacticArmyBaseTroopDamageMath.Advanced(amount,
                    K(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 2), "settings_advanced_damage2"));
        }
        else if (amount > 0 && K(BattleRemakeCurrentSettings.ReadInt32(state, 0xB8), "settings_tactic_damage_B8") == 0)
            amount = TacticArmyBaseTroopDamageMath.OrdinaryReduced(amount,
                P(AutomaticTacticDamageCurrentParameters.OrdinaryReductionPercent));
        amount = Math.Min(amount, P(SimulationParameterKeys.BattleDamage.DamageCap));
        return new(true, amount, null);

        byte Selector() => FactorSelector(source) ?? throw ArmyTroopDamageMissing("current_selector6A");
        int SourceAbility()
        {
            if (!K(_definitions.ReadArmyUnitNativeValidity(casterArmy), "ability_source_Army_validity")) return 0;
            return ArmyAbility(casterArmy, Selector() == 1 ? 1 : 2);
        }
        int ArmyAbility(ArmyUnitReference army, int dimension)
        {
            var commander = Commander(army);
            //22E390 returns literal0 for a real invalid commander.
            if (!commander.Valid) return 0;
            int personId = K(_definitions.ReadPersonIdentity(commander.Physical), "ability_Person10");
            int? value = _abilities is TacticRemakeCurrentAbilities owned
                ? owned.ResolveDimension(state, personId, dimension)
                : dimension == 1 ? _abilities?.Resolve(state, personId)?.Strength
                    : _abilities?.Resolve(state, personId)?.Intelligence;
            return value ?? throw ArmyTroopDamageMissing("current_ability_dimension" + dimension);
        }
        (int Physical, bool Valid) Commander(ArmyUnitReference army)
        {
            ushort raw = K(_definitions.ReadArmyUnitCommanderId(army), "current_Army_Commander12");
            var person = raw <= 6000 ? _definitions.ReadPersonLookupReference(raw)
                : _definitions.ReadTransferPersonTableReference(0);
            int physical = K(person?.PhysicalSlot, "current_Commander_pointer");
            return (physical, K(_definitions.ReadPersonNativeValidity(physical), "current_Commander_validity"));
        }
        int P(string key)
        {
            return AutomaticTacticDamageCurrentParameters.ReadCurrent(state, key)
                ?? throw ArmyTroopDamageMissing(key);
        }
        T K<T>(T? value, string owner) where T : struct => value ?? throw ArmyTroopDamageMissing(owner);
    }
    private static InvalidDataException ArmyTroopDamageMissing(string owner) => new("automatic_Army_troop_damage." + owner);
}
