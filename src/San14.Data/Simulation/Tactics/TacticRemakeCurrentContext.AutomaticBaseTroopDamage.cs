using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record AutomaticArmyBaseTroopDamagePrefix(int CasterId, int SourceArmyPhysicalSlot, ushort SourceTroops,
    byte SourceForceSlot, int StageProduct);
internal sealed record AutomaticArmyBaseTroopDamageAmount(bool TargetValid,
    bool TargetPermitted, int? Amount, string? SkipReason);

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>Capture the Army-origin176C10 wrapper once, before walking
    /// this instruction's ordered target vector. No stock writer, tree update or RNG draw.</summary>
    internal AutomaticArmyBaseTroopDamagePrefix BeginAutomaticArmyBaseTroopDamage(
        GameState state, UnitState caster, int stageOrdinal)
    {
        if (!Bound(state) || ArmyValid(state, caster) != true ||
            caster.NativeArmyOrigin?.NativeSlotIndex is not int physical)
            throw BaseTroopDamageMissing("current_Army_caster");
        if (state.NativeArmySimulation!.Parameters.Language is not ("TC" or "SC"))
            throw BaseTroopDamageMissing("numeric_source_not_qualified");
        var army = _definitions.ReadArmyUnitTableReference(physical)
            ?? throw BaseTroopDamageMissing("caster_pointer");
        int nativePhysical = army.PhysicalSlot ?? throw BaseTroopDamageMissing("caster_native22DB70");
        ushort troops = _definitions.ReadArmyReturnResourceFields(army)?.Ready16
            ?? throw BaseTroopDamageMissing("caster_ready16");
        byte force = _definitions.ReadArmyUnitRawForceSlot(army);
        int stage = BaseTroopDamageParameter(state, SimulationParameterKeys.Tactics.EffectStageScale);
        return new(caster.Id, nativePhysical, troops, force, unchecked(stageOrdinal * stage));
    }

    /// <summary>176C10's actual Base numeric branch. The caller re-resolves
    /// each Hex's real receiver and owns defer/direct application, Base tree,
    /// markers and the complete shared current/pool/sampler. This returns a
    /// signed requested amount, never a claimed debit or a fake Army target.</summary>
    internal AutomaticArmyBaseTroopDamageAmount ResolveAutomaticArmyBaseTroopDamage(
        GameState state, UnitState caster, BaseReference target, TacticSourceRow source,
        S14TacticEffectInstruction instruction, AutomaticArmyBaseTroopDamagePrefix prefix)
        => ResolveAutomaticArmyBaseTroopDamageCore(state, caster, target, source, instruction, prefix);

    internal AutomaticArmyBaseTroopDamageAmount ResolveRetiredEnteredAutomaticBaseTroopDamageCurrent(GameState state,
        SourceWorldContext current, BaseReference target, TacticSourceRow source,
        S14TacticEffectInstruction instruction, AutomaticArmyBaseTroopDamagePrefix prefix)
    {
        if (!ReferenceEquals(current, _definitions)) throw BaseTroopDamageMissing("retired_current");
        ArmyCampaignRuleAdapter.RequireRetiredAutomaticActorCurrent(state, current, prefix.SourceArmyPhysicalSlot);
        return ResolveAutomaticArmyBaseTroopDamageCore(state, null, target, source, instruction, prefix);
    }

    private AutomaticArmyBaseTroopDamageAmount ResolveAutomaticArmyBaseTroopDamageCore(GameState state,
        UnitState? caster, BaseReference target, TacticSourceRow source,
        S14TacticEffectInstruction instruction, AutomaticArmyBaseTroopDamagePrefix prefix)
    {
        if (instruction.Opcode != 1 || caster is not null && prefix.CasterId != caster.Id)
            throw BaseTroopDamageMissing("instruction_or_prefix_owner");
        if (!Known(_definitions.ReadBaseNativeValidity(target), "target_Base_validity"))
            return new(false, false, null, "invalid_Base");
        bool special = Known(_definitions.ReadConditionBaseSpecialGovernor(target), "target_special_Governor232950");
        if (special && Known(_definitions.ReadBaseObjectLink(target), "target_Base4E") <= 51)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, _definitions)
                ?? throw BaseTroopDamageMissing("current_player_Force_pointer");
            if (!Known(_definitions.ReadForceNativeValidity(player), "current_player_Force_validity"))
                return new(true, false, null, "special_City_invalid_player");
            if (prefix.SourceForceSlot != Known(_definitions.ReadForcePhysicalSlot(player), "current_player_Force_identity"))
                return new(true, false, null, "special_City_nonplayer_caster");
        }
        var casterArmy = _definitions.ReadArmyUnitTableReference(prefix.SourceArmyPhysicalSlot)
            ?? throw BaseTroopDamageMissing("caster_Army_pointer");
        bool sourceValid = Known(_definitions.ReadArmyUnitNativeValidity(casterArmy), "caster_Army_validity");
        var runtime = sourceValid ? caster?.NativeTacticRuntime ?? throw BaseTroopDamageMissing("caster_runtime") : null;
        float waiting = runtime is null || runtime.Waiting21 != 0 ? 1f : (float)P(runtime.Waiting22 == 0
            ? SimulationParameterKeys.Tactics.Waiting0SharedFactorPercent
            : SimulationParameterKeys.Tactics.Waiting1SharedFactorPercent) / 100f;
        int ability = SourceAbility();
        int baseline = P(AutomaticTacticDamageCurrentParameters.AbilityBaseline);
        int divisor = P(AutomaticTacticDamageCurrentParameters.TroopDivisor);
        if (divisor == 0) throw BaseTroopDamageMissing("zero_troop_divisor");
        int rootOffset = P(AutomaticTacticDamageCurrentParameters.SquareRootOffset);
        //16B820 independently reads16D840,16E820,then selector6A again.
        int factorSource = SourceAbility();
        byte targetSelector = Selector();
        ushort rawGovernor = Known(_definitions.ReadArmyReturnBaseGovernor2E(target), "target_Governor2E");
        var governor = rawGovernor <= 6000 ? _definitions.ReadPersonLookupReference(rawGovernor)
            : _definitions.ReadTransferPersonTableReference(0);
        int governorPhysical = Known(governor?.PhysicalSlot, "target_Governor_pointer");
        bool governorValid = Known(_definitions.ReadPersonNativeValidity(governorPhysical), "target_Governor_validity");
        //16E820 returns literal50 only for a real invalid governor.
        int factorTarget = governorValid ? Ability(Known(_definitions.ReadPersonIdentity(governorPhysical),
            "target_Governor_Person10"), targetSelector == 1 ? 1 : 2) : 50;
        byte factorSelector = Selector();
        int factorDivisor = P(factorSelector == 1 ? SimulationParameterKeys.Tactics.StrengthDifferenceDivisor
            : SimulationParameterKeys.Tactics.IntelligenceDifferenceDivisor);
        if (factorDivisor == 0) throw BaseTroopDamageMissing("zero_ability_factor_divisor");
        float factor = (float)unchecked(factorSource - factorTarget) / (float)factorDivisor;
        factor += 1f;
        factor = MathF.Max(factor, (float)P(SimulationParameterKeys.Tactics.EffectFactorFloorPercent) / 100f);
        float casterModifier = 1f;
        if (runtime is not null && runtime.StatDurations50 is not { Length: 6 }) throw BaseTroopDamageMissing("caster_duration53");
        if (runtime is not null && runtime.StatDurations50![3] != 0)
        {
            if (runtime.StatModifiers4A is not { Length: 6 }) throw BaseTroopDamageMissing("caster_signed_modifier4D");
            casterModifier = 1f - ((float)runtime.StatModifiers4A[3] *
                (float)P(AutomaticTacticDamageCurrentParameters.CasterModifierScale)) / 100f;
        }
        int forceSlot = prefix.SourceForceSlot <= 51 ? prefix.SourceForceSlot : 0;
        var force = _definitions.ReadForceTableReference(forceSlot) ?? throw BaseTroopDamageMissing("caster_Force_pointer");
        float panel = 1f;
        if (Known(_definitions.ReadForceNativeValidity(force), "caster_Force_validity"))
        {
            var tactic = _definitions.ReadSkillTacticReference(source.Slot) ?? throw BaseTroopDamageMissing("Tactic_pointer");
            byte category = Known(_definitions.ReadSkillTacticRawByte(tactic, 0x40), "Tactic_category40");
            if (category is 1 or 2)
                panel = (float)Known(_definitions.ReadBaseForceModifier(force, category == 1 ? 32 : 33), "caster_Force_panel32_33") / 100f + 1f;
        }
        byte forceFlags = state.NativeArmySimulation!.Forces.GetValueOrDefault(forceSlot)?.Flags193
            ?? throw BaseTroopDamageMissing("caster_Force193");
        int forcePercent = (forceFlags & 16) != 0 ? P(AutomaticTacticDamageCurrentParameters.SpecialForcePercent) : 100;
        int amount = TacticArmyBaseTroopDamageMath.BeforeSettings(new(instruction.EffectValue,
            ability, baseline, prefix.StageProduct, prefix.SourceTroops, divisor, rootOffset,
            factor, casterModifier, waiting, forcePercent, panel), 1f, sourceValid ? 1f : 0f);
        int setting = Known(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selectorE58");
        if (setting == 0)
        {
            if (Known(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "settings_advanced"))
                amount = TacticArmyBaseTroopDamageMath.Advanced(amount,
                    Known(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 2), "settings_advanced_damage2"));
        }
        else if (amount > 0 && Known(BattleRemakeCurrentSettings.ReadInt32(state, 0xB8), "settings_tactic_damage_B8") == 0)
            amount = TacticArmyBaseTroopDamageMath.OrdinaryReduced(amount,
                P(AutomaticTacticDamageCurrentParameters.OrdinaryReductionPercent));
        amount = Math.Min(amount, P(SimulationParameterKeys.BattleDamage.DamageCap));
        return new(true, true, amount, null);

        byte Selector() => FactorSelector(source) ?? throw BaseTroopDamageMissing("current_selector6A");
        int SourceAbility() => !Known(_definitions.ReadArmyUnitNativeValidity(casterArmy), "source_ability_Army_validity")
            ? 0 : Ability(caster?.OfficerId ?? throw BaseTroopDamageMissing("source_ability_image"), Selector() == 1 ? 1 : 2);
        int Ability(int personId, int dimension)
        {
            int? value = _abilities is TacticRemakeCurrentAbilities owned
                ? owned.ResolveDimension(state, personId, dimension)
                : dimension == 1 ? _abilities?.Resolve(state, personId)?.Strength
                    : _abilities?.Resolve(state, personId)?.Intelligence;
            return value ?? throw BaseTroopDamageMissing("current_ability_dimension" + dimension);
        }
        int P(string key) => BaseTroopDamageParameter(state, key);
        T Known<T>(T? value, string owner) where T : struct => value ?? throw BaseTroopDamageMissing(owner);
    }
    private static int BaseTroopDamageParameter(GameState state, string key)
    {
        return AutomaticTacticDamageCurrentParameters.ReadCurrent(state, key)
            ?? throw BaseTroopDamageMissing(key);
    }
    private static InvalidDataException BaseTroopDamageMissing(string owner) => new("automatic_Base_troop_damage." + owner);
}
