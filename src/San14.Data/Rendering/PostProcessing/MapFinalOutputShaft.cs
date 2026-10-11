using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Accepted source Camera group0 writes. LightPos keeps its actual
/// retained parameter55 because E7007D skips that native upload.</summary>
public sealed record MapFinalOutputShaftPacket(long FrameNumber,
    IReadOnlyDictionary<int,ReadOnlyMemory<byte>> CurrentParameters,
    Vector4 Power,Vector4 NoizeParams,bool DedicatedLightScene,
    string NativeContextSource);

/// <summary>Finite original E7004D..E7026D Camera writers and ordinary shaft
/// pass19..24 uploads. Input images remain actual separate native producers.</summary>
public static class MapFinalOutputShaft
{
    public const string BlurWeightsSha256="A6E1D089EB31641BE163A71A15188637CDD22CBE3AC57D1B502531567AD2D129";
    public const string WhiteNoiseSamplerKeyHex="1514300000000000";
    public const uint WhiteNoisePixelFormat=1;
    public const uint WhiteNoisePackedTexel=0xFFFFFFFF;
    public const uint LightSceneObjectId=0x2A691F4A;
    public const uint LightScenePixelFormat=0x6B;
    public const string LightSceneStockPixelSha256="F2C4CB1A2BE802F1E80CEEA3C27810BD102410687B9394029F8CE4D0816FBBD2";

    private sealed record ShaftCameraSettings(float Length, float Scale, float Frequency,
        float Weight, Vector3 Color, uint SceneType);
    private static readonly ConditionalWeakTable<LoadedMapCameraConstructionSource, ShaftCameraSettings> CameraSettings = new();

    private static ShaftCameraSettings ImportCameraSettings(LoadedMapCameraConstructionSource source)
    {
        var asset = source.Resource.Source;
        if (asset.FileId != MapCameraConstructionSource.SourceFileId ||
            asset.ResourceUri != GameResourceId.Rdb("system", MapCameraConstructionSource.SourceFileId).ToString())
            throw new InvalidDataException("Shaft settings require the supported camera library.");
        byte[] Property(uint hash, uint type, uint count)
        {
            var fields = source.Camera.Properties.Where(field => field.NameHash == hash).ToArray();
            if (fields.Length != 1 || fields[0].TypeCode != type || fields[0].ElementCount != count || fields[0].RawValue.Length != count * 4)
                throw new InvalidDataException("Shaft settings require declared typed camera fields.");
            return fields[0].RawValue;
        }
        float Scalar(uint hash) => BinaryPrimitives.ReadSingleLittleEndian(Property(hash, 8, 1));
        uint group = BinaryPrimitives.ReadUInt32LittleEndian(Property(0x5AE536A8, 5, 1));
        if (group != 0)
            throw new NotSupportedException("Shaft selected light groups require their actual light projection producer.");
        float length = Scalar(0xB2EB70EA), scale = Scalar(0xAD7E9994),
            frequency = Scalar(0x834DC2A6), weight = Scalar(0x7AD72492);
        var rgb = Property(0x41C4B441, 8, 3);
        var color = new Vector3(BinaryPrimitives.ReadSingleLittleEndian(rgb),
            BinaryPrimitives.ReadSingleLittleEndian(rgb.AsSpan(4)), BinaryPrimitives.ReadSingleLittleEndian(rgb.AsSpan(8)));
        if (new[] { length, scale, frequency, weight, color.X, color.Y, color.Z }.Any(value => !float.IsFinite(value)))
            throw new InvalidDataException("Shaft source Camera contains nonfinite scalar fields.");
        return new(length, scale, frequency, weight, color,
            BinaryPrimitives.ReadUInt32LittleEndian(Property(0x4168C674, 5, 1)));
    }

    /// <summary>Original shared SPK quad113FA60. The runner's POSITION4/UV2
    /// layout keeps every referenced component from the native128B rows;
    /// original UV Z/W are zero and unused by the recovered SPK programs.</summary>
    public static ReadOnlyMemory<byte> CopyNativeQuad96()
    {
        byte[] bytes=new byte[96];float[][] rows=[[0,0,.5f,1,0,0],[0,1,.5f,1,0,1],
            [1,0,.5f,1,1,0],[1,1,.5f,1,1,1]];
        for(int i=0;i<4;i++)for(int j=0;j<6;j++)Write(bytes,i*24+j*4,rows[i][j]);
        return bytes;
    }

