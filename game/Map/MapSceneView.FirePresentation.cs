using San14.Core;
using NMatrix = System.Numerics.Matrix4x4;

public partial class MapSceneView
{
    private readonly Dictionary<CampaignEffectRequest, Task<FacilityEffectsDispatch>> _fireDispatches =
        new(ReferenceEqualityComparer.Instance);
    private CancellationTokenSource? _firePresentationLifetime;

    internal ArmyFireViewSnapshot? CaptureArmyFireView(ArmyReceiverId receiver)
    {
        var frame = Volatile.Read(ref _facilityArmyViewSnapshots);
        if (frame is null) return null;
        return frame.TryGetValue(receiver.Value, out var view) ? view : new(false, 0, 0, 0, 1);
    }

    internal Task<FacilityEffectsDispatch> DispatchFirePresentationAsync(CampaignEffectRequest request,
        SceneAudioBinding? audio, CancellationToken cancellation)
    {
        if (_fireDispatches.TryGetValue(request, out var existing)) return existing.WaitAsync(cancellation);
        EnsureFirePresentationLifetime();
        var pending = Dispatch(); _fireDispatches.Add(request, pending);
        return pending.WaitAsync(cancellation);

        async Task<FacilityEffectsDispatch> Dispatch()
        {
            try
            {
                var token = _firePresentationLifetime!.Token;
                token.ThrowIfCancellationRequested();
                if (!D3D11MapInteractive || _captureEffects is null) return new(false, "fire.cast.native_scene");
                if (request.ArmyFireCast?.SourceSnapshot is not { } source) return new(false, "fire.cast.source_snapshot");
                if (!source.SourceView.Present) return new(true); //2E41F0 has no actual Army view.
                var point = source.SourceView;
                var matrix = NMatrix.CreateTranslation(point.X, point.Y + 300f, point.Z);
                matrix.M44 = point.W;
                //The source literal is hexadecimal56, i.e. normal kind86.
                var pending = _captureEffects.Enqueue(request, 0x56, matrix, null, token,
                    ArmyOwner(source.SourceReceiverId.Value));
                ScheduleD3D11MapFrame();
                var result = await pending;
                return new(result.Completed, result.UnresolvedInput);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { return new(false, error.Message); }
        }
    }

    private void EnsureFirePresentationLifetime()
    {
        if (_firePresentationLifetime is not null) return;
        EnsureCaptureFeedbackLifetime(); _firePresentationLifetime = new();
        TreeExiting += RetireFirePresentation;
    }

    internal void ForgetFirePresentation(CampaignEffectRequest request)
    { _fireDispatches.Remove(request); _captureEffects?.Forget(request); }

    internal void RetireFirePresentation()
    {
        _firePresentationLifetime?.Cancel(); _firePresentationLifetime?.Dispose(); _firePresentationLifetime = null;
        foreach (var request in _fireDispatches.Keys)
        { _captureEffects?.Retire(request); _captureEffects?.Forget(request); }
        _fireDispatches.Clear();
    }
}
