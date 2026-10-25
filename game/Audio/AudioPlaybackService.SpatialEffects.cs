using Godot;
using San14.Data;

public partial class AudioPlaybackService
{
    internal async Task<NormalSoundAdmission> PlaySpatialEffectAsync(SpatialEffectSoundPlayback source,
        OriginalSpatialOutput output, object owner, Node3D world, Vector3 position, SpatialSoundMode mode,
        Func<float> cameraDistance, CancellationToken cancellationToken)
    {
        RequireEngineThread(); cancellationToken.ThrowIfCancellationRequested();
        OriginalOutputs.ValidateHost();
        //The current product host is Windows stereo. The supplementary source
        //LFE matrix applies only to the six/eight-channel device branch.
        if (AudioServer.GetSpeakerMode() != AudioServer.SpeakerMode.ModeStereo)
            throw new NotSupportedException("Capture sound needs its multichannel spatial matrix consumer.");
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        EffectVoice? effect = null;
        var context = new NormalSoundManagerContext(_repository is not null, !_lifetime.IsCancellationRequested,
            IsInsideTree() && _decoder is not null, false);
        var admission = _normalSounds.TryAllocateSpatial(source.ManagerId,
            new(position.X, position.Y, position.Z), mode, context, QueryEffectHandle, request =>
            {
                effect = new(request.Slot, request.ManagerId, "effects:" + request.Slot)
                { Owner = owner, Preparation = lifetime, Reserved = true, Spatial = true };
                _effects[request.Slot] = effect;
                return true;
            }, RetireEffect);
        if (effect is null) { lifetime.Dispose(); return admission; }

        var clips = new List<AudioClip>(source.Layers.Count);
        try
        {
            //All layers are prepared before any starts. Each layer retains its
            //native sample rate, pitch, cursor and authored distance curve.
            foreach (var layer in source.Layers)
                clips.Add(await PrepareInputAsync(layer.Input, AudioLoopPolicy.Disabled, lifetime.Token).ConfigureAwait(false));
            await InvokeOnEngineThreadAsync(() =>
            {
                if (!ReferenceEquals(_effects[effect.Slot], effect) || lifetime.IsCancellationRequested)
                    throw new OperationCanceledException(lifetime.Token);
                float initialGain = output.Plan.InitialCategoryGains.Where(gain => gain.Key != 0xF4E09144)
                    .Aggregate(1f, (value, gain) => value * gain.InitialValue);
                var layers = source.Layers.Select((layer, index) => new SpatialAudioLayer(clips[index],
                    layer.PlaybackRate, () => layer.GainAt(cameraDistance()) * initialGain,
                    MathF.Max(0, layer.StartOffsetSeconds))).ToArray();
                PlaySpatialLayers(layers, effect.Channel, world, position, output.Sends);
                effect.Reserved = false;
                clips.Clear(); //The one cue now owns all decoded streams.
                return true;
            }, lifetime.Token).ConfigureAwait(false);
            return admission;
        }
        catch
        {
            await InvokeOnEngineThreadAsync(() => { RemoveEffect(effect); return true; }).ConfigureAwait(false);
            throw;
        }
        finally
        {
            foreach (var clip in clips) clip.Dispose();
            await RetireStoryPreparationAsync(effect, lifetime).ConfigureAwait(false);
        }
    }
}
