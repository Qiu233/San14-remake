using San14.Core;

namespace San14.Data;

/// <summary>The capture prefix has already changed people, ownership and
/// resources before29F220 can ask the player. Only ordered reward inputs and
/// the identity of that actual capture cross this wait.</summary>
internal static class CaptureArmyReturnContinuationSource
{
    internal static ArmyReturnCaptiveContinuation? Read(GameState state) =>
        state.ArmyReturnCaptiveContinuation is { Caller: ArmyReturnCaptiveCaller.BaseCapture } value ? value : null;

    internal static ArmyReturnCaptiveCaptureTail Save(SourceWorldContext current,
        BaseCaptureOperationContext operation, BaseCaptureTerritoryReceipt counts,
        bool completingEmperorCapture = false, int settlementIndex = -1) =>
        new(operation.Request, operation.WinnerForceId, operation.WinnerDistrictId,
            CaptureEmperorTailLists.ReadStatic(current, counts.States, "State"),
            CaptureEmperorTailLists.ReadStatic(current, counts.Regions, "Region"),
            counts.StateCount, counts.RegionCount)
        { Operation = operation, SettlementIndex = settlementIndex, CompletingEmperorCapture = completingEmperorCapture };

    internal static GameState Attach(GameState state, ArmyReturnCaptiveCaptureTail tail)
    {
        var owner = state.ArmyReturnCaptiveContinuation ?? throw Missing("suspended_return_owner");
        var operation = tail.Operation ?? throw Missing("suspended_capture_operation");
        if (owner.BatchId != operation.WinnerReceiverId || owner.ArmyPhysicalSlot != operation.WinnerArmyPhysicalSlot ||
            owner.ArmyUnitId != operation.Request.WinnerUnitId || owner.Destination != operation.Request.Target)
            throw Missing("suspended_capture_receiver");
        return state with { ArmyReturnCaptiveContinuation = owner with
            { Caller = ArmyReturnCaptiveCaller.BaseCapture, CaptureTail = tail } };
    }

    internal static CaptureArmyReturnCompletion Resume(ArmyCampaignRuleAdapter package,
        GameState input, SourceWorldContext current, BaseCaptureRequest request)
    {
        var pending = Read(input) ?? throw Missing("resume_owner");
        var tail = pending.CaptureTail ?? throw Missing("resume_capture_tail");
        var operation = tail.Operation ?? throw Missing("resume_capture_operation");
        if (tail.Request != request || operation.Request != request || tail.WinningForceId != operation.WinnerForceId ||
            tail.WinningDistrictId != operation.WinnerDistrictId || pending.BatchId != operation.WinnerReceiverId ||
            pending.ArmyPhysicalSlot != operation.WinnerArmyPhysicalSlot || pending.ArmyUnitId != request.WinnerUnitId ||
            pending.Destination != request.Target)
            throw Missing("resume_capture_identity");
        if (pending.Phase != ArmyReturnCaptivePhase.ReadyToFinishReturn)
            return new(input, operation, null);
        var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("resume_capture_pool");
        var working = package.ResumePlayerArmyReturnCurrent(input, current, pool);
        var target = ArmyDestructionQueue.ResolveBaseReference(working, current, request.Target);
        //These are the same ordered payloads retained before the modal wait.
        //BeginTerritoryRewards would incorrectly count the already conquered
        //territory as the before image, so it is never called on this path.
        var states = CaptureEmperorTailLists.RestoreStatic(current, tail.StatePhysicalSlots, "State");
        BaseCaptureTerritoryReceipt counts;
        try
        {
            var regions = CaptureEmperorTailLists.RestoreStatic(current, tail.RegionPhysicalSlots, "Region");
            counts = new(states, regions, tail.BeforeStateCount, tail.BeforeRegionCount);
        }
        catch { current.ClearTemporaryHexList(states); current.ReleaseTemporaryHexList(states); throw; }
        //ApplyTailWithinCapture owns both restored descriptors, including its
        //failure path. This is the original late reward operation, once only.
        var reward = BaseCaptureRewardSource.ApplyTailWithinCapture(working, current, target,
            new(operation.WinnerForceId), new(operation.WinnerDistrictId), new(operation.WinnerArmyPhysicalSlot), counts);
        working = reward.State ?? throw Missing(reward.UnresolvedOwner ?? "resume_capture_reward");
        if (tail.CompletingEmperorCapture)
            working = working with { CityCaptureEmperorTail = null, AutomaticEmperorCapture = null };
        working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
        return new(working, operation, reward.Receipt ?? throw Missing("resume_capture_reward_receipt"));
    }

    private static InvalidDataException Missing(string owner) => new("Base-capture.player-return." + owner);
}

internal sealed record CaptureArmyReturnCompletion(GameState State,
    BaseCaptureOperationContext Operation, BaseCaptureTailReceipt? Rewards);
