using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Exact ordinary factory7 shader, effective Before reads and color
/// outputs. A full pixel domain and actual completed inputs remain necessary
/// before the renderer may publish the written target as readable.</summary>
public sealed class MapPostEffect3DepthOfField2GraphicsSource
{
    private readonly MapPostEffect3ParameterDepthOfField2Update update;
    private readonly MapPostEffect3TextureParameterState table;
    private MapPostEffect3DepthOfField2GraphicsSource(MapPostEffect3ParameterDepthOfField2Update update,
        MapPostEffect3TextureParameterState table,MapPostEffect3DepthOfField2BeforeBindings before,
        MapPostEffect3Program vertex,MapPostEffect3Program pixel,
        IReadOnlyList<MapPostEffect3Binding> reads,IReadOnlyDictionary<int,byte> outputs,
        IReadOnlyDictionary<int,string> cbs)
        =>(this.update,this.table,Before,VertexProgramSha256,PixelProgramSha256,EffectivePixelTextureReads,
            OutputChannelMasks,RequiredConstantBufferBytesSha256)=
            (update,table,before,vertex.Sha256,pixel.Sha256,reads,outputs,cbs);
    public MapPostEffect3DepthOfField2BeforeBindings Before{get;}
    public long FrameNumber=>update.FrameNumber;
    public string ContextOwnerIdentity=>Before.ContextOwnerIdentity;
    public int SourcePassIndex=>Before.SourcePassIndex;
    public string SourceSha256=>update.ConstantBuffer.SourceSha256;
    public string VertexProgramSha256{get;}
    public string PixelProgramSha256{get;}
    public IReadOnlyList<MapPostEffect3Binding> EffectivePixelTextureReads{get;}
    public IReadOnlyDictionary<int,byte> OutputChannelMasks{get;}
    public IReadOnlyDictionary<int,string> RequiredConstantBufferBytesSha256{get;}
    public bool CurrentOutputColorDependsOnHistoryWrite=>update.PassSelection.OrderedFactory7Passes.Any(p=>p is >=68 and <=83);
    // This does not change PS16's read or shader-output dependency. It only
    // proves that no selected factory7 color consumer reads its history output.
    public bool AllowsAllocatedOnlyUnusedHistorySource=>SourcePassIndex==66&&!CurrentOutputColorDependsOnHistoryWrite;
    public bool AllowsUninitializedExposureLiteralRead=>SourcePassIndex is 55 or 56&&
        !(BinaryPrimitives.ReadSingleLittleEndian(update.ConstantBuffer.Bytes.Span[12..])<0);
    public bool ExposureTexelValueAffectsOutput=>SourcePassIndex is 55 or 56&&
        BinaryPrimitives.ReadSingleLittleEndian(update.ConstantBuffer.Bytes.Span[12..])<0;
    public float VertexClipDepth=>BinaryPrimitives.ReadSingleLittleEndian(update.ConstantBuffer.Bytes.Span[84..]);
    public bool RequiresPriorTargetPixels=>Before.RequiresTemporaryBlendOverride;
    public string SourceEvidence=>Before.SourceEvidence+
        ";original-DOF2-VS5..8/13..30-fullscreen-SV_VertexID;exact-PS8..11/16..33;"+
        "unique-final-return-no-discard;PS18..21-two-monotone-int-loops-bounded-by-CB18-shapeSide;"+
        "PS9-MRT-RGB-and-R;PS16-copies-t0-not-zero;CB19-native-setters-retained-with-original-declaration-absence";
    public void RequireCurrent(){update.RequireCurrentTextureTable(table);Before.RequireCurrent();}
    public void RequireCurrentTextureTable(MapPostEffect3TextureParameterState actualTable)
    {update.RequireCurrentTextureTable(actualTable);RequireCurrent();}
    public IReadOnlyList<(int Register,MapPostEffect3ResolvedTextureParameter Texture)> ReadEffectiveTargets()
    {
        RequireCurrent();
        if(Before.ActualTarget0 is{} dynamic)return Array.AsReadOnly(new[]{(0,dynamic)});
        return Array.AsReadOnly(table.Allocation.Passes[SourcePassIndex].ActiveBindings
            .Where(b=>b.Role==MapPostEffect3BindingRole.RenderTargetWrite)
            .GroupBy(b=>b.RegisterIndex).Select(g=>g.Last()).OrderBy(b=>b.RegisterIndex)
            .Select(b=>((int)b.RegisterIndex,table.Require(b.ParameterIndex))).ToArray());
    }
    public FinalEffectScissor? ReadExecuteScissor(int actualTarget0Width,int actualTarget0Height,
        FinalEffectScissor? actualPreviousScissor,MapPostEffect3ParameterState state)
    {
        RequireCurrent();
        if(!Before.RequiresNativeScissorOverride)return actualPreviousScissor;
        if(actualTarget0Width<=0||actualTarget0Height<=0||state.FrameNumber!=FrameNumber||
            state.OwnerIdentity!=ContextOwnerIdentity||state.SourceSha256!=SourceSha256)
            throw new InvalidDataException("DOF2 scissor requires its actual current target0 extent and scalar frame.");
        var vp=state.RequireValue("Viewport").ToArray();
        int X(int extent,int at)=>checked((int)Floor(NativeFloat.Multiply(extent,BinaryPrimitives.ReadSingleLittleEndian(vp.AsSpan(at)))));
        return new(X(actualTarget0Width,0),X(actualTarget0Height,4),X(actualTarget0Width,8),X(actualTarget0Height,12));
    }
    public static MapPostEffect3DepthOfField2GraphicsSource Prepare(MapPostEffect3ParameterState state,
        MapPostEffect3AllocationPlan allocation,MapPostEffect3TextureParameterState table,
        MapPostEffect3ParameterDepthOfField2Update update,MapPostEffect3DepthOfField2BeforeBindings before)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(table);ArgumentNullException.ThrowIfNull(update);ArgumentNullException.ThrowIfNull(before);
        update.RequireCurrentScalarState(state);update.RequireCurrentTextureTable(table);before.RequirePrepared(update);int pass=before.SourcePassIndex;
        if(!ReferenceEquals(table.Allocation,allocation)||state.FrameNumber!=update.FrameNumber||
            state.OwnerIdentity!=before.ContextOwnerIdentity||state.SourceSha256!=update.ConstantBuffer.SourceSha256||
            !update.PassSelection.OrderedFactory7Passes.Contains(pass)||!Profiles.TryGetValue(pass,out var profile))
            throw new NotSupportedException("DOF2 ordinary source proof requires its actual selected fullscreen profile.");
        var vertex=MapPostEffect3Functor1FootprintSource.RequireProgram(allocation,pass,1,profile.Vertex);
        var pixel=MapPostEffect3Functor1FootprintSource.RequireProgram(allocation,pass,0,profile.Pixel);
        MapPostEffect3Functor1FootprintSource.RequireFiniteRgbaOutput(pixel,pass is not(68 or 69 or 70 or 71));
        var overrides=before.OrderedPixelTextureSetters.Select(s=>new MapPostEffect3Binding(-1,
            MapPostEffect3BindingTable.TextureParameters,s.ActualTexture.ParameterIndex,1,
            checked((short)s.NativeRegisterIndex),-1,-1,4,MapPostEffect3BindingRole.ShaderResourceRead,0,ReadOnlyMemory<byte>.Empty)).ToArray();
        var reads=MapPostEffect3Functor1FootprintSource.ReadPixelTextures(allocation,pass,overrides);
        var outputs=DxbcSignatureReader.Read(pixel.Bytes.Span).Parts.Single(p=>p.Kind==DxbcSignatureKind.Output).Parameters;
        if(outputs.Any(o=>o.SemanticName.Text!="SV_Target"||o.ComponentType!=3))
            throw new InvalidDataException("DOF2 original color output signature changed.");
        var masks=new ReadOnlyDictionary<int,byte>(outputs.ToDictionary(o=>checked((int)o.Register),o=>o.Mask));
        if(pass==54&&(masks.Count!=2||masks[0]!=7||masks[1]!=1)||
            pass is 55 or 56&&(masks.Count!=3||masks[0]!=7||masks[1]!=1||masks[2]!=7)||
            pass is 66 or 67&&(masks.Count!=1||masks[0]!=1)||pass is not(54 or 55 or 56 or 66 or 67)&&(masks.Count!=1||masks[0]!=15))
            throw new InvalidDataException("DOF2 original emitted output channels differ from its source profile.");
        float side=BinaryPrimitives.ReadSingleLittleEndian(update.ConstantBuffer.Bytes.Span[96..]);
        float gather=BinaryPrimitives.ReadSingleLittleEndian(update.ConstantBuffer.Bytes.Span[20..]);
        if(pass is 68 or 69 or 70 or 71&&(!float.IsFinite(side)||side!=update.ShapeRequirement.RequestedSide||side<1||side>32767||
            !float.IsFinite(gather)||gather<0||gather>32767))
            throw new NotSupportedException("DOF2 finite native int loop requires its actual positive bounded shapeSide.");
        return new(update,table,before,vertex,pixel,reads,masks,
            MapPostEffect3Functor1FootprintSource.CaptureConstantBuffers(allocation,pass,state,[update.ConstantBuffer]));
    }
    private static float Floor(float value)
    {
        uint bits=BitConverter.SingleToUInt32Bits(value),magnitude=bits&0x7FFFFFFF;bool negative=(bits&0x80000000)!=0;
        if(magnitude>=0x4B800000)
        {if(magnitude>0x7F800000)throw new NotSupportedException("DOF2 native floor NaN exception route is unresolved.");return value;}
        if(magnitude<0x3F800000)return magnitude==0?value:negative?-1:0;
        int shift=(0x96-(int)(bits>>23))&31;uint truncated=bits&~((1u<<shift)-1);
        float result=BitConverter.UInt32BitsToSingle(truncated);
        return negative&&truncated!=bits?NativeFloat.Subtract(result,1):result;
    }
    private sealed record Profile(int Vertex,int Pixel);
    private static readonly IReadOnlyDictionary<int,Profile> Profiles=new Dictionary<int,Profile>
    {
        [53]=new(5,8),
        [54]=new(6,9),
        [55]=new(7,10),
        [56]=new(8,11),
        [66]=new(13,16),
        [67]=new(14,17),
        [68]=new(15,18),
        [69]=new(16,19),
        [70]=new(17,20),
        [71]=new(18,21),
        [72]=new(19,22),
        [73]=new(20,23),
        [74]=new(21,24),
        [75]=new(22,25),
        [76]=new(23,26),
        [77]=new(24,27),
        [78]=new(25,28),
        [79]=new(26,29),
        [80]=new(27,30),
        [81]=new(28,31),
        [82]=new(29,32),
        [83]=new(30,33)
    };
}
