using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record AutomaticArmyModifierExtensionPrefix(int CasterId, int InstructionIndex,
    int Opcode, int SourceForce, int StageProduct);

public sealed partial class TacticRemakeCurrentContext
{
    internal (int Modifier, int Duration) ResolveAutomaticArmyModifierExtensionInput(GameState state,
        SourceWorldContext current, UnitState caster, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction, AutomaticArmyModifierExtensionPrefix prefix)
    {
        if (!ReferenceEquals(current, _definitions) || !Bound(state) || prefix.CasterId != caster.Id ||
            prefix.InstructionIndex != instruction.Index || prefix.Opcode != instruction.Opcode ||
            instruction.Index is < 0 or > 1 || instruction.Opcode is not (>= 5 and <= 14 or 21))
            throw AllModifierMissing("extension_instruction_or_prefix");
        return ResolveAutomaticArmyModifierInput(state, caster, target, source, instruction,
            prefix.StageProduct, prefix.SourceForce);
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>176610/176980's real source-Force and stage prefix, including
    /// an empty target vector. Opcode22 retains its existing independent entry.</summary>
    internal AutomaticArmyModifierExtensionPrefix BeginBorrowedAutomaticArmyModifierExtensions(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, int casterId,
        S14TacticEffectInstruction instruction, int stageOrdinal)
    {
        if (instruction.Index is < 0 or > 1 || instruction.Opcode is not (>= 5 and <= 14 or 21))
            throw AutomaticAllModifierMissing("extension_instruction");
        var caster = RequireLatestAutomaticActorImageCurrent(state, current, casterId);
        var army = current.ReadArmyUnitTableReference(AutomaticAllModifierKnown(caster.NativeArmyOrigin?.NativeSlotIndex,
            "extension_source_physical")) ?? throw AutomaticAllModifierMissing("extension_source_Army_pointer");
        if (!AutomaticAllModifierKnown(current.ReadArmyUnitNativeValidity(army), "extension_source_Army_validity"))
            throw AutomaticAllModifierMissing("extension_invalid_source_Army_fallback_Base");
        int force = current.ReadArmyUnitRawForceSlot(army);
        int scale = context.ReadAutomaticAllModifierParameter(state, SimulationParameterKeys.Tactics.EffectStageScale);
        return new(casterId, instruction.Index, instruction.Opcode, force, unchecked(stageOrdinal * scale));
    }

    /// <summary>The actual ordered Hex recipient. Both native wrappers ignore
    /// non-Army kinds; a Base is never converted into a fictitious Army.</summary>
    internal AutomaticArmyAllModifierApplication ApplyBorrowedAutomaticArmyModifierExtensions(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, int casterId,
        FieldHexOccupantReceipt target, TacticSourceRow source, S14TacticEffectInstruction instruction,
        AutomaticArmyModifierExtensionPrefix prefix, NativeArmyCombatCycleSampler sampler,
        NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        if (prefix.CasterId != casterId || prefix.InstructionIndex != instruction.Index ||
            prefix.Opcode != instruction.Opcode || instruction.Opcode is not (>= 5 and <= 14 or 21))
            throw AutomaticAllModifierMissing("extension_instruction_or_prefix");
        if (target.NativeKind != 27) return new(state, false);
        var army = new ArmyUnitReference(target.NativePhysicalSlot);
        if (!AutomaticAllModifierKnown(current.ReadArmyUnitNativeValidity(army), "extension_target_validity"))
            return new(state, false);
        int targetId = target.GameplayArmyId ?? throw AutomaticAllModifierMissing("extension_target_ArmyId");
        var recipient = state.Units.GetValueOrDefault(targetId)
            ?? throw AutomaticAllModifierMissing("extension_target_Army_image");
        var caster = RequireLatestAutomaticActorImageCurrent(state, current, casterId);
        var amount = context.ResolveAutomaticArmyModifierExtensionInput(state, current, caster, recipient,
            source, instruction, prefix);
        return ApplyAutomaticResolvedArmyModifiersCurrent(state, current, army, targetId, source,
            instruction, amount, sampler, effects);
    }

    //Shared actual2EC6A0/2ECB80 store/report tail. Source-specific wrappers
    //resolve their own abilities, waiting, Force policy and prefix exactly once.
    private AutomaticArmyAllModifierApplication ApplyAutomaticResolvedArmyModifiersCurrent(GameState state,
        SourceWorldContext current, ArmyUnitReference army, int targetId, TacticSourceRow source,
        S14TacticEffectInstruction instruction, (int Modifier, int Duration) amount,
        NativeArmyCombatCycleSampler sampler, List<CampaignEffectRequest> effects)
    {
        var unit = state.Units.GetValueOrDefault(targetId) ?? throw Missing("target_Army_image");
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
        static InvalidDataException Missing(string name) => new("automatic_Army_modifier." + name);
    }
}