    // Arguments i*i*(-0.02f), i=0..15, are fixed in E701C0. Independent
    // decoded-opcode replay of10334E0 and its FMA branch yields these same
    // normalized float32 bits in both locales; this is a finite native table.
    private static readonly uint[] WeightBits=[0x3E179965,0x3E1498EB,0x3E0BF196,0x3DFD40AA,
        0x3DDC2ACF,0x3DB7E62D,0x3D93951F,0x3D639663,0x3D2899D7,0x3CF002A1,0x3CA4223E,
        0x3C57AFEA,0x3C0828E7,0x3BA52B8F,0x3B408158,0x3AD79114];
    public static ReadOnlyMemory<byte> CopyNativeBlurWeights()
    {
        byte[] bytes=new byte[64];for(int i=0;i<16;i++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4,4),WeightBits[i]);
        return bytes;
    }

    public static MapFinalOutputShaftPacket PrepareSourceGroupZero(
        MapFinalOutputPassGraph graph,MapCameraConstructionPacket camera,
        uint applyWidth,uint applyHeight,
        IReadOnlyDictionary<int,ReadOnlyMemory<byte>> completeCurrentParameters,string nativeContextSource)
    {
        ArgumentNullException.ThrowIfNull(graph);ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(completeCurrentParameters);
        if(camera.FrameNumber<=0||applyWidth==0||applyHeight==0||string.IsNullOrWhiteSpace(nativeContextSource)||
            camera.Prefix.Length!=0x3C0||graph.Parameters.Count!=80)
            throw new InvalidDataException("Shaft writers require an accepted source Camera, original SPK parameters and actual Apply extent.");
        var source = CameraSettings.GetValue(camera.Source, ImportCameraSettings);
        float length = source.Length, scale = source.Scale, frequency = source.Frequency, weight = source.Weight;
        float r = source.Color.X, g = source.Color.Y, b = source.Color.Z;
        uint sceneType = source.SceneType;
        var parameters=new Dictionary<int,ReadOnlyMemory<byte>>();
        foreach(var declaration in graph.Parameters)
        {
            if(!completeCurrentParameters.TryGetValue(declaration.Index,out var value)||value.Length!=declaration.ByteSize)
                throw new InvalidDataException("Shaft requires each complete current parameter, including its retained LightPos.");
            parameters.Add(declaration.Index,value.ToArray());
        }
        if(completeCurrentParameters.Count!=parameters.Count||graph.Parameters[55].Name!="vLightShaftLightPos"||
            graph.Parameters[56].Name!="vLightShaftPower"||graph.Parameters[57].Name!="vLightShaftNoizeParams"||
            graph.Parameters[58].Name!="vLightShaftBlurWeights")
            throw new InvalidDataException("Shaft native parameter identities differ.");
        var power=new Vector4(0,0,0,length);var noise=new Vector4(scale,frequency,weight,0);
        parameters[56]=Floats(power.X,power.Y,power.Z,power.W);parameters[57]=Floats(noise.X,noise.Y,noise.Z,noise.W);
        parameters[58]=CopyNativeBlurWeights();
        int screen=graph.Parameters.Single(p=>p.Name=="vScreenSize"&&p.ByteSize==8).Index;
        parameters[screen]=Floats(applyWidth,applyHeight);
        bool reverse=(BinaryPrimitives.ReadUInt32LittleEndian(camera.Prefix.Span.Slice(0x158,4))&2)!=0;
        parameters[11]=Floats(reverse?-1:1,reverse?1:0);
        bool dedicated=sceneType!=0&&length>0&&(r>0||g>0||b>0);
        return new(camera.FrameNumber,new ReadOnlyDictionary<int,ReadOnlyMemory<byte>>(parameters),power,noise,dedicated,
            nativeContextSource+";"+camera.Source.ConstructorOrigin+";native=E7004D..E7026D;group0-retains-LightPos55;LightSceneGate=EB620B..EB64A3");
    }

    /// <summary>Target extent normalization113BD80, wrapper uploads1143C40
    /// and113FD00/113FF40, and exact reflected current parameter copies.</summary>
    public static IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> BuildDrawConstants(
        MapFinalOutputPassGraph graph,int passIndex,int baseWidth,int baseHeight,
        IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> completeRetainedTemplates,
        MapFinalOutputShaftPacket packet,MapFinalOutputVertexWrapperState wrapper)
    {
        ArgumentNullException.ThrowIfNull(graph);ArgumentNullException.ThrowIfNull(packet);ArgumentNullException.ThrowIfNull(wrapper);
        if(passIndex is <19 or >24||packet.FrameNumber<=0||string.IsNullOrWhiteSpace(packet.NativeContextSource)||
            wrapper.RetainedUvVectors.Length!=0||!float.IsFinite(wrapper.PixelOffsetX)||!float.IsFinite(wrapper.PixelOffsetY))
            throw new InvalidDataException("Shaft ordinary upload requires its actual pass, accepted parameters and wrapper state.");
        var pass=graph.Passes[passIndex];int expectedVertex=passIndex switch{20=>4,22 or 23=>5,_=>6};
        if(pass.FunctorName!="PostEffectFunctorDefaultForKids"||pass.Program.VertexProgramIndex!=expectedVertex||
            pass.Program.PixelProgramIndex!=passIndex||pass.Outputs.Count!=1||pass.Outputs[0].TextureIndex!=(passIndex%2==1?25:26))
            throw new InvalidDataException("Shaft source pass program and target sequence differ.");
        var target=graph.ResolveAllocation(pass.Outputs[0].TextureIndex,baseWidth,baseHeight);
        if(target.Width<=0||target.Height<=0||target.Width>ushort.MaxValue||target.Height>ushort.MaxValue)
            throw new InvalidDataException("Shaft target dimensions must fit the native positive u16 resource view.");
        var constants=graph.BuildConstants(passIndex,completeRetainedTemplates,packet.CurrentParameters).ToDictionary(p=>p.Key,p=>p.Value);
        byte[] bytes=constants[(1,0)].ToArray();int vectorCount=passIndex switch{20=>7,22 or 23=>4,_=>0};
        if(bytes.Length!=(vectorCount==0?32:288)||!packet.CurrentParameters.TryGetValue(14,out var viewport)||viewport.Length!=16)
            throw new InvalidDataException("Shaft vertex upload requires its complete native CB0 and current VIEWPORT14.");
        float width=target.Width,height=target.Height;
        Write(bytes,0,Add(Div(wrapper.PixelOffsetX,width),0));Write(bytes,4,Add(Div(wrapper.PixelOffsetY,height),0));Write(bytes,8,0);Write(bytes,12,0);
        var x=Bounds(target.Width,BinaryPrimitives.ReadSingleLittleEndian(viewport.Span),BinaryPrimitives.ReadSingleLittleEndian(viewport.Span[8..]));
        var y=Bounds(target.Height,BinaryPrimitives.ReadSingleLittleEndian(viewport.Span[4..]),BinaryPrimitives.ReadSingleLittleEndian(viewport.Span[12..]));
        int tex=vectorCount==0?16:272;Write(bytes,tex,Sub(x.End,x.Begin));Write(bytes,tex+4,Sub(y.End,y.Begin));Write(bytes,tex+8,x.Begin);Write(bytes,tex+12,y.Begin);
        if(vectorCount>0)
        {
            int index=passIndex switch{20=>5,22=>7,_=>6};
            if(BinaryPrimitives.ReadUInt32LittleEndian(pass.RawFunctor.Span.Slice(40,4))!=index||
                !packet.CurrentParameters.TryGetValue(index,out var offsets)||offsets.Length!=vectorCount*16)
                throw new InvalidDataException("Shaft current TEX_OFFSET identity or complete vectors differ.");
            float rw=Div(1,width),rh=Div(1,height);
            for(int i=0;i<vectorCount*4;i++)Write(bytes,16+i*4,Mul(BinaryPrimitives.ReadSingleLittleEndian(offsets.Span.Slice(i*4,4)),(i&1)==0?rw:rh));
        }
        constants[(1,0)]=bytes;return new ReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>>(constants);
    }
    private static byte[] Floats(params float[] values)
    {byte[] bytes=new byte[values.Length*4];for(int i=0;i<values.Length;i++)Write(bytes,i*4,values[i]);return bytes;}
    private static (float Begin,float End) Bounds(int extent,float origin,float span)
    {
        if(!float.IsFinite(origin)||!float.IsFinite(span)||span<=0)
            throw new InvalidDataException("Shaft current viewport must have finite origin and positive span.");
        int first=Rounded(extent,origin),last=unchecked(first+Rounded(extent,span));
        float begin=Div(first,extent),normalized=Sub(Div(last,extent),begin),ratio=Div(span,normalized);
        return(Sub(origin,Mul(ratio,begin)),Add(origin,Mul(Sub(1,begin),ratio)));
    }
    private static int Rounded(int extent,float value)
    {float v=MathF.Floor(Add(Mul(extent,value),.5f));return float.IsNaN(v)||v>=2147483648f||v< -2147483648f?int.MinValue:(int)v;}
    private static void Write(byte[] bytes,int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at,4),value);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Sub(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Mul(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Div(float a,float b)=>a/b;
}
