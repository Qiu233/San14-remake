using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private object? storyPresentationOwner;

    internal CampaignCameraSnapshot? CaptureStoryCamera()
    {
        if (_sourceMainCamera is not { } camera) return null;
        var p = camera.CurrentState;
        return new((float)p.FocusX, (float)p.FocusY, (float)p.FocusZ, (float)p.FocusW,
            p.Distance90, p.YawA8, p.PitchC0);
    }

    internal void BeginStoryPresentation(object owner)
    {
        storyPresentationOwner = owner;
        SetDetailsOpen(true, keepMapVisible: true);
        if (!UseD3D11Map) ((SubViewport)_surface.GetChild(0)).RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
    }

    internal void EndStoryPresentation(object owner)
    {
        if (!ReferenceEquals(storyPresentationOwner, owner)) return;
        storyPresentationOwner = null;
        SetDetailsOpen(false);
    }

    internal bool IsStoryCameraIdle(object owner) => ReferenceEquals(storyPresentationOwner, owner) &&
        _sourceMainCamera is { CurrentRuntime.Transition: null, CurrentRuntime.RequestMode188: 0 };

    internal bool MoveStoryCamera(object owner, StoryCameraTarget target, int milliseconds)
        => RequestStoryCamera(owner, new(milliseconds, new(target.FocusX, target.FocusY, target.FocusZ, 1),
            target.Distance, target.Yaw, target.Pitch, 1));

    internal bool RestoreStoryCamera(object owner, CampaignCameraSnapshot snapshot, bool immediate = false)
    {
        if (!ReferenceEquals(storyPresentationOwner, owner) || _sourceMainCamera is not { } camera) return false;
        var p = camera.CurrentState;
        //3D8850 compares the four source floats, not the other retained axes.
        if (!immediate && (float)p.FocusX == snapshot.FocusX && (float)p.FocusY == snapshot.FocusY &&
            (float)p.FocusZ == snapshot.FocusZ && (float)p.FocusW == snapshot.FocusW) return true;
        return RequestStoryCamera(owner, new(immediate ? 0 : 1,
            new(snapshot.FocusX, snapshot.FocusY, snapshot.FocusZ, snapshot.FocusW),
            snapshot.Distance, snapshot.Yaw, snapshot.Pitch, 1));
    }

    private bool RequestStoryCamera(object owner, MainMapCameraControllerRequest request)
    {
        if (!IsStoryCameraIdle(owner) || _sourceMainCamera is not { } camera || _sourceMainCameraInputs is not { } input)
            return false;
        if (request.DurationMilliseconds <= 0)
        {
            bool applied = camera.ApplyImmediateRequest(request, input.CurrentMaximumYOverride, SourceCameraTerrainQualifier);
            if (applied) PublishSourceCamera();
            return applied;
        }
        //The source event request uses the complete supplied packet directly;
        //wheel target materialization would change this caller's contract.
        return camera.QueueTimedRequest(request, unchecked((int)Time.GetTicksMsec()));
    }
}
