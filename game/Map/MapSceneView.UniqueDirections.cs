using Godot;
using San14.Data;
using San14.Core;

public partial class MapSceneView
{
    private object? uniqueDirectionOwner;
    private Action<double, int>? uniqueDirectionUpdate;
    private Action<Exception>? uniqueDirectionFailure;
    private double uniqueDirectionSeconds;
    private OrdinaryTacticViewIdentity[] uniqueSuppressedTargets = [];
    private SceneViewSourceCameraInputs? uniqueCameraPriorInputs;

    internal void SetUniqueDirectionDriver(Action<double, int>? update, Action<Exception>? failed = null)
    { uniqueDirectionUpdate = update; uniqueDirectionFailure = failed; uniqueDirectionSeconds = 0; }

    internal void BeginUniqueDirection(object owner, TacticPresentationRequest request)
    {
        if (uniqueDirectionOwner is not null || storyPresentationOwner is not null)
            throw new InvalidOperationException("unique.camera.controller_already_owned");
        uniqueDirectionOwner = owner; ResetSourceCameraInput(); CloseCityCommandMenu(); CloseAreaHexCommandMenu();
        uniqueCameraPriorInputs = _sourceMainCameraInputs;
        if (_sourceMainCameraInputs is { } input) _sourceMainCameraInputs = input with { CurrentMaximumYOverride = true };
        uniqueSuppressedTargets = request.Actors.SelectMany(actor => actor.InstructionResults ?? [])
            .SelectMany(row => row).Where(marker => marker.TargetKind is TacticResultTargetKind.ArmyReceiver or TacticResultTargetKind.BaseLocation)
            .Select(marker => marker.TargetKind == TacticResultTargetKind.ArmyReceiver
                ? new OrdinaryTacticViewIdentity(OrdinaryTacticViewKind.Army, marker.TargetId)
                : marker.TargetId <= 51 ? new(OrdinaryTacticViewKind.City, marker.TargetId)
                    : new(OrdinaryTacticViewKind.Gate, marker.TargetId - 51))
            .Where(target => QueryTacticViewPose(target).NativeValid).ToArray();
        _d3d11Markers?.ChangeDirectionSuppression(uniqueSuppressedTargets, 1);
    }

    internal void EndUniqueDirection(object owner)
    {
        if (!ReferenceEquals(uniqueDirectionOwner, owner)) return;
        _d3d11Markers?.ChangeDirectionSuppression(uniqueSuppressedTargets, -1);
        uniqueSuppressedTargets = []; uniqueDirectionOwner = null;
        if (uniqueCameraPriorInputs is not null) _sourceMainCameraInputs = uniqueCameraPriorInputs;
        uniqueCameraPriorInputs = null; ResetSourceCameraInput();
    }

    internal bool UniqueDirectionCameraMoving => _sourceMainCamera?.TimedMotionActive == true;

    internal void StopUniqueDirectionCamera() => _sourceMainCamera?.StopTimedMotion();

    internal void SubmitUniqueDirectionCamera(MainMapCameraControllerRequest request)
    {
        if (uniqueDirectionOwner is null || _sourceMainCamera is not { } camera || _sourceMainCameraInputs is not { } input)
            throw new InvalidOperationException("unique.camera.controller_owner");
        if (request.DurationMilliseconds <= 0)
        {
            camera.ApplyImmediateRequest(request, input.CurrentMaximumYOverride, SourceCameraTerrainQualifier);
            PublishSourceCamera();
        }
        else camera.QueueTimedRequest(request, unchecked((int)Time.GetTicksMsec()));
    }

    private void AdvanceUniqueDirectionClock(double seconds, bool due)
    {
        uniqueDirectionSeconds += seconds;
        if (!due || _d3d11AnimationPacer is not { } pacer) return;
        var elapsed = uniqueDirectionSeconds; uniqueDirectionSeconds = 0;
        uniqueDirectionUpdate?.Invoke(elapsed, pacer.FramesPerSecond(pacer.FpsMode));
    }
}
