using San14.Core;

namespace San14.Data;

public enum OriginalNormalArmyPersonalityApplicationOutcome { CurrentInputsUnresolved, ResolvedWithoutWrite, Committed }
public sealed record OriginalNormalArmyPersonalityActivation(int Index, int PersonalityPhysicalSlot,
    ushort MaskBefore, ushort MaskAfter, ushort RawDurationB4, ushort TimerAfter);
public sealed record OriginalNormalArmyPersonalityApplication(OriginalNormalArmyPersonalityApplicationOutcome Outcome,
    GameState State, OriginalNormalArmyPersonalityReactionReceipt? Receipt,
    OriginalNormalArmyPersonalityActivation[] Activations, string? MissingOwner);

/// <summary>TC1795xx's ordinary nonlethal nine-slot writes, including17A120
/// clamp0..720. This independent client has no original manager20 presentation
/// receiver; activation writes remain separate from later combat/report tails.</summary>
public static class OriginalNormalArmyPersonalityApplicationSource
{
    public static OriginalNormalArmyPersonalityApplication TryApply(GameState state, UnitState target,
        OriginalTacticRemakeCurrentContext current, OriginalEventInitializationContext definitions, string revision)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(target);
        if (current.ArmyValid(state, target) != true || target.OfficerId is < 0 or > 6000 ||
            definitions.ReadPersonLookupReference(target.OfficerId)?.PhysicalSlot is not int physical ||
            definitions.ReadPersonIdentity(physical) != target.OfficerId ||
            definitions.ReadPersonNativeValidity(physical) is not bool valid)
            return Unknown("current.Commander_validity");
        if (!valid) return new(OriginalNormalArmyPersonalityApplicationOutcome.ResolvedWithoutWrite, state,
            new(target.Id, target.OfficerId, false, [], revision), [], null);
        var steps = new List<OriginalNormalArmyPersonalityReactionStep>();
        var activations = new List<OriginalNormalArmyPersonalityActivation>();
        ushort? mask = null; ushort[]? timers = null;
        for (int index = 0; index < 9; index++)
        {
            if (OriginalTacticRemakeCurrentTraits.ResolveCommanderPersonality(definitions, physical, index) is not { } trait ||
                definitions.ReadSkillPersonalityValidity(trait) is not bool traitValid)
                return Unknown("current.Personality_validity");
            if (!traitValid) { Add(OriginalNormalArmyPersonalityReactionStepOutcome.InvalidPersonality); continue; }
            if (definitions.ReadPersonalityByte(trait, 0xA4) is not byte category) return Unknown("current.Personality_A4");
            if (category != 0x1E) { Add(OriginalNormalArmyPersonalityReactionStepOutcome.DifferentCategory, category); continue; }
            ushort? currentTimer = timers is null ? definitions.ReadPersonTraitExtra1BE(physical, index) : timers[index];
            if (currentTimer is not ushort timer) return Unknown("current.Person_timer1BE");
            if (timer > 0) { Add(OriginalNormalArmyPersonalityReactionStepOutcome.TimerRunning, category, timer); continue; }
            mask ??= definitions.ReadPersonTraitMask1BC(physical);
            if (mask is not ushort active) return Unknown("current.Person_mask1BC");
            if ((active & (1 << index)) != 0)
            { Add(OriginalNormalArmyPersonalityReactionStepOutcome.AlreadyActive, category, timer, active); continue; }
            if (definitions.ReadSkillPersonalityFlags(trait) is not byte flags) return Unknown("current.Personality_flags");
            if ((flags & 4) == 0)
            { Add(OriginalNormalArmyPersonalityReactionStepOutcome.ActivationFlagDisabled, category, timer, active, flags); continue; }
            if (trait.PhysicalSlot is not int traitPhysical || definitions.ReadPersonalityWord(trait, 0xB4) is not ushort duration)
                return Unknown("current.Personality_durationB4");
            if (state.Officers[target.OfficerId].NativeLocationRuntime is not { TraitExtras1BE: { Length: 9 } ownerTimers })
                return Unknown("current.saved_Person_activation_owner");
            timers ??= ownerTimers.ToArray();
            ushort after = checked((ushort)(active | (1 << index)));
            ushort afterTimer = (ushort)Math.Min(720, (int)duration);
            timers[index] = afterTimer; mask = after;
            activations.Add(new(index, traitPhysical, active, after, duration, afterTimer));
            Add(OriginalNormalArmyPersonalityReactionStepOutcome.ActivationCallbackRequired, category, timer, active, flags);

            void Add(OriginalNormalArmyPersonalityReactionStepOutcome outcome, byte? cat = null,
                ushort? time = null, ushort? activeMask = null, byte? flag = null) =>
                steps.Add(new(index, trait.PhysicalSlot, outcome, cat, time, activeMask, flag));
        }
        var receipt = new OriginalNormalArmyPersonalityReactionReceipt(target.Id, target.OfficerId, true, steps.ToArray(), revision);
        if (activations.Count == 0) return new(OriginalNormalArmyPersonalityApplicationOutcome.ResolvedWithoutWrite, state, receipt, [], null);
        OfficerState commander = state.Officers[target.OfficerId];
        GameState result = state with { Officers = new(state.Officers) { [commander.Id] = commander with
            { NativeLocationRuntime = commander.NativeLocationRuntime! with { TraitMask1BC = mask, TraitExtras1BE = timers } } } };
        NativeArmyReturnCurrentStateValidation.Validate(result);
        return new(OriginalNormalArmyPersonalityApplicationOutcome.Committed, result, receipt, activations.ToArray(), null);

        OriginalNormalArmyPersonalityApplication Unknown(string owner) =>
            new(OriginalNormalArmyPersonalityApplicationOutcome.CurrentInputsUnresolved, state, null, [], owner);
    }
}
