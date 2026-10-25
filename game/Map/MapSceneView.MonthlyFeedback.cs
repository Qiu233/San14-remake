using Godot;
using San14.Core;
using San14.Data;
using NVector4 = System.Numerics.Vector4;

internal sealed record MonthlyTerritoryFeedbackDispatch(bool AudioAccepted, bool VisualAccepted,
    string? UnresolvedInput)
{
    public bool AudioCulled { get; init; }
}

internal sealed record MonthlyTerritoryVisual(int EffectKind, int? HexId, int? ForceId,
    Vector3 Position, float Scale, Color? ForceColor);

public partial class MapSceneView
{
    private readonly Dictionary<MonthlyMaintenanceRequest, Task<MonthlyTerritoryFeedbackDispatch>>
        _monthlyFeedbackDispatches = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<MonthlyMaintenanceRequest, MonthlyTerritoryVisual>
        _monthlyTerritoryVisuals = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<MapObjectPresentationRequest, Task<MonthlyTerritoryFeedbackDispatch>>
        _baseCaptureDispatches = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<MapObjectPresentationRequest, MonthlyTerritoryVisual>
        _baseCaptureVisuals = new(ReferenceEqualityComparer.Instance);
    private CancellationTokenSource? _monthlyFeedbackLifetime;
    private MapMonthlyAudioWorld? _monthlyAudioWorld;

    // Pending source commands remain here until the native effect owner accepts submission.
    internal IReadOnlyList<MonthlyTerritoryVisual> PendingMonthlyTerritoryVisuals =>
        _monthlyTerritoryVisuals.Values.Concat(_baseCaptureVisuals.Values).ToArray();

