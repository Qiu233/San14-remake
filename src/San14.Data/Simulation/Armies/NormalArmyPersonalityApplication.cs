using San14.Core;

namespace San14.Data;

public enum NormalArmyPersonalityApplicationOutcome { CurrentInputsUnresolved, ResolvedWithoutWrite, Committed }
public sealed record NormalArmyPersonalityActivation(int Index, int PersonalityPhysicalSlot,
    ushort MaskBefore, ushort MaskAfter, ushort RawDurationB4, ushort TimerAfter);
public sealed record NormalArmyPersonalityApplication(NormalArmyPersonalityApplicationOutcome Outcome,
    GameState State, NormalArmyPersonalityReactionReceipt? Receipt,
    NormalArmyPersonalityActivation[] Activations, string? MissingOwner);

/// <summary>TC1795xx's ordinary nonlethal nine-slot writes, including17A120
/// clamp0..720. This independent client has no original manager20 presentation
/// receiver; activation writes remain separate from later combat/report tails.</summary>
public static class NormalArmyPersonalityApplicationSource
{
    public static NormalArmyPersonalityApplication TryApply(GameState state, UnitState target,
        TacticRemakeCurrentContext current, SourceWorldContext definitions, string revision)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(target);
        if (current.ArmyValid(state, target) != true || target.OfficerId is < 0 or > 6000 ||
            definitions.ReadPersonLookupReference(target.OfficerId)?.PhysicalSlot is not int physical ||
            definitions.ReadPersonIdentity(physical) != target.OfficerId ||
            definitions.ReadPersonNativeValidity(physical) is not bool valid)
            return Unknown("current.Commander_validity");
        if (!valid) return new(NormalArmyPersonalityApplicationOutcome.ResolvedWithoutWrite, state,
            new(target.Id, target.OfficerId, false, [], revision), [], null);
        var steps = new List<NormalArmyPersonalityReactionStep>();
        var activations = new List<NormalArmyPersonalityActivation>();
        ushort? mask = null; ushort[]? timers = null;
        for (int index = 0; index < 9; index++)
        {
            if (TacticRemakeCurrentTraits.ResolveCommanderPersonality(definitions, physical, index) is not { } trait ||
                definitions.ReadSkillPersonalityValidity(trait) is not bool traitValid)
                return Unknown("current.Personality_validity");
            if (!traitValid) { Add(NormalArmyPersonalityReactionStepOutcome.InvalidPersonality); continue; }
            if (definitions.ReadPersonalityByte(trait, 0xA4) is not byte category) return Unknown("current.Personality_A4");
            if (category != 0x1E) { Add(NormalArmyPersonalityReactionStepOutcome.DifferentCategory, category); continue; }
            ushort? currentTimer = timers is null ? definitions.ReadPersonTraitExtra1BE(physical, index) : timers[index];
            if (currentTimer is not ushort timer) return Unknown("current.Person_timer1BE");
            if (timer > 0) { Add(NormalArmyPersonalityReactionStepOutcome.TimerRunning, category, timer); continue; }
            mask ??= definitions.ReadPersonTraitMask1BC(physical);
            if (mask is not ushort active) return Unknown("current.Person_mask1BC");
            if ((active & (1 << index)) != 0)
            { Add(NormalArmyPersonalityReactionStepOutcome.AlreadyActive, category, timer, active); continue; }
            if (definitions.ReadSkillPersonalityFlags(trait) is not byte flags) return Unknown("current.Personality_flags");
            if ((flags & 4) == 0)
            { Add(NormalArmyPersonalityReactionStepOutcome.ActivationFlagDisabled, category, timer, active, flags); continue; }
            if (trait.PhysicalSlot is not int traitPhysical || definitions.ReadPersonalityWord(trait, 0xB4) is not ushort duration)
                return Unknown("current.Personality_durationB4");
            if (state.Officers[target.OfficerId].NativeLocationRuntime is not { TraitExtras1BE: { Length: 9 } ownerTimers })
                return Unknown("current.saved_Person_activation_owner");
            timers ??= ownerTimers.ToArray();
            ushort after = checked((ushort)(active | (1 << index)));
            ushort afterTimer = (ushort)Math.Min(720, (int)duration);
            timers[index] = afterTimer; mask = after;
            activations.Add(new(index, traitPhysical, active, after, duration, afterTimer));
            Add(NormalArmyPersonalityReactionStepOutcome.ActivationCallbackRequired, category, timer, active, flags);

            void Add(NormalArmyPersonalityReactionStepOutcome outcome, byte? cat = null,
                ushort? time = null, ushort? activeMask = null, byte? flag = null) =>
                steps.Add(new(index, trait.PhysicalSlot, outcome, cat, time, activeMask, flag));
        }
        var receipt = new NormalArmyPersonalityReactionReceipt(target.Id, target.OfficerId, true, steps.ToArray(), revision);
        if (activations.Count == 0) return new(NormalArmyPersonalityApplicationOutcome.ResolvedWithoutWrite, state, receipt, [], null);
        OfficerState commander = state.Officers[target.OfficerId];
        GameState result = state with { Officers = new(state.Officers) { [commander.Id] = commander with
            { NativeLocationRuntime = commander.NativeLocationRuntime! with { TraitMask1BC = mask, TraitExtras1BE = timers } } } };
        NativeArmyReturnCurrentStateValidation.Validate(result);
        return new(NormalArmyPersonalityApplicationOutcome.Committed, result, receipt, activations.ToArray(), null);

        NormalArmyPersonalityApplication Unknown(string owner) =>
            new(NormalArmyPersonalityApplicationOutcome.CurrentInputsUnresolved, state, null, [], owner);
    }
}
