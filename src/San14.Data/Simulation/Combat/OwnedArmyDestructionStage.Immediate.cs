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
        Action<ArmyUnitReference> refresh, Action<int> personChanged)
        => ApplyCurrentDefeat(state, current, pool, request, sampler, sourceDate, feedback,
            refresh, personChanged, forcedDuelCapture: false).State;

    /// <summary>The actual cause2 current consumer. Queue admission/dispatch
    /// must preserve that cause separately; an ordinary cause0 request must
    /// never reach this entry merely because its victim lost a duel.</summary>
    internal OwnedArmyCurrentDefeatResult ApplyForcedDuelCaptureCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, UnitDefeatRequest request, TacticSourceSamplerWorld? sampler,
        NativeMapSeasonDate sourceDate, List<CampaignEffectRequest> feedback,
        Action<ArmyUnitReference> refresh, Action<int> personChanged)
        => ApplyCurrentDefeat(state, current, pool, request, sampler, sourceDate, feedback,
            refresh, personChanged, forcedDuelCapture: true);

    private OwnedArmyCurrentDefeatResult ApplyCurrentDefeat(GameState state, SourceWorldContext current,
        WorldIndexContext pool, UnitDefeatRequest request, TacticSourceSamplerWorld? sampler,
        NativeMapSeasonDate sourceDate, List<CampaignEffectRequest> feedback,
        Action<ArmyUnitReference> refresh, Action<int> personChanged, bool forcedDuelCapture)
    {
        var queue = current.OwnedDestructionQueue ?? state.PendingCombatSettlements
            ?? throw Missing("immediate_current_manager140");
        current.BindOwnedDestructionQueue(queue, state);
        if (ArmyDestructionQueue.ResolveUnitSlot(state, request.VictimUnitId) is not int physical) return new(state, null);
        var victim = new ArmyUnitReference(physical);
        if (!Known(current.ReadArmyUnitNativeValidity(victim), "immediate_victim_validity") ||
            ArmyPending70Admission.ContainsCurrentPointer(new(RegistryKind.Army, physical), new Pending(current))) return new(state, null);
        var result = DestroyCurrent(state, current, pool, request, physical, sampler, sourceDate,
            feedback, refresh, personChanged, projectOwnedRoles: true, forcedDuelCapture: forcedDuelCapture);
        queue = current.OwnedDestructionQueue ?? throw Missing("immediate_postcallback_manager140");
        return new(PublishCurrent(result.State, current, pool, [physical], queue, projectOwnedRoles: true), result.Receipt);
    }
}
