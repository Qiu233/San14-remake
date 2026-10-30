using Godot;
using San14.Data;

public partial class AudioPlaybackService
{
    private readonly AudioCueSelectionRandom _cueSelectionRandom = new();

    internal Task<NormalSoundAdmission> PlaySpatialEffectAsync(SpatialEffectSoundPlayback source,
        OriginalSpatialOutput output, object owner, Node3D world, Vector3 position, SpatialSoundMode mode,
        Func<SpatialSoundCameraFrame> readCamera, CancellationToken cancellationToken)
        => PlaySpatialEffectCoreAsync(source.ManagerId, source, null, output, owner, world, position,
            mode, readCamera, cancellationToken);

    internal Task<NormalSoundAdmission> PlayArmyDamageSoundAsync(ArmyDamageSoundDeclaration declaration,
        OriginalSpatialOutput output, object owner, Node3D world, Vector3 position, SpatialSoundMode mode,
        Func<SpatialSoundCameraFrame> readCamera, CancellationToken cancellationToken)
        => PlaySpatialEffectCoreAsync(declaration.ManagerId, null, declaration, output, owner, world, position,
            mode, readCamera, cancellationToken);

    private async Task<NormalSoundAdmission> PlaySpatialEffectCoreAsync(int managerId,
        SpatialEffectSoundPlayback? source, ArmyDamageSoundDeclaration? declaration,
        OriginalSpatialOutput output, object owner, Node3D world, Vector3 position, SpatialSoundMode mode,
        Func<SpatialSoundCameraFrame> readCamera, CancellationToken cancellationToken)
    {
        RequireEngineThread(); cancellationToken.ThrowIfCancellationRequested();
        var outputEpoch=OriginalOutputs;
        outputEpoch.ValidateHost();
        var layout=outputEpoch.SpeakerLayout;
        var matrixAvailability=OriginalSpeakerPairGate.MatrixAvailability(layout);
        if(!matrixAvailability.Available)throw new NotSupportedException(matrixAvailability.UnresolvedOwner);
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        EffectVoice? effect = null;
        var context = new NormalSoundManagerContext(_repository is not null, !_lifetime.IsCancellationRequested,
            IsInsideTree() && _decoder is not null, false);
        var admission = _normalSounds.TryAllocateSpatial(managerId,
            new(position.X, position.Y, position.Z), mode, context, QueryEffectHandle, request =>
            {
                effect = new(request.Slot, request.ManagerId, "effects:" + request.Slot)
                { Owner = owner, Preparation = lifetime, Reserved = true, Spatial = true };
                _effects[request.Slot] = effect;
                return true;
            }, RetireEffect);
        if (effect is null) { lifetime.Dispose(); return admission; }

        var clips = new List<AudioClip>();
        try
        {
            // Selection belongs to the admitted audio-library request. Parsing,
            // clip rejection and map/World replacement never reset this stream.
            source ??= declaration!.Select(_cueSelectionRandom);
            //All layers are prepared before any starts. Each layer retains its
            //native sample rate, pitch, cursor and authored distance curve.
            foreach (var layer in source.Layers)
                clips.Add(await PrepareInputAsync(layer.Input, AudioLoopPolicy.Disabled, lifetime.Token).ConfigureAwait(false));
            await InvokeOnEngineThreadAsync(() =>
            {
                if (!ReferenceEquals(_effects[effect.Slot], effect) || lifetime.IsCancellationRequested ||
                    !ReferenceEquals(_originalOutputs,outputEpoch) || outputEpoch.SpeakerLayout!=layout)
                    throw new OperationCanceledException(lifetime.Token);
                outputEpoch.ValidateHost();
                float initialGain = output.Plan.InitialCategoryGains.Where(gain => gain.Key != 0xF4E09144)
                    .Aggregate(1f, (value, gain) => value * gain.InitialValue);
                var emitter=new System.Numerics.Vector3(position.X,position.Y,position.Z);
                var layers=source.Layers.Select((layer,index)=>
                {
                    var clip=clips[index];
                    return new SpatialAudioLayer(clip,layer.PlaybackRate,
                        ()=>layer.GainAt(readCamera().CameraDistance)*initialGain,
                        MathF.Max(0,layer.StartOffsetSeconds),
                        distance=>layer.GainAt(distance)*initialGain,
                        (camera,logicalChannels)=>SpatialSoundSpeakerMatrixSource.PrepareEffect(
                            source,layer,camera,emitter,clip.OutputChannels,logicalChannels,source.OutputPlan.Graph.MixChannels,
                            outputEpoch.ReadSpatialSpread(source.OutputPlan)))
                        { ControlMatrix = SpatialSoundControlMatrixSource.PrepareOrdinaryEffect(layer, clip.OutputChannels) };
                }).ToArray();
                PlaySpatialMatrixLayers(layers,effect.Channel,world,output.Sends,outputEpoch,
                    output.Graph.MixChannels,readCamera,()=>effect.ControlGain);
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
