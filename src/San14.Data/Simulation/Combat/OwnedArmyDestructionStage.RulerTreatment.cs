using San14.Core;

namespace San14.Data;

public sealed partial class OwnedArmyDestructionStage
{
    private ArmyDefeatContinuation CreateDefeatCheckpoint(GameState state, SourceWorldContext current,
        UnitDefeatRequest request, int physical, int commanderId, ForceReference force,
        ArmyDestructionResourceOwners captured, int hex, ArmyUnitReference winner, int? winnerCommander,
        BaseReference? winnerBase, TacticSourceSamplerWorld? sampler, ArmyDefeatResumePoint point, int immunity,
        bool specialCommanderReclaim, bool? reclaimDefeatedCommander)
    {
        var arena = state.ReadArmyLifecycle() ?? throw Missing("checkpoint_lifecycle");
        SettlementBase? location = winnerBase is null ? null : new(
            winnerBase.Kind == BaseKind.Gate ? SettlementBaseKind.Gate : SettlementBaseKind.City,
            Known(current.ReadBaseObjectLink(winnerBase), "checkpoint_winner_Base4E"));
        return new(-1, request, physical, arena.Slots[physical].ReceiverId,
            Known(force.PhysicalSlot, "checkpoint_victim_Force"), commanderId,
            ReadDefeatReportCity(current, captured.VictimHome), hex, winner.PhysicalSlot,
            winnerCommander, location, captured.WinnerForce.PhysicalSlot, sampler?.Day450, sampler?.Salt45C,
            point, immunity)
        { VictimHome = CaptureDefeatHome(current, captured.VictimHome),
          SpecialCommanderReclaim = specialCommanderReclaim,
          ReclaimDefeatedCommander = point == ArmyDefeatResumePoint.AfterPersonDeath ? null : reclaimDefeatedCommander };
    }

    private static SettlementBase? CaptureDefeatHome(SourceWorldContext current, BaseReference home)
        => home.PhysicalSlot is > 0 ? new(
            home.Kind == BaseKind.Gate ? SettlementBaseKind.Gate : SettlementBaseKind.City,
            Known(current.ReadBaseObjectLink(home), "checkpoint_Home_Base4E")) : null;

    private static BaseReference ResolveDefeatHome(GameState state, SourceWorldContext current,
        ArmyDefeatContinuation checkpoint) => checkpoint.VictimHome is { } home
            ? ArmyDestructionQueue.ResolveBaseReference(state, current, home)
            : new(BaseKind.City, null);

    private static int ReadDefeatReportCity(SourceWorldContext current, BaseReference home)
        => home.PhysicalSlot is not > 0 ? 0 :
            Known((home.Kind == BaseKind.City ? home : CurrentGateMapQueries.FindCity(home, current)).PhysicalSlot,
                "checkpoint_report_City");

    private (GameState State, OwnedArmyDestructionReceipt? Receipt) ResumeRulerTreatmentTail(
        GameState state, SourceWorldContext current, WorldIndexContext pool, ArmyDefeatContinuation checkpoint,
        NativeMapSeasonDate? date, List<CampaignEffectRequest> feedback)
    {
        var lifecycle = state.ReadArmyLifecycle() ?? throw Missing("ruler_treatment_lifecycle");
        if (lifecycle.Slots[checkpoint.ArmyPhysicalSlot].ReceiverId != checkpoint.ReceiverId)
            throw Missing("ruler_treatment_receiver_changed");
        if (checkpoint.PendingCapturedRulerTreatment is not null || state.PersonDeathContinuation is not null)
            throw Missing("ruler_treatment_actual_player_decision");
        if (checkpoint.ResumePoint == ArmyDefeatResumePoint.AfterSpecialCaptureAnnouncement)
            return ResumeHiredCaptureAnnouncementCurrent(state, current, pool, checkpoint, date, feedback);
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
