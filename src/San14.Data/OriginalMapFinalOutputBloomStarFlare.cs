using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public readonly record struct OriginalFinalEffectScissor(int Left,int Top,int Width,int Height);
public sealed record OriginalBloomOrdinaryDrawRecipe(OriginalMapFinalOutputPass Pass,
    OriginalMapFinalOutputTextureAllocation Allocation,OriginalRenderTextureFormats Formats,
    IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> Constants,
    ReadOnlyMemory<byte> Quad,OriginalFinalEffectScissor Scissor,bool Clear,uint ClearPackedColor,
    bool EnableBlend,uint NativeBlendWord,string SourceEvidence)
{
    public uint NativeClearFlags=>Clear?15u:0u;
}
public enum OriginalStarTextureRole { Source,Scratch0,Scratch1,Final }
public sealed record OriginalStarTextureSelector(uint Register,OriginalStarTextureRole Role,int? SpkTextureIndex);
public sealed record OriginalStarScratchRecipe(int Width,int Height,uint NativePixelFormat,
    OriginalRenderTextureFormats Formats,int SourceTextureIndex,int Count);
public sealed record OriginalStarDrawRecipe(int Line,int Phase,OriginalMapFinalOutputPass Pass,
    IReadOnlyList<OriginalStarTextureSelector> Inputs,IReadOnlyList<OriginalStarTextureSelector> Outputs,
    bool Clear,uint ClearPackedColor,bool EnableBlend,uint NativeBlendWord,float MergeWeight,
    IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> Constants,ReadOnlyMemory<byte> Quad)
{
    public uint NativeClearFlags=>Clear?15u:0u;
    public int SourcePassIndex=>16;
}
public sealed record OriginalStarEffectRecipe(IReadOnlyList<OriginalStarDrawRecipe> Draws,
    OriginalStarScratchRecipe Scratch,int LineCount,float Length,float Attenuation,float InclinationDegrees,
    string SourceEvidence);
public sealed record OriginalStockFlareDrawRecipe(int QuadIndex,int SourceTextureIndex,int TargetTextureIndex,
    float Ratio,uint PackedColor,ReadOnlyMemory<byte> Quad,IReadOnlyList<uint> NativeIndices,
    IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> Constants,
    G1TextureSamplerRecipe Sampler,bool Clear,uint ClearPackedColor,bool EnableBlend,uint NativeBlendWord)
{
    public uint NativeClearFlags=>Clear?15u:0u;
    public int ActualStockPixelMode=>3;
    public int SourcePassIndex=>18;
    public int VertexStride=>24;
}
public sealed record OriginalStockFlareEffectRecipe(IReadOnlyList<OriginalStockFlareDrawRecipe> Draws,
    OriginalMapFinalOutputTextureAllocation Allocation,OriginalRenderTextureFormats Formats,string SourceEvidence);

/// <summary>Original six ordinary Bloom draws, twelve source Star draws and
/// thirteen actual stock-screen LensFlare quads. No GPU dependency or native
/// function execution. All retained CBs and current viewport/quad are explicit.
/// Native final weights never gate these producer recipes.</summary>
public static class OriginalMapFinalOutputBloomStarFlare
{
    public const string MathEvidenceId="ce-20261002-star-1032034-101EC10-finite-opcode";
    public const uint NativeInverseTangentResultBits=0x3EBF96B5,NativeStarExponentBaseBits=0x3FAFE5AD;
    private static readonly uint[] PowerBits=
    [0x3F800000,0x3F6E939B,0x3F5E56C9,0x3F4F34E1,0x3F411AA2,0x3F33F61A,0x3F27B68F,0x3F1C4C6C,
     0x3F800000,0x3F11A92B,0x3EA5C225,0x3E3CA0FF,0x3DD6A7AE,0x3D7445A6,0x3D0AFCDD,0x3C9E2A25,
     0x3F800000,0x3C33FCC5,0x38FD16EA,0x35B1F0E6,0x327A364C,0x2F2FEB0A,0x2BF75E02,0x28ADEB11];
    private static readonly uint[] RatioBits=
    [0x3F333333,0x3ECCCCCD,0x3EA66666,0x3E800000,0x3E051EB8,0x3DCCCCCD,
     0xBF333333,0xBECCCCCD,0xBEA66666,0xBE800000,0xBE051EB8,0xBDCCCCCD,0xC0C00000];
    private static readonly uint[] Colors=
    [0xFFFF6060,0xFF60FF60,0xFF6060FF,0xFFFF6060,0xFF60FF60,0xFF6060FF,
     0x80FF6060,0x8060FF60,0x806060FF,0x80FF6060,0x8060FF60,0x806060FF,0x60FFA4A4];
    private static readonly float[][] StarColors=
    [[.5f,.5f,.5f,0],[.8f,.3f,.3f,0],[1,.2f,.2f,0],[.5f,.2f,.6f,0],
     [.2f,.2f,1,0],[.2f,.3f,.7f,0],[.2f,.6f,.2f,0],[.3f,.5f,.3f,0]];

