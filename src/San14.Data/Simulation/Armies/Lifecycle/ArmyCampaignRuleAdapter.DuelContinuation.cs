using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState ResumeDuelAftermathDuringArmyOperation(GameState state,
        NativeArmyProgressStageMoment moment)
    {
        var saved = state.DuelAftermathContinuation ?? throw DuelMissing("resume_aftermath_owner");
        var registered = state.CampaignDuelService?.Registered ?? throw DuelMissing("resume_registered_result");
        if (registered.Sequence != saved.DuelSequence || registered.Date != moment.PreWriterThirdDate ||
            registered.CalendarCycleOrdinal != moment.CalendarCycleOrdinal ||
            state.CampaignDuelService is not { Active: true, Busy: true } ||
            state.ArmyDefeatContinuation is not { SettlementIndex: -1 } defeat)
            throw DuelMissing("resume_registered_lifecycle");

        //The source's loser receiver was captured before2709A0/259360. The
        //explicit ruler decision may already have changed the Person's Home.
        var originalLoserArmy = new ArmyUnitReference(defeat.ArmyPhysicalSlot);
        var current = CaptureWritableTransportRouteCurrent(state);
        var pool = current.ReadOwnedCurrentListPool() ?? throw DuelMissing("resume_shared_pool");
        _baseCapacityInputs.BindCurrent(state, current);
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        OwnedArmyDestructionCurrent.BindOwnedCurrentContext(state, current);
        if (current.ReadOwnedPersonCommandManager() is null)
            PersonReturnCommands.BindCurrent(state, current);
        BindActorlessDestructionInputs(state, current);

        var effects = new List<CampaignEffectRequest>();
        state = new OwnedArmyDestructionStage(this).ResumeImmediateDefeatCurrent(state, current, pool,
            moment.PreWriterThirdDate, effects);
        if (effects.Count != 0)
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests
                ?? throw DuelMissing("resume_effect_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                    moment.CalendarCycleOrdinal, effects) };

        //Only the saved loser tail and rewards remain. Registration, winner
        //aftermath, result history and the opportunity/duel draws are complete.
        state = CompleteDuelAftermathDuringArmyOperation(state with { DuelAftermathContinuation = null },
            current, moment, saved, originalLoserArmy);
        var lifecycle = state.ReadArmyLifecycle() ?? throw DuelMissing("resume_saved_lifecycle");
        return state.WithArmyLifecycle(lifecycle with { Indexes = pool.ExportOwnedState() });
    }
}
