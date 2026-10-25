using Godot;
using San14.Core;
using San14.Data;
using NVector4 = System.Numerics.Vector4;

internal sealed record MonthlyTerritoryFeedbackDispatch(bool AudioAccepted, bool VisualAccepted,
    string? UnresolvedInput)
{
    public bool AudioCulled { get; init; }
}

internal sealed record MonthlyTerritoryVisual(int EffectKind, int HexId, int ForceId,
    Vector3 Position, float Scale, Color ForceColor);

public partial class MapSceneView
{
    private readonly Dictionary<MonthlyMaintenanceRequest, Task<MonthlyTerritoryFeedbackDispatch>>
        _monthlyFeedbackDispatches = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<MonthlyMaintenanceRequest, MonthlyTerritoryVisual>
        _monthlyTerritoryVisuals = new(ReferenceEqualityComparer.Instance);
    private CancellationTokenSource? _monthlyFeedbackLifetime;
    private MapMonthlyAudioWorld? _monthlyAudioWorld;

    //These are actual ready source commands, not completed particle receipts.
    //The native map currently has no dynamic37E6C0 emitter/handle consumer.
    internal IReadOnlyList<MonthlyTerritoryVisual> PendingMonthlyTerritoryVisuals =>
        _monthlyTerritoryVisuals.Values.ToArray();

    internal Task<MonthlyTerritoryFeedbackDispatch> DispatchMonthlyTerritoryFeedbackAsync(
        MonthlyMaintenanceRequest request, SceneAudioBinding audio, CancellationToken cancellationToken)
    {
        if (_monthlyFeedbackDispatches.TryGetValue(request, out var pending)) return pending;
        if (_monthlyFeedbackLifetime is null)
        {
            _monthlyFeedbackLifetime = new();
            TreeExiting += RetireMonthlyFeedback;
        }
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
            if (!audible) return new(true, false, "monthly.territory.dynamic_particle_owner") { AudioCulled = true };

            if (_monthlyAudioWorld is null)
            {
                var viewport = _previewWorld.GetViewport();
                viewport.AudioListenerEnable3D = true;
                _monthlyAudioWorld = new() { Name = "MapFeedbackAudio",
                    ReadCamera = () => _sourceMainCameraFrame is { } current
                        ? CreateSourceCameraGodotTransform(current.Camera) : null };
                _previewWorld.AddChild(_monthlyAudioWorld);
            }
            float lastDistance = (float)camera.ControllerState.Distance90;
            await audio.PlayMonthlyTerritorySoundAsync(request.EventKind.Value, _monthlyAudioWorld, position,
                () =>
                {
                    if (_sourceMainCameraFrame is { } current) lastDistance = (float)current.ControllerState.Distance90;
                    return lastDistance;
                }, linked.Token);
            return new(true, false, "monthly.territory.dynamic_particle_owner");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return new(false, false, error.Message); }
    }

    private void RetireMonthlyFeedback()
    {
        _monthlyFeedbackLifetime?.Cancel(); _monthlyFeedbackLifetime?.Dispose(); _monthlyFeedbackLifetime = null;
        _monthlyFeedbackDispatches.Clear(); _monthlyTerritoryVisuals.Clear(); _monthlyAudioWorld = null;
    }
}
