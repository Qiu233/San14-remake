using Godot;
using San14.Data;

public partial class AudioPlaybackService
{
    private sealed class MatrixSpatialOutput(OriginalAudioPlayer player, SpatialAudioLayer layer, SpatialAudioSend send)
    {
        internal readonly OriginalAudioPlayer Player=player;
        internal readonly SpatialAudioLayer Layer=layer;
        internal readonly float SendGain=send.Gain;
        internal readonly bool IsControl=send.IsControl;
        internal bool Finished;
    }

    private void PlaySpatialMatrixLayers(IReadOnlyList<SpatialAudioLayer> layers,string channel,
        Node3D world,IReadOnlyList<SpatialAudioSend> sends,OriginalAudioOutputs epoch,
        int logicalOutputChannels,Func<SpatialSoundCameraFrame> readCamera,Func<float>? handleGain)
    {
        RequireEngineThread();epoch.ValidateHost();
        if(!GodotObject.IsInstanceValid(world)||!world.IsInsideTree())
            throw new InvalidOperationException("audio.spatial.world_retired");
        if(logicalOutputChannels!=6)throw new NotSupportedException("audio.spatial.logical_output_channels");
        if(layers.Count==0||sends.Count==0)throw new InvalidDataException("audio.spatial.empty_cue");
        var availability=OriginalSpeakerPairGate.MatrixAvailability(epoch.SpeakerLayout);
        if(!availability.Available)throw new NotSupportedException(availability.UnresolvedOwner);
        StopSpatial(channel);
        var outputs=layers.SelectMany(layer=>sends.Select(send=>new MatrixSpatialOutput(
            new OriginalAudioPlayer { Name="MapSourceSpatialEffect",Bus=send.Bus,PitchScale=layer.PlaybackRate },layer,send))).ToArray();
        var voice=new SpatialVoice([],layers.Select(layer=>layer.Clip).Distinct(ReferenceEqualityComparer.Instance)
            .Cast<AudioClip>().ToArray(),null,AudioVolumeGroup.Effects,handleGain)
            { MatrixOutputs=outputs,OutputEpoch=epoch,World=world,ReadCamera=readCamera,LogicalOutputChannels=logicalOutputChannels };
        _spatialVoices.Add(channel,voice);
        try
        {
            var initialCamera=readCamera();
            AudioServer.Lock();
            try
            {
                foreach(var output in outputs)
                {
                    world.AddChild(output.Player);
                    var matrix=output.IsControl ? output.Layer.ControlMatrix ?? throw new InvalidDataException("audio.spatial.control_matrix_source") :
                        SpatialDeviceMatrix(epoch,(output.Layer.SourceMatrix ?? throw new InvalidDataException("audio.spatial.matrix_source"))(initialCamera,logicalOutputChannels));
                    output.Player.SetSpatialMatrixClip(output.Layer.Clip,matrix,output.IsControl ? GameAudioSpeakerLayout.Stereo : epoch.SpeakerLayout);
                    output.Player.Finished+=()=>
                    {
                        output.Finished=true;
                        if(voice.MatrixOutputs.All(item=>item.Finished))ReleaseSpatial(channel,voice);
                    };
                    output.Player.TreeExiting+=()=>ReleaseSpatial(channel,voice);
                }
                ApplySpatialMatrixVolume(voice);
                // Every layer/send/pair starts under the same driver lock.
                // Shorter layers only end their own child, never the cue slot.
                foreach(var output in outputs)output.Player.Play(output.Layer.StartOffsetSeconds);
            }
            finally { AudioServer.Unlock(); }
        }
        catch { ReleaseSpatial(channel,voice);throw; }
    }

    private void ApplySpatialMatrixVolume(SpatialVoice voice)
    {
        if(voice.MatrixOutputs.Length==0)return;
        var epoch=voice.OutputEpoch??throw new InvalidOperationException("audio.spatial.output_epoch");
        if(!ReferenceEquals(epoch,_originalOutputs))throw new InvalidOperationException("audio.spatial.output_replaced");
        epoch.ValidateHost();
        if(!GodotObject.IsInstanceValid(voice.World)||!voice.World!.IsInsideTree())
            throw new InvalidOperationException("audio.spatial.world_retired");
        var camera=voice.ReadCamera?.Invoke();
        float category=voice.Group == AudioVolumeGroup.Voice && !_voiceEnabled ? 0f : Volumes.Percent(voice.Group)/100f,handle=voice.HandleGain?.Invoke()??1;
        var matrices=new Dictionary<SpatialAudioLayer,SpatialSoundSpeakerMatrix>(ReferenceEqualityComparer.Instance);
        AudioServer.Lock();
        try
        {
            foreach(var output in voice.MatrixOutputs)
            {
                var layer=output.Layer;
                if (!output.IsControl)
                {
                    if (camera is null) throw new InvalidOperationException("audio.spatial.camera_owner");
                    if(!matrices.TryGetValue(layer,out var matrix))
                        matrices[layer]=matrix=SpatialDeviceMatrix(epoch,(layer.SourceMatrix ?? throw new InvalidDataException("audio.spatial.matrix_source"))(camera,voice.LogicalOutputChannels));
                    output.Player.ApplySpatialMatrix(matrix);
                }
                //The immutable Mono1 matrix was bound at actual start. Its
                //coefficients do not depend on geometry/spread/device fold.
                float source=camera is not null && layer.DistanceGain is { } gain ? gain(camera.CameraDistance) : layer.SourceGain();
                output.Player.VolumeLinear=MathF.Min(source*category,4)*output.SendGain*handle;
            }
        }
        finally { AudioServer.Unlock(); }
    }
    private static SpatialSoundSpeakerMatrix SpatialDeviceMatrix(OriginalAudioOutputs epoch,SpatialSoundSpeakerMatrix logical)
    {
        if(logical.OutputChannels!=6)throw new InvalidDataException("audio.spatial.logical_matrix_shape");
        if(epoch.SpeakerLayout==GameAudioSpeakerLayout.Surround51)return epoch.SurroundDeviceRoute.Fold(logical);
        // The authored output graph is six-to-six. Its actual Stereo master
        // fold follows the existing Windows matrix, including disabled LFE;
        // it is not a second positional panner or a mono pre-mix.
        var coefficients=new float[logical.SourceChannels*2];
        for(int input=0;input<logical.SourceChannels;input++)
            for(int output=0;output<2;output++)
                for(int speaker=0;speaker<6;speaker++)
                    coefficients[input*2+output]+=logical.ReadGain(input,speaker)*epoch.EffectsStereoRoute.Gain(speaker,output);
        return new(logical.SourceChannels,2,coefficients);
    }

}
