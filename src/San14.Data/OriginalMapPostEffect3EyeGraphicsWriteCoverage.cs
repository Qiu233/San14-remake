using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Native pixel rectangle, in selected target-mip coordinates.
/// Viewport and scissor are deliberately distinct source states.</summary>
public readonly record struct OriginalMapPostEffect3PixelRectangle(int X,int Y,int Width,int Height);

public enum OriginalMapPostEffect3ShaderReadFootprintKind
{
    EntireSelectedMip,
    LiteralTexel,
    EntireSelectedVolumeMip
}

/// <summary>An exact original shader read and its initialized-pixel dependency.
/// An unconditional native load may have no output dependency after a known
/// constant selects its other operand. Such a receipt still retains the load,
/// resource declaration and native binding; it grants no initialized pixels.</summary>
public sealed record OriginalMapPostEffect3ShaderReadFootprint(
    uint SourceStageCode,int SourceRegister,OriginalMapPostEffect3Binding NativeBinding,
    OriginalMapPostEffect3ShaderReadFootprintKind Kind,int RelativeViewMipLevel,
    int? ActualSelectedPhysicalMipLevel,int LiteralX,int LiteralY,int LiteralZ,
    bool NativeReadInstructionExecutes,bool OutputDependsOnRead,string SourceEvidence)
{
    public bool RequiresInitializedContents=>NativeReadInstructionExecutes&&OutputDependsOnRead;
    public bool EstablishesGpuContents=>false;
}

/// <summary>Original Eye PS34/36..41 emits all four color channels after each
/// finite invocation. The complete source image is still conditional on the
/// actual renderer's viewport/scissor, sample rejection, write mask, blend,
/// initialized inputs and successful GPU submission.</summary>
public sealed class OriginalMapPostEffect3EyeGraphicsWriteCoverage
{
    private readonly OriginalMapPostEffect3EyeAdaptation2Frame frame;
    private readonly OriginalMapPostEffect3EyeBeforeBindings before;
    private OriginalMapPostEffect3EyeGraphicsWriteCoverage(OriginalMapPostEffect3EyeAdaptation2Frame frame,
        OriginalMapPostEffect3EyeBeforeBindings before,int physical,int mip,uint width,uint height,
        string vs,string ps,IReadOnlyDictionary<int,string> cbs,
        IReadOnlyList<OriginalMapPostEffect3ShaderReadFootprint> reads,
        OriginalMapPostEffect3PixelRectangle sourceScissor,uint depthWord)
    {
        this.frame=frame;this.before=before;PhysicalAllocationParameterIndex=physical;TextureMipLevel=mip;
        TextureWidth=width;TextureHeight=height;VertexProgramSha256=vs;PixelProgramSha256=ps;
        RequiredConstantBufferBytesSha256=cbs;RequiredShaderReads=reads;ExpectedSourceScissor=sourceScissor;
        VertexClipDepthWord=depthWord;
        SourceEvidence=before.SourceEvidence+";exact-native-VS31..38-fullscreen-SV_VertexID0/1/2;"+
            "originalPS34/36..41-no-discard/no-early-return;RGBA-output-after-finite-control;"+
            "source963D30/95BC90=scissor-not-viewport;target+60-native-viewport-required;"+
            "source-pixel-domain-only;actual-GPU/raster/input/CB-content-joins-required";
    }
    public long FrameNumber=>frame.FrameNumber;
    public string ContextOwnerIdentity=>frame.OwnerIdentity;
    public string SourceSha256=>frame.State.SourceSha256;
    public int SourcePassIndex=>before.Iteration.SourcePassIndex;
    public int SourceTargetRegister=>0;
    public int PhysicalAllocationParameterIndex{get;}
    public int TextureMipLevel{get;}
    public uint TextureWidth{get;}
    public uint TextureHeight{get;}
    public string VertexProgramSha256{get;}
    public string PixelProgramSha256{get;}
    public IReadOnlyDictionary<int,string> RequiredConstantBufferBytesSha256{get;}
    public IReadOnlyList<OriginalMapPostEffect3ShaderReadFootprint> RequiredShaderReads{get;}
    public OriginalMapPostEffect3PixelRectangle ExpectedSourceScissor{get;}
    public uint VertexClipDepthWord{get;}
    public bool RequiresPriorTargetContents=>before.RequiresTemporaryBlendOverride;
    public uint? RequiredTemporaryNativeColorStateF4=>before.TemporaryNativeColorStateF4;
    public byte? RequiredTemporaryNativeRt0BlendEnable1C0=>before.TemporaryNativeRt0BlendEnable1C0;
    public bool RequiresAllFourColorChannelsEnabled=>true;
    public bool RequiresRasterizationWithoutSampleRejection=>true;
    public bool RequiresNativeThreeVertexFullscreenTriangle=>true;
    public bool EstablishesGpuCompletion=>false;
    public bool EstablishesCompleteResourceContents=>false;
    public string SourceEvidence{get;}
    public void RequireCurrent()=>frame.RequireCurrent();

