using San14.Core;

namespace San14.Data;

public enum OriginalNormalArmyPersonalityReactionStepOutcome
{
    InvalidPersonality, DifferentCategory, TimerRunning, AlreadyActive, ActivationFlagDisabled,
    ActivationCallbackRequired
}

public sealed record OriginalNormalArmyPersonalityReactionStep(int Index, int? PersonalityPhysicalSlot,
    OriginalNormalArmyPersonalityReactionStepOutcome Outcome, byte? CategoryA4,
    ushort? Timer1BE, ushort? ActiveMask1BC, byte? PersonalityFlags);

public sealed record OriginalNormalArmyPersonalityReactionReceipt(int ArmyId, int CommanderId,
    bool CommanderNativeValid, OriginalNormalArmyPersonalityReactionStep[] OrderedSteps, string Revision)
{
    public bool RequiresActivationCallback => OrderedSteps.Any(step =>
        step.Outcome == OriginalNormalArmyPersonalityReactionStepOutcome.ActivationCallbackRequired);
}

/// <summary>17921B..179329's nonlethal target-commander personality reads.
/// Complete known no-reaction branches resolve without changing the state.
/// An actual activation flag requires179E80; no1BC bit is published before an
/// unowned callback, and this receipt never completes UI/death/stage8 tails.</summary>
public static class OriginalNormalArmyPersonalityReaction
{
    public static OriginalNormalArmyPersonalityReactionReceipt? Resolve(GameState state, UnitState target,
        OriginalTacticRemakeCurrentContext current, OriginalEventInitializationContext definitions, string revision)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(definitions);
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Normal personality needs source revision.");
        if (current.ArmyValid(state, target) != true || target.OfficerId is < 0 or > 6000 ||
            definitions.ReadPersonLookupReference(target.OfficerId)?.PhysicalSlot is not int physical ||
            definitions.ReadPersonIdentity(physical) != target.OfficerId ||
            definitions.ReadPersonNativeValidity(physical) is not bool valid)
            return null;
        if (!valid) return new(target.Id, target.OfficerId, false, [], revision);
        var steps = new List<OriginalNormalArmyPersonalityReactionStep>();
        for (int index = 0; index < 9; index++)
        {
            if (OriginalTacticRemakeCurrentTraits.ResolveCommanderPersonality(definitions, physical, index) is not { } trait ||
                definitions.ReadSkillPersonalityValidity(trait) is not bool traitValid)
                return null;
            if (!traitValid) { Add(OriginalNormalArmyPersonalityReactionStepOutcome.InvalidPersonality); continue; }
            if (definitions.ReadPersonalityByte(trait, 0xA4) is not byte category) return null;
            if (category != 0x1E) { Add(OriginalNormalArmyPersonalityReactionStepOutcome.DifferentCategory, category); continue; }
            if (definitions.ReadPersonTraitExtra1BE(physical, index) is not ushort timer) return null;
            if (timer > 0) { Add(OriginalNormalArmyPersonalityReactionStepOutcome.TimerRunning, category, timer); continue; }
            if (definitions.ReadPersonTraitMask1BC(physical) is not ushort active) return null;
            if ((active & (1 << index)) != 0)
            { Add(OriginalNormalArmyPersonalityReactionStepOutcome.AlreadyActive, category, timer, active); continue; }
            if (definitions.ReadSkillPersonalityFlags(trait) is not byte flags) return null;
            Add((flags & 4) == 0 ? OriginalNormalArmyPersonalityReactionStepOutcome.ActivationFlagDisabled :
                OriginalNormalArmyPersonalityReactionStepOutcome.ActivationCallbackRequired, category, timer, active, flags);
            if ((flags & 4) != 0) break; //Later slots read the output of unclosed179E80.

            void Add(OriginalNormalArmyPersonalityReactionStepOutcome outcome, byte? cat = null,
                ushort? time = null, ushort? mask = null, byte? flag = null) =>
                steps.Add(new(index, trait.PhysicalSlot, outcome, cat, time, mask, flag));
        }
        return new(target.Id, target.OfficerId, true, steps.ToArray(), revision);
    }
}
