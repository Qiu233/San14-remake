using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal static bool HasPendingForceAbsorptionCurrent(GameState state) =>
        state.AutomaticEmperorCapture is not null || state.ForceTransferEmperorTail is not null ||
        state.ForceAbsorptionContinuation is not null;

    // The central worker completes the actual inner operation first. These
    // are reached business tails, never a second selection/trial/prefix.
    internal GameState ResumeForceAbsorptionCallerTailCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        if (HasPendingForceAbsorptionCurrent(state)) return state;
        if (state.DemandSurrenderCompletionTail is { } demand)
        {
            var effects = new List<CampaignEffectRequest>();
            state = CompleteDemandSurrenderCurrent(state, current, demand.Task, demand.Accepted,
                demand.OperationDate, effects);
            return AppendDiplomacyEffects(state, effects);
        }
        if (state.DemandSurrenderAbsorptionTail is not null)
            return ResumeDemandSurrenderAbsorptionCurrent(state, current);
        if (state.PlayerCoalitionAbsorptionTail is { } coalition)
            return new PlayerForceAbsorptionSource(this, state, current, pool,
                new(coalition.PlayerForcePhysicalSlot)).Resume(coalition);
        if (state.NpcForceAbsorptionTail is { } npc)
            return new NpcForceAbsorptionSource(this, state, current, pool,
                new(npc.PlayerForcePhysicalSlot)).Resume(npc);
        return state;
    }

    private (GameState State, int NativeReturn) ResumeStoryForceAbsorptionCallerCurrent(
        GameState state, SourceWorldContext current, WorldIndexContext pool,
        StoryForceAbsorptionTail tail, ForceReference first, ForceReference recipient,
        Action<ForceReference, int>? recordJoined, AutomaticEmperorCaptureInputs? emperorInputs = null)
    {
        if (first.PhysicalSlot != tail.OriginalForcePhysicalSlot ||
            recipient.PhysicalSlot != tail.RecipientForcePhysicalSlot)
            throw StoryForceLifecycleMissing("resume_Force_owner");
        if (HasPendingForceAbsorptionCurrent(state))
            state = ResumeNormalTurnForceAbsorptionCurrent(state, current, pool, emperorInputs, eventPlaybackPresent: true);
        if (HasPendingForceAbsorptionCurrent(state)) return (state, 0);
        BindStoryForceLifecycleCurrent(state, current);
        var effects = new List<CampaignEffectRequest>();
        state = RetireDiplomacyAreaObjects(state, current, pool, effects);
        if (tail.RestoreRecordingGate)
        {
            current.ReportBlockerState = new(true, 0);
            state = state with { CampaignEventEligibility = (state.CampaignEventEligibility
                ?? throw StoryForceLifecycleMissing("resume_event_manager_owner")) with { HistoryRecordingSuppressed = false } };
        }
        // This iterator is created after actual absorption, as in 3A4A80.
        int force = StoryForceLifecycleNeed(current.ReadForcePhysicalSlot(first), "resume_notification_Force60");
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Person)
            ?? throw StoryForceLifecycleMissing("resume_notification_Person100");
        var node = current.ReadRegistryHead(descriptor) ?? throw StoryForceLifecycleMissing("resume_notification_head");
        while (node.Identity.HasValue)
        {
            var payload = current.ReadRegistryNodePayload(node) ?? throw StoryForceLifecycleMissing("resume_notification_payload");
            if (payload is not { Kind: RegistryKind.Person, PhysicalSlot: int person })
                throw StoryForceLifecycleMissing("resume_notification_pointer");
            if (force is >= 1 and <= 51 &&
                StoryForceLifecycleNeed(current.ReadPersonOldForceSlot(person), "resume_notification_Force") == force &&
                StoryForceLifecycleNeed(current.ReadPersonStatus(person), "resume_notification_status") is >= 1 and <= 4 &&
                StoryForceLifecycleNeed(current.ReadPersonNativeValidity(person), "resume_notification_validity") &&
                StoryForceLifecycleNeed(current.ReadEventManagerPresent(), "resume_notification_manager"))
                (recordJoined ?? throw StoryForceLifecycleMissing("resume_joined_recorder"))(recipient, person);
            node = current.ReadRegistryNodeNext(node) ?? throw StoryForceLifecycleMissing("resume_notification_next");
        }
        state = RefreshEditedEventRegionStatistics(state, current);
        state = PublishNormalTurnCurrent(state, current, pool);
        state = PersonAssignmentPrecallInputs.PublishCurrent(state, current);
        if (effects.Count != 0)
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests
                ?? throw StoryForceLifecycleMissing("resume_effect_queue")).Append(CampaignEffectSource.MonthlyMaintenance,
                    state.NativeClock?.UpdateOrdinal ?? throw StoryForceLifecycleMissing("resume_effect_revision"), effects) };
        return (state with { StoryForceAbsorptionTail = null }, 1);
    }
}
