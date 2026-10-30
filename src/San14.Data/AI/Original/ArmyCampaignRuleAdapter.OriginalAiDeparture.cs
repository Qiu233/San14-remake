using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal OriginalAiDepartureApplication ApplyOriginalAiDepartureCurrent(GameState state,
        OriginalAiActor actor, OriginalAiDepartureInput input)
    {
        // A real order transaction owns this current; no cached operation or
        // current World is retained by the command or returned application.
        var current = CaptureDeploymentPersonSelectionCurrent(state);
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        current.RestoreOwnedArmyReturnSlots(state, state.ReadArmyLifecycle()!);
        _moraleWaveInputs.BindCurrent(state, current);
        _baseCapacityInputs.BindCurrent(state, current);
        DeploymentTacticCurrentInputs.BindCurrent(state, current);
        var prepared = PrepareOriginalAiDepartureCurrent(state, current, actor, input.OfficerId,
            input.RequestedTroops, input.FormationId, input.TacticIds, input.Action, input.TargetEntity,
            input.TargetHex, input.Category, input.Retreat, input.Pursuit, input.Choice, input.Facility, input.Continuation);
        if (prepared is null) return new(state, 0, null, false);
        int person = OCKnown(current.ReadPersonLookupReference(input.OfficerId)?.PhysicalSlot, "AI_commander_pointer");
        state = MarkOrdinaryDepartureCommanderCurrent(state, current, person, input.OfficerId);
        //1EC980 writes bit0 before every generic allocation gate. A known
        //exhausted roster preserves that write and returns a real failed node.
        var lifecycle = state.ReadArmyLifecycle() ?? throw OCMissing("AI_lifecycle");
        var allocation = lifecycle.Receivers.ReadFirstAllocation();
        if (allocation.Kind == ArmyReceiverAllocationKind.Unknown) throw OCMissing("AI_receiver_readiness");
        //2D2CA0 leaves its returned pointer null when no receiver is found.
        //B27E20 still stores that known null; no placeholder Unit is invented.
        if (allocation.Kind == ArmyReceiverAllocationKind.Unavailable) return new(state, 0, null, true);
        state = ArmyIdentityAllocation.Initialize(state);
        int unitId = state.ArmyIdentities!.Candidate;
        var target = new ArmyCommandTarget((byte)Math.Clamp(prepared.Configuration.Action34, 0, 17),
            prepared.TargetFields, prepared.GoalHex);
        var next = CreateOrdinaryDeploymentArmyCurrent(state, current, prepared.Source, prepared.Unit,
            target, debitCommandBook: true, directConfiguration: prepared.Configuration);
        return new(next, 1, unitId, true);
    }
}
