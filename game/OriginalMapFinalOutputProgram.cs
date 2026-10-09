using System.Buffers.Binary;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Owns the exact explicitly selected original final output VS/PS.
/// The Vulkan vertex adaptation changes raster Position.Y. The optional proved
/// source-zero LUT profile removes descriptors and samples only inside its
/// unreachable branch; all reachable original pixel math remains intact.</summary>
public sealed class OriginalMapFinalOutputProgram : IAsyncDisposable
{
    public OriginalMapFinalOutputProgramSource Source { get; }
    public OriginalShaderSpirv VertexTranslation { get; }
    public OriginalAdaptedVertexShader VertexAdapter { get; }
    public OriginalShaderSpirv PixelTranslation { get; }
    public OriginalTranslatedShaderBindings VertexBindings { get; }
    public OriginalTranslatedShaderBindings PixelBindings { get; }
    public OriginalMapFinalOutputDeadColorCorrection? DeadColorCorrection { get; }
    public OriginalMapFinalOutputDeadColorCorrectionSpirv? PixelSpecialization { get; }
    public OriginalMapFinalOutputDeadToneLut? DeadToneLut { get; }
    public OriginalMapFinalOutputDeadToneLutSpirv? PixelToneSpecialization { get; }
    public Rid ShaderRid { get; }
    private Task? _disposal;
    private OriginalMapFinalOutputProgram(OriginalMapFinalOutputProgramSource source,OriginalShaderSpirv vertex,
        OriginalAdaptedVertexShader adapter,OriginalShaderSpirv pixel,OriginalTranslatedShaderBindings vb,
        OriginalTranslatedShaderBindings pb,Rid shader,OriginalMapFinalOutputDeadColorCorrection? dead,
        OriginalMapFinalOutputDeadColorCorrectionSpirv? specialization,
        OriginalMapFinalOutputDeadToneLut? deadTone,OriginalMapFinalOutputDeadToneLutSpirv? toneSpecialization)
        =>(Source,VertexTranslation,VertexAdapter,PixelTranslation,VertexBindings,PixelBindings,ShaderRid,DeadColorCorrection,PixelSpecialization,
            DeadToneLut,PixelToneSpecialization)=
            (source,vertex,adapter,pixel,vb,pb,shader,dead,specialization,deadTone,toneSpecialization);

