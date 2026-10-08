using System.Security.Cryptography;
using System.Buffers.Binary;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Actual selected original auxiliary VS/PS. Only the proved Vulkan
/// raster-position/base-vertex adaptation changes bytes; pixel math is intact.</summary>
public sealed class OriginalDeferredAuxiliaryPixelProgram : IAsyncDisposable
{
    public LoadedShaderContainer? Source { get; }
    public G2ShaderCombination? Combination { get; }
    public LoadedOriginalDeferredAuxiliaryDepthCopySource? DepthCopySource { get; }
    public LoadedOriginalDeferredAuxiliaryStockCopySource? StockCopySource { get; }
    public OriginalShaderSpirv VertexTranslation { get; }
    public OriginalAdaptedVertexShader VertexAdapter { get; }
    public OriginalShaderSpirv PixelTranslation { get; }
    public OriginalTranslatedShaderBindings VertexBindings { get; }
    public OriginalTranslatedShaderBindings PixelBindings { get; }
    public Rid ShaderRid { get; }
    private Task? disposal;
    private readonly object retirementGate=new();

    private OriginalDeferredAuxiliaryPixelProgram(LoadedShaderContainer? source,G2ShaderCombination? combination,
        OriginalShaderSpirv vertex,OriginalAdaptedVertexShader adapter,OriginalShaderSpirv pixel,
        OriginalTranslatedShaderBindings vb,OriginalTranslatedShaderBindings pb,Rid shader,
        LoadedOriginalDeferredAuxiliaryDepthCopySource? embedded=null,LoadedOriginalDeferredAuxiliaryStockCopySource? stockCopy=null)
        =>(Source,Combination,VertexTranslation,VertexAdapter,PixelTranslation,VertexBindings,PixelBindings,ShaderRid,DepthCopySource,StockCopySource)=
            (source,combination,vertex,adapter,pixel,vb,pb,shader,embedded,stockCopy);

