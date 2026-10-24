using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Explicit fresh constructor call; never used during loading.</summary>
    public GameState InstallFreshArmyLifecycle(GameState state) =>
        OwnedArmyLifecycleCurrent.InstallFresh(state, _definitions);

    /// <summary>Native manager receipt for the actually constructed image.
    /// The caller publishes it atomically with that Unit's creation.</summary>
    public ArmyLifecycleCreationAdmission? PrepareArmyLifecycleCreation(GameState state, UnitState created) =>
        OwnedArmyLifecycleCurrent.PrepareCreationAdmission(state, created, _definitions, _creation.Configuration.TargetKind38);

    public ArmyLifecycleSourceView? PrepareArmyLifecyclePending(GameState state, int physical) =>
        OwnedArmyLifecycleCurrent.PreparePendingAdmission(state, physical, _definitions);

    public GameState? ConsumeArmyLifecyclePending(GameState state) =>
        OwnedArmyLifecycleAuxiliaries.ConsumePending(state, _definitions, ResetMapArmyGroupReceiver);

    public OwnedArmyReturnWorld CaptureOwnedArmyReturnWorld(GameState state)
    {
        var world = OwnedArmyReturnWorld.Capture(state, _definitions);
        BindOwnedArmyReturnWorld(state, world.Context);
        return world;
    }

    internal OwnedArmyReturnWorld BorrowOwnedArmyReturnWorld(GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        var world = OwnedArmyReturnWorld.Borrow(state, current, pool);
        BindOwnedArmyReturnWorld(state, current);
        return world;
    }

    internal GameState? ConsumeArmyLifecyclePendingCurrent(GameState state, SourceWorldContext current, WorldIndexContext pool) =>
        OwnedArmyLifecycleAuxiliaries.ConsumePendingCurrent(state, current, pool, ResetMapArmyGroupReceiver);

    private void BindOwnedArmyReturnWorld(GameState state, SourceWorldContext current)
    {
        OwnedArmyLifecycleTargets.BindCurrent(state, current);
        AreaStatisticsSourceAdapter.BindCurrent(state, current);
        _personDestructionInputs.BindCurrent(state, current);
        _personRelationInputs.BindCurrent(state, current);
        _force164Inputs.BindCurrent(state, current);
        _moraleWaveInputs.BindCurrent(state, current);
        PersonArmyCaptureInputs.BindCurrent(state, current);
        RulerCaptureReleaseSource.BindCurrent(state, current);
        CaptureBaseThreatInputs.BindCurrent(state, current);
        _personAssignmentPrecallInputs.BindCurrent(state, current);
        current.BindOwnedDestructionQueue(state.PendingCombatSettlements, state);
        current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
        _baseCapacityInputs.BindCurrent(state, current);
        PersonAdvisorResetSource.BindCurrent(state, current);
        ForceUnregisterInputs.BindCurrent(state, current);
        BaseCaptureCurrentInputs.BindCurrent(state, current);
        OwnedArmyReturnServiceInputs.BindCurrent(state, current);
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        //The independent ordinary campaign has no original tutorial-mode
        //receiver (570850). Its known absent branch continues eligibility.
        current.ArmyReturnTransferMode = new(false, null);
        current.TransferService = new();
        OwnedArmyLifecycleAuxiliaries.RestoreCurrent(current, state.ReadArmyLifecycle()!);
        PersonReturnCommands.BindCurrent(state, current);
    }
}