    /// <summary>The renderer supplies its current native viewport and enabled
    /// scissor. This checks geometry only: a full rectangle never substitutes
    /// for actual no-rejection raster/depth state, RGBA writes, prior blended
    /// contents, shader input/CB coverage or GPU completion.</summary>
    public OriginalMapPostEffect3PixelRectangle RequireActualPixelDomain(
        OriginalMapPostEffect3PixelRectangle actualViewport,
        OriginalMapPostEffect3PixelRectangle? actualEnabledScissor)
    {
        RequireCurrent();
        var full=new OriginalMapPostEffect3PixelRectangle(0,0,checked((int)TextureWidth),checked((int)TextureHeight));
        if(actualViewport!=full)
            throw new NotSupportedException("Eye coverage needs its actual full target-mip viewport, independently of scissor.");
        if(actualEnabledScissor!=ExpectedSourceScissor)
            throw new InvalidDataException("Eye actual enabled scissor differs from original floor(Viewport*targetExtent).");
        return ExpectedSourceScissor;
    }
    public bool HasCompleteMipPixelDomain(OriginalMapPostEffect3PixelRectangle actualViewport,
        OriginalMapPostEffect3PixelRectangle? actualEnabledScissor)
    {
        var domain=RequireActualPixelDomain(actualViewport,actualEnabledScissor);
        return domain==new OriginalMapPostEffect3PixelRectangle(0,0,checked((int)TextureWidth),checked((int)TextureHeight));
    }

