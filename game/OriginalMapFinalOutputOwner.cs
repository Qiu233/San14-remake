using Godot;
using San14.Data;
using System.Security.Cryptography;

/// <summary>All current native final-pass bytes and resources are explicit.
/// SceneInput and Exposure are borrowed from their accepted-frame GPU producers.
/// For SPK PS28, SceneInput is the actual radial-blur TEMP output, not an inferred
/// alias to the preceding node73 scene. ExtraImages contains every other original
/// PS image except these two explicit inputs; sampling
/// states are the complete original 52B descriptors. Quad contains four source
/// vertices in the exact selected VS layout, submitted as a triangle strip.</summary>
public sealed record OriginalMapFinalOutputFramePacket(long AcceptedFrameNumber,
    IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> VertexConstants,
    IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> PixelConstants,
    ReadOnlyMemory<byte> Quad,OriginalModelDeviceBinding SceneInput,OriginalModelDeviceBinding Exposure,
    IReadOnlyList<OriginalModelDeviceBinding> ExtraImages,
    IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> SamplerDescriptors,
    OriginalModelRasterInputs Raster,string NativeContextSource)
{
    /// <summary>Required by the explicit HDR transfer route. The sampled image's
    /// current sampler key and mutable stock-screen context are producer-owned.</summary>
    public OriginalMapFinalOutputStockCopyPacket? OutputCopy { get; init; }
}

public enum OriginalMapFinalOutputTargetRoute
{
    /// <summary>Source Host virtual80=false and matching scale1/full extent.
    /// node192 preserves its parent. This standalone owner explicitly selects
    /// the original declared Output surface as its parent.</summary>
    SourceHostParentOutput,
    /// <summary>Explicit linear HDR context followed by stock output copy;
    /// this native conditional route is separate from the Host default.</summary>
    LinearHdrThenOutput
}

/// <summary>Own the explicitly selected node193 attachment and original final
/// draw. The source Host scale1/full extent path preserves its parent Output;
/// an explicit linear context uses node192 HDR and stock output copy. Retains
/// all source color math. Additional effect inputs
/// and prepared native context remain caller-owned. This is not a swapchain
/// identity or a replacement tone operator.</summary>
public sealed class OriginalMapFinalOutputOwner : IAsyncDisposable
{
    private sealed record TargetResources(OriginalRenderTargetFrame BorrowedScene,
        OriginalRenderTargetDeviceTexture Primary,Rid Framebuffer,
        OriginalMapFinalOutputTransfer? Transfer,OriginalRenderOutputPresentation Presentation,IReadOnlyList<Rid> Owned);
    private sealed record DrawResources(Rid Pipeline,Rid Vertices,Rid VertexBuffer,
        Rid VertexSet,Rid PixelSet,IReadOnlyDictionary<(uint Stage,uint Register),Rid> Constants,
        IReadOnlyList<OriginalModelDeviceBinding> Inputs,string SamplerKey,
        OriginalModelRasterInputs Raster,IReadOnlyList<Rid> Owned);
    public LoadedOriginalMapFinalOutputResources Source { get; }
    public OriginalMapFinalOutputProgram Program { get; }
    public uint SceneRegister { get; }
    public uint ExposureRegister { get; }
    public OriginalMapFinalOutputTargetRoute TargetRoute { get; }
    public OriginalRenderOutputPresentation? Presentation=>_target?.Presentation;
    public OriginalRenderTargetDeviceTexture? HdrTexture=>TargetRoute==OriginalMapFinalOutputTargetRoute.LinearHdrThenOutput?_target?.Primary:null;
    public OriginalRenderTargetDeviceTexture? TargetTexture=>_target?.Primary;
    public OriginalRenderTargetDeviceTexture? OutputTexture=>_target is null?null:_target.Transfer?.Output??_target.Primary;
    public long AcceptedFrameCount { get; private set; }
    public long TargetCreationCount { get; private set; }
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetime=new();
    private TargetResources? _target;private DrawResources? _draw;
    private readonly OriginalShaderSpirv _transferVertex,_transferPixel;
    private int _accepted;private bool _retiring;private Task? _disposal;private TaskCompletionSource? _drained;
    private OriginalMapFinalOutputOwner(LoadedOriginalMapFinalOutputResources source,
        OriginalMapFinalOutputProgram program,uint sceneRegister,uint exposureRegister,OriginalShaderSpirv transferVertex,OriginalShaderSpirv transferPixel,OriginalMapFinalOutputTargetRoute route)
        =>(Source,Program,SceneRegister,ExposureRegister,_transferVertex,_transferPixel,TargetRoute)=(source,program,sceneRegister,exposureRegister,transferVertex,transferPixel,route);

