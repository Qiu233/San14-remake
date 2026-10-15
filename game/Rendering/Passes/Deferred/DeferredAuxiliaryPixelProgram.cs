using System.Buffers.Binary;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Actual selected original auxiliary VS/PS. Only the proved Vulkan
/// raster-position/base-vertex adaptation changes bytes; pixel math is intact.</summary>
public sealed class DeferredAuxiliaryPixelProgram : IAsyncDisposable
{
    public LoadedShaderContainer? Source { get; }
    public G2ShaderCombination? Combination { get; }
    public LoadedDeferredAuxiliaryDepthCopySource? DepthCopySource { get; }
    public LoadedDeferredAuxiliaryStockCopySource? StockCopySource { get; }
    public ShaderSpirv VertexTranslation { get; }
    public AdaptedVertexShader VertexAdapter { get; }
    public ShaderSpirv PixelTranslation { get; }
    public TranslatedShaderBindings VertexBindings { get; }
    public TranslatedShaderBindings PixelBindings { get; }
    public Rid ShaderRid { get; }
    private readonly RenderDeviceResources resources;
    private volatile bool retiring;

    private DeferredAuxiliaryPixelProgram(LoadedShaderContainer? source,G2ShaderCombination? combination,
        ShaderSpirv vertex,AdaptedVertexShader adapter,ShaderSpirv pixel,
        TranslatedShaderBindings vb,TranslatedShaderBindings pb,Rid shader,RenderDeviceResources resources,
        LoadedDeferredAuxiliaryDepthCopySource? embedded=null,LoadedDeferredAuxiliaryStockCopySource? stockCopy=null)
        =>(Source,Combination,VertexTranslation,VertexAdapter,PixelTranslation,VertexBindings,PixelBindings,ShaderRid,DepthCopySource,StockCopySource,this.resources)=
            (source,combination,vertex,adapter,pixel,vb,pb,shader,embedded,stockCopy,resources);

    public static async Task<DeferredAuxiliaryPixelProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
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
            ?VertexShaderAdapter.Adapt(vertex.Bytes,0,0,0)
            :VertexShaderAdapter.AdaptPositionInput(vertex.Bytes);
        var vb=TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(vertexIndex),adapter.Bytes);
        var pb=TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(pixelIndex),pixel.Bytes);
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
        return await InstallAsync(source,combination,vertex,adapter,pixel,vb,pb,
            "OriginalDeferredAuxiliaryPixelProgram","Device rejected actual auxiliary source shader bytecode.");
    }

    public void RequireAvailable()
    {
        ObjectDisposedException.ThrowIf(retiring,this);
    }

    public static async Task<DeferredAuxiliaryPixelProgram> BuildDepthCopyAsync(
        LoadedDeferredAuxiliaryDepthCopySource source,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        // The compiler's explicit mapping reader is intentionally SM5-only.
        // The hash-pinned stock VS4 has one actual CB; move its SPIR-V set
        // after normal translation and retain the original DXBC unchanged.
        var vertex=compiler.Compile(source.VertexProgram.Dxbc);
        var pixel=compiler.Compile(source.PixelProgram.Dxbc);
        var adapter=VertexShaderAdapter.AdaptPositionInput(MoveStockVertexSet(vertex.Bytes));
        var vb=TranslatedShaderBindingReader.Read(DeferredAuxiliaryDepthCopySource.ReadVertexDefinitions(source.VertexProgram.Dxbc),adapter.Bytes);
        var pb=TranslatedShaderBindingReader.Read(DxbcResourceDefinitionReader.Read(source.PixelProgram.Dxbc),pixel.Bytes);
        if(vb.Bindings.Count!=1||vb.Bindings[0].DescriptorKind!="UniformBuffer"||vb.Bindings[0].Source.BindPoint!=0||
            vb.Bindings[0].ConstantBuffer?.ByteSize!=32||pb.Bindings.Count!=1||pb.Bindings[0].DescriptorKind!="Texture"||
            pb.Bindings[0].Source.BindPoint!=0||pb.Bindings[0].Source.Dimension!=4||
            pb.Interface.StageVariables.Where(v=>v.StorageClass==3).ToArray() is not [var output]||output.BuiltIn!=22)
            throw new InvalidDataException("Embedded depth copy differs from its actual stock VS/PS interface.");
        return await InstallAsync(null,null,vertex,adapter,pixel,vb,pb,
            "OriginalDeferredAuxiliaryEmbeddedDepthCopy","Device rejected the exact source embedded depth copy shader.",source);
    }

    public static async Task<DeferredAuxiliaryPixelProgram> BuildStockCopyAsync(
        LoadedDeferredAuxiliaryStockCopySource source,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        var vertex=compiler.Compile(source.VertexProgram.Dxbc);
        var pixel=compiler.Compile(source.PixelProgram.Dxbc);
        var adapter=VertexShaderAdapter.AdaptPositionInput(MoveStockVertexSet(vertex.Bytes));
        var vb=TranslatedShaderBindingReader.Read(DeferredAuxiliaryDepthCopySource.ReadVertexDefinitions(source.VertexProgram.Dxbc),adapter.Bytes);
        var pb=TranslatedShaderBindingReader.Read(DeferredAuxiliaryStockCopySource.ReadPixelDefinitions(source.PixelProgram.Dxbc),pixel.Bytes);
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
        return await InstallAsync(null,null,vertex,adapter,pixel,vb,pb,
            "OriginalDeferredAuxiliaryStockCopy","Device rejected the actual stock copy VS/PS.",stockCopy:source);
    }

    private static Task<DeferredAuxiliaryPixelProgram> InstallAsync(
        LoadedShaderContainer? source,G2ShaderCombination? combination,
        ShaderSpirv vertex,AdaptedVertexShader adapter,ShaderSpirv pixel,
        TranslatedShaderBindings vb,TranslatedShaderBindings pb,string debugName,string allocationFailureMessage,
        LoadedDeferredAuxiliaryDepthCopySource? embedded=null,LoadedDeferredAuxiliaryStockCopySource? stockCopy=null)
        =>TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var resources=new RenderDeviceResources(allocationFailureMessage);
            try
            {
                using var stages=new RDShaderSpirV();
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,pixel.Bytes);
                Rid shader=resources.Own(rd.ShaderCreateFromSpirV(stages,debugName));
                return new DeferredAuxiliaryPixelProgram(source,combination,vertex,adapter,pixel,vb,pb,shader,resources,embedded,stockCopy);
            }
            catch(Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd,failure,debugName+"-partial");
                throw;
            }
        });
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
    public ValueTask DisposeAsync()
    {
        retiring=true;
        return resources.DisposeAsync();
    }
}
