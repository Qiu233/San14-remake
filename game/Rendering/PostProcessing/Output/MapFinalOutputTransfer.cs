using System.Buffers.Binary;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Current original stock-screen context and sampled descriptor.
/// UV endpoints include the selected Camera viewport substitution when present.
/// Scale/color are mutable context values, not implicit constructor defaults.</summary>
public sealed record MapFinalOutputStockCopyPacket(Vector2 Scale,Color ColorScale,
    Vector4 UvEndpoints,ReadOnlyMemory<byte> SampledImageSamplerDescriptor,string NativeContextSource);

/// <summary>The original stock screen VS/default3 texture pixel shader performs
/// a sampled HDR copy into the original Output sRGB target. The target hardware
/// applies its source-declared transfer; no replacement math or CPU pixels are
/// introduced. This class owns the destination, program and presentation.</summary>
public sealed class MapFinalOutputTransfer:IAsyncDisposable
{
    public RenderTargetDeviceTexture Output { get; }
    public RenderOutputPresentation Presentation { get; }
    public ShaderSpirv VertexTranslation { get; }
    public ShaderSpirv PixelTranslation { get; }
    private readonly Rid _shader,_framebuffer,_pipeline,_vertices,_vertexSet,_vertexCb,_vertexBuffer,_sampledTexture;
    private Rid _pixelSet;
    private readonly IReadOnlyDictionary<int,Rid> _pixelResources;
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetime=new();private int _accepted;private bool _retiring;private TaskCompletionSource? _drained;
    private readonly RenderDeviceResources _resources;
    private RenderDeviceResources? _pairResources,_candidatePairResources;
    private NativeMapBorrowerRetirementPackage? _pendingRetirement,_retirement;
    private Task? _disposal;
    private MapFinalOutputTransfer(RenderTargetDeviceTexture output,RenderOutputPresentation presentation,
        ShaderSpirv vs,ShaderSpirv ps,Rid shader,Rid framebuffer,Rid pipeline,Rid vertices,Rid vsSet,Rid psSet,Rid vertexCb,Rid vertexBuffer,
        RenderDeviceResources resources,RenderDeviceResources pairResources,IReadOnlyDictionary<int,Rid> pixelResources)
        =>(Output,Presentation,VertexTranslation,PixelTranslation,_shader,_framebuffer,_pipeline,_vertices,_vertexSet,_pixelSet,_vertexCb,_vertexBuffer,_resources,_pairResources,_pixelResources,_sampledTexture)=
            (output,presentation,vs,ps,shader,framebuffer,pipeline,vertices,vsSet,psSet,vertexCb,vertexBuffer,resources,pairResources,pixelResources,
                pixelResources[(int)SpirvShaderInterfaceReader.Read(ps.Bytes).Descriptors.Single(d=>d.Type.Kind=="image").Binding!.Value]);
    public static Task<MapFinalOutputTransfer> BuildAsync(LoadedMapFinalOutputResources source,
        RenderTargetDeviceTexture hdr,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(hdr);ArgumentNullException.ThrowIfNull(compiler);
        var vs=compiler.Compile(source.StockScreen.VertexProgram.Span);var ps=compiler.Compile(source.StockScreen.PixelProgram.Span);
        return BuildAsync(source,hdr,vs,ps);
    }
    public static async Task<MapFinalOutputTransfer> BuildAsync(LoadedMapFinalOutputResources source,
        RenderTargetDeviceTexture hdr,ShaderSpirv vs,ShaderSpirv ps)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(hdr);ArgumentNullException.ThrowIfNull(vs);ArgumentNullException.ThrowIfNull(ps);
        var adapter=VertexShaderAdapter.AdaptPositionInput(MoveVertexSet(vs.Bytes));
        var vi=SpirvShaderInterfaceReader.Read(adapter.Bytes);var pi=SpirvShaderInterfaceReader.Read(ps.Bytes);
        if(vi.Descriptors.Count!=1||vi.Descriptors[0] is not{StorageClass:2,DescriptorSet:1,RequiredBufferBytes:32}||
            pi.Descriptors.Count!=3||pi.Descriptors.Any(d=>d.DescriptorSet!=0)||pi.Descriptors.Count(d=>d.StorageClass==2&&d.RequiredBufferBytes==16)!=1||
            pi.Descriptors.Count(d=>d.Type.Kind=="sampler")!=1||pi.Descriptors.Count(d=>d.Type is{Kind:"image",Dimension:1,Arrayed:false})!=1)
            throw new InvalidDataException("Final output stock screen interface differs from its source32B/16B/t0/s0 layout.");
        var resources=new RenderDeviceResources("Final output transfer allocation failed.");
        var pairResources=new RenderDeviceResources("Final output transfer allocation failed.");
        RenderOutputPresentation? presentation=null;
        try
        {
            var built=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                Rid Own(Rid resource)=>resources.Own(resource);
                var native=RenderTextureFormatMath.Resolve(source.OutputTexture);
                if(native.TargetViewFormat!=91||native.ShaderViewFormat!=91||source.OutputTexture.Width!=0||source.OutputTexture.Height!=0)
                    throw new InvalidDataException("Final output transfer requires exact source Output BGRA8 sRGB dynamic extent.");
                using var stages=new RDShaderSpirV();stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,ps.Bytes);
                Rid shader=Own(rd.ShaderCreateFromSpirV(stages,"OriginalMapFinalOutputStockCopy"));
                using var format=new RDTextureFormat{Format=RenderingDevice.DataFormat.B8G8R8A8Srgb,Width=(uint)hdr.ExplicitExtent.X,Height=(uint)hdr.ExplicitExtent.Y,
                    Depth=1,ArrayLayers=1,Mipmaps=1,TextureType=RenderingDevice.TextureType.Type2D,UsageBits=RenderingDevice.TextureUsageBits.ColorAttachmentBit|
                    RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.CanCopyFromBit};
                using var view=new RDTextureView();Rid storage=Own(rd.TextureCreate(format,view));Rid framebuffer=Own(rd.FramebufferCreate([storage]));
                var output=new RenderTargetDeviceTexture(source.OutputTexture,hdr.ExplicitExtent,storage,storage,format.Format,format.Format,format.Format);
                byte[] cb=new byte[32];float[] viewport=[2f/hdr.ExplicitExtent.X,-2f/hdr.ExplicitExtent.Y,-1,1,1,1,1,1];
                for(int i=0;i<8;i++)BinaryPrimitives.WriteSingleLittleEndian(cb.AsSpan(i*4,4),viewport[i]);
                Rid vsCb=Own(rd.UniformBufferCreate(32,cb));byte[] alpha=new byte[16];BinaryPrimitives.WriteSingleLittleEndian(alpha,-float.MaxValue);
                Rid psCb=Own(rd.UniformBufferCreate(16,alpha));Rid sampler=pairResources.Own(DeviceSampler.Create(rd,source.StockScreen.InputSampler.EncodeD3D11Descriptor()));
                Rid Set(uint set,IReadOnlyList<SpirvInterfaceVariable> declarations)
                {
                    var uniforms=new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach(var d in declarations)
                        {
                            var u=new RDUniform{Binding=(int)d.Binding!.Value,UniformType=d.StorageClass==2?RenderingDevice.UniformType.UniformBuffer:
                                d.Type.Kind=="sampler"?RenderingDevice.UniformType.Sampler:RenderingDevice.UniformType.Texture};uniforms.Add(u);
                            u.AddId(d.StorageClass==2?(set==1?vsCb:psCb):d.Type.Kind=="sampler"?sampler:hdr.ShaderViewRid);
                        }
                        Rid created=(set==0?pairResources:resources).Own(rd.UniformSetCreate(uniforms,shader,set));if(!rd.UniformSetIsValid(created))throw new InvalidDataException("Final transfer uniform set failed.");return created;
                    }
                    finally{foreach(var u in uniforms)u.Dispose();}
                }
                Rid vertexSet=Set(1,vi.Descriptors),pixelSet=Set(0,pi.Descriptors);
                byte[] quad=new byte[96];float w=hdr.ExplicitExtent.X,h=hdr.ExplicitExtent.Y;float[][] rows=[[0,0,0,0,0],[w,0,0,1,0],[0,h,0,0,1],[w,h,0,1,1]];
                for(int i=0;i<4;i++){for(int c=0;c<3;c++)BinaryPrimitives.WriteSingleLittleEndian(quad.AsSpan(i*24+c*4,4),rows[i][c]);
                    BinaryPrimitives.WriteUInt32LittleEndian(quad.AsSpan(i*24+12,4),uint.MaxValue);for(int c=0;c<2;c++)BinaryPrimitives.WriteSingleLittleEndian(quad.AsSpan(i*24+16+c*4,4),rows[i][c+3]);}
                Rid vb=Own(rd.VertexBufferCreate(96,quad));var attributes=new Godot.Collections.Array<RDVertexAttribute>();long vertexFormat;
                try
                {
                    foreach(var input in vi.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null).OrderBy(v=>v.Location))
                    {
                        uint location=input.Location!.Value;attributes.Add(new(){Location=location,Offset=location switch{0=>0u,1=>12u,2=>16u,_=>throw new InvalidDataException("Final stock VS vertex location differs.")},
                            Stride=24,Format=location switch{0=>RenderingDevice.DataFormat.R32G32B32Sfloat,1=>RenderingDevice.DataFormat.R8G8B8A8Unorm,_=>RenderingDevice.DataFormat.R32G32Sfloat},Frequency=RenderingDevice.VertexFrequency.Vertex});
                    }
                    vertexFormat=rd.VertexFormatCreate(attributes);
                }
                finally{foreach(var a in attributes)a.Dispose();}
                Rid vertices=Own(rd.VertexArrayCreate(4,vertexFormat,[vb,vb,vb]));using var raster=new RDPipelineRasterizationState{CullMode=RenderingDevice.PolygonCullMode.Disabled};
                using var multi=new RDPipelineMultisampleState();using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
                using var attachment=new RDPipelineColorBlendStateAttachment{EnableBlend=false};using var blend=new RDPipelineColorBlendState{Attachments=[attachment]};
                Rid pipeline=Own(rd.RenderPipelineCreate(shader,rd.FramebufferGetFormat(framebuffer),vertexFormat,RenderingDevice.RenderPrimitive.TriangleStrips,raster,multi,depth,blend));
                if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("Original final stock copy pipeline was rejected.");
                var pixelResources=pi.Descriptors.Where(d=>d.Type.Kind!="sampler").ToDictionary(d=>(int)d.Binding!.Value,d=>d.StorageClass==2?psCb:hdr.ShaderViewRid);
                return(output,shader,framebuffer,pipeline,vertices,vertexSet,pixelSet,vsCb,vb,pixelResources);
            });
            presentation=await RenderOutputPresentation.BuildAsync(built.output);
            return new(built.output,presentation,vs,ps,built.shader,built.framebuffer,built.pipeline,built.vertices,built.vertexSet,built.pixelSet,
                built.vsCb,built.vb,resources,pairResources,built.pixelResources);
        }
        catch(Exception failure)
        {
            var pending=NativeMapBorrowerRetirementPackage.CreatePending("Final-output-transfer-partial",
                new IAsyncDisposable?[]{presentation,pairResources.Count>0?pairResources:null,resources.Count>0?resources:null}.OfType<IAsyncDisposable>(),
                (failure as NativeMapBorrowerConstructionException)?.RetainedPackage);
            try{await NativeMapBorrowerRetirementPackage.DrainPendingAsync(pending);}
            catch(Exception retirementFailure)
            {throw new NativeMapBorrowerConstructionException(failure,retirementFailure,pending!);}
            throw;
        }
    }
    public static MapFinalOutputStockCopyPacket Snapshot(MapFinalOutputStockCopyPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        float[] values=[packet.Scale.X,packet.Scale.Y,packet.ColorScale.R,packet.ColorScale.G,packet.ColorScale.B,packet.ColorScale.A,
            packet.UvEndpoints.X,packet.UvEndpoints.Y,packet.UvEndpoints.Z,packet.UvEndpoints.W];
        if(values.Any(v=>!float.IsFinite(v))||packet.SampledImageSamplerDescriptor.Length!=52||string.IsNullOrWhiteSpace(packet.NativeContextSource))
            throw new InvalidDataException("Final stock copy requires finite current scale/color/UV and the sampled producer's complete52B sampler.");
        return packet with{SampledImageSamplerDescriptor=packet.SampledImageSamplerDescriptor.ToArray()};
    }
    public Task DrawAsync(MapFinalOutputStockCopyPacket packet)
    {
        var current=Snapshot(packet);
        lock(_lifetime){ObjectDisposedException.ThrowIf(_retiring,this);_accepted++;}
        return DrawAcceptedAsync(current);
    }
    private async Task DrawAcceptedAsync(MapFinalOutputStockCopyPacket current)
    {
        await _gate.WaitAsync();
        try
        {
        _pendingRetirement=await NativeMapBorrowerRetirementPackage.DrainPendingAsync(_pendingRetirement);
        if(_candidatePairResources is not null){await _candidatePairResources.DisposeAsync();_candidatePairResources=null;}
        await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{
            if(!rd.TextureIsValid(_sampledTexture)||!rd.TextureIsValid(Output.StorageRid))
                throw new InvalidDataException("Final stock copy sampled producer or output retired before accepted draw.");
            byte[] cb=new byte[32];float[] values=[2*current.Scale.X/Output.ExplicitExtent.X,-2*current.Scale.Y/Output.ExplicitExtent.Y,-1,1,
                current.ColorScale.R,current.ColorScale.G,current.ColorScale.B,current.ColorScale.A];
            for(int i=0;i<8;i++)BinaryPrimitives.WriteSingleLittleEndian(cb.AsSpan(i*4,4),values[i]);
            byte[] quad=new byte[96];float w=Output.ExplicitExtent.X,h=Output.ExplicitExtent.Y;var uv=current.UvEndpoints;
            float[][] rows=[[0,0,0,uv.X,uv.Y],[w,0,0,uv.Z,uv.Y],[0,h,0,uv.X,uv.W],[w,h,0,uv.Z,uv.W]];
            for(int i=0;i<4;i++){for(int c=0;c<3;c++)BinaryPrimitives.WriteSingleLittleEndian(quad.AsSpan(i*24+c*4,4),rows[i][c]);
                BinaryPrimitives.WriteUInt32LittleEndian(quad.AsSpan(i*24+12,4),uint.MaxValue);for(int c=0;c<2;c++)BinaryPrimitives.WriteSingleLittleEndian(quad.AsSpan(i*24+16+c*4,4),rows[i][c+3]);}
            if(rd.BufferUpdate(_vertexCb,0,32,cb)!=Error.Ok||rd.BufferUpdate(_vertexBuffer,0,96,quad)!=Error.Ok)
                throw new InvalidDataException("Final stock copy current context upload failed.");
            // The complete producer descriptor is installed on every accepted copy.
            var nextResources=new RenderDeviceResources("Final stock copy producer allocation failed.");
            _candidatePairResources=nextResources;
            Rid nextSampler=default,nextSet=default;
            try
            {
                nextSampler=DeviceSampler.Create(rd,current.SampledImageSamplerDescriptor.Span);
                if(!nextSampler.IsValid)throw new InvalidDataException("Final stock copy producer sampler failed.");
                nextResources.Own(nextSampler);
                var uniforms=new Godot.Collections.Array<RDUniform>();
                try
                {
                    foreach(var d in SpirvShaderInterfaceReader.Read(PixelTranslation.Bytes).Descriptors)
                    {
                        var u=new RDUniform{Binding=(int)d.Binding!.Value,UniformType=d.StorageClass==2?RenderingDevice.UniformType.UniformBuffer:
                            d.Type.Kind=="sampler"?RenderingDevice.UniformType.Sampler:RenderingDevice.UniformType.Texture};
                        // Preserve the immutable original CB/image bindings; only the producer sampler changes.
                        uniforms.Add(u);u.AddId(d.Type.Kind=="sampler"?nextSampler:_pixelResources[(int)d.Binding.Value]);
                    }
                    nextSet=rd.UniformSetCreate(uniforms,_shader,0);
                    if(nextSet.IsValid)nextResources.Own(nextSet);
                }
                finally{foreach(var u in uniforms)u.Dispose();}
                if(!nextSet.IsValid||!rd.UniformSetIsValid(nextSet))throw new InvalidDataException("Final stock copy producer sampler binding failed.");
            }
            catch(Exception failure)
            {
                try{nextResources.ReleaseAfterConstructionFailure(rd,failure,"Final-transfer-pair-partial");}
                catch(NativeMapBorrowerConstructionException retained)
                {_pendingRetirement=retained.RetainedPackage;_candidatePairResources=null;throw;}
                _candidatePairResources=null;throw;
            }
            try{_pairResources?.Release(rd);}
            catch
            {
                _pendingRetirement=NativeMapBorrowerRetirementPackage.CreatePending("Final-transfer-pair-replaced",
                    new IAsyncDisposable?[]{_pairResources,nextResources}.OfType<IAsyncDisposable>());
                _pairResources=null;_candidatePairResources=null;_pixelSet=default;
                _pendingRetirement!.RetainFailure();
                throw;
            }
            _pairResources=nextResources;_candidatePairResources=null;_pixelSet=nextSet;
            long list=rd.DrawListBegin(_framebuffer);
            try{rd.DrawListBindRenderPipeline(list,_pipeline);rd.DrawListBindUniformSet(list,_vertexSet,1);rd.DrawListBindUniformSet(list,_pixelSet,0);rd.DrawListBindVertexArray(list,_vertices);rd.DrawListDraw(list,false,1,4);}finally{rd.DrawListEnd();}return true;});
        await Presentation.RefreshAsync();
        }
        finally{_gate.Release();lock(_lifetime){if(--_accepted==0&&_retiring)_drained?.TrySetResult();}}
    }
    private static byte[] MoveVertexSet(byte[] source)
    {
        byte[] result=source.ToArray();int count=0;
        for(int at=20;at<result.Length;){uint first=BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(at,4));int words=(int)(first>>16);
            if(words<1||at+words*4>result.Length)throw new InvalidDataException("Final stock VS SPIR-V instruction bounds differ.");
            if((first&65535)==71&&words==4&&BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(at+8,4))==34)
            {if(BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(at+12,4))!=0)throw new InvalidDataException("Final stock VS original descriptor set differs.");BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(at+12,4),1);count++;}at+=words*4;}
        if(count!=1)throw new InvalidDataException("Final stock VS must have exactly one source descriptor set.");return result;
    }
    public ValueTask DisposeAsync(){lock(_lifetime){_retiring=true;if(_disposal is null||_disposal.IsFaulted||_disposal.IsCanceled)_disposal=DisposeCoreAsync();return new(_disposal);}}
    private async Task DisposeCoreAsync()
    {
        Task wait;lock(_lifetime){wait=_accepted==0?Task.CompletedTask:(_drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;}
        await wait;await _gate.WaitAsync();
        try
        {
            _retirement??=new("Final-output-transfer:"+Guid.NewGuid().ToString("N"),
                Presentation,_pendingRetirement,_candidatePairResources,_pairResources,_resources);
            await _retirement.DisposeAsync();_pendingRetirement=null;_candidatePairResources=null;
        }
        finally{_gate.Release();}
    }
}