    public static async Task<OriginalMapFinalOutputOwner> BuildAsync(LoadedOriginalMapFinalOutputResources source,
        OriginalMapFinalOutputProgramSource explicitProgram,uint explicitSceneRegister,
        uint explicitExposureRegister,Vkd3dShaderCompiler compiler,OriginalMapFinalOutputTargetRoute explicitTargetRoute,
        OriginalMapFinalOutputDeadColorCorrection? sourceDeadColorCorrection=null,
        LoadedOriginalMapFinalOutputToneShaderSource? sourceToneShader=null,
        OriginalMapFinalOutputDeadToneLut? sourceDeadToneLut=null)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(explicitProgram);
        if(!Enum.IsDefined(explicitTargetRoute))throw new InvalidDataException("Final target route must be selected explicitly from the supported source contexts.");
        if(explicitProgram.SourceResourceUri==source.PostEffect.Source.ResourceUri+"#SPK")
        {
            var expected=OriginalMapFinalOutputResources.SpkProgram(source,explicitProgram.VertexProgramIndex,explicitProgram.PixelProgramIndex);
            if(expected.VertexSha256!=explicitProgram.VertexSha256||expected.PixelSha256!=explicitProgram.PixelSha256)
                throw new InvalidDataException("Selected final SPK shader pair differs from its original source directory.");
        }
        else if(explicitProgram.SourceResourceUri==source.AuxiliaryShader.Source.ResourceUri)
        {
            var vertex=source.AuxiliaryShader.Container.GetProgramBytes(source.AuxiliaryShader.Source.Bytes,explicitProgram.VertexProgramIndex);
            var pixel=source.AuxiliaryShader.Container.GetProgramBytes(source.AuxiliaryShader.Source.Bytes,explicitProgram.PixelProgramIndex);
            if(Convert.ToHexString(SHA256.HashData(vertex))!=explicitProgram.VertexSha256||Convert.ToHexString(SHA256.HashData(pixel))!=explicitProgram.PixelSha256)
                throw new InvalidDataException("Selected final auxiliary pair differs from its original source directory.");
        }
        else if(sourceToneShader is not null&&explicitProgram.SourceResourceUri==sourceToneShader.SourceResourceUri)
        {
            var actual=OriginalMapFinalOutputToneShaderSource.ReadCurrent(
                Path.GetDirectoryName(sourceToneShader.PhysicalPath)??throw new InvalidDataException("Tone source has no physical directory."),
                sourceToneShader.Locale);
            var expected=actual.ToProgramSource();
            if(explicitProgram.VertexProgramIndex!=expected.VertexProgramIndex||
                explicitProgram.PixelProgramIndex!=expected.PixelProgramIndex||
                explicitProgram.SourceResourceUri!=expected.SourceResourceUri||
                sourceToneShader.ExecutableSha256!=actual.ExecutableSha256||
                explicitProgram.VertexSha256!=expected.VertexSha256||explicitProgram.PixelSha256!=expected.PixelSha256)
                throw new InvalidDataException("Selected embedded tone pair differs from its exact original programs.");
        }
        else throw new InvalidDataException("Final shader selection must refer to this exact PostEffect or auxiliary source.");
        if(explicitSceneRegister==explicitExposureRegister)
            throw new InvalidDataException("Final output Scene and Exposure source registers must differ.");
        if(sourceDeadToneLut is not null&&sourceToneShader is null)
            throw new InvalidDataException("Tone source specialization requires its exact installed embedded program receipt.");
        var program=await OriginalMapFinalOutputProgram.BuildAsync(explicitProgram,compiler,sourceDeadColorCorrection,sourceDeadToneLut);
        try
        {
            foreach(uint register in new[]{explicitSceneRegister,explicitExposureRegister})
            {
                var declaration=program.PixelBindings.Bindings.SingleOrDefault(b=>b.DescriptorKind=="Texture"&&b.Source.BindPoint==register);
                if(declaration is null||declaration.Source.Dimension!=4||declaration.Source.ReturnType!=5)
                    throw new InvalidDataException("Final output Scene and Exposure require exact original float2D declarations.");
            }
            var transferVertex=compiler.Compile(source.StockScreen.VertexProgram.Span);
            var transferPixel=compiler.Compile(source.StockScreen.PixelProgram.Span);
            return new(source,program,explicitSceneRegister,explicitExposureRegister,transferVertex,transferPixel,explicitTargetRoute);
        }
        catch{await program.DisposeAsync();throw;}
    }

    public Task<OriginalRenderOutputPresentation> RenderAsync(OriginalNativeMapFrameContext frame,
        OriginalMapFinalOutputFramePacket packet)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(packet);
        var snapshot=Snapshot(frame,packet);Accept();return RenderAcceptedAsync(frame,snapshot);
    }
    private async Task<OriginalRenderOutputPresentation> RenderAcceptedAsync(OriginalNativeMapFrameContext frame,
        OriginalMapFinalOutputFramePacket packet)
    {
        await _gate.WaitAsync();
        try
        {
            if(_target is null||!ReferenceEquals(_target.BorrowedScene,frame.OutputFrame))
            {
                var next=await BuildTargetAsync(frame.OutputFrame);
                try{await RetireTargetCoreAsync();_target=next;TargetCreationCount++;}
                catch{if(next.Transfer is not null)await next.Transfer.DisposeAsync();else await next.Presentation.DisposeAsync();await FreeAsync(next.Owned);throw;}
            }
            var target=_target!;
            var inputs=packet.ExtraImages.Concat(new[]{packet.Exposure with{SourceBindPoint=ExposureRegister},packet.SceneInput}).OrderBy(i=>i.SourceBindPoint).ToArray();
            string samplerKey=string.Join(';',packet.SamplerDescriptors.OrderBy(p=>p.Key).Select(p=>$"{p.Key}:{Convert.ToHexString(p.Value.Span)}"));
            if(_draw is null||!_draw.Inputs.SequenceEqual(inputs)||_draw.SamplerKey!=samplerKey||!SameRaster(_draw.Raster,packet.Raster))
            {
                var next=await BuildDrawAsync(packet,inputs,samplerKey);
                if(_draw is not null)await FreeAsync(_draw.Owned);_draw=next;
            }
            var draw=_draw!;
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                Program.RequireLive();
                foreach(var image in draw.Inputs)
                    if(!rd.TextureIsValid(image.Resource)||!rd.TextureIsValid(image.OwnerTexture!.Value))
                        throw new InvalidDataException("Final output accepted producer image or parent retired before draw.");
                foreach(var stage in new[]{(1u,packet.VertexConstants),(0u,packet.PixelConstants)})
                    foreach(var cb in stage.Item2)
                        if(rd.BufferUpdate(draw.Constants[(stage.Item1,cb.Key)],0,(uint)cb.Value.Length,cb.Value.ToArray())!=Error.Ok)
                            throw new InvalidDataException("Final output source constant upload failed.");
                if(rd.BufferUpdate(draw.VertexBuffer,0,(uint)packet.Quad.Length,packet.Quad.ToArray())!=Error.Ok)
                    throw new InvalidDataException("Final output source quad upload failed.");
                long list=rd.DrawListBegin(target.Framebuffer);
                try
                {
                    rd.DrawListBindRenderPipeline(list,draw.Pipeline);rd.DrawListBindVertexArray(list,draw.Vertices);
                    if(draw.VertexSet.IsValid)rd.DrawListBindUniformSet(list,draw.VertexSet,1);
                    if(draw.PixelSet.IsValid)rd.DrawListBindUniformSet(list,draw.PixelSet,0);
                    rd.DrawListDraw(list,false,1,4);
                }
                finally{rd.DrawListEnd();}
                return true;
            });
            if(target.Transfer is not null)await target.Transfer.DrawAsync(packet.OutputCopy!);else await target.Presentation.RefreshAsync();AcceptedFrameCount++;
            return target.Presentation;
        }
        finally{_gate.Release();Complete();}
    }

    private async Task<TargetResources> BuildTargetAsync(OriginalRenderTargetFrame scene)
    {
        if(scene.Source.Graph.Source.ResourceUri!=Source.Graph.Source.ResourceUri||
            scene.Source.Graph.Source.DecodedSha256!=Source.Graph.Source.DecodedSha256||
            !scene.Textures.TryGetValue(Source.SceneTexture.Object.ObjectId,out var input)||
            scene.Source.ColorTargets.Count!=1||scene.Source.ColorTargets[0].ObjectNameHash!=Source.SceneTexture.Object.ObjectId)
            throw new InvalidDataException("Final output requires this source graph's actual node73 SceneTexture frame.");
        var allocated=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();Rid Own(Rid rid){if(!rid.IsValid)throw new InvalidDataException("Final HDR allocation failed.");owned.Add(rid);return rid;}
            try
            {
                bool hdr=TargetRoute==OriginalMapFinalOutputTargetRoute.LinearHdrThenOutput;
                var declaration=hdr?Source.HdrTexture:Source.OutputTexture;var formats=OriginalRenderTextureFormatMath.Resolve(declaration);
                uint viewFormat=hdr?26u:91u;
                if(formats.ShaderViewFormat!=viewFormat||formats.TargetViewFormat!=viewFormat||declaration.Width!=0||declaration.Height!=0)
                    throw new InvalidDataException("Final attachment differs from its explicitly selected original dynamic format.");
                using var format=new RDTextureFormat{Format=hdr?RenderingDevice.DataFormat.B10G11R11UfloatPack32:RenderingDevice.DataFormat.B8G8R8A8Srgb,
                    Width=(uint)input.ExplicitExtent.X,Height=(uint)input.ExplicitExtent.Y,Depth=1,ArrayLayers=1,Mipmaps=1,
                    TextureType=RenderingDevice.TextureType.Type2D,UsageBits=RenderingDevice.TextureUsageBits.SamplingBit|
                        RenderingDevice.TextureUsageBits.ColorAttachmentBit|RenderingDevice.TextureUsageBits.CanCopyFromBit};
                using var view=new RDTextureView();Rid storage=Own(rd.TextureCreate(format,view));
                Rid framebuffer=Own(rd.FramebufferCreate([storage]));if(!rd.FramebufferIsValid(framebuffer))throw new InvalidDataException("Final HDR framebuffer failed.");
                var texture=new OriginalRenderTargetDeviceTexture(declaration,input.ExplicitExtent,storage,storage,format.Format,format.Format,format.Format);
                return(texture,framebuffer,owned.AsReadOnly());
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
        try
        {
            var transfer=TargetRoute==OriginalMapFinalOutputTargetRoute.LinearHdrThenOutput?
                await OriginalMapFinalOutputTransfer.BuildAsync(Source,allocated.texture,_transferVertex,_transferPixel):null;
            var presentation=transfer?.Presentation??await OriginalRenderOutputPresentation.BuildAsync(allocated.texture);
            return new(scene,allocated.texture,allocated.framebuffer,transfer,presentation,allocated.Item3);
        }
        catch{await FreeAsync(allocated.Item3);throw;}
    }

    private Task<DrawResources> BuildDrawAsync(OriginalMapFinalOutputFramePacket packet,
        OriginalModelDeviceBinding[] inputs,string samplerKey)
    {
        var target=_target!;var bindings=Program.PixelBindings.Bindings;
        var expected=bindings.Where(b=>b.DescriptorKind=="Texture").ToArray();
        if(inputs.Length!=expected.Length||inputs.Select(i=>i.SourceBindPoint).Distinct().Count()!=inputs.Length||
            inputs.Any(i=>i.DescriptorKind!="Texture"||!i.Resource.IsValid||i.OwnerTexture is not{IsValid:true}||
                string.IsNullOrWhiteSpace(i.SourceResourceUri)||i.Resource==target.Primary.StorageRid||i.OwnerTexture==target.Primary.StorageRid)||
            expected.Any(b=>!inputs.Any(i=>i.SourceBindPoint==b.Source.BindPoint)))
            throw new InvalidDataException("Final output needs every exact source image and explicit parent; HDR output aliasing is forbidden.");
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();Rid Own(Rid rid){if(!rid.IsValid)throw new InvalidDataException("Final output draw allocation failed.");owned.Add(rid);return rid;}
            try
            {
                Program.RequireLive();var cbs=new Dictionary<(uint,uint),Rid>();
                foreach(var stage in new[]{(1u,packet.VertexConstants),(0u,packet.PixelConstants)})
                    foreach(var cb in stage.Item2)cbs.Add((stage.Item1,cb.Key),Own(rd.UniformBufferCreate((uint)cb.Value.Length,cb.Value.ToArray())));
                var resources=inputs.ToList();
                foreach(var descriptor in packet.SamplerDescriptors)resources.Add(new("Sampler",descriptor.Key,
                    Own(OriginalDeviceSampler.Create(rd,descriptor.Value.Span)),null,packet.NativeContextSource+"#sampler/"+descriptor.Key));
                foreach(var declaration in expected)
                {
                    var image=inputs.Single(i=>i.SourceBindPoint==declaration.Source.BindPoint);
                    if(!rd.TextureIsValid(image.Resource)||!rd.TextureIsValid(image.OwnerTexture!.Value))throw new InvalidDataException("Final output source image or parent is retired.");
                    using var shape=rd.TextureGetFormat(image.Resource);
                    if(shape.TextureType!=RenderingDevice.TextureType.Type2D||!shape.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
                        throw new InvalidDataException("Final output image does not satisfy its source2D sampler shape.");
                }
                Rid Set(OriginalTranslatedShaderBindings layout,uint stage)
                {
                    if(layout.Bindings.Count==0)return default;
                    var uniforms=new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach(var declaration in layout.Bindings)
                        {
                            var uniform=new RDUniform{Binding=(int)declaration.Device.Binding!.Value,UniformType=declaration.DescriptorKind switch{
                                "UniformBuffer"=>RenderingDevice.UniformType.UniformBuffer,"Texture"=>RenderingDevice.UniformType.Texture,
                                "Sampler"=>RenderingDevice.UniformType.Sampler,_=>throw new InvalidDataException("Unsupported final source descriptor.")}};
                            uniforms.Add(uniform);uniform.AddId(declaration.DescriptorKind=="UniformBuffer"?cbs[(stage,declaration.Source.BindPoint)]:
                                resources.Single(i=>i.DescriptorKind==declaration.DescriptorKind&&i.SourceBindPoint==declaration.Source.BindPoint).Resource);
                        }
                        Rid rid=Own(rd.UniformSetCreate(uniforms,Program.ShaderRid,stage));if(!rd.UniformSetIsValid(rid))throw new InvalidDataException("Final output uniform set failed.");return rid;
                    }
                    finally{foreach(var uniform in uniforms)uniform.Dispose();}
                }
                Rid vs=Set(Program.VertexBindings,1),ps=Set(Program.PixelBindings,0);
                var attributes=new Godot.Collections.Array<RDVertexAttribute>();long vertexFormat;
                int stride=packet.Quad.Length/4;
                try
                {
                    foreach(var input in Program.VertexBindings.Interface.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null))
                    {
                        int components=(int)(input.Type.ComponentCount??throw new InvalidDataException("Final VS input is not a source vector."));
                        bool stock=Program.Source.VertexSha256==OriginalMapFinalOutputToneShaderSource.VertexSha256&&
                            Program.Source.PixelSha256==OriginalMapFinalOutputToneShaderSource.PixelSha256;
                        uint offset=stock?input.Location switch{0=>0u,1=>12u,2=>16u,_=>throw new InvalidDataException("Unknown stock screen vertex location.")}:
                            stride==24?input.Location==0?0u:16u:input.Location switch{0=>0u,1=>16u,2=>32u,3=>40u,_=>throw new InvalidDataException("Unknown source final vertex location.")};
                        attributes.Add(new(){Location=input.Location!.Value,Offset=offset,Stride=(uint)stride,
                            Format=stock&&input.Location==1?RenderingDevice.DataFormat.R8G8B8A8Unorm:
                                components==4?RenderingDevice.DataFormat.R32G32B32A32Sfloat:
                                components==3?RenderingDevice.DataFormat.R32G32B32Sfloat:components==2?RenderingDevice.DataFormat.R32G32Sfloat:
                                throw new InvalidDataException("Unsupported source final vertex width."),Frequency=RenderingDevice.VertexFrequency.Vertex});
                    }
                    vertexFormat=rd.VertexFormatCreate(attributes);
                }
                finally{foreach(var attribute in attributes)attribute.Dispose();}
                Rid vertexBuffer=Own(rd.VertexBufferCreate((uint)packet.Quad.Length,packet.Quad.ToArray()));
                int sourceAttributes=Program.VertexBindings.Interface.StageVariables.Count(v=>v.StorageClass==1&&v.Location is not null);
                Rid vertices=Own(rd.VertexArrayCreate(4,vertexFormat,new Godot.Collections.Array<Rid>(Enumerable.Repeat(vertexBuffer,sourceAttributes))));
                using var raster=new RDPipelineRasterizationState{CullMode=packet.Raster.Cull,FrontFace=packet.Raster.FrontFace};
                using var multi=new RDPipelineMultisampleState();using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
                var a=packet.Raster.Attachments.Single();using var color=new RDPipelineColorBlendStateAttachment{EnableBlend=a.EnableBlend,
                    SrcColorBlendFactor=a.SourceColor,DstColorBlendFactor=a.DestinationColor,ColorBlendOp=a.ColorOperation,
                    SrcAlphaBlendFactor=a.SourceAlpha,DstAlphaBlendFactor=a.DestinationAlpha,AlphaBlendOp=a.AlphaOperation,
                    WriteR=a.WriteR,WriteG=a.WriteG,WriteB=a.WriteB,WriteA=a.WriteA};
                using var blend=new RDPipelineColorBlendState{Attachments=[color]};
                Rid pipeline=Own(rd.RenderPipelineCreate(Program.ShaderRid,rd.FramebufferGetFormat(target.Framebuffer),vertexFormat,
                    RenderingDevice.RenderPrimitive.TriangleStrips,raster,multi,depth,blend));
                if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("Final output original VS/PS pipeline failed.");
                return new DrawResources(pipeline,vertices,vertexBuffer,vs,ps,cbs,Array.AsReadOnly(inputs),samplerKey,packet.Raster,owned.AsReadOnly());
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }

    private OriginalMapFinalOutputFramePacket Snapshot(OriginalNativeMapFrameContext frame,OriginalMapFinalOutputFramePacket p)
    {
        if(p.AcceptedFrameNumber!=frame.FrameNumber||p.AcceptedFrameNumber<=0||string.IsNullOrWhiteSpace(p.NativeContextSource)||
            p.SceneInput is not{DescriptorKind:"Texture",OwnerTexture:not null}||p.SceneInput.SourceBindPoint!=SceneRegister||
            !p.SceneInput.Resource.IsValid||!p.SceneInput.OwnerTexture.Value.IsValid||string.IsNullOrWhiteSpace(p.SceneInput.SourceResourceUri)||
            p.Exposure is not{DescriptorKind:"Texture",OwnerTexture:not null}||!p.Exposure.Resource.IsValid||!p.Exposure.OwnerTexture.Value.IsValid||
            string.IsNullOrWhiteSpace(p.Exposure.SourceResourceUri)||p.Raster.DepthTest||p.Raster.DepthWrite||p.Raster.Attachments.Count!=1||
            p.ExtraImages.Any(i=>i.SourceBindPoint==SceneRegister||i.SourceBindPoint==ExposureRegister))
            throw new InvalidDataException("Final output needs a coherent accepted native frame, live Exposure and complete depth-disabled color state.");
        Dictionary<uint,ReadOnlyMemory<byte>> Constants(OriginalTranslatedShaderBindings layout,IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> values)
        {
            var expected=layout.Bindings.Where(b=>b.DescriptorKind=="UniformBuffer").ToArray();
            if(values.Count!=expected.Length||expected.Any(b=>!values.TryGetValue(b.Source.BindPoint,out var bytes)||bytes.Length!=b.ConstantBuffer!.ByteSize))
                throw new InvalidDataException("Final output constant bytes must match every complete original stage CB.");
            return values.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray());
        }
        var samplers=Program.PixelBindings.Bindings.Where(b=>b.DescriptorKind=="Sampler").ToArray();
        if(p.SamplerDescriptors.Count!=samplers.Length||p.SamplerDescriptors.Values.Any(v=>v.Length!=52)||
            samplers.Any(b=>!p.SamplerDescriptors.ContainsKey(b.Source.BindPoint)))
            throw new InvalidDataException("Final output requires every source sampler descriptor.");
        var inputs=Program.VertexBindings.Interface.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null).OrderBy(v=>v.Location).ToArray();
        bool spk=inputs.Length==2&&inputs[0].Location==0&&inputs[1].Location==1&&inputs[0].Type.ComponentCount==4&&inputs[1].Type.ComponentCount==2;
        bool auxiliary=inputs.Length==4&&inputs.Select(i=>i.Location).SequenceEqual(new uint?[]{0,1,2,3})&&
            inputs.Select(i=>i.Type.ComponentCount).SequenceEqual(new uint?[]{4,4,2,2});
        bool stock=Program.Source.PixelSha256==OriginalMapFinalOutputToneShaderSource.PixelSha256&&
            Program.Source.VertexSha256==OriginalMapExposureSource.VertexSha256&&inputs.Length==3&&
            inputs.Select(i=>i.Location).SequenceEqual(new uint?[]{0,1,2})&&
            inputs.Select(i=>i.Type.ComponentCount).SequenceEqual(new uint?[]{3,4,2});
        if((!spk&&!auxiliary&&!stock)||p.Quad.Length!=4*(spk||stock?24:48))throw new InvalidDataException("Final output quad differs from the selected original VS input layout.");
        var copy=TargetRoute==OriginalMapFinalOutputTargetRoute.LinearHdrThenOutput?
            OriginalMapFinalOutputTransfer.Snapshot(p.OutputCopy??throw new InvalidDataException("Final HDR output requires the actual stock-copy context and sampled-producer sampler.")):null;
        var vertexSnapshot=Constants(Program.VertexBindings,p.VertexConstants);
        var pixelSnapshot=Constants(Program.PixelBindings,p.PixelConstants);
        if(Program.DeadColorCorrection is not null)
        {
            if(!pixelSnapshot.TryGetValue(0,out var sourceCb))throw new InvalidDataException("Source-dead final profile requires its complete current CB.");
            Program.DeadColorCorrection.ValidateFrame(sourceCb.Span);
        }
        if(Program.DeadToneLut is not null)
        {
            if(!pixelSnapshot.TryGetValue(0,out var sourceCb))
                throw new InvalidDataException("Source-dead tone profile requires its complete current32B CB.");
            Program.DeadToneLut.ValidateFrame(sourceCb.Span);
        }
        return p with{VertexConstants=vertexSnapshot,PixelConstants=pixelSnapshot,OutputCopy=copy,
            Quad=p.Quad.ToArray(),ExtraImages=Array.AsReadOnly(p.ExtraImages.ToArray()),
            SamplerDescriptors=p.SamplerDescriptors.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray()),
            Raster=p.Raster with{Attachments=Array.AsReadOnly(p.Raster.Attachments.ToArray())}};
    }
    private static bool SameRaster(OriginalModelRasterInputs a,OriginalModelRasterInputs b)=>a.Cull==b.Cull&&a.FrontFace==b.FrontFace&&a.Attachments.SequenceEqual(b.Attachments);
    public Task RetireTargetAsync(){Accept();return RetireAcceptedAsync();}
    private async Task RetireAcceptedAsync(){await _gate.WaitAsync();try{await RetireTargetCoreAsync();}finally{_gate.Release();Complete();}}
    private async Task RetireTargetCoreAsync(){if(_draw is not null)await FreeAsync(_draw.Owned);_draw=null;if(_target is not null){if(_target.Transfer is not null)await _target.Transfer.DisposeAsync();else await _target.Presentation.DisposeAsync();await FreeAsync(_target.Owned);}_target=null;}
    private static Task<bool> FreeAsync(IReadOnlyList<Rid> owned)=>OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);return true;});
    private void Accept(){lock(_lifetime){ObjectDisposedException.ThrowIf(_retiring,this);_accepted++;}}
    private void Complete(){lock(_lifetime){if(--_accepted==0&&_retiring)_drained?.TrySetResult();}}
    public ValueTask DisposeAsync(){lock(_lifetime){_retiring=true;return new(_disposal??=DisposeCoreAsync());}}
    private async Task DisposeCoreAsync(){Task wait;lock(_lifetime){wait=_accepted==0?Task.CompletedTask:(_drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;}await wait;await _gate.WaitAsync();try{await RetireTargetCoreAsync();await Program.DisposeAsync();}finally{_gate.Release();}}
}
