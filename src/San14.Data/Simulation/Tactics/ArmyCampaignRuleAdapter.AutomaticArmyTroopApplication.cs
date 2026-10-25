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
        List<CampaignEffectRequest> effects)
    {
        var pool = current.ReadOwnedCurrentListPool();
        if (pool is null || !pool.OwnsArmyReturnProfile() || !ReferenceEquals(pool.ArmyReturnOwner, current))
            throw BaseAutomaticMissing("Army_damage_shared_pool");
        var context = Context(current, sampler);
        TacticRemakeCurrentContext ReadLatest(GameState reached)
        {
            OwnedArmyDestructionCurrent.BindOwnedCurrentContext(reached, current);
            return context;
        }
        _baseCapacityInputs.BindCurrent(input, current);
        var body = TryApplyNativeDamageTargetBody(input, casterId, targetId, requestedLoss,
            ReadLatest, ordinaryCurrent: current);
        if (body.Outcome == NormalArmyApplicationPrefixOutcome.CurrentInputsUnresolved || body.Receipt is not { } receipt)
            throw BaseAutomaticMissing(body.MissingPhase ?? "Army_damage_body");
        var working = body.PendingCleanupState ?? body.State;
        if (body.Outcome == NormalArmyApplicationPrefixOutcome.NativeTargetSkipped)
            return new(working, false);
        bool killed = receipt.Killed ?? throw BaseAutomaticMissing("Army_damage_native_return");
        if (receipt.TroopDebit is not { } debit || !working.Units.TryGetValue(casterId, out var actor) ||
            !working.Units.TryGetValue(targetId, out var target) ||
            target.NativeArmyOrigin?.NativeSlotIndex is not int targetPhysical)
            throw BaseAutomaticMissing("Army_damage_current_images");
        var force = ApplyBorrowedNormalForceResult(working, current, actor.ForceId, target.ForceId,
            debit.AppliedLoss, killed, new NormalArmyForceResultDefinitions(current));
        if (force.Outcome == NormalArmyForceResultOutcome.CurrentInputsUnresolved)
            throw BaseAutomaticMissing("Army_damage_Force_result");
        working = force.State;
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
            int winnerPhysical = working.Units[casterId].NativeArmyOrigin?.NativeSlotIndex
                ?? throw BaseAutomaticMissing("Army_damage_winner_receiver");
            var admission = OwnedArmyDestructionCurrent.AdmitOrdinaryArmy1745B0(working, current,
                targetPhysical, winnerPhysical, null);
            if (!admission.IsPending || admission.State is not { } queued)
                throw BaseAutomaticMissing(admission.UnresolvedOwner ?? (admission.RequiresImmediateSettlement
                    ? "Army_damage_Full128_immediate259360" : "Army_damage1745B0"));
            working = queued;
            OwnedArmyDestructionCurrent.BindOwnedCurrentContext(working, current);
        }
        return new(working, killed);
    }
}
