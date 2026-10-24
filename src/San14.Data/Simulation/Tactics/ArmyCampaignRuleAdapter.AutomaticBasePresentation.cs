using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>168C40 consumes the actual D8 actor/result payload into the
    /// existing effect/Direction owner. No target is reselected for display,
    /// and no receipt or empty-queue observation is manufactured here.</summary>
    private GameState EnqueueBorrowedAutomaticBasePresentation(GameState state, SourceWorldContext current,
        TacticRemakeCurrentContext context, UnitState actor, int tacticSlot, int selectedHex, List<TacticResultPresentation>[] results,
        List<TacticEffectPresentation> armyEffects)
    {
        var manager = state.ManualTacticIntervention ?? throw BaseAutomaticMissing("effect_manager168C40");
        if (manager.Effects.Length >= 563) return state; //Actual outer capacity branch.
        bool direction = false;
        if (context.IsAutomaticSpecialPresentationTactic(tacticSlot))
            direction = _automaticPresentationAdmission?.ReadUniqueDirectionAdmission(state, actor, tacticSlot)
                ?? throw BaseAutomaticMissing("unique_direction_admission369580");
        int physical = actor.NativeArmyOrigin?.NativeSlotIndex ?? throw BaseAutomaticMissing("presentation_actor_slot");
        var arena = state.ReadArmyLifecycle() ?? throw BaseAutomaticMissing("presentation_Army_roster");
        var source = arena.Slots[physical];
        var receiver = arena.Receivers.Find(source.ReceiverId) ?? throw BaseAutomaticMissing("presentation_actor_receiver");
        if (receiver.ActiveUnitId != actor.Id) throw BaseAutomaticMissing("presentation_actor_identity");
        var ordered = results.Select(row => row.ToArray()).ToArray();
        var armies = ordered.Select(row => row.Where(item => item.TargetKind == TacticResultTargetKind.ArmyReceiver)
            .Select(item => item.TargetId).ToArray()).ToArray();
        var presentation = new TacticActorPresentation(receiver.ReceiverId, tacticSlot,
            new(ManualTacticTargetKind.Hex, selectedHex), armies)
        { InstructionResults = ordered, Effects = armyEffects.ToArray() };
        manager = manager.Enqueue(direction ? TacticPresentationKind.UniqueDirection : TacticPresentationKind.OrdinaryEffects,
            [presentation]);
        return state with { ManualTacticIntervention = manager };
    }

    /// <summary>The actual16D9F0/+98 setter after a true178EC0 return. Its
    /// already-owned queues are retained, including the later168C40 insertion.
    /// The existing case6 owner alone may clear busy after real consumption.</summary>
    internal static GameState MarkBorrowedAutomaticTacticFatalFeedback(GameState state)
    {
        var manager = state.ManualTacticIntervention ?? throw BaseAutomaticMissing("fatal_effect_manager16D9F0");
        return state with { ManualTacticIntervention = manager with { EffectBusy = true } };
    }
}

public sealed partial class TacticRemakeCurrentContext
{
    internal bool IsAutomaticSpecialPresentationTactic(int tacticSlot)
    {
        var reference = _definitions.ReadSkillTacticReference(tacticSlot)
            ?? throw new InvalidDataException("automatic_Base_presentation.Tactic_pointer");
        int physical = _definitions.ReadSkillTacticPhysicalSlot(reference)
            ?? throw new InvalidDataException("automatic_Base_presentation.Tactic_identity");
        //233820 reads the actual current definition receiver, not a name,
        //program opcode or a hardcoded equipped-tactic observation.
        return physical is >= 31 and <= 130 or >= 140 and <= 200;
    }
}
