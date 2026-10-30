using Godot;
using San14.Data;

public partial class MapSceneView
{
    private StoryEffectCommonCamera? storyEffectCommonCamera;
    private readonly Dictionary<object, (D3D11CaptureEffects Effects, D3D11MapSurface Surface)> storyMapEffectOwners =
        new(ReferenceEqualityComparer.Instance);

    private void PublishStoryEffectCommonCamera(MainMapCameraControllerState controller, SceneViewSourceCameraInputs input)
    {
        storyEffectCommonCamera ??= StoryEffectCommonCamera.ConstructFresh(_repository, _text.SourceLocale,
            input.ContextOwner3E4Bit29, input.NativeClipProfile, input.TangentDispatch,
            _sourceMainCameraFrameNumber, input.Provenance + ";Common-camera-id1");
        storyEffectCommonCamera.PublishController(controller, input.ContextOwner3E4Bit29,
            input.NativeClipProfile, input.TangentDispatch);
    }

    private void RetireStoryEffectCommonCamera()
    { storyEffectCommonCamera?.Dispose(); storyEffectCommonCamera = null; }

    private Task<CaptureEffectAdmission> CreateStoryMapEffect(object owner, object key, int kind, CancellationToken token)
    {
        if (!storyMapEffectOwners.TryGetValue(owner, out var scope))
        {
            if (!D3D11MapInteractive || _captureEffects is not { } effects || _d3d11Map is not { } surface)
                return Task.FromResult(new CaptureEffectAdmission(false, "story_effect.mode1_native_map_owner"));
            if (storyEffectCommonCamera is null)
                return Task.FromResult(new CaptureEffectAdmission(false, "story_effect.mode1_common_camera_owner"));
            scope = (effects, surface); storyMapEffectOwners.Add(owner, scope);
        }
        return scope.Effects.EnqueueStory(key, owner, kind, token);
    }

    private async Task RetireStoryMapEffectsAsync(object owner)
    {
        if (!storyMapEffectOwners.TryGetValue(owner, out var scope)) return;
        await scope.Surface.RetireStoryEffectOwnerAsync(scope.Effects, owner);
        if (storyMapEffectOwners.TryGetValue(owner, out var current) && current == scope)
            storyMapEffectOwners.Remove(owner);
    }
}
