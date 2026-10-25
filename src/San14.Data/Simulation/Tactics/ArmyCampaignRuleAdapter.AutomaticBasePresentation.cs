using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>168C40 consumes the actual D8 actor/result payload into the
    /// existing effect/Direction owner. No target is reselected for display,
    /// and no receipt or empty-queue observation is manufactured here.</summary>
    private AutomaticTacticLinkedActorResult CaptureBorrowedAutomaticActorResult(GameState state,
        UnitState actor, int tacticSlot, List<TacticResultPresentation>[] results,
        List<TacticEffectPresentation> armyEffects)
    {
        int physical = actor.NativeArmyOrigin?.NativeSlotIndex ?? throw BaseAutomaticMissing("presentation_actor_slot");
        var arena = state.ReadArmyLifecycle() ?? throw BaseAutomaticMissing("presentation_Army_roster");
        var source = arena.Slots[physical];
        var receiver = arena.Receivers.Find(source.ReceiverId) ?? throw BaseAutomaticMissing("presentation_actor_receiver");
        if (receiver.ActiveUnitId != actor.Id) throw BaseAutomaticMissing("presentation_actor_identity");
        return new(actor.Id, receiver.ReceiverId, tacticSlot, results[0].ToImmutableArray(),
            results[1].ToImmutableArray(), armyEffects.ToImmutableArray(), physical, true);
    }

    private GameState EnqueueBorrowedAutomaticLinkedPresentation(GameState state, SourceWorldContext current,
        TacticRemakeCurrentContext context, IReadOnlyList<AutomaticTacticLinkedActorResult> actors, int selectedHex)
    {
        var manager = state.ManualTacticIntervention ?? throw BaseAutomaticMissing("effect_manager168C40");
        if (manager.Effects.Length >= 563) return state;
        var main = actors[0];
        bool direction = false;
        if (context.IsAutomaticSpecialPresentationTactic(main.TacticId))
        {
            var actor = state.Units.GetValueOrDefault(main.CasterUnitId) ?? throw BaseAutomaticMissing("presentation_main_Army");
            direction = _automaticPresentationAdmission?.ReadUniqueDirectionAdmission(state, actor, main.TacticId)
                ?? throw BaseAutomaticMissing("unique_direction_admission369580");
        }
        var presentations = actors.Where(actor => actor.WasExecuted).Select(actor =>
        {
            var ordered = new[] { actor.FirstInstructionResults.ToArray(), actor.SecondInstructionResults.ToArray() };
            var armies = ordered.Select(row => row.Where(item => item.TargetKind == TacticResultTargetKind.ArmyReceiver)
                .Select(item => item.TargetId).ToArray()).ToArray();
            return new TacticActorPresentation(actor.Actor, actor.TacticId,
                new(ManualTacticTargetKind.Hex, selectedHex), armies)
                { InstructionResults = ordered, Effects = actor.ArmyPresentationEffects.ToArray() };
        }).ToArray();
        if (presentations.Length == 0) throw BaseAutomaticMissing("presentation_no_executed_actor");
        manager = manager.Enqueue(direction ? TacticPresentationKind.UniqueDirection : TacticPresentationKind.OrdinaryEffects,
            presentations);
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