    /// <summary>C01FF0 RT property overrides, not 96AFC0 constructor alone:
    /// U/V1, Mag/Min1, Mip2, Aniso0, bias0 and one native texture level.</summary>
    public static ReadOnlyMemory<byte> TempProducerKey(OriginalMapFinalOutputPassGraph graph,int textureIndex)
    {
        if(textureIndex<0||textureIndex>=graph.Textures.Count||graph.Textures[textureIndex].NativeKind is not(3 or 4))
            throw new InvalidDataException("Fresh TEMP sampler needs an actual source TEMP/KEEP allocation.");
        return Convert.FromHexString("1594300000000000");
    }

    /// <summary>E6F675/E6F90A/E6F958 writes current prepared Camera .178.
    /// False TrueHDR forces exact MaxLum3F800054. No source default100 survives.</summary>
    public static IReadOnlyDictionary<int,ReadOnlyMemory<byte>> ApplyBloomCameraParameters(
        IReadOnlyDictionary<int,ReadOnlyMemory<byte>> currentParameters,ReadOnlyMemory<byte> preparedCameraPrefix,
        bool trueHdr,float currentMaxLum)
    {
        ArgumentNullException.ThrowIfNull(currentParameters);
        if(preparedCameraPrefix.Length<0x17C||!float.IsFinite(currentMaxLum))
            throw new InvalidDataException("Bloom requires accepted prepared Camera fields and finite current max luminance.");
        float threshold=BinaryPrimitives.ReadSingleLittleEndian(preparedCameraPrefix.Span[0x178..]);
        if(!float.IsFinite(threshold))throw new InvalidDataException("Bloom Camera threshold must be finite.");
        float max=trueHdr?currentMaxLum:F(0x3F800054);
        var result=currentParameters.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray());
        result[43]=Floats(max);result[44]=Floats(trueHdr?Mul(threshold,max):threshold);
        result[45]=Floats(trueHdr?Mul(threshold,max):Math.Clamp(threshold,0,1));
        return Freeze(result);
    }

    /// <summary>Copies the exact accepted Camera prefix to the native named
    /// SPK fields consumed by this family. The caller's other current80 fields
    /// remain snapshots. Host's recovered false-TrueHDR route is the default;
    /// another route must pass its resolved flag and current maxLum explicitly.</summary>
    public static IReadOnlyDictionary<int,ReadOnlyMemory<byte>> ApplySourceCreatedCameraParameters(
        OriginalMapFinalOutputPassGraph graph,OriginalMapCameraConstructionPacket camera,
        IReadOnlyDictionary<int,ReadOnlyMemory<byte>> current80,string provenance,
        bool trueHdr=false,float currentMaxLum=0)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(current80);
        if(camera.FrameNumber<=0||string.IsNullOrWhiteSpace(provenance)||current80.Count!=graph.Parameters.Count||
            graph.Parameters.Any(p=>!current80.TryGetValue(p.Index,out var bytes)||bytes.Length!=p.ByteSize))
            throw new InvalidDataException("Source effect Camera snapshot requires all80 current named payloads and accepted provenance.");
        var result=ApplyBloomCameraParameters(current80,camera.Prefix,trueHdr,currentMaxLum).ToDictionary(p=>p.Key,p=>p.Value);
        // E6F807/E6F832 copy the two source Hex words. Their offsets lie
        // outside the prepared prefix; validate the pinned Camera properties.
        var verified=OriginalMapCameraConstructionSource.ReadPerspective(camera.Source.Resource,camera.Source.Camera.ObjectId);
        foreach(var (index,ordinal,hash) in new[]{(22,163,0xFFA618FEu),(23,164,0xEA3ADA96u)})
        {
            var property=verified.Camera.Properties[ordinal];
            if(property.Index!=ordinal||property.NameHash!=hash||property.TypeCode!=8||property.ElementCount!=1||
                property.ElementSize!=4||property.RawValue.Length!=4)
                throw new InvalidDataException("Source Hex Camera property differs from its audited native binding.");
            result[index]=property.RawValue.ToArray();
        }
        foreach(var (index,at) in new[]{(28,0x1A4),(29,0x1A8),(30,0x1AC),(31,0x1B0),(66,0x180),(67,0x184),(68,0x188)})
            result[index]=camera.Prefix.Slice(at,4).ToArray();
        return Freeze(result);
    }

    public static OriginalBloomOrdinaryDrawRecipe CreateOrdinary(OriginalMapFinalOutputPassGraph graph,
        int passIndex,int baseWidth,int baseHeight,
        IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> completeRetainedTemplates,
        IReadOnlyDictionary<int,ReadOnlyMemory<byte>> currentParameters,
        OriginalMapFinalOutputVertexWrapperState wrapper,ReadOnlyMemory<byte> currentSpkQuad,
        bool originalClearFlagPending=true)
    {
        ValidateQuad(currentSpkQuad);ArgumentNullException.ThrowIfNull(wrapper);
        if(passIndex is not(10 or 11 or 12 or 13 or 14 or 15 or 17))
            throw new InvalidDataException("Ordinary source effect recipe supports Bloom10..15 and MakeFlare17.");
        var pass=graph.Passes[passIndex];if(pass.FunctorName!="PostEffectFunctorDefaultForKids"||pass.Outputs.Count!=1)
            throw new InvalidDataException("Ordinary effect requires its actual DefaultForKids target/program.");
        var output=graph.ResolveAllocation(pass.Outputs[0].TextureIndex,baseWidth,baseHeight);ValidateExtent(output);
        var constants=graph.BuildConstants(passIndex,completeRetainedTemplates,currentParameters).ToDictionary(p=>p.Key,p=>p.Value);
        var raw=pass.RawFunctor.Span;int sourceIndex=BinaryPrimitives.ReadUInt16LittleEndian(raw[0x16..]);
        OriginalMapFinalOutputTextureAllocation? source=sourceIndex==65535?null:graph.ResolveAllocation(sourceIndex,baseWidth,baseHeight);
        if(source is not null)ValidateExtent(source);
        var mapping=ViewportMapping(raw,currentParameters,output,source);
        var vs=constants[(1,0)].ToArray();WritePosition(vs,wrapper,output.Width,output.Height);
        int texAt=vs.Length==32?16:272;WriteVector(vs,texAt,mapping.TextureParameter);
        int count=pass.Program.VertexProgramIndex switch{4=>7,5=>4,_=>0};
        if(wrapper.RetainedUvVectors.Length!=0)throw new InvalidDataException("Ordinary effects replace native UV vectors from current TEX_OFFSET fields.");
        if(count>0)
        {
            int parameter=BinaryPrimitives.ReadInt32LittleEndian(raw[0x28..]);var offsets=Current(currentParameters,parameter,count*16);
            float iw=Div(1,output.Width),ih=Div(1,output.Height);
            for(int j=0;j<count*4;j++)Write(vs,16+j*4,Mul(BinaryPrimitives.ReadSingleLittleEndian(offsets.Span[(j*4)..]),(j&1)==0?iw:ih));
        }
        constants[(1,0)]=vs;
        bool clear=raw[0x1C]!=0&&(raw[0x1D]==0||originalClearFlagPending);
        return new(pass,output,graph.ResolveTempFormats(output.TextureIndex),Freeze(constants),currentSpkQuad.ToArray(),mapping.Scissor,
            clear,BinaryPrimitives.ReadUInt32LittleEndian(raw[0x18..]),false,0x00554400,Evidence(graph,$"DefaultForKids/{passIndex}"));
    }

    public static OriginalStarEffectRecipe CreateStar(OriginalMapFinalOutputPassGraph graph,int baseWidth,int baseHeight,
        IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> completeRetainedTemplates,
        IReadOnlyDictionary<int,ReadOnlyMemory<byte>> currentParameters,
        OriginalMapFinalOutputVertexWrapperState wrapper,ReadOnlyMemory<byte> preparedCameraPrefix,
        ReadOnlyMemory<byte> currentSpkQuad)
    {
        ValidateQuad(currentSpkQuad);ArgumentNullException.ThrowIfNull(wrapper);
        if(preparedCameraPrefix.Length<0x1B4||wrapper.RetainedUvVectors.Length!=0)
            throw new InvalidDataException("Star needs current prepared semantic fields and replaces four UV vectors.");
        int lines=BinaryPrimitives.ReadInt32LittleEndian(preparedCameraPrefix.Span[0x1A4..]);
        float length=BinaryPrimitives.ReadSingleLittleEndian(preparedCameraPrefix.Span[0x1A8..]);
        float attenuation=BinaryPrimitives.ReadSingleLittleEndian(preparedCameraPrefix.Span[0x1AC..]);
        float inclination=BinaryPrimitives.ReadSingleLittleEndian(preparedCameraPrefix.Span[0x1B0..]);
        if(lines is <0 or >6||!float.IsFinite(length)||length<0||!float.IsFinite(inclination)||BitConverter.SingleToUInt32Bits(attenuation)!=0x3F733333)
            throw new InvalidDataException("Star finite source math requires attenuation bits3F733333, at most6 lines and finite accepted length/inclination.");
        var pass=graph.Passes[16];
        if(pass.FunctorName!="PostEffectFunctorRenderStarForKids"||pass.Program.VertexProgramIndex!=3||pass.Program.PixelProgramIndex!=16||
            pass.Program.PixelSha256!="D2373CE632D7788DD4DFE2C2048D289D181617A76E4FB6101E1640B94BC5A402")
            throw new InvalidDataException("Star requires original custom VS3/PS16.");
        var source=graph.ResolveAllocation(16,baseWidth,baseHeight);var final=graph.ResolveAllocation(20,baseWidth,baseHeight);ValidateExtent(source);ValidateExtent(final);
        if(source.Width!=final.Width||source.Height!=final.Height||source.PixelFormatCode!=final.PixelFormatCode)
            throw new InvalidDataException("Star source/final extents and private format must match.");
        var draws=new List<OriginalStarDrawRecipe>();var banks=ColorBanks();float inclinationRadians=Mul(inclination,F(0x3C8EFA35));
        for(int line=0;line<lines;line++)
        {
            float angle=Add(Div(Mul(line,F(0x40C90FDB)),lines),inclinationRadians);
            var direction=OriginalMapFinalOutputHexDof.NativeSinCos(angle);
            float du=Mul(Div(Mul(direction.Sin,F(0x3E99999A)),source.Width),length);
            float dv=Mul(Div(Mul(direction.Cos,F(0x3E99999A)),source.Height),length);
            for(int phase=0;phase<3;phase++)
            {
                float merge=phase==2?Div(1,lines):1;byte[] weights=new byte[128],uv=new byte[64];
                for(int tap=0;tap<8;tap++)
                {
                    float u=Mul(tap,du),v=Mul(tap,dv);
                    Write(uv,tap*8,u);Write(uv,tap*8+4,v);
                    float scalar=Mul(Mul(NativeSourcePower(phase,tap,attenuation),phase+1),.5f);
                    // E1DCD5 zeros this weight, but UV zero scratch indices are
                    // 4*tap+(tap&1): future UVs are overwritten by later taps,
                    // and tap4..7 touch the unsubmitted scratch gap. The final
                    // uploaded64B UV span therefore retains computed offsets.
                    bool rejected=MathF.Abs(u)>=F(0x3F666666)||MathF.Abs(v)>=F(0x3F666666);
                    for(int channel=0;channel<4;channel++)Write(weights,tap*16+channel*4,rejected?0:Mul(banks[2-phase][tap][channel],scalar));
                }
                var parameters=currentParameters.ToDictionary(p=>p.Key,p=>p.Value);parameters[9]=weights;parameters[10]=Floats(merge);
                var constants=graph.BuildConstants(16,completeRetainedTemplates,parameters).ToDictionary(p=>p.Key,p=>p.Value);
                var vs=constants[(1,0)].ToArray();if(vs.Length!=288)throw new InvalidDataException("Star VS3 requires complete288B CB0.");
                WritePosition(vs,wrapper,source.Width,source.Height);uv.CopyTo(vs,16);WriteVector(vs,272,new(1,1,0,0));constants[(1,0)]=vs;
                OriginalStarTextureRole input=phase switch{0=>OriginalStarTextureRole.Source,1=>OriginalStarTextureRole.Scratch0,_=>OriginalStarTextureRole.Scratch1};
                OriginalStarTextureRole output=phase switch{0=>OriginalStarTextureRole.Scratch0,1=>OriginalStarTextureRole.Scratch1,_=>OriginalStarTextureRole.Final};
                draws.Add(new(line,phase,pass,Array.AsReadOnly(new[]{new OriginalStarTextureSelector(0,input,phase==0?16:null)}),
                    Array.AsReadOnly(new[]{new OriginalStarTextureSelector(0,output,phase==2?20:null)}),phase<2||line==0,0,true,0x00111100,merge,Freeze(constants),currentSpkQuad.ToArray()));
                du=Mul(du,8);dv=Mul(dv,8);
            }
        }
        return new(draws.AsReadOnly(),new(source.Width,source.Height,source.PixelFormatCode,graph.ResolveTempFormats(16),16,2),
            lines,length,attenuation,inclination,Evidence(graph,MathEvidenceId));
    }

    /// <summary>Bounded24 source requests; TC/SC, SSE/AVX opcode replay agrees.
    /// Native1032034 is inverse tangent, not platform tanf. No Math.Pow used.</summary>
    public static float NativeSourcePower(int phase,int tap,float attenuation)
    {
        if(phase is <0 or >2||tap is <0 or >7||BitConverter.SingleToUInt32Bits(attenuation)!=0x3F733333)
            throw new InvalidDataException("Finite Star power covers only its24 exact source .95 requests.");
        return F(PowerBits[phase*8+tap]);
    }

    public static OriginalStockFlareEffectRecipe CreateFlare(OriginalMapFinalOutputPassGraph graph,int baseWidth,int baseHeight,
        ReadOnlyMemory<byte> completeRetainedPixelTemplate,Vector2 screenScale,Vector4 colorScale)
    {
        if(completeRetainedPixelTemplate.Length!=16||!Finite(screenScale.X,screenScale.Y,colorScale.X,colorScale.Y,colorScale.Z,colorScale.W))
            throw new InvalidDataException("Stock flare requires complete16B PS cb0 and explicit finite native screen scale/color.");
        var pass=graph.Passes[18];if(pass.FunctorName!="PostEffectFunctorRenderLensFlare")throw new InvalidDataException("Flare requires its actual custom executor.");
        var allocation=graph.ResolveAllocation(22,baseWidth,baseHeight);ValidateExtent(allocation);
        var sampler=OriginalDeferredSamplerSources.DecodeKey(graph.Source.StockScreen.Native,Convert.FromHexString("1F94300000000000"));
        if(sampler.Filter!=21||sampler.AddressU!=4||sampler.AddressV!=4||sampler.AddressW!=3||sampler.MaximumLodBits!=0x7F7FFFFF)
            throw new InvalidDataException("Flare original Border/linearMip sampler conversion changed.");
        byte[] vs=new byte[32];WriteVector(vs,0,new(Div(Add(screenScale.X,screenScale.X),allocation.Width),Div(Mul(screenScale.Y,-2),allocation.Height),-1,1));WriteVector(vs,16,colorScale);
        byte[] ps=completeRetainedPixelTemplate.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(ps,0xFF7FFFFF);
        float hw=Mul(allocation.Width,.5f),hh=Mul(allocation.Height,.5f);var draws=new List<OriginalStockFlareDrawRecipe>();
        for(int j=0;j<13;j++)
        {
            float ratio=F(RatioBits[j]),dw=Mul(hw,ratio),dh=Mul(hh,ratio),l=Sub(hw,dw),t=Sub(hh,dh),r=Add(hw,dw),b=Add(hh,dh);
            byte[] quad=new byte[96];float[][] corners=[[l,t,0,0],[l,b,0,1],[r,b,1,1],[r,t,1,0]];
            for(int v=0;v<4;v++)
            {
                Write(quad,v*24,corners[v][0]);Write(quad,v*24+4,corners[v][1]);Write(quad,v*24+8,0);
                BinaryPrimitives.WriteUInt32LittleEndian(quad.AsSpan(v*24+12),Colors[j]);Write(quad,v*24+16,corners[v][2]);Write(quad,v*24+20,corners[v][3]);
            }
            var constants=new Dictionary<(uint,uint),ReadOnlyMemory<byte>>{[(1,0)]=vs.ToArray(),[(0,0)]=ps.ToArray()};
            draws.Add(new(j,21,22,ratio,Colors[j],quad,Array.AsReadOnly(new uint[]{0,1,2,0,2,3}),Freeze(constants),sampler,j==0,0,true,0x00114400));
        }
        return new(draws.AsReadOnly(),allocation,graph.ResolveTempFormats(22),Evidence(graph,"1140140-stockScreenMode3"));
    }

    private static (Vector4 TextureParameter,OriginalFinalEffectScissor Scissor) ViewportMapping(ReadOnlySpan<byte> raw,
        IReadOnlyDictionary<int,ReadOnlyMemory<byte>> parameters,OriginalMapFinalOutputTextureAllocation target,OriginalMapFinalOutputTextureAllocation? source)
    {
        var viewport=Current(parameters,14,16).Span;float x=Read(viewport,0),y=Read(viewport,4),w=Read(viewport,8),h=Read(viewport,12);
        if(!Finite(x,y,w,h)||w<=0||h<=0)throw new InvalidDataException("Effect current viewport must be finite and nonempty.");
        (float X,float Y,float W,float H,int L,int T,int R,int B) Rect(OriginalMapFinalOutputTextureAllocation a,int mx,int my)
        {
            int left=Rounded(a.Width,x),top=Rounded(a.Height,y),right=checked(left+Rounded(a.Width,w)),bottom=checked(top+Rounded(a.Height,h));
            left=checked(left-mx);top=checked(top-my);right=checked(right+mx);bottom=checked(bottom+my);
            float nx=Div(left,a.Width),ny=Div(top,a.Height),nw=Sub(Div(right,a.Width),nx),nh=Sub(Div(bottom,a.Height),ny);
            if(nw<=0||nh<=0)throw new InvalidDataException("Effect crop produces an empty native pixel rectangle.");
            return(nx,ny,nw,nh,left,top,right,bottom);
        }
        var d=Rect(target,BinaryPrimitives.ReadInt16LittleEndian(raw[0x20..]),BinaryPrimitives.ReadInt16LittleEndian(raw[0x22..]));
        float sx=x,sy=y,sw=w,sh=h;
        if(source is not null){var s=Rect(source,BinaryPrimitives.ReadInt16LittleEndian(raw[0x24..]),BinaryPrimitives.ReadInt16LittleEndian(raw[0x26..]));sx=s.X;sy=s.Y;sw=s.W;sh=s.H;}
        float rw=Div(sw,d.W),rh=Div(sh,d.H),u0=Sub(sx,Mul(rw,d.X)),v0=Sub(sy,Mul(rh,d.Y));
        float u1=Add(sx,Mul(Sub(1,d.X),rw)),v1=Add(sy,Mul(Sub(1,d.Y),rh));
        return(new(Sub(u1,u0),Sub(v1,v0),u0,v0),new(d.L,d.T,d.R-d.L,d.B-d.T));
    }
    private static float[][][] ColorBanks()
    {
        var banks=new float[3][][];
        for(int j=1;j<=3;j++)
        {
            banks[j-1]=new float[8][];float t=Div(j,3),complement=Sub(1,t),bright=Mul(t,F(0x3F2147AE));
            for(int tap=0;tap<8;tap++)
            {
                banks[j-1][tap]=new float[4];
                for(int channel=0;channel<3;channel++)banks[j-1][tap][channel]=Add(Mul(Add(Mul(StarColors[tap][channel],complement),bright),.5f),F(0x3EA147AE));
                banks[j-1][tap][3]=Add(Mul(Add(Mul(StarColors[tap][3],complement),Mul(t,0)),.5f),0);
            }
        }
        return banks;
    }
    private static string Evidence(OriginalMapFinalOutputPassGraph graph,string recipe)=>graph.Source.PostEffect.Source.ResourceUri+
        "#SPK@"+OriginalMapFinalOutputResources.SpkSha256+";"+graph.Source.StockScreen.Native.RenderRuleIdentity+";"+recipe;
    private static void ValidateQuad(ReadOnlyMemory<byte> quad){if(quad.Length!=96)throw new InvalidDataException("Effect requires copied actual four-vertex SPK position4/UV2 quad.");}
    private static void ValidateExtent(OriginalMapFinalOutputTextureAllocation a){if(a.Width<=0||a.Height<=0||a.Width>65535||a.Height>65535)throw new InvalidDataException("Effect requires native positive u16 view extent.");}
    private static ReadOnlyMemory<byte> Current(IReadOnlyDictionary<int,ReadOnlyMemory<byte>> p,int index,int count)=>p.TryGetValue(index,out var v)&&v.Length==count?v:throw new InvalidDataException($"Current effect parameter {index} is missing or incomplete.");
    private static IReadOnlyDictionary<K,ReadOnlyMemory<byte>> Freeze<K>(Dictionary<K,ReadOnlyMemory<byte>> values) where K:notnull=>new ReadOnlyDictionary<K,ReadOnlyMemory<byte>>(values);
    private static ReadOnlyMemory<byte> Floats(params float[] values){byte[] b=new byte[values.Length*4];for(int j=0;j<values.Length;j++)Write(b,j*4,values[j]);return b;}
    private static void WritePosition(byte[] b,OriginalMapFinalOutputVertexWrapperState w,int width,int height){Write(b,0,Add(Div(w.PixelOffsetX,width),0));Write(b,4,Add(Div(w.PixelOffsetY,height),0));Write(b,8,0);Write(b,12,0);}
    private static void WriteVector(byte[] b,int at,Vector4 v){Write(b,at,v.X);Write(b,at+4,v.Y);Write(b,at+8,v.Z);Write(b,at+12,v.W);}
    private static void Write(byte[] b,int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(at,4),value);
    private static float Read(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadSingleLittleEndian(b[at..]);
    private static int Rounded(int extent,float value){float r=MathF.Floor(Add(Mul(extent,value),.5f));if(!float.IsFinite(r)||r>=2147483648f||r< -2147483648f)throw new InvalidDataException("Native effect viewport pixel exceeds finite signed conversion.");return(int)r;}
    private static bool Finite(params float[] v)=>v.All(float.IsFinite);
    private static float F(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Mul(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Sub(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Div(float a,float b)=>a/b;
}
