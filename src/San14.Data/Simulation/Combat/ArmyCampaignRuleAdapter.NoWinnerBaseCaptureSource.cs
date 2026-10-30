using San14.Core;

namespace San14.Data;

internal sealed record NoWinnerBaseCaptureResult(GameState State, bool NativeTerminal);

public sealed partial class ArmyCampaignRuleAdapter
{
    //1745B0's packet retains genuinely absent Army/Base winners. The
    //original Full branch passes a literal null District to2851B0, whose
    //entry dereferences District+10 before testing validity. The rewrite
    //explicitly resolves a no-recipient capture through the proven neutral
    //District0/Force0 operation; it does not emulate that invalid pointer read.
    private static BaseFatalAdmissionReceipt AdmitNoWinnerBaseFatalCurrent(GameState state,
        SourceWorldContext current, BaseReference target, int originalUnitId, int sourcePhysical)
    {
        RequireRetiredAutomaticActorCurrent(state, current, sourcePhysical);
        var queue = current.OwnedDestructionQueue ?? throw NoWinnerCaptureMissing("manager140");
        var entity = ArmyDestructionQueue.ResolveBase(current, target) ?? throw NoWinnerCaptureMissing("target_identity");
        var request = new BaseCaptureRequest(entity, null) { HasNoWinner = true };
        var admitted = CombatSettlementRules.Admit(queue, request, ArmyDestructionQueue.BindCurrent(state, current));
        var kind = admitted.Kind ?? throw NoWinnerCaptureMissing(admitted.MissingInput ?? "admission");
        current.BindOwnedDestructionQueue(admitted.Queue, state);
        return new(target, originalUnitId, sourcePhysical, new(null), kind) { HasNoWinner = true };
    }

    private GameState ApplyImmediateNoWinnerBaseCaptureCurrent(GameState state, SourceWorldContext current,
        BaseReference target, NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate date,
        long revision, List<CampaignEffectRequest> precedingEffects)
    {
        //The original128 entries stay intact. Only this reached Full call
        //settles immediately, after its178EC0 numeric stores and preceding
        //combat intents, and without inventing an extra queue entry.
        state = FlushNormalCombatEffects(state, revision, precedingEffects);
        var request = new BaseCaptureRequest(ArmyDestructionQueue.ResolveBase(current, target)
            ?? throw NoWinnerCaptureMissing("immediate_target_identity"), null) { HasNoWinner = true };
        var effects = new List<CampaignEffectRequest>();
        var result = ApplyNoWinnerBaseCaptureCurrent(state, current, request, date,
            sampler.Read(state), effects, CreateCaptureMapGroupingOwner());
        return AppendImmediateCombatEffects(result.State, revision, effects);
    }

