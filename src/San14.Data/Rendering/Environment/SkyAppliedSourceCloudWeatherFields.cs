using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using San14.Data.Formats;
namespace San14.Data;

/// <summary>The finite V418 cloud readset of this actual source-created Main
/// season/weather frame. Direct loader copies and current weather stores are
/// separate; no complete Camera allocation or unknown padding is fabricated.</summary>
public sealed class SkyAppliedSourceCloudWeatherFields
{
    private readonly Dictionary<int,uint> words;
    private SkyAppliedSourceCloudWeatherFields(MapSourceCreatedSeasonLightingFrame present,Dictionary<int,uint> words,string source)
    {AcceptedPresent=present;this.words=words;SourceProvenance=source;}
    public MapSourceCreatedSeasonLightingFrame AcceptedPresent {get;}
    public long FrameNumber=>AcceptedPresent.FrameNumber;
    public string SourceProvenance {get;}
    public float ReadFloat(int cameraOffset)=>BitConverter.UInt32BitsToSingle(ReadBits(cameraOffset));
    public uint ReadBits(int cameraOffset)=>words.TryGetValue(cameraOffset,out uint value)?value:throw new InvalidDataException($"Cloud Camera{cameraOffset:X} has no closed source writer.");
    /// <summary>Immutable selected Camera readset at the resource import boundary.
    /// Current1D0/1D8 and later weather stores are not part of this source.</summary>
    public static IReadOnlyDictionary<int,uint> LoadSelectedReadset(MapSeasonCameraPackage package)
    {
        var camera=package.Camera;
        Dictionary<int,uint> values=[];
        foreach(var f in Fields)
        {
            var p=camera.Properties.Single(p=>p.Index==f.Ordinal);
            if(p.NameHash!=f.Hash||p.TypeCode!=f.Type||p.ElementCount!=f.Count||p.ElementSize!=4||p.RawValue.Length!=f.Count*4)
                throw new InvalidDataException("Cloud exact Camera loader property changed.");
            uint bits=BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue.AsSpan(f.Lane*4,4));
            if(f.Type==8&&!float.IsFinite(BitConverter.UInt32BitsToSingle(bits)))throw new InvalidDataException("Cloud source field is nonfinite.");
            values.Add(f.Camera,bits);
        }
        return new ReadOnlyDictionary<int,uint>(values);
    }
    public void ValidateCurrent(MapSourceCreatedSeasonLightingFrame present)
    {if(!ReferenceEquals(AcceptedPresent,present))throw new InvalidDataException("Cloud weather fields belong to another accepted season frame.");present.AcceptedWeather.RequireCurrent(present.AcceptedSeasonCamera);}
    public static SkyAppliedSourceCloudWeatherFields PrepareCurrent(MapSourceCreatedSeasonLightingFrame present)
    {
        ArgumentNullException.ThrowIfNull(present);present.AcceptedWeather.RequireCurrent(present.AcceptedSeasonCamera);
        var source=present.AcceptedSeasonCamera.Inputs.Source;source.Selection.Validate();
        if(!source.UsesSourceCreatedEmptyAxes||source.Selection.SecondPackage is not null||source.Selection.FirstPackage.Camera.ObjectId!=MapSeasonCameraApplication.RootCameraObjectId)
            throw new NotSupportedException("Cloud weather continuation requires the actual source-created direct Root package; edited weather/interpolated packages require their own writer.");
        var camera=source.Selection.FirstPackage.Camera;
        Dictionary<int,uint> values=[];
        foreach(var f in Fields)
        {
            var p=camera.Properties.Single(p=>p.Index==f.Ordinal);
            if(p.NameHash!=f.Hash||p.TypeCode!=f.Type||p.ElementCount!=f.Count||p.ElementSize!=4||p.RawValue.Length!=f.Count*4)
                throw new InvalidDataException("Cloud exact Camera loader property changed.");
            uint bits=BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue.AsSpan(f.Lane*4,4));
            if(f.Type==8&&!float.IsFinite(BitConverter.UInt32BitsToSingle(bits)))throw new InvalidDataException("Cloud source field is nonfinite.");
            values.Add(f.Camera,bits);
        }
        uint cloudyBits=BitConverter.SingleToUInt32Bits(present.AcceptedSeasonCamera.Inputs.Weather.Climate.Cloudiness);
        string finalWriter="declared-cloud-quality-without-Main-post-selection-receipt";
        if(present.AcceptedSeasonCamera.Inputs.MainPostSelection is { } post)
        {
            post.ValidateSameMain(present.AcceptedSeasonCamera.Inputs.MainFrame.Camera);
            post.ValidateSameSelection(source.Selection);
            if(post.FrameNumber!=present.FrameNumber||!post.TryReadPreparedWord(0x674,out uint lowCost)||
                !post.TryReadPreparedWord(0x7A0,out uint resolution)||!post.TryReadPreparedWord(0x1D0,out uint finalCloudy)||
                finalCloudy!=cloudyBits)
                throw new InvalidDataException("Cloud weather requires the same-frame final Main674/7A0/1D0 quality writers.");
            values[0x674]=lowCost;values[0x7A0]=resolution;
            finalWriter=post.SourceEvidence+";actual-final-Main674/7A0/1D0;CG_QUALITY="+post.QualityOption18;
        }
        values.Add(0x1D0,cloudyBits);
        uint humidityBits=BitConverter.SingleToUInt32Bits(present.AcceptedSeasonCamera.Inputs.Weather.Climate.Humidity);
        values.Add(0x1D8,humidityBits);
        float R(int at)=>values.TryGetValue(at,out uint v)?BitConverter.UInt32BitsToSingle(v):throw new InvalidDataException($"Missing Cloud source field {at:X}.");
        void W(int at,float v){if(!float.IsFinite(v))throw new InvalidDataException("Cloud current weather result is nonfinite.");values[at]=BitConverter.SingleToUInt32Bits(v);}
        // C452A8 reads final prepared1D0. Its later640 store does not reset
        // that input to the independent SceneDatabase weather seed.
        // C45322 reads Humidity at1D8 (CPU84/ordinal852). Neighbor1D4
        // is Temperature; the independent SceneDatabase seed is not this input.
        float cloud=BitConverter.UInt32BitsToSingle(cloudyBits),humidity=BitConverter.UInt32BitsToSingle(humidityBits);
        if(!float.IsFinite(cloud)||!float.IsFinite(humidity))throw new InvalidDataException("Cloud final Main1D0/1D8 is nonfinite.");
        float cloudyWeight=Clamp(M(cloud,R(0x644)));
        float complement=S(1,cloud);
        float erodeWeight=Clamp(A(M(complement,R(0x650)),M(cloud,R(0x654))));
        float humidityWeight=Weight(humidity,R(0x78C),R(0x790));
        cloudyWeight=M(cloudyWeight,humidityWeight);erodeWeight=M(erodeWeight,humidityWeight);
        // C45379 is MAXSS: the second loaded endpoint is never below first.
        float coefficient=A(M(humidity,Max(R(0x660),R(0x65C))),M(S(1,humidity),R(0x65C)));
        float h=Weight(humidity,R(0x634),1);
        float detail=A(M(S(1,h),R(0x62C)),M(h,R(0x630)));
        float baseWeight=Weight(cloudyWeight,R(0x6E0),R(0x6DC));
        float detailWeight=Weight(cloudyWeight,R(0x718),R(0x714));
        for(int i=0;i<3;i++)
        {
            W(0x6B8+i*4,A(M(S(1,baseWeight),R(0x6D0+i*4)),M(baseWeight,R(0x6C4+i*4))));
            W(0x6F0+i*4,A(M(S(1,detailWeight),R(0x708+i*4)),M(detailWeight,R(0x6FC+i*4))));
        }
        float cirrusStrength=Clamp(A(M(complement,R(0x74C)),M(cloud,R(0x750))));
        float cloudDistortion=Clamp(A(M(complement,R(0x758)),M(cloud,R(0x75C))));
        float humidityCirrus=Weight(humidity,R(0x794),R(0x798));
        W(0x748,M(S(1,humidityCirrus),cirrusStrength));W(0x754,M(S(1,humidityCirrus),cloudDistortion));
        W(0x640,cloudyWeight);W(0x628,detail);W(0x64C,erodeWeight);W(0x658,coefficient);
        if(values[0x640]!=BitConverter.SingleToUInt32Bits(present.AcceptedWeather.SourceWeather640))
            throw new InvalidDataException("Cloud finite current640 differs from the accepted Main weather writer.");
        return new(present,values,present.SourceProvenance+";"+finalWriter+";C452A8/C45322-final-Main1D0/1D8-inputs;exact C4F6F0 loader->C409C0 extendedCamera->C452A8..C45693 finite weather stores;no Camera padding/default fallback");
    }
    private static float Weight(float x,float low,float high)=>high>low?Clamp(D(S(x,low),S(high,low))):x>=low?1:0;
    private static float Clamp(float x)=>x>=1?1:Max(0,x);
    private static float Max(float a,float b)=>a>b?a:b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
    private static readonly (int Camera,int Ordinal,uint Hash,uint Type,uint Count,int Lane)[] Fields=[
        (0x624,766,0x7269D49Bu,8u,1u,0),
        (0x62C,767,0xAA1206D2u,8u,1u,0),
        (0x630,884,0x8E2AC764u,8u,1u,0),
        (0x634,922,0x12B4C4C5u,8u,1u,0),
        (0x638,768,0xAD3CC5E1u,5u,1u,0),
        (0x63C,769,0x91558673u,5u,1u,0),
        (0x644,770,0x6C473EE9u,8u,1u,0),
        (0x648,771,0x6D91D1B1u,8u,1u,0),
        (0x650,772,0x79FA72D7u,8u,1u,0),
        (0x654,865,0x8D15E090u,8u,1u,0),
        (0x65C,773,0x965A17A9u,8u,1u,0),
        (0x660,883,0x7A72D83Bu,8u,1u,0),
        (0x664,774,0xA78F7EB7u,8u,1u,0),
        (0x668,775,0xC7CB0B89u,8u,1u,0),
        (0x66C,776,0x5C050D0Bu,5u,1u,0),
        (0x670,777,0xBE086657u,8u,1u,0),
        (0x674,778,0x2FB5219Au,5u,1u,0),
        (0x678,779,0x0808750Du,5u,1u,0),
        (0x67C,780,0x5205A6FCu,8u,1u,0),
        (0x680,783,0x90D6FF06u,8u,1u,0),
        (0x684,784,0xA5BFC747u,8u,1u,0),
        (0x688,785,0xAF0D5A27u,8u,1u,0),
        (0x68C,786,0xB192FD43u,8u,1u,0),
        (0x690,787,0xB343A1B4u,8u,3u,0),
        (0x694,787,0xB343A1B4u,8u,3u,1),
        (0x698,787,0xB343A1B4u,8u,3u,2),
        (0x69C,788,0xEECC5E46u,8u,3u,0),
        (0x6A0,788,0xEECC5E46u,8u,3u,1),
        (0x6A4,788,0xEECC5E46u,8u,3u,2),
        (0x6A8,789,0x3B68CC4Cu,8u,3u,0),
        (0x6AC,789,0x3B68CC4Cu,8u,3u,1),
        (0x6B0,789,0x3B68CC4Cu,8u,3u,2),
        (0x6B4,781,0x10FCBAA5u,8u,1u,0),
        (0x6C4,790,0x8EAF526Au,8u,3u,0),
        (0x6C8,790,0x8EAF526Au,8u,3u,1),
        (0x6CC,790,0x8EAF526Au,8u,3u,2),
        (0x6D0,960,0xAA9691D8u,8u,3u,0),
        (0x6D4,960,0xAA9691D8u,8u,3u,1),
        (0x6D8,960,0xAA9691D8u,8u,3u,2),
        (0x6DC,962,0x8C8CCA68u,8u,1u,0),
        (0x6E0,963,0xE1F08DBAu,8u,1u,0),
        (0x6E4,791,0x7DC45186u,8u,3u,0),
        (0x6E8,791,0x7DC45186u,8u,3u,1),
        (0x6EC,791,0x7DC45186u,8u,3u,2),
        (0x6FC,792,0x049506D2u,8u,3u,0),
        (0x700,792,0x049506D2u,8u,3u,1),
        (0x704,792,0x049506D2u,8u,3u,2),
        (0x708,961,0x207C4640u,8u,3u,0),
        (0x70C,961,0x207C4640u,8u,3u,1),
        (0x710,961,0x207C4640u,8u,3u,2),
        (0x714,964,0xEC741B98u,8u,1u,0),
        (0x718,965,0x4388C286u,8u,1u,0),
        (0x71C,782,0xD54F1225u,8u,1u,0),
        (0x720,793,0xA5912020u,8u,2u,0),
        (0x724,793,0xA5912020u,8u,2u,1),
        (0x728,794,0xB2C1424Eu,8u,2u,0),
        (0x72C,794,0xB2C1424Eu,8u,2u,1),
        (0x730,795,0x02A8CE88u,8u,2u,0),
        (0x734,795,0x02A8CE88u,8u,2u,1),
        (0x738,796,0x581C699Eu,8u,3u,0),
        (0x73C,796,0x581C699Eu,8u,3u,1),
        (0x740,796,0x581C699Eu,8u,3u,2),
        (0x744,866,0x25D30EB7u,8u,1u,0),
        (0x74C,867,0x06DA86C3u,8u,1u,0),
        (0x750,868,0x1F6079BCu,8u,1u,0),
        (0x758,869,0xBFEB0FF3u,8u,1u,0),
        (0x75C,870,0x14618EECu,8u,1u,0),
        (0x760,871,0xEBD6415Fu,8u,1u,0),
        (0x764,845,0xCE543121u,8u,2u,0),
        (0x768,845,0xCE543121u,8u,2u,1),
        (0x76C,846,0xE1D915B9u,8u,2u,0),
        (0x770,846,0xE1D915B9u,8u,2u,1),
        (0x774,847,0x0EB2E27Cu,8u,2u,0),
        (0x778,847,0x0EB2E27Cu,8u,2u,1),
        (0x77C,848,0xF85822E4u,8u,2u,0),
        (0x780,848,0xF85822E4u,8u,2u,1),
        (0x784,849,0x1D34957Au,8u,2u,0),
        (0x788,849,0x1D34957Au,8u,2u,1),
        (0x78C,879,0x550E67B9u,8u,1u,0),
        (0x790,880,0x1474A2B2u,8u,1u,0),
        (0x794,881,0xE7149CB3u,8u,1u,0),
        (0x798,882,0x1475C0DAu,8u,1u,0),
        (0x7A0,838,0x58804691u,4u,1u,0)];
}