    public static OriginalMapPostEffect3EyeGraphicsWriteCoverage Prepare(
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3EyeAdaptation2Frame frame,
        OriginalMapPostEffect3EyeBeforeBindings before)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(before);
        frame.RequireCurrent();OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(!ReferenceEquals(frame.Allocation,allocation)||!frame.OrderedIterations.Contains(before.Iteration)||
            frame.State.SourceSha256!=allocation.Source.Sha256||frame.OwnerIdentity!=allocation.ContextOwnerIdentity)
            throw new InvalidDataException("Eye graphics coverage needs its current actual selected iteration/allocation.");
        int ordinal=before.Iteration.NativeCachedPassOrdinal;
        int pass=before.Iteration.SourcePassIndex;
        if(!Profiles.TryGetValue(pass,out var profile)||profile.Ordinal!=ordinal)
            throw new NotSupportedException("Only original Eye prefilter/downsample/upsample source raster domains are closed; debug and compute use separate producers.");
        // PrepareBefore already emits the loaded locale's actual loop writer:
        // TC11592F0 / SC115A3A0. Require that same source here, rather than
        // comparing a correctly prepared SC packet with the TC role anchor.
        uint loopWriter=checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(
            frame.Locale,"OriginalMapPostEffect3EyeGraphicsWriteCoverage.cs",0x11592F0));
        if(before.TargetTextureParameterIndex is not(85 or 86)||before.TargetMipLevel is not{} mip||
            before.BloomLoopConstants is not{Bytes.Length:32} loop||loop.WriterRva!=loopWriter||
            before.Locale!=frame.Locale||loop.Locale!=frame.Locale||
            !before.NativeDrawCommand.Span.SequenceEqual(Convert.FromHexString("2D0003000000000003000000")))
            throw new InvalidDataException("Eye source target/loop writer/procedural draw differs.");
        int physical=before.TargetTextureParameterIndex.Value;
        int selectedPhysical=BinaryPrimitives.ReadInt32LittleEndian(frame.Values["Enable4KCBFix"])==1?86:85;
        int i=before.Iteration.NativeLoopIndex;
        int expectedMip=ordinal switch{0=>0,2 or 9=>1,3 or 10=>i+2,4 or 11=>frame.LevelCount-i-2,_=>-1};
        int? expectedRead=ordinal switch{0=>null,2 or 9=>0,3 or 10=>i+1,4 or 11=>frame.LevelCount-i-1,_=>null};
        if(physical!=selectedPhysical||mip!=expectedMip||before.SourceMipLevel!=expectedRead||
            (ordinal!=0&&before.SourceTextureParameterIndex!=physical))
            throw new InvalidDataException("Eye source dynamic input/target mip ordering differs.");
        var shape=OriginalMapPostEffect3AllocationShapeReader.Read(allocation.TextureAllocations.Single(a=>a.Parameter.Index==physical));
        if(shape.Dimension!=OriginalMapPostEffect3TextureDimension.Texture2D||!shape.HasRenderTargetPath||mip<0||mip>=shape.MipLevels)
            throw new InvalidDataException("Eye raster target is not its actual owned two-dimensional selected mip.");
        uint width=Math.Max(1u,shape.Width>>mip),height=Math.Max(1u,shape.Height>>mip);
        var vs=OriginalMapPostEffect3Functor1FootprintSource.RequireProgram(allocation,pass,1,profile.Vertex,EyeVertexSha);
        var ps=OriginalMapPostEffect3Functor1FootprintSource.RequireProgram(allocation,pass,0,profile.Pixel,profile.PixelSha);
        OriginalMapPostEffect3Functor1FootprintSource.RequireFiniteRgbaOutput(ps,true);
        if(loop.FrameNumber!=frame.FrameNumber||loop.OwnerIdentity!=frame.OwnerIdentity||loop.SourceSha256!=frame.State.SourceSha256||loop.Declaration.Index!=20)
            throw new InvalidDataException("Eye source loop CB belongs to another invocation.");
        uint depthWord=BinaryPrimitives.ReadUInt32LittleEndian(loop.Bytes.Span[8..]);
        float depth=BitConverter.UInt32BitsToSingle(depthWord);
        if(!float.IsFinite(depth)||depth<0||depth>1)
            throw new NotSupportedException("Original fullscreen vertex clip-Z is outside the proved D3D clip interval.");
        var packets=new[]{loop,frame.BloomMainConstants};
        var cbs=OriginalMapPostEffect3Functor1FootprintSource.CaptureConstantBuffers(allocation,pass,frame.State,packets);
        OriginalMapPostEffect3Binding[] overrides=before.RequiresDynamicPixelTextureBinding?
            [new(-1,OriginalMapPostEffect3BindingTable.TextureParameters,physical,1,0,-1,-1,4,
                OriginalMapPostEffect3BindingRole.ShaderResourceRead,0,ReadOnlyMemory<byte>.Empty)]:[];
        var reads=OriginalMapPostEffect3Functor1FootprintSource.ReadPixelTextures(allocation,pass,overrides);
        var dependencies=new List<OriginalMapPostEffect3ShaderReadFootprint>();
        foreach(var binding in reads)
        {
            if(binding.RegisterIndex==0)
            {
                var actual=before.RequiresDynamicPixelTextureBinding?
                    new OriginalMapPostEffect3Binding(-1,OriginalMapPostEffect3BindingTable.TextureParameters,physical,1,0,-1,-1,4,
                        OriginalMapPostEffect3BindingRole.ShaderResourceRead,0,ReadOnlyMemory<byte>.Empty):binding;
                dependencies.Add(new(0,0,actual,OriginalMapPostEffect3ShaderReadFootprintKind.EntireSelectedMip,0,
                    before.SourceMipLevel,0,0,0,true,true,
                    "originalEye-PS-t0-sample_l;single-mip-source-view-clamps-relativeLOD;conservative-entire-selected-mip-pixel-footprint"));
            }
            else if(pass==84&&binding.RegisterIndex==1)
            {
                bool depends=BinaryPrimitives.ReadSingleLittleEndian(frame.BloomMainConstants.Bytes.Span[16..])<0;
                dependencies.Add(new(0,1,binding,OriginalMapPostEffect3ShaderReadFootprintKind.LiteralTexel,0,null,0,0,0,true,depends,
                    "originalPS34-ld-t1(0,0).x;lt(cb4[1].x,0);movc-load-versus-known-exposure;native-load-retained-output-dependency-exact"));
            }
            else throw new NotSupportedException("Eye raster footprint has an unclosed original resource read.");
        }
        if(dependencies.Count!=(pass==84?2:1))throw new InvalidDataException("Eye exact shader texture read set differs.");
        var vp=frame.Values["Viewport"];
        float V(int n)=>BinaryPrimitives.ReadSingleLittleEndian(vp.AsSpan(n*4));
        float x=V(0),y=V(1),w=V(2),h=V(3);
        if(!float.IsFinite(x)||!float.IsFinite(y)||!float.IsFinite(w)||!float.IsFinite(h)||x<0||y<0||w<0||h<0||x+w>1||y+h>1)
            throw new NotSupportedException("Eye source scissor is limited to finite nonnegative in-target normalized rectangles.");
        var scissor=new OriginalMapPostEffect3PixelRectangle(FloorProduct(x,width),FloorProduct(y,height),FloorProduct(w,width),FloorProduct(h,height));
        return new(frame,before,physical,mip,width,height,vs.Sha256,ps.Sha256,cbs,dependencies.AsReadOnly(),scissor,depthWord);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Multiply(float a,float b)=>a*b;
    private static int FloorProduct(float fraction,uint extent)=>checked((int)MathF.Floor(Multiply(fraction,extent)));
    public const string EyeVertexSha="A6F2DA853E9EFF45D4F0137E681902401E69226626A5F55ADD04F00B9FC5AD14";
    private sealed record Profile(int Ordinal,int Vertex,int Pixel,string PixelSha);
    private static readonly IReadOnlyDictionary<int,Profile> Profiles=new Dictionary<int,Profile>
    {
        [84]=new(0,31,34,"2427A9434AE69796287E1722E0B24D80BCDE596FD50948456C41CDBCE7C2EBA3"),
        [86]=new(2,33,36,"F8D31A6D3F1ABD36E6B4070516F8FE07D4AB990DC13F4E2A7B65FFBE25A50F8A"),
        [87]=new(9,34,37,"1937188C5447C55D35BF45B7C24014C0E3920508C1C31B733C3ED21C2B8080F7"),
        [88]=new(3,35,38,"77395841B20B83720B05D87A8C62421ED5D002E3BEF6BB93FFF7A93C887157E1"),
        [89]=new(10,36,39,"FF84E6B88C535682C0C8EEA8FCDC3AEA1CE6D3BE3EF05025F30B429C8EDE4E09"),
        [90]=new(4,37,40,"66CFB1CC490EB1B58EF47DA9349AB4934989DBE9B78F2243348D418EBC524FDA"),
        [91]=new(11,38,41,"8ABE2D174D9637AB9B156D201805E8C79C21552A725F8BE4FAE10A60391E03C4")
    };
}