    internal NoWinnerBaseCaptureResult ApplyNoWinnerBaseCaptureCurrent(GameState state, SourceWorldContext current,
        BaseCaptureRequest request, NativeMapSeasonDate? date, TacticSourceSamplerWorld? sampler,
        List<CampaignEffectRequest> effects, ArmyMapGroupingOwner? groups = null)
    {
        if (!request.HasNoWinner || request.WinnerUnitId is not null || request.WinnerDistrictId is not null)
            throw NoWinnerCaptureMissing("known_absent_winners");
        var pool = current.ReadOwnedCurrentListPool() ?? throw NoWinnerCaptureMissing("shared_pool");
        if (!pool.OwnsArmyReturnProfile() || !ReferenceEquals(pool.ArmyReturnOwner, current))
            throw NoWinnerCaptureMissing("shared_pool_owner");
        var target = ArmyDestructionQueue.ResolveBaseReference(state, current, request.Target);
        if (!N(current.ReadBaseNativeValidity(target), "target_validity")) return new(state, false);
        var neutralDistrict = current.ReadDistrictTableReference(0) ?? throw NoWinnerCaptureMissing("District0_pointer");
        var neutralForce = current.ReadForceTableReference(0) ?? throw NoWinnerCaptureMissing("Force0_pointer");
        var noArmy = new ArmyUnitReference(null);
        if (N(current.ReadDistrictNativeValidity(neutralDistrict), "District0_validity") ||
            N(current.ReadForceNativeValidity(neutralForce), "Force0_validity") ||
            N(current.ReadArmyUnitNativeValidity(noArmy), "null_Army_validity"))
            throw NoWinnerCaptureMissing("neutral_records");
        int oldForce = N(current.ReadBaseForceSlot(target), "target_ForceB0");
        //The two early comparisons in2851B0 independently compare the
        //District's recipient and22CD80(nullArmy), both genuinely Force0.
        if (oldForce == N(current.ReadDistrictForceSlot(neutralDistrict), "District0_Force10") ||
            oldForce == current.ReadArmyUnitRawForceSlot(noArmy)) return new(state, false);

        //The shared inputs use the latest operation, preserving its live
        //pool and manager. No capture/export or alternate world is created.
        BindActorlessDestructionInputs(state, current);
        if (current.ReadOwnedPersonCommandManager() is null)
        {
            if (state.PersonnelCommands is null) throw NoWinnerCaptureMissing("personnel_owner");
            PersonReturnCommands.BindCurrent(state, current);
        }
        if (effects.Count != 0)
        {
            state = AppendImmediateCombatEffects(state,
                state.NativeClock?.UpdateOrdinal ?? throw NoWinnerCaptureMissing("effect_revision"), effects);
            effects.Clear();
        }
        var initial = BaseCaptureRewardSource.ApplyInitialForceResult(state, current, neutralForce, target);
        var working = initial.State ?? throw NoWinnerCaptureMissing(initial.UnresolvedOwner ?? "initial_257670");
        if (target.Kind == BaseKind.City)
            working = ApplyCityCaptureAttackRequestsCurrent(working, current, target, neutralForce, effects);
        //The request outcomes precede the Person prefix's own immediate
        //visual/history batches, even when the recipient Force is invalid0.
        if (effects.Count != 0)
        {
            working = AppendImmediateCombatEffects(working,
                working.NativeClock?.UpdateOrdinal ?? throw NoWinnerCaptureMissing("request_effect_revision"), effects);
            effects.Clear();
        }
        BaseCapturePersonPrefixReceipt? prefix = null;
        BaseCaptureTerritoryReceipt? counts = null;
        bool nativeTerminal = false;
        try
        {
            var people = new BaseCapturePersonPrefix(this).TryApplyCurrent(working, current,
                target, noArmy, date, sampler, winnerDistrict: neutralDistrict, allowNoWinner: true);
            working = people.State ?? throw NoWinnerCaptureMissing(people.UnresolvedOwner ?? "person_prefix");
            prefix = people.Receipt ?? throw NoWinnerCaptureMissing("person_prefix_receipt");
            var begin = BaseCaptureRewardSource.BeginTerritoryRewards(working, current, neutralForce);
            counts = begin.Receipt ?? throw NoWinnerCaptureMissing(begin.UnresolvedOwner ?? "State_Region_temporaries");
            working = ApplyBaseCaptureNearbyArmiesCurrent(working, current, pool, target, date, effects, sampler);
            var transfer = ApplyNeutralBaseCaptureTerritoryCurrent(working, current, pool, target, date, effects, groups);
            working = transfer.State;
            nativeTerminal = transfer.NativeTerminal;
            if (!nativeTerminal)
            {
                //Invalid Force/Army/District skip their actual reward, return,
                //capture-history and World1562 gates. The shared tail still owns
                //and releases both allocated territory descriptors.
                var tail = BaseCaptureRewardSource.ApplyTailWithinCapture(working, current, target,
                    neutralForce, neutralDistrict, noArmy, counts);
                counts = null;
                working = tail.State ?? throw NoWinnerCaptureMissing(tail.UnresolvedOwner ?? "reward_tail");
            }
        }
        finally
        {
            if (counts is not null) BaseCaptureRewardSource.ReleaseTerritoryRewards(current, counts);
            if (prefix is not null) working = BaseCapturePersonPrefix.ReleaseWithinCapture(working, current, prefix);
        }
        return new(BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true), nativeTerminal);

        static T N<T>(T? value, string name) where T : struct => value ?? throw NoWinnerCaptureMissing(name);
    }

    private static InvalidDataException NoWinnerCaptureMissing(string owner) => new("Base-capture.no-winner." + owner);
}
