using San14.Core;

namespace San14.Data;

internal sealed record AutomaticArmyTroopApplication(GameState State, bool NativeReturn);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The reached176C10 ->178EC0 Army target path. Its external
    /// signed request is not an ordinary frame request. The same numeric body,
    /// Force/personality writers and1745B0 queue stay in the stage13 operation;
    /// no phase19/25/26 settlement or independent capture runs here.</summary>
    private AutomaticArmyTroopApplication ApplyBorrowedAutomaticArmyTroopDamage(GameState input,
        SourceWorldContext current, int casterId, int targetId, int requestedLoss,
        NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate operationDate,
        List<CampaignEffectRequest> effects, long? immediatePauseRevision = null)
        => ApplyAutomaticArmyTroopDamageCurrentCore(input, current, casterId, null, targetId, requestedLoss,
            sampler, operationDate, effects, immediatePauseRevision);

    internal AutomaticArmyTroopApplication ApplyRetiredEnteredAutomaticArmyTroopDamageCurrent(GameState input,
        SourceWorldContext current, int sourcePhysical, int targetId, int requestedLoss,
        NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate operationDate,
        List<CampaignEffectRequest> effects, long? immediatePauseRevision = null)
    {
        RequireRetiredAutomaticActorCurrent(input, current, sourcePhysical);
        return ApplyAutomaticArmyTroopDamageCurrentCore(input, current, null, sourcePhysical, targetId,
            requestedLoss, sampler, operationDate, effects, immediatePauseRevision);
    }

    private AutomaticArmyTroopApplication ApplyAutomaticArmyTroopDamageCurrentCore(GameState input,
        SourceWorldContext current, int? casterId, int? retiredSourcePhysical, int targetId, int requestedLoss,
        NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate operationDate,
        List<CampaignEffectRequest> effects, long? immediatePauseRevision)
    {
        var pool = current.ReadOwnedCurrentListPool();
        if (pool is null || !pool.OwnsArmyReturnProfile() || !ReferenceEquals(pool.ArmyReturnOwner, current))
            throw BaseAutomaticMissing("Army_damage_shared_pool");
        var context = Context(current, sampler);
        var sourceImage = casterId is int liveId ? RequireLatestAutomaticActorImageCurrent(input, current, liveId) : null;
        int sourcePhysical = sourceImage?.NativeArmyOrigin?.NativeSlotIndex ?? retiredSourcePhysical
            ?? throw BaseAutomaticMissing("Army_damage_source_physical");
        if (sourceImage is not null) context.BindAutomaticActorResumeCurrent(input, current, sourcePhysical);
        TacticRemakeCurrentContext ReadLatest(GameState reached)
        {
            OwnedArmyDestructionCurrent.BindOwnedCurrentContext(reached, current);
            return context;
        }
        _baseCapacityInputs.BindCurrent(input, current);
        var body = TryApplyNativeDamageTargetBody(input, casterId, targetId, requestedLoss,
            ReadLatest, ordinaryCurrent: current, borrowedAttacker: sourceImage,
            retiredArmyOrigin: retiredSourcePhysical is int retired ? new(retired) : null);
        if (body.Outcome == NormalArmyApplicationPrefixOutcome.CurrentInputsUnresolved || body.Receipt is not { } receipt)
            throw BaseAutomaticMissing(body.MissingPhase ?? "Army_damage_body");
        var working = body.PendingCleanupState ?? body.State;
        if (body.Outcome == NormalArmyApplicationPrefixOutcome.NativeTargetSkipped)
            return new(working, false);
        bool killed = receipt.Killed ?? throw BaseAutomaticMissing("Army_damage_native_return");
        if (receipt.TroopDebit is not { } debit || !working.Units.TryGetValue(targetId, out var target) ||
            target.NativeArmyOrigin?.NativeSlotIndex is not int targetPhysical)
            throw BaseAutomaticMissing("Army_damage_current_images");
        //178EC0 reaches257670 only for a native-valid source Army.
        //The explicit retired path does not substitute Force0 or grant rewards.
        if (retiredSourcePhysical is null)
        {
            //Full can change the source commander's District while its Army
            //receiver remains pending.22CD80 reads that current owner afresh.
            int attackerForce = current.ReadArmyUnitRawForceSlot(new(sourcePhysical));
            int targetForce = current.ReadArmyUnitRawForceSlot(new(targetPhysical));
            var force = ApplyBorrowedNormalForceResult(working, current, attackerForce, targetForce,
                debit.AppliedLoss, killed, new NormalArmyForceResultDefinitions(current));
            if (force.Outcome == NormalArmyForceResultOutcome.CurrentInputsUnresolved)
                throw BaseAutomaticMissing("Army_damage_Force_result");
            working = force.State;
        }
        var feedback = new ArmyTraitFeedback(working, current, operationDate, effects, () => working,
            speechConsumer: CreateArmyTraitSpeechConsumer(current, () => working, next => working = next));
        var targetReference = new ArmyUnitReference(targetPhysical);
        if (!killed)
        {
            var personality = ApplyBorrowedNormalPersonality(working, current, ReadLatest(working), targetId);
            if (personality.Outcome == NormalArmyPersonalityApplicationOutcome.CurrentInputsUnresolved)
                throw BaseAutomaticMissing(personality.MissingOwner ?? "Army_damage_personality");
            working = personality.State;
            foreach (var activation in personality.Activations)
                feedback.RefreshAndFeedback(targetReference, new(activation.PersonalityPhysicalSlot));
        }
        //The actual1A3B20 point follows personality and precedes1745B0.
        //This is pending semantic view work, not a view-existence or ACK test.
        feedback.Refresh(targetReference);
        working = feedback.Publish(working);
        if (killed)
        {
            int winnerPhysical = retiredSourcePhysical ?? RequireLatestAutomaticActorImageCurrent(working, current,
                casterId ?? throw BaseAutomaticMissing("Army_damage_winner_id")).NativeArmyOrigin?.NativeSlotIndex
                ?? throw BaseAutomaticMissing("Army_damage_winner_receiver");
            var admission = OwnedArmyDestructionCurrent.AdmitOrdinaryArmy1745B0(working, current,
                targetPhysical, winnerPhysical, null);
            if (admission.RequiresImmediateSettlement && immediatePauseRevision is long revision)
            {
                //The body, Force result and trait refresh above have already
                //run. A full queue settles only this defeat, retaining the
                //existing128 and allowing its actual ruler decision to wait.
                working = ApplyNormalCombatImmediateDefeatCurrent(working, current,
                    new(targetId, casterId, null), sampler.Read(working)
                        ?? throw BaseAutomaticMissing("Army_damage_immediate_sampler"),
                    operationDate, revision, effects);
            }
            else if (admission.IsPending && admission.State is { } queued)
                working = queued;
            else
                throw BaseAutomaticMissing(admission.UnresolvedOwner ?? (admission.RequiresImmediateSettlement
                    ? "Army_damage_Full128_immediate259360" : "Army_damage1745B0"));
            OwnedArmyDestructionCurrent.BindOwnedCurrentContext(working, current);
        }
        return new(working, killed);
    }
}
