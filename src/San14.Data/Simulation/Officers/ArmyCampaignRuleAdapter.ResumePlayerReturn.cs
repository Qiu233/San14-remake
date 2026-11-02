using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Resumes the actual saved29F220 suffix. The prefix is already
    /// in this candidate; no traits, Home/resources, merit, Items or recruitment
    /// trials are prepared again. The caller continues its own held suffix.</summary>
    internal GameState ResumePlayerArmyReturnCurrent(GameState input, SourceWorldContext current,
        WorldIndexContext pool)
    {
        var saved = input.ArmyReturnCaptiveContinuation
            ?? throw PlayerReturnCaptiveMissing("resume_owner");
        if (saved.Phase != ArmyReturnCaptivePhase.ReadyToFinishReturn || !saved.RemainingOfficerIds.IsEmpty)
            throw PlayerReturnCaptiveMissing("resume_not_ready");
        if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
            throw PlayerReturnCaptiveMissing("resume_current_pool");
        var army = new ArmyUnitReference(saved.ArmyPhysicalSlot);
        var target = saved.Destination.Kind == SettlementBaseKind.City
            ? new BaseReference(BaseKind.City, saved.Destination.Id)
            : new BaseReference(BaseKind.Gate, saved.Destination.Id - 51);
        var arena = input.ReadArmyLifecycle() ?? throw PlayerReturnCaptiveMissing("resume_lifecycle");
        var slot = arena.ReceiverSlots()[saved.ArmyPhysicalSlot];
        if (slot.ReceiverId != saved.BatchId || slot.ActiveUnitId != saved.ArmyUnitId ||
            current.ReadArmyUnitCommanderId(army) != saved.ReturningCommanderId ||
            current.ReadBaseForceSlot(target) != saved.CapturingForceId)
            throw PlayerReturnCaptiveMissing("resume_receiver_or_holder_changed");
        var world = BorrowOwnedArmyReturnWorld(input, current, pool);
        var draft = current.CreateArmyReturnTransactionDraft();
        var stagedPool = pool.DetachForArmyReturn(draft);
        var captives = new ArmyReturnCaptivesSource(this, input, new(true));
        int Day(GameState _) => saved.SampleDay;
        TacticSourceSamplerWorld Sampler(GameState _) => new(saved.SampleDay, saved.SampleSalt);
        var queries = captives.BorrowDistrictOperationCurrent(draft, CreateCaptureGovernorQueries(input),
            saved.SourceDate, saved.SourceRevision, Day, Sampler);
        //66DD20 clears only this controller's transient treatment flags over
        //the actual registered Person chain.2F8F00 then repeats its bit4 tail
        //for valid original Persons. This is a once-only rule, not a proof scan.
        foreach (int? entry in draft.ReadRegisteredPersonPhysicalSlots()
            ?? throw PlayerReturnCaptiveMissing("exit_registered_people"))
        {
            int person = entry ?? throw PlayerReturnCaptiveMissing("exit_Person_pointer");
            ushort flags = draft.ReadArmyReturnPersonFlags196(person) ?? throw PlayerReturnCaptiveMissing("exit_Person_flags");
            draft.WriteArmyReturnPersonFlags196(person, (ushort)(flags & 0xEFAF));
        }
        foreach (int id in saved.OriginalOfficerIds)
        {
            int person = draft.ReadPersonLookupReference(id)?.PhysicalSlot ?? throw PlayerReturnCaptiveMissing("exit_original_lookup");
            if (draft.ReadPersonNativeValidity(person) == true)
                draft.WriteArmyReturnPersonFlags196(person, OfficerBehaviorSourceAdapter.FinishCaptiveProcessing(
                    draft.ReadArmyReturnPersonFlags196(person) ?? throw PlayerReturnCaptiveMissing("exit_final_flags")));
        }
        captives.AdoptCurrentState(OwnedArmyReturnCurrentFields.PublishCurrent(input, draft));
        var prepared = ArmyOrdinaryReturnRuntime.PrepareTailCurrent(army, target, current, pool,
            draft, stagedPool, queries, new(saved.SavedCommanderDistrictId), new(saved.SavedTargetDistrictId),
            saved.ReturningTransportPerson, captives, null, queries, null, true,
            ["29F220.saved_player_captive_prefix_resumed"],
            (reached, person, assignment) => captives.ApplyAssignmentReports(reached, person, assignment,
                saved.SourceRevision, Day));
        var packet = prepared.Packet ?? throw PlayerReturnCaptiveMissing(prepared.UnresolvedOwner ?? "resume_return_tail");
        var result = world.CommitPrepared(input, packet) ?? throw PlayerReturnCaptiveMissing("resume_return_publication");
        return OwnedArmyLifecycleDetachedInputs.PublishCurrent(result, current);
    }
}