    public static async Task<OriginalDeferredAuxiliaryPixelProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        ValidateAsset(source);
        var combination=source.ReadG1ResourceBindings(explicitCombinationIndex).Combination;
        var vertices=combination.Stages.Where(s=>s.StageCode==1&&s.Entry is not null).ToArray();
        var pixels=combination.Stages.Where(s=>s.StageCode==0&&s.Entry is not null).ToArray();
        if(vertices.Length!=1||pixels.Length!=1||combination.Stages.Any(s=>s.StageCode>1&&s.Entry is not null))
            throw new InvalidDataException("Auxiliary source pass requires its exact original VS/PS combination.");
        int vertexIndex=vertices[0].Entry!.ProgramIndex,pixelIndex=pixels[0].Entry!.ProgramIndex;
        var vertex=compiler.Compile(source.Container.GetProgramBytes(source.Source.Bytes,vertexIndex),1);
        var pixel=compiler.Compile(source.Container.GetProgramBytes(source.Source.Bytes,pixelIndex));
        var rawVertex=SpirvShaderInterfaceReader.Read(vertex.Bytes);
        var adapter=rawVertex.StageVariables.Any(v=>v.BuiltIn is 4424 or 4425 or 4426)
            ?OriginalVertexShaderAdapter.Adapt(vertex.Bytes,0,0,0)
            :OriginalVertexShaderAdapter.AdaptPositionInput(vertex.Bytes);
        var vb=OriginalTranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(vertexIndex),adapter.Bytes);
        var pb=OriginalTranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(pixelIndex),pixel.Bytes);
        if(vb.Bindings.Any(b=>b.Device.DescriptorSet!=1||b.DescriptorKind!="UniformBuffer")||
            pb.Bindings.Any(b=>b.Device.DescriptorSet!=0||b.DescriptorKind is not("UniformBuffer" or "Texture" or "Sampler")))
            throw new InvalidDataException("Auxiliary source pass declares an unsupported device descriptor contract.");
        foreach(var input in pb.Interface.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null))
        {
            var outputs=vb.Interface.StageVariables.Where(v=>v.StorageClass==3&&v.Location==input.Location).ToArray();
            if(outputs.Length!=1||!Compatible(outputs[0].Type,input.Type))
                throw new InvalidDataException("Actual auxiliary VS does not provide its selected original PS input.");
        }
        var outputsPixel=pb.Interface.StageVariables.Where(v=>v.StorageClass==3).ToArray();
        if(outputsPixel.Length==0||outputsPixel.Any(v=>v.Location is not(0 or null)||
            v.Location is null&&v.BuiltIn!=22||!FloatValue(v.Type)))
            throw new InvalidDataException("Auxiliary source output is outside the color0/depth float contract.");
        Rid shader=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            using var stages=new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,pixel.Bytes);
            Rid created=rd.ShaderCreateFromSpirV(stages,"OriginalDeferredAuxiliaryPixelProgram");
            if(!created.IsValid)throw new InvalidDataException("Device rejected actual auxiliary source shader bytecode.");
            return created;
        });
        return new(source,combination,vertex,adapter,pixel,vb,pb,shader);
    }

    public void ValidateSource()
    {
        ObjectDisposedException.ThrowIf(disposal is not null,this);
        if(Source is null)
        {
            var sourceVertex=DepthCopySource?.VertexProgram??StockCopySource?.VertexProgram
                ??throw new InvalidDataException("Auxiliary program has no exact source owner.");
            var sourcePixel=DepthCopySource?.PixelProgram??StockCopySource!.PixelProgram;
            string expectedPixel=DepthCopySource is null?OriginalDeferredAuxiliaryStockCopySource.PixelSha256:OriginalDeferredAuxiliaryDepthCopySource.PixelSha256;
            if(Hash(sourceVertex.Dxbc)!=OriginalDeferredAuxiliaryDepthCopySource.VertexSha256||
                Hash(sourcePixel.Dxbc)!=expectedPixel||
                VertexTranslation.SourceProgramSha256!=OriginalDeferredAuxiliaryDepthCopySource.VertexSha256||
                PixelTranslation.SourceProgramSha256!=expectedPixel||
                Hash(VertexTranslation.Bytes)!=VertexTranslation.SpirvSha256||Hash(PixelTranslation.Bytes)!=PixelTranslation.SpirvSha256||
                Hash(VertexAdapter.Bytes)!=VertexAdapter.AdaptedSha256)
                throw new InvalidDataException("Auxiliary embedded depth copy differs from its exact original bytecode.");
            return;
        }
        ValidateAsset(Source);
        if(Combination is null)throw new InvalidDataException("Original auxiliary G1S source combination is absent.");
        var canonical=Source.ReadG1ResourceBindings(Combination.Index).Combination;
        if(!canonical.RawRow.AsSpan().SequenceEqual(Combination.RawRow)||
            Hash(Source.Container.GetProgramBytes(Source.Source.Bytes,canonical.Stages.Single(s=>s.StageCode==1&&s.Entry is not null).Entry!.ProgramIndex))!=VertexTranslation.SourceProgramSha256||
            Hash(Source.Container.GetProgramBytes(Source.Source.Bytes,canonical.Stages.Single(s=>s.StageCode==0&&s.Entry is not null).Entry!.ProgramIndex))!=PixelTranslation.SourceProgramSha256||
            Hash(VertexTranslation.Bytes)!=VertexTranslation.SpirvSha256||Hash(PixelTranslation.Bytes)!=PixelTranslation.SpirvSha256||
            Hash(VertexAdapter.Bytes)!=VertexAdapter.AdaptedSha256)
            throw new InvalidDataException("Auxiliary GPU program differs from its actual original source selection.");
    }

    public static async Task<OriginalDeferredAuxiliaryPixelProgram> BuildDepthCopyAsync(
        LoadedOriginalDeferredAuxiliaryDepthCopySource source,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        if(Hash(source.VertexProgram.Dxbc)!=OriginalDeferredAuxiliaryDepthCopySource.VertexSha256||
            Hash(source.PixelProgram.Dxbc)!=OriginalDeferredAuxiliaryDepthCopySource.PixelSha256||
            source.VertexProgram.Sha256!=OriginalDeferredAuxiliaryDepthCopySource.VertexSha256||
            source.PixelProgram.Sha256!=OriginalDeferredAuxiliaryDepthCopySource.PixelSha256)
            throw new InvalidDataException("Embedded auxiliary depth copy requires its actual pinned VS/PS pair.");
        // The compiler's explicit mapping reader is intentionally SM5-only.
        // The hash-pinned stock VS4 has one actual CB; move its SPIR-V set
        // after normal translation and retain the original DXBC unchanged.
        var vertex=compiler.Compile(source.VertexProgram.Dxbc);
        var pixel=compiler.Compile(source.PixelProgram.Dxbc);
        var adapter=OriginalVertexShaderAdapter.AdaptPositionInput(MoveStockVertexSet(vertex.Bytes));
        var vb=OriginalTranslatedShaderBindingReader.Read(OriginalDeferredAuxiliaryDepthCopySource.ReadVertexDefinitions(source.VertexProgram.Dxbc),adapter.Bytes);
        var pb=OriginalTranslatedShaderBindingReader.Read(DxbcResourceDefinitionReader.Read(source.PixelProgram.Dxbc),pixel.Bytes);
        if(vb.Bindings.Count!=1||vb.Bindings[0].DescriptorKind!="UniformBuffer"||vb.Bindings[0].Source.BindPoint!=0||
            vb.Bindings[0].ConstantBuffer?.ByteSize!=32||pb.Bindings.Count!=1||pb.Bindings[0].DescriptorKind!="Texture"||
            pb.Bindings[0].Source.BindPoint!=0||pb.Bindings[0].Source.Dimension!=4||
            pb.Interface.StageVariables.Where(v=>v.StorageClass==3).ToArray() is not [var output]||output.BuiltIn!=22)
            throw new InvalidDataException("Embedded depth copy differs from its actual stock VS/PS interface.");
        Rid shader=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            using var stages=new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,pixel.Bytes);
            Rid created=rd.ShaderCreateFromSpirV(stages,"OriginalDeferredAuxiliaryEmbeddedDepthCopy");
            if(!created.IsValid)throw new InvalidDataException("Device rejected the exact source embedded depth copy shader.");
            return created;
        });
        return new(null,null,vertex,adapter,pixel,vb,pb,shader,source);
    }

    public static async Task<OriginalDeferredAuxiliaryPixelProgram> BuildStockCopyAsync(
        LoadedOriginalDeferredAuxiliaryStockCopySource source,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        if(Hash(source.VertexProgram.Dxbc)!=OriginalDeferredAuxiliaryStockCopySource.VertexSha256||
            Hash(source.PixelProgram.Dxbc)!=OriginalDeferredAuxiliaryStockCopySource.PixelSha256)
            throw new InvalidDataException("Auxiliary stock copy requires its actual pinned embedded VS/default3 PS.");
        var vertex=compiler.Compile(source.VertexProgram.Dxbc);
        var pixel=compiler.Compile(source.PixelProgram.Dxbc);
        var adapter=OriginalVertexShaderAdapter.AdaptPositionInput(MoveStockVertexSet(vertex.Bytes));
        var vb=OriginalTranslatedShaderBindingReader.Read(OriginalDeferredAuxiliaryDepthCopySource.ReadVertexDefinitions(source.VertexProgram.Dxbc),adapter.Bytes);
        var pb=OriginalTranslatedShaderBindingReader.Read(OriginalDeferredAuxiliaryStockCopySource.ReadPixelDefinitions(source.PixelProgram.Dxbc),pixel.Bytes);
        if(vb.Bindings is not [var vertexCb]||vertexCb.DescriptorKind!="UniformBuffer"||vertexCb.ConstantBuffer?.ByteSize!=32||
            pb.Bindings.Count!=3||pb.Bindings.Count(b=>b.DescriptorKind=="Texture"&&b.Source.BindPoint==0&&b.Source.Dimension==4)!=1||
            pb.Bindings.Count(b=>b.DescriptorKind=="Sampler"&&b.Source.BindPoint==0)!=1||
            pb.Bindings.Count(b=>b.DescriptorKind=="UniformBuffer"&&b.Source.BindPoint==0&&b.ConstantBuffer?.ByteSize==16)!=1||
            pb.Interface.StageVariables.Where(v=>v.StorageClass==3).ToArray() is not [var output]||output.Location!=0)
            throw new InvalidDataException("Auxiliary stock copy differs from its original complete CB32/CB16/t0/s0/color0 interface.");
        foreach(var input in pb.Interface.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null))
        {
            var outputs=vb.Interface.StageVariables.Where(v=>v.StorageClass==3&&v.Location==input.Location).ToArray();
            if(outputs.Length!=1||!Compatible(outputs[0].Type,input.Type))
                throw new InvalidDataException("Actual stock copy VS/PS varyings differ.");
        }
        Rid shader=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            using var stages=new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,pixel.Bytes);
            Rid created=rd.ShaderCreateFromSpirV(stages,"OriginalDeferredAuxiliaryStockCopy");
            if(!created.IsValid)throw new InvalidDataException("Device rejected the actual stock copy VS/PS.");
            return created;
        });
        return new(null,null,vertex,adapter,pixel,vb,pb,shader,stockCopy:source);
    }
    private static bool FloatValue(SpirvInterfaceType type)=>type is {Kind:"float",Bits:32}||
        type is {Kind:"vector",ComponentCount:>=1 and <=4,ComponentType:{Kind:"float",Bits:32}};
    private static bool Compatible(SpirvInterfaceType a,SpirvInterfaceType b)=>a.Kind==b.Kind&&a.Bits==b.Bits&&
        a.ComponentCount==b.ComponentCount&&a.ComponentType?.Kind==b.ComponentType?.Kind&&
        a.ComponentType?.Bits==b.ComponentType?.Bits&&a.Signed==b.Signed;

    private static byte[] MoveStockVertexSet(byte[] source)
    {
        var original=SpirvShaderInterfaceReader.Read(source);
        if(original.Descriptors is not [var cb]||cb.StorageClass!=2||cb.DescriptorSet!=0||cb.RequiredBufferBytes!=32)
            throw new InvalidDataException("Stock auxiliary VS needs its exact original one-CB descriptor set.");
        byte[] bytes=source.ToArray();int changed=0;
        for(int at=20;at<bytes.Length;)
        {
            uint first=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at,4));int words=(int)(first>>16);
            if(words<1||at+words*4>bytes.Length)throw new InvalidDataException("Stock VS SPIR-V instruction bounds differ.");
            if((first&65535)==71&&words==4&&BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at+8,4))==34)
            {
                if(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at+12,4))!=0)
                    throw new InvalidDataException("Stock VS source descriptor set differs.");
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at+12,4),1);changed++;
            }
            at+=words*4;
        }
        if(changed!=1)throw new InvalidDataException("Stock VS descriptor relocation must change exactly its single source CB set.");
        return bytes;
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static void ValidateAsset(LoadedShaderContainer source)
    {
        if(source.Source.OverrideSource is not null||string.IsNullOrWhiteSpace(source.Source.ResourceUri)||
            Hash(source.Source.Bytes)!=source.Source.DecodedSha256)
            throw new InvalidDataException("Auxiliary shader source differs from its exact original resource bytes.");
    }
    public ValueTask DisposeAsync(){lock(retirementGate)return new(disposal??=OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
    {rd.FreeRid(ShaderRid);return true;}));}
}
