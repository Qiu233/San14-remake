using Godot;
using San14.Data;

public partial class SceneAudioSession
{
    private OriginalSpatialOutput? _captureSoundOutput;

    internal async Task<NormalSoundAdmission> PlayCaptureSoundAsync(int managerId,
        Node3D world, Vector3 position, SpatialSoundMode mode, Func<SpatialSoundCameraFrame> readCamera, CancellationToken cancellationToken)
    {
        RequireEngineThread();
        if (_repository is null || _streams is null || _service is null || _sceneCancellation is null)
            throw new InvalidOperationException("Territory sound needs an active audio scene.");
        long revision = SceneRevision;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sceneCancellation.Token);
        var selected = await Task.Run<(SpatialEffectSoundPlayback? Playback, ArmyDamageSoundDeclaration? Damage)>(() =>
            {
                lock (_repository)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    var bank = AudioSelectionResources.Load(_repository, MapTerritorySoundSource.SelectionBankId);
                    var categories = _repository.LoadRdbAsset("RRPreview", NarrationVoiceSelection.OutputResourceId);
                    return managerId is 161 or 162
                        ? (null, ArmyDamageSoundSource.Prepare(bank, categories, managerId))
                        : (SpatialEffectSoundSource.Prepare(bank, categories, managerId), null);
                }
            }, linked.Token).ConfigureAwait(false);
        var playback = await _service.InvokeOnEngineThreadAsync(() =>
            {
                if (SceneRevision != revision || !GodotObject.IsInstanceValid(world) || !world.IsInsideTree())
                    throw new OperationCanceledException("Territory audio map has retired.");
                var plan = selected.Damage?.OutputPlan ?? selected.Playback!.OutputPlan;
                if (_captureSoundOutput is null) _captureSoundOutput = new(_service.OriginalOutputs, plan);
                else if (_captureSoundOutput.Graph.Source.DecodedSha256 != plan.Graph.Source.DecodedSha256 ||
                    _captureSoundOutput.Graph.Classification.Id != plan.Graph.Classification.Id)
                    throw new InvalidDataException("Capture audio resource changed; re-enter the map to bind its route.");
                return selected.Damage is { } damage
                    ? _service.PlayArmyDamageSoundAsync(damage, _captureSoundOutput, this, world, position,
                        mode, readCamera, linked.Token)
                    : _service.PlaySpatialEffectAsync(selected.Playback!, _captureSoundOutput, this, world, position,
                        mode, readCamera, linked.Token);
            }, linked.Token).ConfigureAwait(false);
        return await playback.ConfigureAwait(false);
    }

    internal void StopMonthlyTerritorySounds()
    {
        RequireEngineThread();
        _service?.StopOrdinarySoundsOwnedBy(this);
        AudioServer.Lock();
        try { _captureSoundOutput?.Dispose(); _captureSoundOutput = null; }
        finally { AudioServer.Unlock(); }
    }
}
