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
        bool all = instruction.Opcode is 21 or 22;
        bool positive = instruction.Opcode is >= 5 and <= 9 or 21;
        int dimension = all ? -1 : instruction.Opcode - (positive ? 5 : 10);
        var native = unit.NativeTacticRuntime ?? throw Missing("target_tactic_runtime");
        if (native.StatModifiers4A is not { Length: 6 } || native.StatDurations50 is not { Length: 6 })
            throw Missing("target_six_modifier_duration_pairs");
        var modifiers = native.StatModifiers4A.ToArray();
        var durations = native.StatDurations50.ToArray();
        sbyte beforeModifier = all ? (sbyte)0 : modifiers[dimension];
        byte beforeDuration = all ? (byte)0 : durations[dimension];
        //The shared merge is the existing2EC6A0 signed32 energy merge, with
        //its final signed-byte and duration caps. Do not narrow Raw68 early.
        if (all)
        {
            for (int index = 0; index < 5; index++)
                if (Need(current.ReadArmyUnitNativeValidity(army), "all_modifier_validity"))
                    (modifiers[index], durations[index]) = MergeAutomaticArmyModifier(
                        modifiers[index], durations[index], amount.Modifier, amount.Duration);
        }
        else (modifiers[dimension], durations[dimension]) = MergeAutomaticArmyModifier(
            modifiers[dimension], durations[dimension], amount.Modifier, amount.Duration);
        var changed = unit with { NativeTacticRuntime = native with { StatModifiers4A = modifiers, StatDurations50 = durations } };
        var working = state with { Units = new(state.Units) { [targetId] = changed } };

        byte rawDistrict = ReadFacilityHistoryDistrict(current, army);
        var district = current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0)
            ?? throw Missing("report_District_pointer");
        if (BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, Missing))
        {
            if (all)
                Append(0x322E, new(Army0: army, String0: source.Name,
                    String1: ReadHistoryDataText(1, positive ? 0x24E : 0x24F)));
            else
            {
                //The single report re-resolves the tactic identity/validity
                //before comparing the newly stored modifier/duration pair.
                var tactic = current.ReadSkillTacticReference(source.Slot)
                    ?? throw Missing("report_tactic_pointer");
                string name = Need(current.ReadSkillTacticValidity(tactic), "report_tactic_validity") ? source.Name : string.Empty;
                int dimensionTextId = dimension switch { 0 => 0x22F, 1 => 0x22C, 2 => 0x22D, 3 => 0x22E, 4 => 0x230,
                    _ => throw Missing("report_dimension") };
                string dimensionText = ReadHistoryDataText(1, dimensionTextId);
                sbyte afterModifier = modifiers[dimension];
                byte afterDuration = durations[dimension];
                if (beforeModifier != afterModifier)
                    Append(0x322F, new(Army0: army, String0: name, String1: dimensionText,
                        String2: ReadHistoryDataText(1, afterModifier <= beforeModifier ? 0x24F : 0x24E)));
                else if (beforeDuration != afterDuration)
                    Append(0x3230, new(Army0: army, String0: name, String1: dimensionText,
                        String2: ReadHistoryDataText(1, afterModifier < 1 ? 0x24F : 0x24E),
                        String3: ReadHistoryDataText(1, afterDuration <= beforeDuration ? 0x24D : 0x24C)));
            }
        }
        //The wrapper emits marker0 even when every numeric byte is unchanged.
        return new(working, true);

        void Append(int root, HistoryMessageOperands operands)
        {
            var formatted = FormatHistoryReport(working, current, root, operands);
            working = formatted.State;
            int hex = Need(current.ReadArmyUnitRawHex2A(army), "report_Army2A");
            int day = sampler.Read(working)?.Day450 ?? throw Missing("report_World450");
            byte finalDistrict = ReadFacilityHistoryDistrict(current, army);
            working = WriteHistory(working, current, new ReportCommand(5, finalDistrict, hex, day,
                formatted.Text, CampaignHistoryWriter.Supplemental(working), 5012, MarkBit4: 1), effects);
        }
        static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
        static InvalidDataException Missing(string name) => new("automatic_Base_origin.modifier." + name);
    }
}
