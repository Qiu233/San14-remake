using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record RetiredAutomaticActorInstructionPrefix(int SourcePhysical, int OriginalUnitId,
    int TacticId, int InstructionIndex, byte Opcode, int StageOrdinal, int? StageProduct,
    TacticEffectScaleSourceInput? Scale, bool SkipInstruction, AutomaticArmyMoraleDamagePrefix? Morale);
internal sealed record RetiredAutomaticActorApplication(GameState State, bool TargetResultMarker,
    ushort ResultAmount, TacticEffectPresentation[] ArmyPresentationEffects, bool NativeReturn)
{
    public PlayableTacticEffectKind? Effect { get; init; }
    public int? Amount { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    //This branch is entered only after the actual captured receiver has been
    //reset/released. It does not construct a Unit or manufacture a Base source.
    internal RetiredAutomaticActorInstructionPrefix BeginRetiredAutomaticActorInstructionCurrent(
        GameState state, SourceWorldContext current, TacticRemakeCurrentContext context,
        int casterPhysical, int originalUnitId, TacticSourceRow source,
        S14TacticEffectInstruction instruction, int stageOrdinal)
    {
        RequireRetiredAutomaticActorCurrent(state, current, casterPhysical);
        if (!context.IsBoundCurrentState(state) || instruction.Index is < 0 or > 1 || instruction.Opcode > 23)
            throw RetiredActorMissing("instruction_current");
        //176C10 returns when both source-validity tests fail;177610 first
        //requires a valid source Army. Neither reaches its stage parameter.
        if (instruction.Opcode is 0 or 1 or 18)
            return new(casterPhysical, originalUnitId, source.Slot, instruction.Index, instruction.Opcode,
                stageOrdinal, null, null, true, null);
        if (instruction.Opcode == 19)
            return new(casterPhysical, originalUnitId, source.Slot, instruction.Index, 19,
                stageOrdinal, null, null, false, null);
        int step = context.ReadAutomaticAllModifierParameter(state, SimulationParameterKeys.Tactics.EffectStageScale);
        int product = unchecked(stageOrdinal * step);
        //16B6D0's two-invalid branch is an explicit XMM0=0, not ability0/divisor+1.
        //16D900's invalid-Army branch is literal1 and consumes no waiting bytes.
        var scale = instruction.Opcode is 15 or 17 or 20
            ? new TacticEffectScaleSourceInput(instruction.EffectValue, 0f, stageOrdinal, step, 1f) : null;
        var morale = instruction.Opcode == 16
            ? new AutomaticArmyMoraleDamagePrefix(originalUnitId, new(casterPhysical), new(null),
                (float)product / 100f + 1f, 1f) : null;
        return new(casterPhysical, originalUnitId, source.Slot, instruction.Index, instruction.Opcode,
            stageOrdinal, product, scale, false, morale);
    }

    //177C60 must still visit the raw Hex before generic occupant decoding.
    //Its two-invalid policy/source branches supply0, with no policy/Force read.
    internal BaseOriginAutomaticFireResult ApplyRetiredAutomaticActorFireHexCurrent(GameState state,
        SourceWorldContext current, RetiredAutomaticActorInstructionPrefix prefix,
        int nodeHex, int primaryHex, NativeArmyCombatCycleSampler sampler,
        NativeMapSeasonDate date, long revision, List<CampaignEffectRequest> effects)
    {
        RequireRetiredAutomaticActorCurrent(state, current, prefix.SourcePhysical);
        if (prefix.Opcode != 17 || prefix.Scale is null) throw RetiredActorMissing("fire_prefix");
        int duration = TacticEffectSourceMath.Scale(prefix.Scale);
        return ApplyAutomaticFireHexCurrent(state, current, duration, 0, nodeHex, primaryHex,
            reached => (sampler.Read(reached) ?? throw RetiredActorMissing("fire_Day450")).Day450, revision, effects,date);
    }

    internal RetiredAutomaticActorApplication ApplyRetiredAutomaticActorTargetCurrent(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context,
        RetiredAutomaticActorInstructionPrefix prefix, FieldHexOccupantReceipt target,
        TacticSourceRow source, S14TacticEffectInstruction instruction, NativeArmyCombatCycleSampler sampler,
        NativeMapSeasonDate date, List<CampaignEffectRequest> effects, long? immediatePauseRevision = null)
    {
        RequireRetiredAutomaticActorCurrent(state, current, prefix.SourcePhysical);
        if (prefix.TacticId != source.Slot || prefix.InstructionIndex != instruction.Index || prefix.Opcode != instruction.Opcode)
            throw RetiredActorMissing("target_instruction_prefix");
        if (prefix.SkipInstruction || target.NativeKind == 255) return new(state, false, 0, [], false);
        if (instruction.Opcode == 17) throw RetiredActorMissing("fire_requires_raw_Hex_entry");
        if (target.NativeKind is not (5 or 6 or 27)) return new(state, false, 0, [], false);
        var army = new ArmyUnitReference(target.NativePhysicalSlot);
        var targetBase = new BaseReference(target.NativeKind == 6 ? BaseKind.Gate : BaseKind.City, target.NativePhysicalSlot);
        bool valid = target.NativeKind == 27
            ? RetiredNeed(current.ReadArmyUnitNativeValidity(army), "target_Army_validity")
            : RetiredNeed(current.ReadBaseNativeValidity(targetBase), "target_Base_validity");
        if (!valid) return new(state, false, 0, [], false);
        if (instruction.Opcode is 2 or 3 or 4 or 23)
        {
            //16F9A0 reads target ability, then returns0 at its missing-source
            //position branch. No baseline/coefficient/traits/sample is reached.
            _ = context.ReadRetiredAutomaticTargetAbilityCurrent(state, current, target, source);
            if (target.NativeKind == 27 && instruction.Opcode is 2 or 3 or 4)
            {
                int id = target.GameplayArmyId ?? throw RetiredActorMissing("failure_target_ArmyId");
                var native = state.Units.GetValueOrDefault(id)?.NativeTacticRuntime
                    ?? throw RetiredActorMissing("failure_target_counters");
                if (native.Abnormal23 == 0 && native.Abnormal24 == 0 && native.Abnormal25 == 0)
                {
                    var working = state;
                    var feedback = new ArmyTraitFeedback(working, current, date, effects, () => working,
                        speechConsumer: CreateArmyTraitSpeechConsumer(current, () => working, next => working = next));
                    feedback.NoAbnormalApplied(army, 20 + instruction.Opcode - 2);
                    return new(feedback.Publish(working), false, 0, [], false);
                }
            }
            return new(state, false, 0, [], false);
        }
        if (instruction.Opcode is >= 5 and <= 14 or 21 or 22)
        {
            if (target.NativeKind != 27) return new(state, false, 0, [], false);
            var amount = context.ResolveRetiredAutomaticModifierInputCurrent(state, current, target, source,
                instruction, prefix.StageProduct ?? throw RetiredActorMissing("modifier_stage"));
            var applied = ApplyAutomaticResolvedArmyModifiersCurrent(state, current, army,
                target.GameplayArmyId ?? throw RetiredActorMissing("modifier_target_ArmyId"), source,
                instruction, amount, sampler, effects);
            return new(applied.State, applied.TargetResultMarker, 0, [], false);
        }
        if (instruction.Opcode == 16)
        {
            var morale = prefix.Morale ?? throw RetiredActorMissing("morale_prefix");
            if (target.NativeKind == 27)
            {
                var applied = ApplyBorrowedAutomaticArmyMoraleDamage(state, current, context, prefix.OriginalUnitId,
                    target.GameplayArmyId ?? throw RetiredActorMissing("morale_target_ArmyId"), source,
                    instruction, morale, sampler, date, effects, immediatePauseRevision);
                return new(applied.State, applied.TargetResultMarker, 0, [], applied.NativeReturn);
            }
            float value = instruction.EffectValue;
            value *= context.ReadRetiredAutomaticTargetFactorCurrent(state, current, target, source);
            value *= morale.StageFactor;
            value *= 1f;
            int loss = RetiredTruncate(value);
            byte before = RetiredNeed(current.ReadBaseMorale44(targetBase), "morale_Base44");
            bool written = BasePostLoad.WriteMoraleCurrent(targetBase, Math.Max(0, unchecked((int)before - loss)), current);
            return new(PublishCurrentBaseCombatQuantities(state, current, targetBase, false, false, written), true, 0, [], false);
        }
        if (target.NativeKind is 5 or 6)
        {
            BaseTacticEffectApplicationResult applied = instruction.Opcode switch
            {
                15 => ApplyBorrowedBaseTacticMoraleTargetCurrent(state, current, prefix.OriginalUnitId, source,
                    targetBase, instruction, prefix.StageOrdinal, prefix.Scale ?? throw RetiredActorMissing("morale_scale")),
                19 => ApplyBorrowedBaseTacticClearTargetCurrent(state, current, prefix.OriginalUnitId, source,
                    targetBase, instruction, prefix.StageOrdinal),
                _ => throw RetiredActorMissing("opcode" + instruction.Opcode + "_selected_Base")
            };
            return new(applied.State, applied.Applied, 0, [], false)
            {
                Effect = instruction.Opcode == 15 ? PlayableTacticEffectKind.MoraleChange : PlayableTacticEffectKind.ClearAbnormalStatuses,
                Amount = instruction.Opcode == 15 && applied.Receipt is { MoraleBefore: byte before, MoraleAfter: byte after }
                    ? after - before : 0
            };
        }
        int targetId = target.GameplayArmyId ?? throw RetiredActorMissing("target_ArmyId");
        var unit = state.Units.GetValueOrDefault(targetId) ?? throw RetiredActorMissing("target_Army_image");
        var rules = new TacticEffectSourceRules(_catalog, context, new(new HexTravelMap(_map)));
        var resolution = instruction.Opcode switch
        {
            15 => context.ResolveAutomaticArmyMoraleTarget(state, unit, prefix.Scale ?? throw RetiredActorMissing("morale_scale")),
            20 => context.ResolveAutomaticArmyHealingTarget(state, unit, prefix.Scale ?? throw RetiredActorMissing("healing_scale")),
            19 => rules.ResolveCapturedArmyClearTarget(state, unit, (reached, recipient) =>
                context.ArmyValid(reached, recipient) == true && recipient.HexId is >= 0 and < 48400 ? reached.RandomState : null),
            _ => throw RetiredActorMissing("opcode" + instruction.Opcode + "_Army_writer")
        };
        if (resolution is null) throw RetiredActorMissing("opcode" + instruction.Opcode + "_Army_inputs");
        var receiver = state.ReadArmyLifecycle()!.Slots[target.NativePhysicalSlot].ReceiverId;
        var presentations = new List<TacticEffectPresentation>();
        foreach (var effect in resolution.Effects)
        {
            unit = TacticEffectSourceRules.ApplyReceipt(unit, effect);
            if (unit.Troops <= 0 || unit.MoraleState.CurrentValue is not > 0)
                throw RetiredActorMissing("support_retirement");
            presentations.Add(new(instruction.Index, receiver, effect.Kind, effect.Amount,
                effect.HealingResult, effect.NativeAbnormalAfter, effect.CurrentMoraleAfter));
        }
        var result = state with { Units = new(state.Units) { [targetId] = unit }, RandomState = resolution.RandomState,
            ResourceState = resolution.ResourceStateAfter ?? state.ResourceState };
        if (instruction.Opcode == 19)
        {
            var native = unit.NativeTacticRuntime ?? throw RetiredActorMissing("clear_counters");
            var reference = new RegistryObjectReference(RegistryKind.Army, target.NativePhysicalSlot);
            current.SetTacticAbnormalRawByte(reference, 0x23, native.Abnormal23);
            current.SetTacticAbnormalRawByte(reference, 0x24, native.Abnormal24);
            current.SetTacticAbnormalRawByte(reference, 0x25, native.Abnormal25);
            current.SetArmyCommandOverride(target.NativePhysicalSlot, ArmyOverrideTargetSourceAdapter.Read(result, unit));
        }
        else
        {
            var fields = current.ReadArmyReturnResourceFields(army) ?? throw RetiredActorMissing("target_numeric_fields");
            bool healing = instruction.Opcode == 20;
            fields = healing ? fields with { Ready16 = checked((ushort)unit.Troops), Wounded18 = checked((ushort)unit.WoundedTroops!.Value) }
                : fields with { Morale1A = ArmyMoraleSourceAdapter.Read(unit) };
            current.BindArmyReturnResourceFields(target.NativePhysicalSlot, fields);
            current.AcknowledgeArmyReturnNumerics(target.NativePhysicalSlot, wounded: healing, morale: !healing);
        }
        ushort marker = instruction.Opcode == 20
            ? unchecked((ushort)(resolution.NativeResultAmount ?? throw RetiredActorMissing("healing_requested_recovery"))) : (ushort)0;
        return new(result, true, marker, presentations.ToArray(), false);
    }

    //An entered176C10 instruction still reaches ordinary178EC0 after its
    //source resets. Nonfatal Base stores share the real body. Fatal capture
    //requires a genuine null-winner Base settlement owner and remains explicit.
    internal NormalBaseFatalApplicationResult ApplyRetiredEnteredAutomaticBaseTroopDamageCurrent(GameState input,
        SourceWorldContext current, int originalUnitId, int sourcePhysical, int locationId,
        int requestedLoss, NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate? operationDate = null,
        List<CampaignEffectRequest>? effects = null, long? immediatePauseRevision = null)
    {
        RequireRetiredAutomaticActorCurrent(input, current, sourcePhysical);
        BaseKind kind = locationId <= 51 ? BaseKind.City : BaseKind.Gate;
        var applied = TryApplyNativeNormalBaseApplication(input, originalUnitId, kind, locationId,
            requestedLoss, sampler, allowFatal: true, operationCurrent: current, retiredArmyOrigin: new(sourcePhysical),
            operationDate: operationDate, operationEffects: effects, immediateRevision: immediatePauseRevision);
        var r = applied.Receipt;
        return new(applied.Outcome, applied.State, r is null ? null : new(r.LocationId, kind,
            r.RequestedLoss, r.AppliedLoss, r.AttackerReward, r.TroopDebit, r.Wounded,
            r.MoraleLoss, r.MoraleWrite, r.NativeReturn, r.FatalAdmission), applied.UnresolvedOwner);
    }

    internal static void RequireRetiredAutomaticActorCurrent(GameState state, SourceWorldContext current, int physical)
    {
        var reached = ReadAutomaticActorResumeCurrent(state, current, physical);
        if (reached.NativeValid || reached.LatestImage is not null)
            throw RetiredActorMissing("not_released_invalid_source");
    }
    private static int RetiredTruncate(float value) => !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f
        ? int.MinValue : (int)value;
    private static T RetiredNeed<T>(T? value, string owner) where T : struct => value ?? throw RetiredActorMissing(owner);
    private static InvalidDataException RetiredActorMissing(string owner) => new("automatic_retired_actor." + owner);
}

public sealed partial class TacticRemakeCurrentContext
{
    internal int ReadRetiredAutomaticTargetAbilityCurrent(GameState state, SourceWorldContext current,
        FieldHexOccupantReceipt target, TacticSourceRow source)
    {
        if (!ReferenceEquals(current, _definitions) || !Bound(state)) throw RetiredCurrentMissing("context");
        byte selector = FactorSelector(source) ?? throw RetiredCurrentMissing("target_selector6A");
        int dimension = selector == 1 ? 1 : 2;
        if (target.NativeKind == 27)
        {
            var army = new ArmyUnitReference(target.NativePhysicalSlot);
            if (!RetiredCurrentNeed(current.ReadArmyUnitNativeValidity(army), "target_Army_validity")) return 0;
            return OriginPersonAbility(state, OriginCommander(army), dimension, 0);
        }
        if (target.NativeKind is 5 or 6)
        {
            var targetBase = new BaseReference(target.NativeKind == 5 ? BaseKind.City : BaseKind.Gate, target.NativePhysicalSlot);
            if (!RetiredCurrentNeed(current.ReadBaseNativeValidity(targetBase), "target_Base_validity")) return 0;
            return OriginPersonAbility(state, OriginGovernor(targetBase), dimension, 50);
        }
        return 0;
    }

    internal float ReadRetiredAutomaticTargetFactorCurrent(GameState state, SourceWorldContext current,
        FieldHexOccupantReceipt target, TacticSourceRow source)
    {
        int receiving = ReadRetiredAutomaticTargetAbilityCurrent(state, current, target, source);
        byte selector = FactorSelector(source) ?? throw RetiredCurrentMissing("factor_selector6A");
        int divisor = ReadAutomaticAllModifierParameter(state, selector == 1 ? SimulationParameterKeys.Tactics.StrengthDifferenceDivisor
            : SimulationParameterKeys.Tactics.IntelligenceDifferenceDivisor);
        if (divisor == 0) throw RetiredCurrentMissing("factor_zero_divisor");
        float factor = (float)unchecked(-receiving) / (float)divisor;
        factor += 1f;
        float floor = (float)ReadAutomaticAllModifierParameter(state, SimulationParameterKeys.Tactics.EffectFactorFloorPercent) / 100f;
        return factor > floor ? factor : floor;
    }

    internal (int Modifier, int Duration) ResolveRetiredAutomaticModifierInputCurrent(GameState state,
        SourceWorldContext current, FieldHexOccupantReceipt target, TacticSourceRow source,
        S14TacticEffectInstruction instruction, int stageProduct)
    {
        bool positive = instruction.Opcode is >= 5 and <= 9 or 21;
        bool all = instruction.Opcode is 21 or 22;
        float ability = positive ? 0f : ReadRetiredAutomaticTargetFactorCurrent(state, current, target, source);
        float panel = 1f;
        if (!all)
        {
            var force = current.ReadForceTableReference(0) ?? throw RetiredCurrentMissing("modifier_Force0_pointer");
            bool first = RetiredCurrentNeed(current.ReadForceNativeValidity(force), "modifier_Force0_first_validity");
            if (first && positive && Category() == 3)
                panel = (float)RetiredCurrentNeed(current.ReadBaseForceModifier(force, 0x22), "modifier_Force22") / 100f + 1f;
            else if (RetiredCurrentNeed(current.ReadForceNativeValidity(force), "modifier_Force0_second_validity") && !positive && Category() == 4)
                panel = (float)RetiredCurrentNeed(current.ReadBaseForceModifier(force, 0x23), "modifier_Force23") / 100f + 1f;
        }
        float duration = instruction.EffectValue;
        duration *= ability;
        duration *= (float)stageProduct / 100f + 1f;
        duration *= 1f;
        if (!all) duration *= panel;
        int requested = !float.IsFinite(duration) || duration < int.MinValue || duration >= 2147483648f ? int.MinValue : (int)duration;
        return (positive ? instruction.Raw68 : unchecked(-(int)instruction.Raw68), requested);
        byte Category()
        {
            var tactic = current.ReadSkillTacticReference(source.Slot) ?? throw RetiredCurrentMissing("modifier_Tactic_pointer");
            return RetiredCurrentNeed(current.ReadSkillTacticRawByte(tactic, 0x40), "modifier_category40");
        }
    }
    private static T RetiredCurrentNeed<T>(T? value, string owner) where T : struct => value ?? throw RetiredCurrentMissing(owner);
    private static InvalidDataException RetiredCurrentMissing(string owner) => new("automatic_retired_actor.current." + owner);
}