    public static async Task<OriginalMapFinalOutputProgram> BuildAsync(OriginalMapFinalOutputProgramSource source,Vkd3dShaderCompiler compiler,
        OriginalMapFinalOutputDeadColorCorrection? sourceDeadColorCorrection=null,
        OriginalMapFinalOutputDeadToneLut? sourceDeadToneLut=null)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        var vs=source.VertexBytes.ToArray();var ps=source.PixelBytes.ToArray();
        if(string.IsNullOrWhiteSpace(source.SourceResourceUri)||source.Kind==OriginalMapFinalOutputProgramKind.Unspecified)
            throw new InvalidDataException("Final map shader needs its selected original source and program kind.");
        var canonical=source with{VertexBytes=vs,PixelBytes=ps};
        bool stockTone=source.Kind==OriginalMapFinalOutputProgramKind.EmbeddedTone;
        if(sourceDeadToneLut is not null&&(!stockTone||sourceDeadColorCorrection is not null))
            throw new InvalidDataException("Tone source-dead LUT profile belongs only to the exact embedded pair, independently of the A1 profile.");
        var vertex=stockTone?compiler.Compile(vs):compiler.Compile(vs,1);var pixel=compiler.Compile(ps);
        byte[] vertexDevice=stockTone?MoveStockVertexSet(vertex.Bytes):vertex.Bytes;
        var adapter=OriginalVertexShaderAdapter.AdaptPositionInput(vertexDevice);
        var vb=OriginalTranslatedShaderBindingReader.Read(stockTone?
            OriginalDeferredAuxiliaryDepthCopySource.ReadVertexDefinitions(vs):DxbcResourceDefinitionReader.Read(vs),adapter.Bytes);
        var pb=OriginalTranslatedShaderBindingReader.Read(DxbcResourceDefinitionReader.Read(ps),pixel.Bytes);
        OriginalMapFinalOutputDeadColorCorrectionSpirv? specialization=null;
        OriginalMapFinalOutputDeadToneLutSpirv? toneSpecialization=null;
        if(sourceDeadColorCorrection is not null)
        {
            specialization=OriginalMapFinalOutputDeadColorCorrectionAdapter.Adapt(pixel,pb,sourceDeadColorCorrection);
            pb=OriginalMapFinalOutputDeadColorCorrectionAdapter.ReadDeviceBindings(pb,specialization);
        }
        if(sourceDeadToneLut is not null)
        {
            toneSpecialization=OriginalMapFinalOutputDeadToneLutAdapter.Adapt(pixel,pb,sourceDeadToneLut,canonical);
            pb=OriginalMapFinalOutputDeadToneLutAdapter.ReadDeviceBindings(pb,toneSpecialization);
        }
        if(vb.Bindings.Any(b=>b.Device.DescriptorSet!=1)||pb.Bindings.Any(b=>b.Device.DescriptorSet!=0)||
            vb.Bindings.Any(b=>b.DescriptorKind!="UniformBuffer")||
            pb.Bindings.Any(b=>b.DescriptorKind is not("UniformBuffer" or "Texture" or "Sampler")))
            throw new InvalidDataException("Final output device profile requires exact VS CBs and PS CB/image/sampler descriptors.");
        var outputs=pb.Interface.StageVariables.Where(v=>v.StorageClass==3).ToArray();
        if(outputs.Length!=1||outputs[0].Location!=0||outputs[0].Type is not{Kind:"vector",ComponentCount:4,ComponentType:{Kind:"float",Bits:32}})
            throw new InvalidDataException("Final map device profile requires one exact float4 source color output.");
        foreach(var input in pb.Interface.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null))
        {
            var matches=vb.Interface.StageVariables.Where(v=>v.StorageClass==3&&v.Location==input.Location).ToArray();
            if(matches.Length!=1||!Compatible(matches[0].Type,input.Type))
                throw new InvalidDataException("Final map original VS does not provide the selected PS varying.");
        }
        Rid shader=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            using var stages=new RDShaderSpirV();stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,toneSpecialization?.Bytes??specialization?.Bytes??pixel.Bytes);
            Rid value=rd.ShaderCreateFromSpirV(stages,"OriginalMapFinalOutput");
            if(!value.IsValid)throw new InvalidDataException("Device rejected the original final output shader pair.");
            return value;
        });
        return new(canonical,vertex,adapter,pixel,vb,pb,shader,sourceDeadColorCorrection,specialization,sourceDeadToneLut,toneSpecialization);
    }
    private static byte[] MoveStockVertexSet(ReadOnlySpan<byte> source)
    {
        var before=SpirvShaderInterfaceReader.Read(source);
        if(before.Descriptors is not [var cb]||cb.StorageClass!=2||cb.DescriptorSet!=0||cb.RequiredBufferBytes!=32)
            throw new InvalidDataException("Tone stock SM4 VS differs from the exact32B cb0 interface.");
        byte[] bytes=source.ToArray();int changed=0;
        for(int at=20;at<bytes.Length;)
        {
            uint first=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));int count=(int)(first>>16);
            if(count==0||(long)at+count*4>bytes.Length)throw new InvalidDataException("Tone stock VS instruction exceeds its module.");
            if((first&65535)==71&&count==4&&BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at+4))==cb.VariableId&&
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at+8))==34)
            {BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at+12),1);changed++;}
            at+=count*4;
        }
        var after=SpirvShaderInterfaceReader.Read(bytes);
        if(changed!=1||after.Descriptors is not [var actual]||actual.VariableId!=cb.VariableId||
            actual.Binding!=cb.Binding||actual.DescriptorSet!=1||actual.RequiredBufferBytes!=32)
            throw new InvalidDataException("Tone stock SM4 VS remap changed an original descriptor or native allocation.");
        return bytes;
    }
    private static bool Compatible(SpirvInterfaceType a,SpirvInterfaceType b)=>a.Kind==b.Kind&&
        (a.Kind=="vector"?a.ComponentCount>=b.ComponentCount&&Compatible(a.ComponentType!,b.ComponentType!):
         a.Kind is "float" or "int"&&a.Bits==b.Bits&&a.Signed==b.Signed);
    internal void RequireLive()=>ObjectDisposedException.ThrowIf(_disposal is not null,this);
    public ValueTask DisposeAsync()=>new(_disposal??=OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(ShaderRid);return true;}));
}
