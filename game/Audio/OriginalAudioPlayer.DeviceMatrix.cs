using Godot;
using San14.Data;

internal partial class OriginalAudioPlayer
{
    private void AddDeviceSpeakerPair(AudioClip clip,Godot.AudioStream source,int sourcePair)
    {
        var route=clip.SpeakerDeviceRoute??throw new InvalidDataException("audio.output.clip_device_route");
        if(route.LogicalChannels!=(clip.SpeakerPairs?.Count??0)*2)
            throw new InvalidDataException("audio.output.clip_logical_lanes");
        for(int targetPair=0;targetPair<route.DeviceChannels/2;targetPair++)
        {
            int input=sourcePair*2,output=targetPair*2;
            float ll=route.ReadGain(input,output),lr=route.ReadGain(input,output+1),
                rl=route.ReadGain(input+1,output),rr=route.ReadGain(input+1,output+1);
            if(ll==0&&lr==0&&rl==0&&rr==0)continue;
            string name=gainBus+"_Source"+sourcePair+"_Pair"+targetPair;
            int slot=AudioServer.BusCount;AudioServer.AddBus();AudioServer.SetBusName(slot,name);
            AudioServer.SetBusSend(slot,gainBus!);
            try
            {
                pairBuses.Add((name,OriginalSpeakerPairGate.InstallMatrix(slot,targetPair,GameAudioSpeakerLayout.Surround51)));
                using var instance=OriginalSpeakerPairGate.AcquireMatrixInstance(slot,targetPair);
                OriginalSpeakerPairGate.UpdateMatrix(instance,ll,lr,rl,rr);
            }
            catch{AudioServer.RemoveBus(slot);throw;}
            var player=new AudioStreamPlayer {Name="SourcePair"+sourcePair+"_OutputPair"+targetPair,
                Stream=source,Bus=name,VolumeLinear=1,PitchScale=pitch,MixTarget=AudioStreamPlayer.MixTargetEnum.Surround};
            int identity=players.Count;long owner=generation;
            AddChild(player);players.Add(player);
            player.Finished+=()=>
            {
                if(owner!=generation)return;
                finished.Add(identity);
                if(finished.Count==players.Count)Finished?.Invoke();
            };
        }
    }
}
