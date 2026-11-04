using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private sealed record BaseTierCameraScope(object Owner, MainMapCameraAdapter Camera,
        object? PreviousStoryOwner, bool PreviousDetailsOpen)
    {
        internal MainMapCameraControllerState? Retained;
    }
    private BaseTierCameraScope? baseTierCameraScope;

    internal bool IsBaseTierCameraCurrent(object owner) => baseTierCameraScope is { } scope &&
        ReferenceEquals(scope.Owner, owner) && ReferenceEquals(storyPresentationOwner, owner) &&
        ReferenceEquals(_sourceMainCamera, scope.Camera) && !_nativeMapClosing &&
        _d3d11Map is { IsClosed: false, LastError: null };

    internal void BeginBaseTierCamera(object owner)
    {
        if (baseTierCameraScope is not null || _sourceMainCamera is not { } camera)
            throw new InvalidOperationException("base-tier.camera.owner");
        baseTierCameraScope = new(owner, camera, storyPresentationOwner, _detailsOpen);
        BeginStoryPresentation(owner);
    }

    internal void FocusBaseTierCamera(object owner, BaseDamageTierCutsceneRequest request)
    {
        var scope = RequireBaseTierCamera(owner);
        if (!IsStoryCameraIdle(owner)) throw new InvalidOperationException("base-tier.camera.focus_not_idle");
        scope.Retained = scope.Camera.CurrentState;
        float? height = null;
        if (request.HexId != 48400)
        {
            // Reuse the existing map-owned1889A0 cache and prepared current
            // Landscape. Missing terrain is unknown, never guessed height0.
            var field = _sourceMainCameraTerrain?.CurrentField ?? throw new InvalidDataException("base-tier.camera.height_owner");
            if (!ReferenceEquals(field, _rulerHomeHeightField))
            { _rulerHomeHeights.Clear(); _rulerHomeHeightField = field; }
            if (!_rulerHomeHeights.TryGetValue(request.HexId, out float cached) || cached <= 0)
            {
                var point = CeHexWorldCoordinates.Project(request.HexId);
                if (field.Query((float)point.X, (float)point.Z) is not float sampled || !float.IsFinite(sampled))
                    throw new InvalidDataException("base-tier.camera.height1889A0");
                cached = 445f <= sampled ? sampled : 445f;
                _rulerHomeHeights[request.HexId] = cached;
            }
            height = cached;
        }
        if (BaseDamageTierCutsceneSource.CreateFocusRequest(request, height) is { } focus && !RequestStoryCamera(owner, focus))
            throw new InvalidOperationException("base-tier.camera.focus_rejected");
    }

    internal void ReturnBaseTierCamera(object owner)
    {
        var scope = RequireBaseTierCamera(owner);
        if (scope.Retained is not { } retained ||
            !RequestStoryCamera(owner, BaseDamageTierCutsceneSource.CreateRestoreRequest(retained)))
            throw new InvalidOperationException("base-tier.camera.return_rejected");
    }

    private BaseTierCameraScope RequireBaseTierCamera(object owner)
    {
        if (baseTierCameraScope is not { } scope || !ReferenceEquals(scope.Owner, owner) ||
            !ReferenceEquals(storyPresentationOwner, owner) || !ReferenceEquals(_sourceMainCamera, scope.Camera))
            throw new InvalidOperationException("base-tier.camera.scope_changed");
        return scope;
    }

    internal void EndBaseTierCamera(object owner, bool interrupted)
    {
        if (baseTierCameraScope is not { } scope || !ReferenceEquals(scope.Owner, owner)) return;
        baseTierCameraScope = null;
        if (!ReferenceEquals(storyPresentationOwner, owner)) return;
        if (interrupted && scope.Retained is { } retained && ReferenceEquals(_sourceMainCamera, scope.Camera) &&
            _sourceMainCameraInputs is { } inputs)
        {
            scope.Camera.StopTimedMotion();
            scope.Camera.ApplyImmediateRequest(new(0,
                new((float)retained.FocusX, (float)retained.FocusY, (float)retained.FocusZ, (float)retained.FocusW),
                retained.Distance90, retained.YawA8, retained.PitchC0, 1), inputs.CurrentMaximumYOverride, SourceCameraTerrainQualifier);
            PublishSourceCamera();
        }
        storyPresentationOwner = scope.PreviousStoryOwner;
        SetDetailsOpen(scope.PreviousDetailsOpen, keepMapVisible: true);
    }
}