internal static class OriginalMapPostEffect3Functor1FootprintSource
{
    // Programs belong to the loaded immutable container. Current Before
    // bindings, texture mips and CB values are deliberately not cached.
    private static readonly ConditionalWeakTable<OriginalMapPostEffect3Program,Lazy<uint[]>> PixelTextureRegisters=new();
    private static readonly ConditionalWeakTable<OriginalMapPostEffect3Program,Lazy<bool>> PixelContainsLoopControl=new();
    internal static OriginalMapPostEffect3Program RequireProgram(OriginalMapPostEffect3AllocationPlan allocation,
        int pass,uint stage,int index,string sha)
    {
        var reference=allocation.Source.Passes[pass].ProgramReferences.Single(r=>r.StageCode==stage);
        if(reference.StageIndex!=index)throw new InvalidDataException("Fullscreen coverage selected another original stage program.");
        var p=allocation.Source.GetProgram(stage,index);
        // ContainerReader hashes the owned DXBC at source import. Selection
        // still requires this exact stage/index and pinned source program.
        if(p.Sha256!=sha)
            throw new InvalidDataException("Fullscreen coverage exact original DXBC differs.");
        return p;
    }
    internal static IReadOnlyList<OriginalMapPostEffect3Binding> ReadPixelTextures(OriginalMapPostEffect3AllocationPlan allocation,int pass,
        IReadOnlyList<OriginalMapPostEffect3Binding>? actualBeforeOverrides=null)
    {
        var reference=allocation.Source.Passes[pass].ProgramReferences.Single(p=>p.StageCode==0);
        if(reference.StageIndex is not{} index)throw new InvalidDataException("Fullscreen footprint needs the selected original pixel shader.");
        var program=allocation.Source.GetProgram(0,index);
        var registers=PixelTextureRegisters.GetValue(program,static p=>new(()=>ReadTextureRegisters(p))).Value;
        var results=new List<OriginalMapPostEffect3Binding>();
        foreach(uint register in registers)
        {
            var ordered=allocation.Passes[pass].ActiveBindings.Concat(actualBeforeOverrides??[]);
            results.Add(ordered.LastOrDefault(b=>b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&
                b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&b.RegisterIndex==register&&(b.StageMask&4)!=0)
                ??throw new InvalidDataException("Fullscreen source declared texture has no original effective binding."));
        }
        return results.AsReadOnly();
    }
    private static uint[] ReadTextureRegisters(OriginalMapPostEffect3Program program)
    {
        var textures=DxbcResourceDefinitionReader.Read(program.Bytes.Span).Bindings.Where(b=>b.InputType==2).ToArray();
        if(textures.Any(texture=>texture.BindCount!=1))
            throw new NotSupportedException("Fullscreen original texture register arrays are not closed.");
        return textures.Select(texture=>texture.BindPoint).ToArray();
    }
    internal static IReadOnlyDictionary<int,string> CaptureConstantBuffers(OriginalMapPostEffect3AllocationPlan allocation,int pass,
        OriginalMapPostEffect3ParameterState state,IReadOnlyList<OriginalMapPostEffect3ParameterConstantBuffer> packets)
    {
        var hashes=new Dictionary<int,string>();
        foreach(var requirement in OriginalMapPostEffect3ParameterBindingRequirements.Read(allocation,pass)
            .Where(r=>(r.StageCode is 0 or 1)&&r.ShaderDeclaredByteCount is not null).DistinctBy(r=>r.ParameterIndex))
        {
            var packet=packets.SingleOrDefault(p=>p.Declaration.Index==requirement.ParameterIndex)
                ??throw new NotSupportedException("Fullscreen source read has no complete original CB packet.");
            if(packet.FrameNumber!=state.FrameNumber||packet.OwnerIdentity!=state.OwnerIdentity||packet.SourceSha256!=state.SourceSha256||
                packet.Bytes.Length!=requirement.NativeDeclaration.ByteCount)
                throw new InvalidDataException("Fullscreen original CB source/frame/extent differs.");
            hashes.Add(requirement.ParameterIndex,Convert.ToHexString(SHA256.HashData(packet.Bytes.Span)));
        }
        return new ReadOnlyDictionary<int,string>(hashes);
    }
    internal static void RequireFiniteRgbaOutput(OriginalMapPostEffect3Program program,bool forbidLoops)
    {
        bool loops=PixelContainsLoopControl.GetValue(program,static p=>new(()=>ReadFiniteRgbaControlFlow(p))).Value;
        if(forbidLoops&&loops)
            throw new InvalidDataException("Original pixel coverage has unclosed discard/return/loop or call control flow.");
    }
    private static bool ReadFiniteRgbaControlFlow(OriginalMapPostEffect3Program program)
    {
        var tokenPart=program.Container.Parts.Single(p=>p.TokenHeader is not null);
        var b=tokenPart.PayloadRange.Slice(program.Bytes.Span);int words=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(b[4..]));
        if(b.Length!=words*4)throw new InvalidDataException("Original pixel token span differs.");
        int returns=0;int lastOpcode=-1;bool loops=false;
        for(int word=2;word<words;)
        {
            uint token=BinaryPrimitives.ReadUInt32LittleEndian(b[(word*4)..]);int op=(int)(token&2047);
            int n=op==53?checked((int)BinaryPrimitives.ReadUInt32LittleEndian(b[((word+1)*4)..])):(int)((token>>24)&127);
            if(n<=0||word+n>words)throw new InvalidDataException("Original pixel instruction extent differs.");
            if(op==62)returns++;
            if(op is 13 or 63 or 120 or 121)
                throw new InvalidDataException("Original pixel coverage has unclosed discard/return/loop or call control flow.");
            loops|=op is 48 or 76 or 6 or 7 or 8 or 9;
            lastOpcode=op;word+=n;
        }
        // The frozen bytecode profiles above additionally pin the inspected
        // final o0.xyz assignment and o0.w=1 after all branches. Inspect the
        // imported token stream once; a reload supplies a new program object.
        if(returns!=1||lastOpcode!=62)throw new InvalidDataException("Original pixel coverage no longer has its unique final return.");
        return loops;
    }
}
