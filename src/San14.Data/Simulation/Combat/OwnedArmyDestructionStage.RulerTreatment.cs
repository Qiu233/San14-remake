using San14.Core;

namespace San14.Data;

public sealed partial class OwnedArmyDestructionStage
{
    private ArmyDefeatContinuation CreateDefeatCheckpoint(GameState state, SourceWorldContext current,
        UnitDefeatRequest request, int physical, int commanderId, ForceReference force,
        ArmyDestructionResourceOwners captured, int hex, ArmyUnitReference winner, int? winnerCommander,
        BaseReference? winnerBase, TacticSourceSamplerWorld? sampler, ArmyDefeatResumePoint point, int immunity)
    {
        var arena = state.ReadArmyLifecycle() ?? throw Missing("checkpoint_lifecycle");
        SettlementBase? location = winnerBase is null ? null : new(
            winnerBase.Kind == BaseKind.Gate ? SettlementBaseKind.Gate : SettlementBaseKind.City,
            Known(current.ReadBaseObjectLink(winnerBase), "checkpoint_winner_Base4E"));
        return new(-1, request, physical, arena.Slots[physical].ReceiverId,
            Known(force.PhysicalSlot, "checkpoint_victim_Force"), commanderId,
            Known(captured.VictimHome.PhysicalSlot, "checkpoint_Home_City"), hex, winner.PhysicalSlot,
            winnerCommander, location, captured.WinnerForce.PhysicalSlot, sampler?.Day450, sampler?.Salt45C,
            point, immunity);
    }

    private (GameState State, OwnedArmyDestructionReceipt? Receipt) ResumeRulerTreatmentTail(
        GameState state, SourceWorldContext current, WorldIndexContext pool, ArmyDefeatContinuation checkpoint,
        NativeMapSeasonDate? date, List<CampaignEffectRequest> feedback)
    {
        var lifecycle = state.ReadArmyLifecycle() ?? throw Missing("ruler_treatment_lifecycle");
        if (lifecycle.Slots[checkpoint.ArmyPhysicalSlot].ReceiverId != checkpoint.ReceiverId)
            throw Missing("ruler_treatment_receiver_changed");
        if (checkpoint.PendingCapturedRulerTreatment is not null || state.PersonDeathContinuation is not null)
            throw Missing("ruler_treatment_actual_player_decision");
        if (checkpoint.ResumePoint == ArmyDefeatResumePoint.AfterPersonDeath)
        {
            state = state with { ArmyDefeatContinuation = null };
            if (checkpoint.DeathReturnedEarly) return (state, null);
            TacticSourceSamplerWorld? sampler = checkpoint.SampleDay is int day && checkpoint.SampleSalt is int salt
                ? new(day, salt) : null;
            return DestroyCurrent(state, current, pool, checkpoint.Defeat, checkpoint.ArmyPhysicalSlot,
                sampler, date, feedback, projectOwnedRoles: true,
                forcedDuelCapture: checkpoint.Defeat.Cause == UnitDefeatCause.DuelCapture,
                duelDeath: checkpoint.Defeat.Cause == UnitDefeatCause.DuelDeath,
                allowPlayerRulerPause: true, resumed: checkpoint);
        }
        if (checkpoint.ChosenDisposition is null)
            throw Missing("ruler_treatment_chosen_disposition");
        return CompleteDestructionTail(state with { ArmyDefeatContinuation = null }, current, pool,
            checkpoint, date, feedback, projectOwnedRoles: true);
    }

    internal static GameState PublishRulerTreatmentCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool) => PublishCurrent(state, current, pool, [],
            current.OwnedDestructionQueue ?? state.PendingCombatSettlements ?? throw Missing("ruler_treatment_queue"),
            projectOwnedRoles: true);
}
