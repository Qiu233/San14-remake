using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record BaseOriginAutomaticModifierPrefix(BaseReference Origin, int InstructionIndex,
    int Opcode, int SourceForce, int StageProduct);
internal sealed record BaseOriginAutomaticModifierApplication(GameState State, bool TargetResultMarker);

public sealed partial class TacticRemakeCurrentContext
{
    //176610/176980 perform these source reads once, before the Hex walk,
    //including an empty vector. A Base actor has no Army waiting multiplier.
    internal BaseOriginAutomaticModifierPrefix BeginBaseOriginModifierInstruction(GameState state,
        SourceWorldContext current, BaseReference origin, S14TacticEffectInstruction instruction)
    {
        RequireOriginModifierContext(state, current, instruction);
        int force = 0;
        if (OriginNeed(current.ReadBaseNativeValidity(origin), "modifier_source_validity"))
        {
            _ = current.ReadBaseCurrentHexIndex(origin);
            force = OriginNeed(current.ReadBaseForceSlot(origin), "modifier_source_ForceB0");
        }
        int scale = OriginParameter(state, SimulationParameterKeys.Tactics.EffectStageScale);
        return new(origin, instruction.Index, instruction.Opcode, force, unchecked(0 * scale));
    }

    internal (int Modifier, int Duration) ResolveBaseOriginModifierInput(GameState state,
        SourceWorldContext current, BaseReference origin, FieldHexOccupantReceipt target,
        TacticSourceRow source, S14TacticEffectInstruction instruction, BaseOriginAutomaticModifierPrefix prefix)
    {
        RequireOriginModifierContext(state, current, instruction);
        if (prefix.Origin != origin || prefix.InstructionIndex != instruction.Index || prefix.Opcode != instruction.Opcode)
            throw OriginAutomaticMissing("modifier_instruction_prefix");
        bool all = instruction.Opcode is 21 or 22;
        bool positive = instruction.Opcode is >= 5 and <= 9 or 21;
        float ability;
        if (positive)
        {
            //16B6D0 reads both current governor dimensions before selector6A.
            int strength = OriginAbility(state, origin, 1), intelligence = OriginAbility(state, origin, 2);
            byte selector = OriginSelector(source);
            int divisor = OriginParameter(state, selector == 1 ? SimulationParameterKeys.Tactics.StrengthEffectDivisor
                : SimulationParameterKeys.Tactics.IntelligenceEffectDivisor);
            if (divisor == 0) throw OriginAutomaticMissing("modifier_source_divisor_zero");
            ability = (float)(selector == 1 ? strength : intelligence) / divisor;
            ability += 1f;
        }
        else ability = OriginAbilityFactor(state, origin, target, source);

        float panel = 1f;
        if (!all)
        {
            var force = current.ReadForceTableReference(prefix.SourceForce < 52 ? prefix.SourceForce : 0)
                ?? throw OriginAutomaticMissing("modifier_Force_pointer");
            //176610 has two separately reached validity gates;176980 has
            //neither this Force lookup nor a territorial modifier query.
            bool firstValid = OriginNeed(current.ReadForceNativeValidity(force), "modifier_Force_first_validity");
            if (firstValid && positive && Category() == 3)
                panel = (float)OriginNeed(current.ReadBaseForceModifier(force, 0x22), "modifier_positive_Force22") / 100f + 1f;
            else if (OriginNeed(current.ReadForceNativeValidity(force), "modifier_Force_second_validity") && !positive && Category() == 4)
                panel = (float)OriginNeed(current.ReadBaseForceModifier(force, 0x23), "modifier_negative_Force23") / 100f + 1f;
        }
        float duration = instruction.EffectValue;
        duration *= ability;
        duration *= (float)prefix.StageProduct / 100f + 1f;
        duration *= 1f; //A real Base source does not have Army21/22.
        if (!all) duration *= panel;
        int modifier = instruction.Raw68; //16D750 zero-extends the byte.
        return (positive ? modifier : unchecked(-modifier), OriginTruncate(duration));

        byte Category()
        {
            var tactic = current.ReadSkillTacticReference(source.Slot)
                ?? throw OriginAutomaticMissing("modifier_tactic_pointer");
            return OriginNeed(current.ReadSkillTacticRawByte(tactic, 0x40), "modifier_category40");
        }
    }

    private void RequireOriginModifierContext(GameState state, SourceWorldContext current,
        S14TacticEffectInstruction instruction)
    {
        if (!ReferenceEquals(current, _definitions) || !Bound(state) || instruction.Index is < 0 or > 1 ||
            instruction.Opcode is not (>= 5 and <= 14 or 21 or 22))
            throw OriginAutomaticMissing("modifier_current_instruction");
        //Current CE SC176370/1766E0 and TC176610/176980 have identical
        //non-address operands and branch ownership, including Base waiting1,
        //Army-only targets and the single-only Force22/23 multiplier.
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal BaseOriginAutomaticModifierPrefix BeginBaseOriginAutomaticModifiersCurrent(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, BaseReference origin,
        S14TacticEffectInstruction instruction)
        => context.BeginBaseOriginModifierInstruction(state, current, origin, instruction);

    /// <summary>Real Base-caster176610/176980. Only an Army receiver enters
    /// 2EC6A0/2ECB80. A Base, facility or empty Hex has no store or marker.
    /// The caller owns the ordered walk,17A1C0 and final batch publication.</summary>
    internal BaseOriginAutomaticModifierApplication ApplyBaseOriginAutomaticModifiersCurrent(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, BaseReference origin,
        FieldHexOccupantReceipt target, TacticSourceRow source, S14TacticEffectInstruction instruction,
        BaseOriginAutomaticModifierPrefix prefix, NativeArmyCombatCycleSampler sampler,
        NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        if (target.NativeKind != 27) return new(state, false);
        var army = new ArmyUnitReference(target.NativePhysicalSlot);
        if (!Need(current.ReadArmyUnitNativeValidity(army), "target_validity")) return new(state, false);
        int targetId = target.GameplayArmyId ?? throw Missing("target_gameplay_ArmyId");
        var unit = state.Units.GetValueOrDefault(targetId) ?? throw Missing("target_Army_image");
        var amount = context.ResolveBaseOriginModifierInput(state, current, origin, target, source, instruction, prefix);
        var result = ApplyAutomaticResolvedArmyModifiersCurrent(state, current, army, targetId, source,
            instruction, amount, sampler, effects);
        return new(result.State, result.TargetResultMarker);

        static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
        static InvalidDataException Missing(string name) => new("automatic_Base_origin.modifier." + name);
    }
}
