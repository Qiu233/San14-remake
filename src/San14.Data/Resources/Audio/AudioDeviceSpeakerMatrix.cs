using System.Numerics;

namespace San14.Data;

/// <summary>The final Windows device fold of an already mixed six/eight-lane
/// graph. Its output columns follow the actual WAVE speaker-mask bit order.</summary>
public sealed class AudioDeviceSpeakerMatrix
{
    private readonly float[] coefficients;
    private AudioDeviceSpeakerMatrix(int logicalChannels,int deviceChannels,float[] values)
    { LogicalChannels=logicalChannels;DeviceChannels=deviceChannels;coefficients=values; }
    public int LogicalChannels { get; }
    public int DeviceChannels { get; }
    public float ReadGain(int input,int output)
    {
        if((uint)input>=(uint)LogicalChannels||(uint)output>=(uint)DeviceChannels)
            throw new ArgumentOutOfRangeException(nameof(input));
        return coefficients[input*DeviceChannels+output];
    }

    public static AudioDeviceSpeakerMatrix PrepareWindows(int logicalChannels,int deviceChannels,
        uint nativeChannelMask,float missingLfeGain)
    {
        if(logicalChannels is not (6 or 8)||deviceChannels is not (2 or 4 or 6 or 8)||
            !float.IsFinite(missingLfeGain)||missingLfeGain<0)
            throw new InvalidDataException("audio.output.device_matrix_shape");
        // The native backend uses these exact defaults only when the device
        // supplies no speaker mask. A nonzero actual mask always wins.
        uint mask=nativeChannelMask!=0?nativeChannelMask:deviceChannels switch
            {2=>3,4=>0x33,6=>0x60F,8=>0x63F,_=>throw new InvalidDataException("audio.output.device_channels")};
        if(BitOperations.PopCount(mask)!=deviceChannels||(mask&3)!=3)
            throw new NotSupportedException("audio.output.device_speaker_mask");
        int Index(uint bit)=>(mask&bit)==0?-1:BitOperations.PopCount(mask&(bit-1));
        (int Left,int Right) Pair(uint left,uint right)
        { int l=Index(left),r=Index(right);return l<0||r<0?(-1,-1):(l,r); }
        var back=Pair(0x10,0x20);var side=Pair(0x200,0x400);
        // A side-only device supplies the backend's ordinary rear pair.
        if(back.Left<0&&side.Left>=0){back=side;side=(-1,-1);}
        var values=new float[logicalChannels*deviceChannels];
        void Put(int input,int output,float gain)=>values[input*deviceChannels+output]=gain;
        int frontLeft=Index(1),frontRight=Index(2),center=Index(4),lfe=Index(8);
        Put(0,frontLeft,1);Put(1,frontRight,1);
        if(center>=0)Put(2,center,1);
        else{Put(2,frontLeft,.7071f);Put(2,frontRight,.7071f);}
        if(lfe>=0)Put(3,lfe,1);
        else{Put(3,frontLeft,missingLfeGain*.7071f);Put(3,frontRight,missingLfeGain*.7071f);}
        void Rear(int input,(int Left,int Right) primary,(int Left,int Right) alternate)
        {
            if(primary.Left>=0){Put(input,primary.Left,1);Put(input+1,primary.Right,1);}
            else if(alternate.Left>=0){Put(input,alternate.Left,1);Put(input+1,alternate.Right,1);}
            else{Put(input,frontLeft,.7071f);Put(input+1,frontRight,.7071f);}
        }
        if(logicalChannels==6)Rear(4,back,(-1,-1));
        else
        {
            // The eight-lane backend graph orders side before back. This is
            // independent of a decoder's WAVEFORMATEXTENSIBLE PCM order.
            Rear(4,side,back);Rear(6,back,side);
        }
        return new(logicalChannels,deviceChannels,values);
    }

    public SpatialSoundSpeakerMatrix Fold(SpatialSoundSpeakerMatrix logical)
    {
        if(logical.OutputChannels!=LogicalChannels)throw new InvalidDataException("audio.output.logical_matrix_layout");
        var result=new float[logical.SourceChannels*DeviceChannels];
        for(int input=0;input<logical.SourceChannels;input++)
            for(int output=0;output<DeviceChannels;output++)
                for(int speaker=0;speaker<LogicalChannels;speaker++)
                    result[input*DeviceChannels+output]+=logical.ReadGain(input,speaker)*ReadGain(speaker,output);
        return new(logical.SourceChannels,DeviceChannels,result);
    }
}
