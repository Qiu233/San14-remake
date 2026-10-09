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

    public NativeArmyLifecycleState? PrepareArmyLifecyclePending(GameState state, int physical) =>
        OwnedArmyLifecycleCurrent.PreparePendingAdmission(state, physical, _definitions);

    public GameState? ConsumeArmyLifecyclePending(GameState state) =>
        OwnedArmyLifecycleAuxiliaries.ConsumePending(state, _definitions, ResetMapArmyGroupReceiver);

    public OwnedArmyReturnWorld CaptureOwnedArmyReturnWorld(GameState state)
    {
        var world = OwnedArmyReturnWorld.Capture(state, _definitions);
        OwnedArmyLifecycleTargets.BindCurrent(state, world.Context);
        _personDestructionInputs.BindCurrent(state, world.Context);
        _personRelationInputs.BindCurrent(state, world.Context);
        _force164Inputs.BindCurrent(state, world.Context);
        _moraleWaveInputs.BindCurrent(state, world.Context);
        PersonArmyCaptureInputs.BindCurrent(state, world.Context);
        RulerCaptureReleaseSource.BindCurrent(state, world.Context);
        CaptureBaseThreatInputs.BindCurrent(state, world.Context);
        _personAssignmentPrecallInputs.BindCurrent(state, world.Context);
        world.Context.BindOwnedDestructionQueue(state.PendingCombatSettlements, state);
        world.Context.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, world.Context);
        _baseCapacityInputs.BindCurrent(state, world.Context);
        PersonAdvisorResetSource.BindCurrent(state, world.Context);
        ForceUnregisterInputs.BindCurrent(state, world.Context);
        BaseCaptureCurrentInputs.BindCurrent(state, world.Context);
        OwnedArmyReturnServiceInputs.BindCurrent(state, world.Context);
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, world.Context);
        //The independent ordinary campaign has no original tutorial-mode
        //receiver (570850). Its known absent branch continues eligibility.
        world.Context.ArmyReturnTransferMode = new(false, null);
        world.Context.TransferService = new();
        OwnedArmyLifecycleAuxiliaries.RestoreCurrent(world.Context, state.NativeArmyLifecycle!);
        PersonReturnCommands.BindCurrent(state, world.Context);
        return world;
    }
}
