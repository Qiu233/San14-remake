using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Exact ordinary factory7 shader, effective Before reads and color
/// outputs. A full pixel domain and actual completed inputs remain necessary
/// before the renderer may publish the written target as readable.</summary>
public sealed class OriginalMapPostEffect3DepthOfField2GraphicsSource
{
    private readonly OriginalMapPostEffect3ParameterDepthOfField2Update update;
    private readonly OriginalMapPostEffect3TextureParameterState table;
    private OriginalMapPostEffect3DepthOfField2GraphicsSource(OriginalMapPostEffect3ParameterDepthOfField2Update update,
        OriginalMapPostEffect3TextureParameterState table,OriginalMapPostEffect3DepthOfField2BeforeBindings before,
        OriginalMapPostEffect3Program vertex,OriginalMapPostEffect3Program pixel,
        IReadOnlyList<OriginalMapPostEffect3Binding> reads,IReadOnlyDictionary<int,byte> outputs,
        IReadOnlyDictionary<int,string> cbs)
        =>(this.update,this.table,Before,VertexProgramSha256,PixelProgramSha256,EffectivePixelTextureReads,
            OutputChannelMasks,RequiredConstantBufferBytesSha256)=
            (update,table,before,vertex.Sha256,pixel.Sha256,reads,outputs,cbs);
    public OriginalMapPostEffect3DepthOfField2BeforeBindings Before{get;}
    public long FrameNumber=>update.FrameNumber;
    public string ContextOwnerIdentity=>Before.ContextOwnerIdentity;
    public int SourcePassIndex=>Before.SourcePassIndex;
    public string SourceSha256=>update.ConstantBuffer.SourceSha256;
    public string VertexProgramSha256{get;}
    public string PixelProgramSha256{get;}
    public IReadOnlyList<OriginalMapPostEffect3Binding> EffectivePixelTextureReads{get;}
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
    public void RequireCurrentTextureTable(OriginalMapPostEffect3TextureParameterState actualTable)
    {update.RequireCurrentTextureTable(actualTable);RequireCurrent();}
    public IReadOnlyList<(int Register,OriginalMapPostEffect3ResolvedTextureParameter Texture)> ReadEffectiveTargets()
    {
        RequireCurrent();
        if(Before.ActualTarget0 is{} dynamic)return Array.AsReadOnly(new[]{(0,dynamic)});
        return Array.AsReadOnly(table.Allocation.Passes[SourcePassIndex].ActiveBindings
            .Where(b=>b.Role==OriginalMapPostEffect3BindingRole.RenderTargetWrite)
            .GroupBy(b=>b.RegisterIndex).Select(g=>g.Last()).OrderBy(b=>b.RegisterIndex)
            .Select(b=>((int)b.RegisterIndex,table.Require(b.ParameterIndex))).ToArray());
    }
    public OriginalFinalEffectScissor? ReadExecuteScissor(int actualTarget0Width,int actualTarget0Height,
        OriginalFinalEffectScissor? actualPreviousScissor,OriginalMapPostEffect3ParameterState state)
    {
        RequireCurrent();
        if(!Before.RequiresNativeScissorOverride)return actualPreviousScissor;
        if(actualTarget0Width<=0||actualTarget0Height<=0||state.FrameNumber!=FrameNumber||
            state.OwnerIdentity!=ContextOwnerIdentity||state.SourceSha256!=SourceSha256)
            throw new InvalidDataException("DOF2 scissor requires its actual current target0 extent and scalar frame.");
        var vp=state.RequireValue("Viewport").ToArray();
        int X(int extent,int at)=>checked((int)Floor(OriginalNativeFloat.Multiply(extent,BinaryPrimitives.ReadSingleLittleEndian(vp.AsSpan(at)))));
        return new(X(actualTarget0Width,0),X(actualTarget0Height,4),X(actualTarget0Width,8),X(actualTarget0Height,12));
    }
    public static OriginalMapPostEffect3DepthOfField2GraphicsSource Prepare(OriginalMapPostEffect3ParameterState state,
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3TextureParameterState table,
        OriginalMapPostEffect3ParameterDepthOfField2Update update,OriginalMapPostEffect3DepthOfField2BeforeBindings before)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(table);ArgumentNullException.ThrowIfNull(update);ArgumentNullException.ThrowIfNull(before);
        update.RequireCurrentScalarState(state);update.RequireCurrentTextureTable(table);before.RequirePrepared(update);int pass=before.SourcePassIndex;
        if(!ReferenceEquals(table.Allocation,allocation)||state.FrameNumber!=update.FrameNumber||
            state.OwnerIdentity!=before.ContextOwnerIdentity||state.SourceSha256!=update.ConstantBuffer.SourceSha256||
            !update.PassSelection.OrderedFactory7Passes.Contains(pass)||!Profiles.TryGetValue(pass,out var profile))
            throw new NotSupportedException("DOF2 ordinary source proof requires its actual selected fullscreen profile.");
        var vertex=OriginalMapPostEffect3Functor1FootprintSource.RequireProgram(allocation,pass,1,profile.Vertex,
            "6247ACDB399903435E4F66904BF3EF605ADBCFBD3EB642EE2B867C0497AD5C2E");
        var pixel=OriginalMapPostEffect3Functor1FootprintSource.RequireProgram(allocation,pass,0,profile.Pixel,profile.Hash);
        OriginalMapPostEffect3Functor1FootprintSource.RequireFiniteRgbaOutput(pixel,pass is not(68 or 69 or 70 or 71));
        var overrides=before.OrderedPixelTextureSetters.Select(s=>new OriginalMapPostEffect3Binding(-1,
            OriginalMapPostEffect3BindingTable.TextureParameters,s.ActualTexture.ParameterIndex,1,
            checked((short)s.NativeRegisterIndex),-1,-1,4,OriginalMapPostEffect3BindingRole.ShaderResourceRead,0,ReadOnlyMemory<byte>.Empty)).ToArray();
        var reads=OriginalMapPostEffect3Functor1FootprintSource.ReadPixelTextures(allocation,pass,overrides);
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
            OriginalMapPostEffect3Functor1FootprintSource.CaptureConstantBuffers(allocation,pass,state,[update.ConstantBuffer]));
    }
    private static float Floor(float value)
    {
        uint bits=BitConverter.SingleToUInt32Bits(value),magnitude=bits&0x7FFFFFFF;bool negative=(bits&0x80000000)!=0;
        if(magnitude>=0x4B800000)
        {if(magnitude>0x7F800000)throw new NotSupportedException("DOF2 native floor NaN exception route is unresolved.");return value;}
        if(magnitude<0x3F800000)return magnitude==0?value:negative?-1:0;
        int shift=(0x96-(int)(bits>>23))&31;uint truncated=bits&~((1u<<shift)-1);
        float result=BitConverter.UInt32BitsToSingle(truncated);
        return negative&&truncated!=bits?OriginalNativeFloat.Subtract(result,1):result;
    }
    private sealed record Profile(int Vertex,int Pixel,string Hash);
    private static readonly IReadOnlyDictionary<int,Profile> Profiles=new Dictionary<int,Profile>
    {
        [53]=new(5,8,"CDD93F1CF8E8824CBF4F7BE7D29DF575671EB7B02042C236311CB43E79973D12"),
        [54]=new(6,9,"700A40E5A5F65AF68A5FE2783A687D481C1CB0BB241C3108203B8F0528114783"),
        [55]=new(7,10,"8FAF7FE2EB81CFF5906BAF6A69E78DCFA1621642B92473DC1E210FED5B8BD731"),
        [56]=new(8,11,"443BCD4B03CD6FAAB34AE24F3F82E18B2D656291D0F25862B0413E6C0430A3BA"),
        [66]=new(13,16,"BEDB100FC0A11542296B691EA355B6EC06361164F4A93514AAB806FD9D4DF3AE"),
        [67]=new(14,17,"45F60D012CAEB93FDA2595D1B92ED2EBB16251B0D557564731F3210D889CB9EC"),
        [68]=new(15,18,"D90637325C5769C494FA8DFFF985DC09DA6E8C42EA1DB3476F1452066A8CE354"),
        [69]=new(16,19,"E03C8801E1D337C775BD30D348C70DBCFBA5CEF15F4D398CA2C8CE1DE1156055"),
        [70]=new(17,20,"41E9DA3ECCD2088C16269C8FC200E9CC23E0F42A053FAB676D4E6CB2A96CBCE2"),
        [71]=new(18,21,"730BF6A4246F347CC0EE981527C8CBFADF1829A5A610D59DB34DA15977488A78"),
        [72]=new(19,22,"548C4BFD79C6914B4FA425FFB41F6F7C0836A6A28EC000301F3E78BB82360CF3"),
        [73]=new(20,23,"0501D33F68695832C44231E5704E7558524ECD2B4A5A07EA89795AE1120D30F6"),
        [74]=new(21,24,"C0020F73E9C8F185A3DEE1801DAF31CAC5EDA137DBF0CF54045B8280929253D1"),
        [75]=new(22,25,"E3380B5C8439DFB66CF33AD7C649AF34DE62103BC76ECAF308A0D0B1A10D6CE8"),
        [76]=new(23,26,"77921479D01C1DBAB289E608D10A3F997B42E814611A196875B439CCA4F8238F"),
        [77]=new(24,27,"12E7CC376341F1836333F233B0D069CDC9733EF18A92FBCFEA36179D8928E745"),
        [78]=new(25,28,"6F36A14F6036F94DEB77804CBA7E2150C8889506CE7667BDDD4F0D59CD971650"),
        [79]=new(26,29,"6295A69A65FB68D6F7CCEAA6EF0294F8AB03E1E147D8C7935D1F12A4732D3EC5"),
        [80]=new(27,30,"2FA5D91AB21C8C6EFC39F0DB5EDFD76E4ACD779C78F851B7AFC17FA5C3616EA1"),
        [81]=new(28,31,"77756118AB74E398E02F61580C70F767BF24FAC7BAB2F8C8DE6D9E877B0A722B"),
        [82]=new(29,32,"02F65447BE5CCF241D8A2054B98658E39E5BA0ED79D4C9A3E3833FE51A35C31A"),
        [83]=new(30,33,"9A1E32859E4F7D591FAB519D3601A18084CD2CDE89E6DCFA0EDA2854F6A01640")
    };
}
