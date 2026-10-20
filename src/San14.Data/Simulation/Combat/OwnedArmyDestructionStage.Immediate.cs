using San14.Core;

namespace San14.Data;

public sealed partial class OwnedArmyDestructionStage
{
    /// <summary>31F160 Full falls through directly to one259360 invocation.
    /// The new request is deliberately not in manager140. Neither the existing
    /// queue nor requests created by real callbacks are cleared or consumed.</summary>
    internal GameState ApplyBattleDefeatCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, UnitDefeatRequest request, TacticSourceSamplerWorld? sampler,
        NativeMapSeasonDate sourceDate, List<CampaignEffectRequest> feedback,
        Action<ArmyUnitReference> refresh, Action<int> personChanged)
    {
        var queue = current.OwnedDestructionQueue ?? state.PendingCombatSettlements
            ?? throw Missing("immediate_current_manager140");
        current.BindOwnedDestructionQueue(queue, state);
        if (ArmyDestructionQueue.ResolveUnitSlot(state, request.VictimUnitId) is not int physical) return state;
        var victim = new ArmyUnitReference(physical);
        if (!Known(current.ReadArmyUnitNativeValidity(victim), "immediate_victim_validity") ||
            ArmyPending70Admission.ContainsCurrentPointer(new(RegistryKind.Army, physical), new Pending(current))) return state;
        var result = DestroyCurrent(state, current, pool, request, physical, sampler, sourceDate,
            feedback, refresh, personChanged, projectOwnedRoles: true);
        queue = current.OwnedDestructionQueue ?? throw Missing("immediate_postcallback_manager140");
        return PublishCurrent(result.State, current, pool, [physical], queue, projectOwnedRoles: true);
    }
}
