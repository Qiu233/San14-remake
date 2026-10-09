using San14.Core;

namespace San14.Data;

public sealed partial class OriginalOwnedArmyCampaignRules
{
    /// <summary>Explicit fresh constructor call; never used during loading.</summary>
    public GameState InstallFreshArmyLifecycle(GameState state) =>
        OriginalOwnedArmyLifecycleCurrent.InstallFresh(state, _definitions);

    /// <summary>Native manager receipt for the actually constructed image.
    /// The caller publishes it atomically with that Unit's creation.</summary>
    public NativeArmyLifecycleState? PrepareArmyLifecycleCreation(GameState state, UnitState created) =>
        OriginalOwnedArmyLifecycleCurrent.PrepareCreationAdmission(state, created, _definitions, _creation.Configuration.TargetKind38);

    public NativeArmyLifecycleState? PrepareArmyLifecyclePending(GameState state, int physical) =>
        OriginalOwnedArmyLifecycleCurrent.PreparePendingAdmission(state, physical, _definitions);

    public GameState? ConsumeArmyLifecyclePending(GameState state) =>
        OriginalOwnedArmyLifecycleAuxiliaries.ConsumePending(state, _definitions, ResetMapArmyGroupReceiver);

    public OriginalOwnedArmyReturnWorld CaptureOwnedArmyReturnWorld(GameState state)
    {
        var world = OriginalOwnedArmyReturnWorld.Capture(state, _definitions);
        OriginalOwnedArmyLifecycleTargets.BindCurrent(state.NativeArmyLifecycle!, world.Context);
        _personDestructionInputs.BindCurrent(state, world.Context);
        _personRelationInputs.BindCurrent(state, world.Context);
        _force164Inputs.BindCurrent(state, world.Context);
        _moraleWaveInputs.BindCurrent(state, world.Context);
        OriginalPersonArmyCaptureInputs.BindCurrent(state, world.Context);
        OriginalRulerCaptureReleaseSource.BindCurrent(state, world.Context);
        OriginalCaptureBaseThreatInputs.BindCurrent(state, world.Context);
        _personAssignmentPrecallInputs.BindCurrent(state, world.Context);
        world.Context.BindOwnedDestructionQueue(state.PendingCombatSettlements, state);
        world.Context.CurrentPlayerForce = OriginalOwnedCurrentPlayerForce.ReadCurrentReference(state, world.Context);
        _baseCapacityInputs.BindCurrent(state, world.Context);
        OriginalPersonAdvisorResetSource.BindCurrent(state, world.Context);
        OriginalForceUnregisterInputs.BindCurrent(state, world.Context);
        OriginalBaseCaptureCurrentInputs.BindCurrent(state, world.Context);
        OriginalOwnedArmyReturnServiceInputs.BindCurrent(state, world.Context);
        OriginalOwnedArmyLifecycleDetachedInputs.BindCurrent(state, world.Context);
        //The independent ordinary campaign has no original tutorial-mode
        //receiver (570850). Its known absent branch continues eligibility.
        world.Context.ArmyReturnTransferMode = new(false, null);
        world.Context.TransferService = new();
        OriginalOwnedArmyLifecycleAuxiliaries.RestoreCurrent(world.Context, state.NativeArmyLifecycle!);
        OriginalPersonReturnCommands.BindCurrent(state, world.Context);
        return world;
    }
}