    internal Task<MonthlyTerritoryFeedbackDispatch> DispatchBaseCaptureFeedbackAsync(
        MapObjectPresentationRequest request, SceneAudioBinding audio, CancellationToken cancellationToken)
    {
        if (_baseCaptureDispatches.TryGetValue(request, out var existing)) return existing;
        EnsureCaptureFeedbackLifetime();
        var pending = DispatchBaseAsync();
        _baseCaptureDispatches.Add(request, pending);
        return pending;

        async Task<MonthlyTerritoryFeedbackDispatch> DispatchBaseAsync()
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _monthlyFeedbackLifetime!.Token);
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                if (request.BaseCapture is not { EffectKind: 7 or 8, Category: 0, SoundId: 0xB3 } source)
                    throw new InvalidDataException("base_capture.source_snapshot");
                var position = new Vector3(source.SourceX, source.SourceY, source.SourceZ);
                if (request.Event == MapObjectPresentationEvent.BaseOwnershipLostVisual)
                {
                    //The capture factory keeps its authored colour. It does not
                    //query a later city's/force's state to replace the snapshot.
                    _baseCaptureVisuals.Add(request, new(source.EffectKind, request.HexId, null, position, source.Scale, null));
                    var admitted = await AdmitCaptureVisualAsync(request, _baseCaptureVisuals[request], linked.Token);
                    return new(true, admitted.Accepted, admitted.UnresolvedInput);
                }
                if (request.Event != MapObjectPresentationEvent.BaseOwnershipLostSound)
                    throw new InvalidDataException("base_capture.operation");
                var admission = await DispatchCaptureSoundAsync(source.SoundId, position, audio, linked.Token);
                return new(true, true, null) { AudioCulled = !admission.Allocated };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { return new(false, false, error.Message); }
        }
    }

    private void EnsureCaptureFeedbackLifetime()
    {
        if (_monthlyFeedbackLifetime is not null) return;
        _monthlyFeedbackLifetime = new();
        TreeExiting += RetireMonthlyFeedback;
    }

    internal Task<MonthlyTerritoryFeedbackDispatch> DispatchMonthlyTerritoryFeedbackAsync(
        MonthlyMaintenanceRequest request, SceneAudioBinding audio, CancellationToken cancellationToken)
    {
        if (_monthlyFeedbackDispatches.TryGetValue(request, out var pending)) return pending;
        EnsureCaptureFeedbackLifetime();
        var dispatch = DispatchTerritoryAsync(request, audio, cancellationToken);
        _monthlyFeedbackDispatches.Add(request, dispatch);
        return dispatch;
    }

    private async Task<MonthlyTerritoryFeedbackDispatch> DispatchTerritoryAsync(
        MonthlyMaintenanceRequest request, SceneAudioBinding audio, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _monthlyFeedbackLifetime!.Token);
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            if (request.Operation != MonthlyMaintenanceOperation.TerritoryOccupation || request.EventKind is not (4 or 5) ||
                request.HexId is not int hex || (uint)hex >= 48400 || request.ForceId is not int force)
                throw new InvalidDataException("monthly.territory.request");
            if (!_loaded || !IsInsideTree() || TerrainMeshes is null || _sourceMainCameraFrame is not { } camera ||
                !GodotObject.IsInstanceValid(_previewWorld))
                throw new InvalidDataException("monthly.territory.map_not_ready");
            if (_text.SourceLocale != "TC") throw new InvalidDataException("monthly.territory.SC_effect_scale");
            var world = _periodBorrowedCandidate ?? _logicalWorld ?? throw new InvalidDataException("monthly.territory.world");
            byte color = world.Forces.GetValueOrDefault(force)?.ReportColorIndex
                ?? throw new InvalidDataException("monthly.territory.Force49");
            Color tint;
            if (color < 60)
            {
                var value = (ForcePalette ?? throw new InvalidDataException("monthly.territory.force_palette"))
                    .FirstTableColor(color, 2);
                tint = new(value.Red / 255f, value.Green / 255f, value.Blue / 255f, value.Alpha / 255f);
            }
            else tint = Colors.White; //32C8C0's actual entry60 fallback, including61.
            var point = CeHexWorldCoordinates.Project(hex);
            Vector3 position = new((float)point.X,
                MapGrounding.SampleHeight(TerrainMeshes, point.X, point.Z), (float)point.Z);
            _monthlyTerritoryVisuals.Add(request, new(request.EventKind.Value, hex, force, position,
                request.EventKind == 4 ? 3f : 1.7f, tint));

            //405370→011470 tests the actual homogeneous projection. Bounds
            //are strict and depth is0..1; this is a point test, not a sphere.
            var clip = NVector4.Transform(new NVector4(position.X, position.Y, position.Z, 1),
                camera.Camera.Matrices.WorldToProjection100);
            float inverse = 1f / clip.W;
            float x = clip.X * inverse, y = clip.Y * inverse, z = clip.Z * inverse;
            bool audible = x > -1 && x < 1 && y > -1 && y < 1 && z > 0 && z < 1;
            if (audible) await DispatchCaptureSoundAsync(request.EventKind == 4 ? 0xAE : 0xAD, position, audio, linked.Token);
            var admission = await AdmitCaptureVisualAsync(request, _monthlyTerritoryVisuals[request], linked.Token);
            return new(true, admission.Accepted, admission.UnresolvedInput) { AudioCulled = !audible };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return new(false, false, error.Message); }
    }

    private Task<CaptureEffectAdmission> AdmitCaptureVisualAsync(object source, MonthlyTerritoryVisual visual,
        CancellationToken cancellationToken)
    {
        if (_captureEffects is not { } effects || !D3D11MapInteractive)
            return Task.FromResult(new CaptureEffectAdmission(false, "capture_effect.native_map_owner"));
        var pending = effects.Enqueue(source, visual, cancellationToken);
        ScheduleD3D11MapFrame();
        return pending;
    }

    internal void ForgetCaptureFeedback(CampaignEffectRequest request)
    {
        if (request.MonthlyMaintenance is { } monthly)
        { _monthlyFeedbackDispatches.Remove(monthly); _monthlyTerritoryVisuals.Remove(monthly); _captureEffects?.Forget(monthly); }
        if (request.MapObjectPresentation is { } capture)
        { _baseCaptureDispatches.Remove(capture); _baseCaptureVisuals.Remove(capture); _captureEffects?.Forget(capture); }
    }

    private Task<NormalSoundAdmission> DispatchCaptureSoundAsync(int managerId, Vector3 position,
        SceneAudioBinding audio, CancellationToken cancellationToken)
    {
        if (!_loaded || !IsInsideTree() || _sourceMainCameraFrame is not { } camera ||
            !GodotObject.IsInstanceValid(_previewWorld)) throw new InvalidDataException("capture_audio.map_not_ready");
        if (_monthlyAudioWorld is null)
        {
            _previewWorld.GetViewport().AudioListenerEnable3D = true;
            _monthlyAudioWorld = new() { Name = "MapFeedbackAudio",
                ReadCamera = () => _sourceMainCameraFrame is { } current
                    ? CreateSourceCameraGodotTransform(current.Camera) : null };
            _previewWorld.AddChild(_monthlyAudioWorld);
        }
        float lastDistance = (float)camera.ControllerState.Distance90;
        return audio.PlayCaptureSoundAsync(managerId, _monthlyAudioWorld, position,
            new(0, _loaded && IsInsideTree(), true, camera.Camera.Matrices.WorldToProjection100),
            () =>
            {
                if (_sourceMainCameraFrame is { } current) lastDistance = (float)current.ControllerState.Distance90;
                return lastDistance;
            }, cancellationToken);
    }

    private void RetireMonthlyFeedback()
    {
        _monthlyFeedbackLifetime?.Cancel(); _monthlyFeedbackLifetime?.Dispose(); _monthlyFeedbackLifetime = null;
        _monthlyFeedbackDispatches.Clear(); _monthlyTerritoryVisuals.Clear(); _monthlyAudioWorld = null;
        _baseCaptureDispatches.Clear(); _baseCaptureVisuals.Clear();
    }
}
