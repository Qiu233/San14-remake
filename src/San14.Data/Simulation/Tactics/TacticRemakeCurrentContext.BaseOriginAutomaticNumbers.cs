using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record BaseOriginAutomaticTroopPrefix(int SourceKind, int SourceId,
    int SourceTroops, int Object14, int SourceForce, int StageProduct);

public sealed partial class TacticRemakeCurrentContext
{
    internal BaseOriginAutomaticTroopPrefix BeginBaseOriginAutomaticTroops(GameState state, BaseReference source)
    {
        ushort link = OriginNeed(_definitions.ReadBaseObjectLink(source), "troop_source4E");
        var obj = _definitions.ReadObjectTableReference(link < 3001 ? link : 0)
            ?? throw OriginAutomaticMissing("troop_Object_pointer");
        int hp = OriginNeed(_definitions.ReadObjectNativeValidity(obj), "troop_Object_validity")
            ? OriginNeed(_definitions.ReadObjectRawField14(obj), "troop_Object14") : 6000;
        int troops = OriginNeed(_definitions.ReadBaseResource(source, BaseResourceKind.Troops), "troop_source3C");
        int force = OriginNeed(_definitions.ReadBaseForceSlot(source), "troop_source_Force");
        int stage = OriginParameter(state, SimulationParameterKeys.Tactics.EffectStageScale);
        return new(link <= 51 ? 5 : 6, link <= 51 ? link : link <= 61 ? link - 51 : 0,
            troops, hp, force, unchecked(0 * stage));
    }

