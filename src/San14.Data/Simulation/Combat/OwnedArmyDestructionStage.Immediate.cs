using San14.Core;

namespace San14.Data;

internal sealed record OwnedArmyCurrentDefeatResult(GameState State, OwnedArmyDestructionReceipt? Receipt);

public sealed partial class OwnedArmyDestructionStage
{
    /// <summary>31F160 Full falls through directly to one259360 invocation.
    /// The new request is deliberately not in manager140. Neither the existing
    /// queue nor requests created by real callbacks are cleared or consumed.</summary>
    internal GameState ApplyBattleDefeatCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, UnitDefeatRequest request, TacticSourceSamplerWorld? sampler,
        NativeMapSeasonDate sourceDate, List<CampaignEffectRequest> feedback,
        Action<ArmyUnitReference> refresh, Action<int> personChanged, bool allowPlayerRulerPause = false)
        => ApplyCurrentDefeat(state, current, pool, request, sampler, sourceDate, feedback,
            refresh, personChanged, forcedDuelCapture: false, allowPlayerRulerPause: allowPlayerRulerPause).State;

    /// <summary>The actual cause2 current consumer. Queue admission/dispatch
    /// must preserve that cause separately; an ordinary cause0 request must
    /// never reach this entry merely because its victim lost a duel.</summary>
    internal OwnedArmyCurrentDefeatResult ApplyForcedDuelCaptureCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, UnitDefeatRequest request, TacticSourceSamplerWorld? sampler,
        NativeMapSeasonDate sourceDate, List<CampaignEffectRequest> feedback,
        Action<ArmyUnitReference> refresh, Action<int> personChanged, bool allowPlayerRulerPause = false)
        => ApplyCurrentDefeat(state, current, pool, request, sampler, sourceDate, feedback,
            refresh, personChanged, forcedDuelCapture: true, allowPlayerRulerPause: allowPlayerRulerPause);

    private OwnedArmyCurrentDefeatResult ApplyCurrentDefeat(GameState state, SourceWorldContext current,
        WorldIndexContext pool, UnitDefeatRequest request, TacticSourceSamplerWorld? sampler,
        NativeMapSeasonDate sourceDate, List<CampaignEffectRequest> feedback,
        Action<ArmyUnitReference> refresh, Action<int> personChanged, bool forcedDuelCapture,
        bool allowPlayerRulerPause = false)
    {
        var queue = current.OwnedDestructionQueue ?? state.PendingCombatSettlements
            ?? throw Missing("immediate_current_manager140");
        current.BindOwnedDestructionQueue(queue, state);
        if (ArmyDestructionQueue.ResolveUnitSlot(state, request.VictimUnitId) is not int physical) return new(state, null);
        var victim = new ArmyUnitReference(physical);
        if (!Known(current.ReadArmyUnitNativeValidity(victim), "immediate_victim_validity") ||
            ArmyPending70Admission.ContainsCurrentPointer(new(RegistryKind.Army, physical), new Pending(current))) return new(state, null);
        var result = DestroyCurrent(state, current, pool, request, physical, sampler, sourceDate,
            feedback, refresh, personChanged, projectOwnedRoles: true, forcedDuelCapture: forcedDuelCapture,
            duelDeath: request.Cause == UnitDefeatCause.DuelDeath, allowPlayerRulerPause: allowPlayerRulerPause);
        queue = current.OwnedDestructionQueue ?? throw Missing("immediate_postcallback_manager140");
        return new(PublishCurrent(result.State, current, pool,
            result.Receipt is null ? [] : [physical], queue, projectOwnedRoles: true), result.Receipt);
    }

    internal GameState ResumeImmediateDefeatCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, NativeMapSeasonDate sourceDate, List<CampaignEffectRequest> feedback)
    {
        var saved = state.ArmyDefeatContinuation ?? throw Missing("immediate_resume_owner");
        if (saved.SettlementIndex != -1) throw Missing("immediate_resume_not_queued");
        var result = ResumeRulerTreatmentTail(state, current, pool, saved, sourceDate, feedback);
        var queue = current.OwnedDestructionQueue ?? state.PendingCombatSettlements
            ?? throw Missing("immediate_resume_queue");
        return PublishCurrent(result.State, current, pool, result.Receipt is null ? [] : [saved.ArmyPhysicalSlot],
            queue, projectOwnedRoles: true);
    }
}
