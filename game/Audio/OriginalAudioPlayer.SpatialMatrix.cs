using Godot;
using San14.Data;

internal partial class OriginalAudioPlayer
{
    private int matrixSourceChannels, matrixOutputChannels;
    private readonly List<AudioEffectInstance> matrixInstances=[];

    // One unchanged source stream is clocked into each physical destination
    // pair. The native pair effect applies the real 1/2-input matrix; gainBus
    // still owns every category/handle/send gain, including LFE.
    internal void SetSpatialMatrixClip(AudioClip clip, SpatialSoundSpeakerMatrix matrix,
        GameAudioSpeakerLayout layout)
    {
        if (clip.OutputChannels is not (1 or 2) || matrix.SourceChannels != clip.OutputChannels ||
            (layout == GameAudioSpeakerLayout.Surround51 ? matrix.OutputChannels is not (4 or 6 or 8) : matrix.OutputChannels != 2))
            throw new InvalidDataException("audio.spatial.matrix_source_channels");
        ClearClip();
        AudioServer.Lock();
        try
        {
            matrixSourceChannels=matrix.SourceChannels; matrixOutputChannels=matrix.OutputChannels;
            gainBus="OriginalVoiceGain"+GetInstanceId();
            int gain=AudioServer.BusCount; AudioServer.AddBus(); AudioServer.SetBusName(gain,gainBus);
            AudioServer.SetBusSend(gain,bus); AudioServer.SetBusVolumeLinear(gain,volume);
            for(int pair=0;pair<matrixOutputChannels/2;pair++)
            {
                string name=gainBus+"_Pair"+pair;
                int slot=AudioServer.BusCount; AudioServer.AddBus(); AudioServer.SetBusName(slot,name);
                AudioServer.SetBusSend(slot,gainBus);
                try
                {
                    pairBuses.Add((name,OriginalSpeakerPairGate.InstallMatrix(slot,pair,layout)));
                    matrixInstances.Add(OriginalSpeakerPairGate.AcquireMatrixInstance(slot,pair));
                }
                catch { AudioServer.RemoveBus(slot); throw; }
                var player=new AudioStreamPlayer { Name="SourceMatrixPair"+pair,Stream=clip.Stream,
                    Bus=name,VolumeLinear=1,PitchScale=pitch,MixTarget=AudioStreamPlayer.MixTargetEnum.Surround };
                AddChild(player);players.Add(player);
                int identity=pair;long owner=generation;
                player.Finished+=()=>
                {
                    if(owner!=generation)return;
                    finished.Add(identity);
                    if(finished.Count==players.Count)Finished?.Invoke();
                };
            }
            ApplySpatialMatrixLocked(matrix);
        }
        catch { ClearClip(); throw; }
        finally { AudioServer.Unlock(); }
    }

    internal void ApplySpatialMatrix(SpatialSoundSpeakerMatrix matrix)
    {
        AudioServer.Lock();
        try { ApplySpatialMatrixLocked(matrix); }
        finally { AudioServer.Unlock(); }
    }
    private void ApplySpatialMatrixLocked(SpatialSoundSpeakerMatrix matrix)
    {
        if(matrix.SourceChannels!=matrixSourceChannels || matrix.OutputChannels!=matrixOutputChannels ||
            pairBuses.Count!=matrixOutputChannels/2 || matrixInstances.Count!=pairBuses.Count)
            throw new InvalidOperationException("audio.spatial.matrix_layout_changed");
        for(int pair=0;pair<pairBuses.Count;pair++)
        {
            int left=pair*2,right=left+1;
            OriginalSpeakerPairGate.UpdateMatrix(matrixInstances[pair],matrix.ReadGain(0,left),matrix.ReadGain(0,right),
                matrixSourceChannels==2?matrix.ReadGain(1,left):0,
                matrixSourceChannels==2?matrix.ReadGain(1,right):0);
        }
    }
    private void ClearSpatialMatrixBindings()
    {
        matrixOutputChannels=matrixSourceChannels=0;
        foreach(var instance in matrixInstances)instance.Dispose();
        matrixInstances.Clear();
    }

}