    //176C10 shares the scalar pipeline with Army actors, but its real Base
    //prefix contributes Object14 and the Force's territorial advantage3.
    internal int ResolveBaseOriginAutomaticTroopDamage(GameState state, BaseReference source,
        FieldHexOccupantReceipt target, TacticSourceRow row, S14TacticEffectInstruction instruction,
        BaseOriginAutomaticTroopPrefix prefix)
    {
        if (instruction.Opcode != 1) throw OriginAutomaticMissing("troop_opcode");
        int ability = OriginAbility(state, source, OriginSelector(row) == 1 ? 1 : 2);
        int baseline = P(AutomaticTacticDamageCurrentParameters.AbilityBaseline);
        int divisor = P(AutomaticTacticDamageCurrentParameters.TroopDivisor);
        if (divisor == 0) throw OriginAutomaticMissing("troop_divisor_zero");
        int root = P(AutomaticTacticDamageCurrentParameters.SquareRootOffset);
        float factor = OriginAbilityFactor(state, source, target, row);
        float provocation = 1f;
        if (target.NativeKind == 27)
        {
            var army = new ArmyUnitReference(target.NativePhysicalSlot);
            var reference = new RegistryObjectReference(RegistryKind.Army, target.NativePhysicalSlot);
            if (OriginNeed(_definitions.ReadArmyUnitNativeValidity(army), "provocation_Army_validity") &&
                OriginNeed(_definitions.ReadTacticClearByte(reference, 0x25), "provocation25") != 0)
            {
                byte kind = OriginNeed(_definitions.ReadTacticClearByte(reference, 0x3C), "provocation3C");
                if (kind != prefix.SourceKind || OriginNeed(_definitions.ReadTacticArmyTarget3E(army), "provocation3E") != prefix.SourceId)
                    provocation = (float)P(AutomaticTacticDamageCurrentParameters.ProvokedOtherSourcePercent) / 100f;
            }
        }
        int forceId = prefix.SourceForce < 52 ? prefix.SourceForce : 0;
        var force = _definitions.ReadForceTableReference(forceId) ?? throw OriginAutomaticMissing("damage_Force_pointer");
        float panel = 1f;
        if (OriginNeed(_definitions.ReadForceNativeValidity(force), "damage_Force_validity"))
        {
            var tactic = _definitions.ReadSkillTacticReference(row.Slot) ?? throw OriginAutomaticMissing("damage_tactic_pointer");
            byte category = OriginNeed(_definitions.ReadSkillTacticRawByte(tactic, 0x40), "damage_category40");
            if (category is 1 or 2)
                panel += (float)OriginNeed(_definitions.ReadBaseForceModifier(force, category == 1 ? 32 : 33), "damage_Force_panel") / 100f;
        }
        byte flags = state.NativeArmySimulation!.Forces.GetValueOrDefault(forceId)?.Flags193
            ?? throw OriginAutomaticMissing("damage_Force193");
        int percent = (flags & 16) != 0 ? P(AutomaticTacticDamageCurrentParameters.SpecialForcePercent) : 100;
        float additive = (float)OriginStatic(state, "object14-additive-percent", 20) * (float)prefix.Object14 / 100f;
        int amount = TacticArmyBaseTroopDamageMath.BeforeSettings(new(instruction.EffectValue,
            ability, baseline, prefix.StageProduct, prefix.SourceTroops, divisor, root,
            factor, 1f, 1f, percent, panel), provocation, additive);
        var advantage = PersonReturnPathSource.ReadForceAdvantage(state, _definitions, force, 3, 0);
        int advantageValue = advantage.Value ?? throw OriginAutomaticMissing(advantage.UnresolvedOwner ?? "damage_advantage3");
        int addition = unchecked(unchecked(advantageValue * amount) * OriginStatic(state, "advantage3-scale", 1)) / 100;
        if (target.NativeKind == 27)
        {
            var army = new ArmyUnitReference(target.NativePhysicalSlot);
            if (OriginNeed(_definitions.ReadArmyUnitNativeValidity(army), "half_Army_validity"))
            {
                int commander = OriginCommander(army);
                if (OriginNeed(_definitions.ReadPersonNativeValidity(commander), "half_Person_validity") &&
                    OriginNeed(BattleRemakeCurrentDamageBodyQueries.ReadFirstMatchingPermanentEffect(_definitions, commander, 56), "half_effect56"))
                {
                    int half = P(SimulationParameterKeys.BattleDamage.HalfDamageDivisor);
                    if (half == 0 || amount == int.MinValue && half == -1) throw OriginAutomaticMissing("half_divisor");
                    amount = unchecked(amount - amount / half);
                }
            }
        }
        int selector = OriginNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "damage_settings_selector");
        if (selector == 0)
        {
            if (OriginNeed(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "damage_settings_advanced"))
                amount = TacticArmyBaseTroopDamageMath.Advanced(unchecked(amount + addition),
                    OriginNeed(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 2), "damage_settings2"));
        }
        else
        {
            if (amount > 0 && OriginNeed(BattleRemakeCurrentSettings.ReadInt32(state, 0xB8), "damage_settingsB8") == 0)
                amount = TacticArmyBaseTroopDamageMath.OrdinaryReduced(amount, P(AutomaticTacticDamageCurrentParameters.OrdinaryReductionPercent));
            amount = unchecked(amount + addition);
        }
        return Math.Min(amount, P(SimulationParameterKeys.BattleDamage.DamageCap));
        int P(string key) => OriginParameter(state, key);
    }

    internal TacticEffectScaleSourceInput ResolveBaseOriginAutomaticScale(GameState state,
        BaseReference source, TacticSourceRow row, S14TacticEffectInstruction instruction)
    {
        //16B6D0 reads both dimensions before selecting one.
        int strength = OriginAbility(state, source, 1), intelligence = OriginAbility(state, source, 2);
        byte selector = OriginSelector(row);
        string key = selector == 1 ? SimulationParameterKeys.Tactics.StrengthEffectDivisor : SimulationParameterKeys.Tactics.IntelligenceEffectDivisor;
        int divisor = OriginParameter(state, key);
        if (divisor == 0) throw OriginAutomaticMissing("scale_divisor");
        float factor = (float)(selector == 1 ? strength : intelligence) / divisor;
        factor += 1f;
        return new(instruction.EffectValue, factor, 0,
            OriginParameter(state, SimulationParameterKeys.Tactics.EffectStageScale), 1f);
    }

    internal int ResolveBaseOriginAutomaticMoraleLoss(GameState state, BaseReference source,
        FieldHexOccupantReceipt target, TacticSourceRow row, S14TacticEffectInstruction instruction)
    {
        float value = instruction.EffectValue;
        value *= OriginAbilityFactor(state, source, target, row);
        value *= (float)OriginParameter(state, SimulationParameterKeys.Tactics.EffectStageScale) * 0f / 100f + 1f;
        value *= 1f;
        return unchecked(-OriginTruncate(value));
    }

    private float OriginAbilityFactor(GameState state, BaseReference source, FieldHexOccupantReceipt target, TacticSourceRow row)
    {
        int acting = OriginAbility(state, source, OriginSelector(row) == 1 ? 1 : 2);
        int receiving;
        byte targetSelector = OriginSelector(row);
        if (target.NativeKind == 27)
        {
            var army = new ArmyUnitReference(target.NativePhysicalSlot);
            receiving = OriginNeed(_definitions.ReadArmyUnitNativeValidity(army), "factor_target_validity")
                ? OriginPersonAbility(state, OriginCommander(army), targetSelector == 1 ? 1 : 2, 0) : 0;
        }
        else if (target.NativeKind is 5 or 6)
        {
            var targetBase = new BaseReference(target.NativeKind == 5 ? BaseKind.City : BaseKind.Gate, target.NativePhysicalSlot);
            int governor = OriginGovernor(targetBase);
            receiving = OriginPersonAbility(state, governor, targetSelector == 1 ? 1 : 2, 50);
        }
        else receiving = 0;
        byte selector = OriginSelector(row);
        int divisor = OriginParameter(state, selector == 1 ? SimulationParameterKeys.Tactics.StrengthDifferenceDivisor
            : SimulationParameterKeys.Tactics.IntelligenceDifferenceDivisor);
        if (divisor == 0) throw OriginAutomaticMissing("factor_divisor");
        float factor = (float)unchecked(acting - receiving) / divisor + 1f;
        float floor = (float)OriginParameter(state, SimulationParameterKeys.Tactics.EffectFactorFloorPercent) / 100f;
        return factor > floor ? factor : floor;
    }
    private int OriginAbility(GameState state, BaseReference source, int dimension)
        => OriginPersonAbility(state, OriginGovernor(source), dimension, 0);
    private int OriginGovernor(BaseReference source)
    {
        ushort raw = OriginNeed(_definitions.ReadArmyReturnBaseGovernor2E(source), "ability_Governor2E");
        return OriginNeed((raw <= 6000 ? _definitions.ReadPersonLookupReference(raw) : _definitions.ReadTransferPersonTableReference(0))?.PhysicalSlot, "ability_Governor_pointer");
    }
    private int OriginCommander(ArmyUnitReference army)
    {
        ushort raw = OriginNeed(_definitions.ReadArmyUnitCommanderId(army), "ability_Commander12");
        return OriginNeed((raw <= 6000 ? _definitions.ReadPersonLookupReference(raw) : _definitions.ReadTransferPersonTableReference(0))?.PhysicalSlot, "ability_Commander_pointer");
    }
    private int OriginPersonAbility(GameState state, int physical, int dimension, int invalid)
    {
        if (!OriginNeed(_definitions.ReadPersonNativeValidity(physical), "ability_Person_validity")) return invalid;
        int id = OriginNeed(_definitions.ReadPersonIdentity(physical), "ability_Person10");
        int? result = _abilities is TacticRemakeCurrentAbilities owned ? owned.ResolveDimension(state, id, dimension)
            : dimension == 1 ? _abilities?.Resolve(state, id)?.Strength : _abilities?.Resolve(state, id)?.Intelligence;
        return result ?? throw OriginAutomaticMissing("ability_dimension" + dimension);
    }
    private byte OriginSelector(TacticSourceRow row) => FactorSelector(row) ?? throw OriginAutomaticMissing("selector6A");
    private static int OriginTruncate(float value) => !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f ? int.MinValue : (int)value;
}
