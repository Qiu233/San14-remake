using Godot;
using San14.Data;

public partial class SceneAudioSession
{
    private readonly HashSet<string> _monthlySpatialChannels = [];

    internal async Task PlayMonthlyTerritorySoundAsync(int effectKind, string channel,
        Node3D world, Vector3 position, Func<float> cameraDistance, CancellationToken cancellationToken)
    {
        RequireEngineThread();
        if (_repository is null || _streams is null || _service is null || _sceneCancellation is null)
            throw new InvalidOperationException("Territory sound needs an active audio scene.");
        long revision = SceneRevision;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sceneCancellation.Token);
        AudioClip? prepared = null;
        try
        {
            var selected = await Task.Run(() =>
            {
                lock (_repository)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    var bank = AudioSelectionResources.Load(_repository, MapTerritorySoundSource.SelectionBankId);
                    return MapTerritorySoundSource.ReadPlayback(bank, effectKind);
                }
            }, linked.Token).ConfigureAwait(false);
            prepared = await _service.PrepareInputAsync(selected.Input, AudioLoopPolicy.Disabled, linked.Token).ConfigureAwait(false);
            var clip = prepared;
            await _service.InvokeOnEngineThreadAsync(() =>
            {
                if (SceneRevision != revision || !GodotObject.IsInstanceValid(world) || !world.IsInsideTree())
                    throw new OperationCanceledException("Territory audio map has retired.");
                _service.PlaySpatial(clip, channel, world, position, selected.PlaybackRate,
                    () => selected.GainAt(cameraDistance()), () => _monthlySpatialChannels.Remove(channel));
                _monthlySpatialChannels.Add(channel);
                prepared = null; //The spatial voice now owns this clip.
                return true;
            }, linked.Token).ConfigureAwait(false);
        }
        finally { prepared?.Dispose(); }
    }

    internal void StopMonthlyTerritorySounds()
    {
        RequireEngineThread();
        foreach (string channel in _monthlySpatialChannels.ToArray()) _service?.StopSpatial(channel);
        _monthlySpatialChannels.Clear();
    }
}
