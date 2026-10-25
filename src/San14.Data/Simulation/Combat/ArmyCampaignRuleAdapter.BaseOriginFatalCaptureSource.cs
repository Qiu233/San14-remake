using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //1745B0 retains the Base source's actual virtual80 District. The source
    //Base is not an Army, and the full queue settles just this2851B0 call.
    private GameState ApplyBaseOriginFatalCaptureCurrent(GameState state, SourceWorldContext current,
        BaseReference target, DistrictReference district, NativeArmyCombatCycleSampler sampler,
        NativeMapSeasonDate date, long revision, List<CampaignEffectRequest> effects)
    {
        var entity = ArmyDestructionQueue.ResolveBase(current, target)
            ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("capture_target_identity");
        int districtId = district.PhysicalSlot
            ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("capture_District_identity");
        var request = new BaseCaptureRequest(entity, null) { WinnerDistrictId = districtId };
        var queue = current.OwnedDestructionQueue ?? state.PendingCombatSettlements
            ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("capture_manager140");
        var admitted = CombatSettlementRules.Admit(queue, request, ArmyDestructionQueue.BindCurrent(state, current));
        var kind = admitted.Kind ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing(
            admitted.MissingInput ?? "capture_admission");
        current.BindOwnedDestructionQueue(admitted.Queue, state);
        state = state with { PendingCombatSettlements = admitted.Queue };
        if (kind != CombatSettlementAdmissionKind.Full) return state;
        state = FlushNormalCombatEffects(state, revision, effects);
        var captureEffects = new List<CampaignEffectRequest>();
        state = ApplyBaseWinnerCaptureCurrent(state, current, request, date,
            sampler.Read(state) ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("capture_sampler"), captureEffects,
            CreateCaptureMapGroupingOwner());
        return AppendImmediateCombatEffects(state, revision, captureEffects);
    }
}
