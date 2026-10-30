using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //Ordinary combat runs outside CEventPlayback: CHistoryManager+10 is its
    //real constructed/non-event value0. The two reception thresholds remain unknown.
    internal AutomaticEmperorCapturePreparation PrepareCaptureEmperorCurrent(GameState state,
        SourceWorldContext current, CityCaptureAreaScope scope)
    {
        if (scope.OldForcePhysical == scope.NewForcePhysical) return new(state, null);
        var force = current.ReadForceTableReference(scope.NewForcePhysical)
            ?? throw CaptureEmperorMissing("recipient_force");
        if (!(current.ReadForceNativeValidity(force) ?? throw CaptureEmperorMissing("recipient_validity")))
            return new(state, null);
        return PrepareAutomaticEmperorCaptureCurrent(state, current, scope.City, null, force,
            new(null, null, new(null, null, 0)));
    }

    internal GameState ResumeCaptureEmperorServiceCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        if (state.AutomaticEmperorCapture is not { } progress) throw CaptureEmperorMissing("progress");
        if (progress.PendingDecision is not null || AutomaticEmperorCaptureLifecycle.IsPresentationPending(state)) return state;
        var working = AutomaticEmperorCaptureLifecycle.ConsumeCompletedPresentation(state);
        _ = BorrowOwnedArmyReturnWorld(working, current, pool);
        _personRelationInputs.BindCurrent(() => working, current);
        current.BaseForcePolicyEffectReader = (force, effect) =>
            ForcePolicyScratchCurrent.ReadBeforeFreedom(working, current, force, effect, true).Value;
        var advanced = AdvanceAutomaticEmperorCaptureCurrent(working, current, pool,
            working.AutomaticEmperorCapture!, new(null, null, new(null, null, 0)));
        working = advanced.State with { AutomaticEmperorCapture = advanced.Progress };
        if (advanced.Effects.Count != 0)
        {
            var queue = (working.CampaignEffectRequests ?? throw CaptureEmperorMissing("effects"))
                .Append(CampaignEffectSource.ArmyBattleDefeat,
                    working.NativeClock?.UpdateOrdinal ?? throw CaptureEmperorMissing("effect_revision"), advanced.Effects);
            working = working with { CampaignEffectRequests = queue, AutomaticEmperorCapture = advanced.Progress with
                { NeedsPresentationReceipt = true, AwaitingEffectBatchSequence = queue.LastBatchSequence } };
        }
        // These stages already publish RNG, history, loyalty, relations and
        // emperor state. The capture caller publishes its real territory tail;
        // dialogue receipts only need the balanced pool's updated ownership.
        return working.WithArmyLifecycle((working.ReadArmyLifecycle()
            ?? throw CaptureEmperorMissing("phase_lifecycle")) with { Indexes = pool.ProjectOwnedRoles() });
    }

    private static InvalidDataException CaptureEmperorMissing(string field) =>
        new("capture.emperor.lifecycle." + field);
}
