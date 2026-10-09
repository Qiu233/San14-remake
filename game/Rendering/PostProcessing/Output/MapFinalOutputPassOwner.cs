using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>One actual SPK producer draw. Complete retained CBs, original24B
/// vertices, native sampler descriptors and MRT state are supplied explicitly.
/// DrawSucceeded is a replica producer commit, invoked only after submission.</summary>
public sealed record MapFinalOutputPassDrawPacket(int PassIndex,
    IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> VertexConstants,
    IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> PixelConstants,
    ReadOnlyMemory<byte> Quad,IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> SamplerDescriptors,
    ModelRasterInputs Raster)
{
    public Action? DrawSucceeded { get; init; }
}

/// <summary>External images are indexed by the original SPK texture table.
/// Their explicit sampled view and storage parent are borrowed. Owners and
/// native producer leases must outlive every accepted pass submission.</summary>
public sealed record MapFinalOutputPassFramePacket(long AcceptedFrameNumber,
    int BaseWidth,int BaseHeight,IReadOnlyDictionary<int,ModelDeviceBinding> ExternalTextures,
    IReadOnlyDictionary<int,MapFinalOutputPassDrawPacket> Draws,string NativeContextSource);

/// <summary>Explicit native view interpretation and origin. TEMP/KEEP creator
/// C0203D forces ColorSpace0; selections must match the graph's proved views.</summary>
public sealed record MapFinalOutputPassViewSelection(int TextureIndex,
    uint TargetDxgiFormat,uint SampledDxgiFormat,string NativeViewSource);

public sealed record MapFinalOutputPassDeviceTexture(int TextureIndex,string Name,
    MapFinalOutputTextureAllocation Allocation,Rid StorageRid,Rid ShaderViewRid,
    RenderingDevice.DataFormat Format,string SourceResourceUri)
{
    public ModelDeviceBinding Borrow(uint sourceRegister)
        =>new("Texture",sourceRegister,ShaderViewRid,StorageRid,SourceResourceUri);
}

/// <summary>Borrowed results of one accepted producer plan. Texture resources
/// remain owned by the pass owner and retire on resize, retirement or disposal.</summary>
public sealed record MapFinalOutputPassDeviceFrame(long AcceptedFrameNumber,
    IReadOnlyDictionary<int,MapFinalOutputPassDeviceTexture> Textures,
    IReadOnlyDictionary<int,ModelDeviceBinding> AvailableTextures)
{
    public ModelDeviceBinding Borrow(int textureIndex,uint sourceRegister)
        =>AvailableTextures.TryGetValue(textureIndex,out var image)?image with{SourceBindPoint=sourceRegister}:
            throw new InvalidDataException("The accepted SPK producer plan did not publish this texture.");
}

/// <summary>Executes the source SPK producer dependencies on the remake RD.
/// Owns original-format TEMP targets, MRT framebuffers and per-pass draw state.
/// Every dependency is published only after its actual original VS/PS draw.
/// Pixel instructions and varying values remain original; Position.Y receives
/// the existing source vertex adapter. No absent image is replaced or aliased.</summary>
public sealed partial class MapFinalOutputPassOwner:IAsyncDisposable
{
    private sealed record ProgramResources(MapFinalOutputPass Pass,
        ShaderSpirv VertexTranslation,AdaptedVertexShader VertexAdapter,
        ShaderSpirv PixelTranslation,TranslatedShaderBindings VertexBindings,
        TranslatedShaderBindings PixelBindings,Rid Shader);
    private sealed record TargetResources(int Width,int Height,
        IReadOnlyDictionary<int,MapFinalOutputPassDeviceTexture> Textures,
        IReadOnlyDictionary<int,Rid> Framebuffers,IReadOnlyList<Rid> Owned);
    private sealed record DrawResources(Rid Pipeline,Rid Vertices,Rid VertexBuffer,
        Rid VertexSet,Rid PixelSet,IReadOnlyDictionary<(uint Stage,uint Register),Rid> Constants,
        IReadOnlyList<ModelDeviceBinding> Inputs,string SamplerKey,
        ModelRasterInputs Raster,IReadOnlyList<Rid> Owned);

    public MapFinalOutputProducerPlan Plan { get; }
    public MapFinalOutputPassGraph Graph=>Plan.Graph;
    public MapFinalOutputPassDeviceFrame? LatestFrame { get; private set; }
    public long AcceptedFrameCount { get; private set; }
    public long SubmittedPassCount { get; private set; }
    public long TargetCreationCount { get; private set; }
    public IReadOnlyList<int> CompiledPassIndices=>Array.AsReadOnly(_programs.Keys.Order().ToArray());
    private readonly IReadOnlyDictionary<int,ProgramResources> _programs;
    private readonly bool _allowReadback;
    private readonly IReadOnlyDictionary<int,MapFinalOutputPassViewSelection> _views;
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetime=new();
    private TargetResources? _target;
    private readonly Dictionary<int,DrawResources> _draws=new();
    private bool _retiring;private int _accepted;private Task? _disposal;private TaskCompletionSource? _drained;
    private MapFinalOutputPassOwner(MapFinalOutputProducerPlan plan,
        IReadOnlyDictionary<int,ProgramResources> programs,bool allowReadback,
        IReadOnlyDictionary<int,MapFinalOutputPassViewSelection> views)
        =>(Plan,_programs,_allowReadback,_views)=(plan,programs,allowReadback,views);

    public static async Task<MapFinalOutputPassOwner> BuildAsync(
        MapFinalOutputProducerPlan plan,Vkd3dShaderCompiler compiler,
        IReadOnlyDictionary<int,MapFinalOutputPassViewSelection> sourceViews,bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(plan);ArgumentNullException.ThrowIfNull(compiler);ArgumentNullException.ThrowIfNull(sourceViews);
        if(plan.Steps.Count==0||plan.Steps.Select(p=>p.Index).Distinct().Count()!=plan.Steps.Count||
            plan.Steps.Any(p=>p.Outputs.Count==0||p.RequiresUnportedNativeExecutor||p.UnmappedOutputRegisters.Count!=0||p.Outputs.Any(o=>o.UsesCurrentTarget||o.UsesUnresolvedCustomTarget)))
            throw new InvalidDataException("SPK pass owner requires a nonempty source TEMP/MRT producer plan.");
        var outputIndices=plan.Steps.SelectMany(p=>p.Outputs).Select(o=>o.TextureIndex).Distinct().ToArray();
        if(sourceViews.Count!=outputIndices.Length||outputIndices.Any(i=>!sourceViews.TryGetValue(i,out var v)||
            v.TextureIndex!=i||string.IsNullOrWhiteSpace(v.NativeViewSource)))
            throw new InvalidDataException("SPK producer needs an explicit target and sampled view interpretation for each output.");
        var programs=new Dictionary<int,ProgramResources>();
        try
        {
            foreach(var pass in plan.Steps)programs.Add(pass.Index,await BuildProgramAsync(pass,compiler));
            return new(plan,programs,allowReadback,sourceViews.ToDictionary(p=>p.Key,p=>p.Value));
        }
        catch
        {
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{foreach(var program in programs.Values)rd.FreeRid(program.Shader);return true;});
            throw;
        }
    }

    private static async Task<ProgramResources> BuildProgramAsync(MapFinalOutputPass pass,Vkd3dShaderCompiler compiler)
    {
        var source=pass.Program;byte[] vs=source.VertexBytes.ToArray(),ps=source.PixelBytes.ToArray();
        if(Convert.ToHexString(SHA256.HashData(vs))!=source.VertexSha256||
            Convert.ToHexString(SHA256.HashData(ps))!=source.PixelSha256)
            throw new InvalidDataException("SPK producer programs differ from their complete original source identity.");
        var vertex=compiler.Compile(vs,1);var pixel=compiler.Compile(ps);
        var adapter=VertexShaderAdapter.AdaptPositionInput(vertex.Bytes);
        var vb=MapFinalOutputSourceBindings.Read(vs,adapter.Bytes);
        var pb=MapFinalOutputSourceBindings.Read(ps,pixel.Bytes);
        if(vb.Bindings.Any(b=>b.Device.DescriptorSet!=1||b.DescriptorKind!="UniformBuffer")||
            pb.Bindings.Any(b=>b.Device.DescriptorSet!=0||b.DescriptorKind is not("UniformBuffer" or "Texture" or "Sampler")))
            throw new InvalidDataException("SPK producer requires exact VS CB and PS CB/image/sampler descriptors.");
        var vertices=vb.Interface.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null).OrderBy(v=>v.Location).ToArray();
        if(vertices.Length!=2||vertices[0].Location!=0||vertices[1].Location!=1||
            vertices[0].Type is not{Kind:"vector",ComponentCount:4,ComponentType:{Kind:"float",Bits:32}}||
            vertices[1].Type is not{Kind:"vector",ComponentCount:2,ComponentType:{Kind:"float",Bits:32}})
            throw new InvalidDataException("SPK producer requires the original24B position4/UV2 source vertex layout.");
        foreach(var input in pb.Interface.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null))
        {
            var expected=input.Type.Kind=="array"?input.Type.ElementType!:input.Type;
            uint count=input.Type.Kind=="array"?input.Type.Count!.Value:1;
            for(uint element=0;element<count;element++)
            {
                uint location=input.Location!.Value+element;
                var outputs=vb.Interface.StageVariables.Where(v=>v.StorageClass==3&&v.Location==location).ToArray();
                if(outputs.Length!=1||!Compatible(outputs[0].Type,expected))
                    throw new InvalidDataException($"SPK pass {pass.Index} VS does not supply original PS varying location {location}.");
            }
        }
        var colors=pb.Interface.StageVariables.Where(v=>v.StorageClass==3).OrderBy(v=>v.Location).ToArray();
        bool ColorType(SpirvInterfaceType t)=>t.Kind=="vector"&&t.ComponentCount is >=1 and <=4&&
            t.ComponentType is{Kind:"float",Bits:32};
        var locations=pass.Outputs.OrderBy(o=>o.Register).Select(o=>o.Register).ToArray();
        if(colors.Length!=locations.Length||colors.Any(v=>v.Location is null||!ColorType(v.Type))||
            !colors.Select(v=>v.Location!.Value).SequenceEqual(locations)||
            !locations.SequenceEqual(Enumerable.Range(0,locations.Length).Select(i=>(uint)i)))
            throw new InvalidDataException("SPK original PS signature and dense MRT functor outputs disagree.");
        if(pb.Bindings.Count(b=>b.DescriptorKind=="Texture")!=pass.Inputs.Count||
            pb.Bindings.Count(b=>b.DescriptorKind=="Sampler")!=pass.Inputs.Count||
            pass.Inputs.Any(i=>!pb.Bindings.Any(b=>b.DescriptorKind=="Texture"&&b.Source.BindPoint==i.TextureRegister)||
                !pb.Bindings.Any(b=>b.DescriptorKind=="Sampler"&&b.Source.BindPoint==i.SamplerRegister)))
            throw new InvalidDataException("SPK native sampler/texture membership and reflected descriptors disagree.");
        Rid shader=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            using var stages=new RDShaderSpirV();stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,pixel.Bytes);
            Rid value=rd.ShaderCreateFromSpirV(stages,"OriginalSPKProducer/"+pass.Name);
            if(!value.IsValid)throw new InvalidDataException("Device rejected the original SPK producer VS/PS pair.");
            return value;
        });
        return new(pass,vertex,adapter,pixel,vb,pb,shader);
    }
    private static bool Compatible(SpirvInterfaceType a,SpirvInterfaceType b)=>a.Kind==b.Kind&&
        (a.Kind=="vector"?a.ComponentCount>=b.ComponentCount&&Compatible(a.ComponentType!,b.ComponentType!):
         a.Kind is "float" or "int"&&a.Bits==b.Bits&&a.Signed==b.Signed);

    public Task<MapFinalOutputPassDeviceFrame> RenderAsync(MapFinalOutputPassFramePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);var snapshot=Snapshot(packet);Accept();return RenderAcceptedAsync(snapshot);
    }
    private async Task<MapFinalOutputPassDeviceFrame> RenderAcceptedAsync(MapFinalOutputPassFramePacket packet)
    {
        await _gate.WaitAsync();
        try
        {
            return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                if(_target is null||_target.Width!=packet.BaseWidth||_target.Height!=packet.BaseHeight)
                {
                    var next=BuildTargets(rd,packet.BaseWidth,packet.BaseHeight);
                    RetireTargets(rd);_target=next;TargetCreationCount++;
                }
                var target=_target;
                var available=Plan.Execute(packet.BaseWidth,packet.BaseHeight,packet.ExternalTextures,draw=>
                {
                    var current=packet.Draws[draw.Pass.Index];var program=_programs[draw.Pass.Index];
                    var inputs=draw.PixelInputs.OrderBy(i=>i.Key).Select(i=>i.Value with{SourceBindPoint=i.Key}).ToArray();
                    ValidateImages(rd,draw.Pass,inputs,target);
                    string samplerKey=string.Join(';',current.SamplerDescriptors.OrderBy(i=>i.Key).Select(i=>$"{i.Key}:{Convert.ToHexString(i.Value.Span)}"));
                    if(!_draws.TryGetValue(draw.Pass.Index,out var resources)||!resources.Inputs.SequenceEqual(inputs)||
                        resources.SamplerKey!=samplerKey||!SameRaster(resources.Raster,current.Raster))
                    {
                        var next=BuildDraw(rd,program,current,inputs,samplerKey,target.Framebuffers[draw.Pass.Index]);
                        if(resources is not null)Free(rd,resources.Owned);_draws[draw.Pass.Index]=resources=next;
                    }
                    foreach(var stage in new[]{(1u,current.VertexConstants),(0u,current.PixelConstants)})
                        foreach(var cb in stage.Item2)
                            if(rd.BufferUpdate(resources.Constants[(stage.Item1,cb.Key)],0,(uint)cb.Value.Length,cb.Value.ToArray())!=Error.Ok)
                                throw new InvalidDataException("SPK producer source constant upload failed.");
                    if(rd.BufferUpdate(resources.VertexBuffer,0,96,current.Quad.ToArray())!=Error.Ok)
                        throw new InvalidDataException("SPK producer source vertex upload failed.");
                    long list=rd.DrawListBegin(target.Framebuffers[draw.Pass.Index]);
                    try
                    {
                        rd.DrawListBindRenderPipeline(list,resources.Pipeline);rd.DrawListBindVertexArray(list,resources.Vertices);
                        if(resources.VertexSet.IsValid)rd.DrawListBindUniformSet(list,resources.VertexSet,1);
                        if(resources.PixelSet.IsValid)rd.DrawListBindUniformSet(list,resources.PixelSet,0);
                        rd.DrawListDraw(list,false,1,4);
                    }
                    finally{rd.DrawListEnd();}
                    SubmittedPassCount++;
                    current.DrawSucceeded?.Invoke();
                    return draw.Pass.Outputs.ToDictionary(o=>o.TextureIndex,o=>target.Textures[o.TextureIndex].Borrow(0));
                });
                AcceptedFrameCount++;
                return LatestFrame=new(packet.AcceptedFrameNumber,target.Textures,available);
            });
        }
        finally{_gate.Release();Complete();}
    }

    private TargetResources BuildTargets(RenderingDevice rd,int width,int height)
    {
        var owned=new List<Rid>();Rid Own(Rid value){if(!value.IsValid)throw new InvalidDataException("SPK producer target allocation failed.");owned.Add(value);return value;}
        try
        {
            var textures=new Dictionary<int,MapFinalOutputPassDeviceTexture>();
            foreach(int index in Plan.Steps.SelectMany(p=>p.Outputs).Select(o=>o.TextureIndex).Distinct())
            {
                var allocation=Graph.ResolveAllocation(index,width,height);
                if(allocation.Width<=0||allocation.Height<=0||allocation.PixelFormatCode>255||
                    allocation.NativeTextureKind is not(3 or 4)||allocation.ForcedColorSpace!=0||allocation.ForcedLevelNum!=1)
                    throw new NotSupportedException("SPK producer allocation is outside the source single mip/sample2D profile.");
                var views=_views[index];var native=Graph.ResolveTempFormats(index);
                if(views.TargetDxgiFormat!=native.TargetViewFormat||views.SampledDxgiFormat!=native.ShaderViewFormat)
                    throw new InvalidDataException("SPK explicit TEMP views disagree with the proved native creator's ColorSpace0 format.");
                var formatCode=DeviceFormat(views.TargetDxgiFormat);var sampledFormat=DeviceFormat(views.SampledDxgiFormat);
                var usage=RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.ColorAttachmentBit;
                if(_allowReadback)usage|=RenderingDevice.TextureUsageBits.CanCopyFromBit;
                if(!rd.TextureIsFormatSupportedForUsage(formatCode,usage))throw new NotSupportedException("Device does not support this source SPK TEMP format/usage.");
                using var format=new RDTextureFormat{Format=formatCode,Width=(uint)allocation.Width,Height=(uint)allocation.Height,
                    Depth=1,ArrayLayers=1,Mipmaps=1,Samples=RenderingDevice.TextureSamples.Samples1,
                    TextureType=RenderingDevice.TextureType.Type2D,UsageBits=usage};
                foreach(var shareable in new[]{formatCode,sampledFormat}.Distinct())format.AddShareableFormat(shareable);
                using var viewDescription=new RDTextureView();Rid texture=Own(rd.TextureCreate(format,viewDescription));
                Rid sampled=texture;
                if(sampledFormat!=formatCode){using var sampleView=new RDTextureView{FormatOverride=sampledFormat};sampled=Own(rd.TextureCreateShared(sampleView,texture));}
                textures.Add(index,new(index,Graph.Textures[index].Name,allocation,texture,sampled,formatCode,
                    Graph.Source.PostEffect.Source.ResourceUri+$"#SPK/texture/{index}/{Graph.Textures[index].Name};view="+views.NativeViewSource));
            }
            var framebuffers=new Dictionary<int,Rid>();
            foreach(var pass in Plan.Steps)
            {
                var outputs=pass.Outputs.OrderBy(o=>o.Register).Select(o=>textures[o.TextureIndex]).ToArray();
                if(outputs.Select(o=>(o.Allocation.Width,o.Allocation.Height)).Distinct().Count()!=1)
                    throw new InvalidDataException("SPK MRT outputs have different source allocation extents.");
                Rid framebuffer=Own(rd.FramebufferCreate(new Godot.Collections.Array<Rid>(outputs.Select(t=>t.StorageRid))));
                if(!rd.FramebufferIsValid(framebuffer))throw new InvalidDataException("SPK source MRT framebuffer creation failed.");
                framebuffers.Add(pass.Index,framebuffer);
            }
            return new(width,height,textures,framebuffers,owned.AsReadOnly());
        }
        catch{Free(rd,owned);throw;}
    }

    private static RenderingDevice.DataFormat DeviceFormat(uint value)=>value switch{
        28=>RenderingDevice.DataFormat.R8G8B8A8Unorm,29=>RenderingDevice.DataFormat.R8G8B8A8Srgb,
        87=>RenderingDevice.DataFormat.B8G8R8A8Unorm,91=>RenderingDevice.DataFormat.B8G8R8A8Srgb,
        10=>RenderingDevice.DataFormat.R16G16B16A16Sfloat,2=>RenderingDevice.DataFormat.R32G32B32A32Sfloat,
        41=>RenderingDevice.DataFormat.R32Sfloat,_=>throw new NotSupportedException("SPK TEMP native format has no exact device adapter.")};

    private static void ValidateImages(RenderingDevice rd,MapFinalOutputPass pass,
        ModelDeviceBinding[] inputs,TargetResources target)
    {
        if(inputs.Length!=pass.Inputs.Count||inputs.Select(i=>i.SourceBindPoint).Distinct().Count()!=inputs.Length)
            throw new InvalidDataException("SPK producer needs every exact source input image.");
        var outputs=pass.Outputs.Select(o=>target.Textures[o.TextureIndex].StorageRid).ToHashSet();
        foreach(var input in inputs)
        {
            if(input is not{DescriptorKind:"Texture",OwnerTexture:{IsValid:true}}||!input.Resource.IsValid||
                string.IsNullOrWhiteSpace(input.SourceResourceUri)||outputs.Contains(input.Resource)||outputs.Contains(input.OwnerTexture.Value)||
                !rd.TextureIsValid(input.Resource)||!rd.TextureIsValid(input.OwnerTexture.Value))
                throw new InvalidDataException("SPK producer borrowed image is retired, incomplete or aliases an output storage parent.");
            using var shape=rd.TextureGetFormat(input.Resource);
            if(shape.TextureType!=RenderingDevice.TextureType.Type2D||!shape.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
                throw new InvalidDataException("SPK producer image differs from its source2D sampling profile.");
        }
    }

    private static DrawResources BuildDraw(RenderingDevice rd,ProgramResources program,
        MapFinalOutputPassDrawPacket packet,ModelDeviceBinding[] inputs,string samplerKey,Rid framebuffer)
    {
        var owned=new List<Rid>();Rid Own(Rid value){if(!value.IsValid)throw new InvalidDataException("SPK draw resource allocation failed.");owned.Add(value);return value;}
        try
        {
            var constants=new Dictionary<(uint,uint),Rid>();
            foreach(var stage in new[]{(1u,packet.VertexConstants),(0u,packet.PixelConstants)})
                foreach(var cb in stage.Item2)constants.Add((stage.Item1,cb.Key),Own(rd.UniformBufferCreate((uint)cb.Value.Length,cb.Value.ToArray())));
            var samplers=packet.SamplerDescriptors.ToDictionary(p=>p.Key,p=>Own(DeviceSampler.Create(rd,p.Value.Span)));
            Rid Set(TranslatedShaderBindings layout,uint stage)
            {
                if(layout.Bindings.Count==0)return default;
                var uniforms=new Godot.Collections.Array<RDUniform>();
                try
                {
                    foreach(var binding in layout.Bindings)
                    {
                        var uniform=new RDUniform{Binding=(int)binding.Device.Binding!.Value,UniformType=binding.DescriptorKind switch{
                            "UniformBuffer"=>RenderingDevice.UniformType.UniformBuffer,"Texture"=>RenderingDevice.UniformType.Texture,
                            "Sampler"=>RenderingDevice.UniformType.Sampler,_=>throw new InvalidDataException("Unsupported SPK descriptor.")}};
                        uniforms.Add(uniform);uniform.AddId(binding.DescriptorKind switch{
                            "UniformBuffer"=>constants[(stage,binding.Source.BindPoint)],"Sampler"=>samplers[binding.Source.BindPoint],
                            _=>inputs.Single(i=>i.SourceBindPoint==binding.Source.BindPoint).Resource});
                    }
                    Rid value=Own(rd.UniformSetCreate(uniforms,program.Shader,stage));
                    if(!rd.UniformSetIsValid(value))throw new InvalidDataException("SPK producer uniform set failed.");return value;
                }
                finally{foreach(var uniform in uniforms)uniform.Dispose();}
            }
            Rid vs=Set(program.VertexBindings,1),ps=Set(program.PixelBindings,0);
            var attributes=new Godot.Collections.Array<RDVertexAttribute>();long vertexFormat;
            try
            {
                attributes.Add(new(){Location=0,Offset=0,Stride=24,Format=RenderingDevice.DataFormat.R32G32B32A32Sfloat,Frequency=RenderingDevice.VertexFrequency.Vertex});
                attributes.Add(new(){Location=1,Offset=16,Stride=24,Format=RenderingDevice.DataFormat.R32G32Sfloat,Frequency=RenderingDevice.VertexFrequency.Vertex});
                vertexFormat=rd.VertexFormatCreate(attributes);
            }
            finally{foreach(var attribute in attributes)attribute.Dispose();}
            Rid vb=Own(rd.VertexBufferCreate(96,packet.Quad.ToArray()));
            Rid vertices=Own(rd.VertexArrayCreate(4,vertexFormat,[vb,vb]));
            using var raster=new RDPipelineRasterizationState{CullMode=packet.Raster.Cull,FrontFace=packet.Raster.FrontFace};
            using var multi=new RDPipelineMultisampleState();using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
            var attachments=new Godot.Collections.Array<RDPipelineColorBlendStateAttachment>();
            try
            {
                foreach(var a in packet.Raster.Attachments)attachments.Add(new(){EnableBlend=a.EnableBlend,
                    SrcColorBlendFactor=a.SourceColor,DstColorBlendFactor=a.DestinationColor,ColorBlendOp=a.ColorOperation,
                    SrcAlphaBlendFactor=a.SourceAlpha,DstAlphaBlendFactor=a.DestinationAlpha,AlphaBlendOp=a.AlphaOperation,
                    WriteR=a.WriteR,WriteG=a.WriteG,WriteB=a.WriteB,WriteA=a.WriteA});
                using var blend=new RDPipelineColorBlendState{Attachments=attachments};
                Rid pipeline=Own(rd.RenderPipelineCreate(program.Shader,rd.FramebufferGetFormat(framebuffer),vertexFormat,
                    RenderingDevice.RenderPrimitive.TriangleStrips,raster,multi,depth,blend));
                if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("SPK actual original VS/PS MRT pipeline failed.");
                return new(pipeline,vertices,vb,vs,ps,constants,Array.AsReadOnly(inputs),samplerKey,packet.Raster,owned.AsReadOnly());
            }
            finally{foreach(var attachment in attachments)attachment.Dispose();}
        }
        catch{Free(rd,owned);throw;}
    }

    private MapFinalOutputPassFramePacket Snapshot(MapFinalOutputPassFramePacket packet)
    {
        if(packet.AcceptedFrameNumber<=0||packet.BaseWidth<=0||packet.BaseHeight<=0||string.IsNullOrWhiteSpace(packet.NativeContextSource)||
            packet.ExternalTextures.Count!=Plan.ExternalTextureIndices.Count||Plan.ExternalTextureIndices.Any(i=>!packet.ExternalTextures.ContainsKey(i))||
            packet.Draws.Count!=Plan.Steps.Count||Plan.Steps.Any(p=>!packet.Draws.ContainsKey(p.Index)))
            throw new InvalidDataException("SPK producer packet needs an accepted frame, complete source plan and exact external texture registry.");
        foreach(var image in packet.ExternalTextures.Values)
            if(image is not{DescriptorKind:"Texture",OwnerTexture:{IsValid:true}}||!image.Resource.IsValid||string.IsNullOrWhiteSpace(image.SourceResourceUri))
                throw new InvalidDataException("SPK external producer identity is absent or incomplete.");
        var draws=new Dictionary<int,MapFinalOutputPassDrawPacket>();
        foreach(var pass in Plan.Steps)
        {
            var current=packet.Draws[pass.Index];var program=_programs[pass.Index];
            if(current.PassIndex!=pass.Index||current.Quad.Length!=96||current.Raster.DepthTest||current.Raster.DepthWrite||
                current.Raster.Attachments.Count!=pass.Outputs.Count)
                throw new InvalidDataException("SPK source draw needs its96B quad and complete depth-disabled MRT state.");
            Dictionary<uint,ReadOnlyMemory<byte>> Constants(TranslatedShaderBindings layout,IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> values)
            {
                var expected=layout.Bindings.Where(b=>b.DescriptorKind=="UniformBuffer").ToArray();
                if(values.Count!=expected.Length||expected.Any(b=>!values.TryGetValue(b.Source.BindPoint,out var bytes)||bytes.Length!=b.ConstantBuffer!.ByteSize))
                    throw new InvalidDataException("SPK producer requires every complete reflected source CB.");
                return values.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray());
            }
            var samplers=program.PixelBindings.Bindings.Where(b=>b.DescriptorKind=="Sampler").ToArray();
            if(current.SamplerDescriptors.Count!=samplers.Length||current.SamplerDescriptors.Values.Any(v=>v.Length!=52)||
                samplers.Any(s=>!current.SamplerDescriptors.ContainsKey(s.Source.BindPoint)))
                throw new InvalidDataException("SPK producer requires every exact native52B sampler descriptor.");
            draws.Add(pass.Index,current with{VertexConstants=Constants(program.VertexBindings,current.VertexConstants),
                PixelConstants=Constants(program.PixelBindings,current.PixelConstants),Quad=current.Quad.ToArray(),
                SamplerDescriptors=current.SamplerDescriptors.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray()),
                Raster=current.Raster with{Attachments=Array.AsReadOnly(current.Raster.Attachments.ToArray())}});
        }
        return packet with{ExternalTextures=packet.ExternalTextures.ToDictionary(p=>p.Key,p=>p.Value),Draws=draws};
    }
    private static bool SameRaster(ModelRasterInputs a,ModelRasterInputs b)=>a.Cull==b.Cull&&a.FrontFace==b.FrontFace&&a.Attachments.SequenceEqual(b.Attachments);
    private static void Free(RenderingDevice rd,IReadOnlyList<Rid> owned){for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);}
    private void RetireTargets(RenderingDevice rd)
    {
        foreach(var draw in _draws.Values)Free(rd,draw.Owned);_draws.Clear();
        if(_target is not null)Free(rd,_target.Owned);_target=null;LatestFrame=null;
    }
    public Task RetireTargetsAsync(){Accept();return RetireAcceptedAsync();}
    public Task RetireTargetAsync()=>RetireTargetsAsync();
    private async Task RetireAcceptedAsync(){await _gate.WaitAsync();try{await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{RetireTargets(rd);return true;});}finally{_gate.Release();Complete();}}
    private void Accept(){lock(_lifetime){ObjectDisposedException.ThrowIf(_retiring,this);_accepted++;}}
    private void Complete(){lock(_lifetime){if(--_accepted==0&&_retiring)_drained?.TrySetResult();}}
    public ValueTask DisposeAsync(){lock(_lifetime){_retiring=true;return new(_disposal??=DisposeCoreAsync());}}
    private async Task DisposeCoreAsync()
    {
        Task wait;lock(_lifetime){wait=_accepted==0?Task.CompletedTask:(_drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;}
        await wait;await _gate.WaitAsync();
        try{await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{RetireTargets(rd);foreach(var program in _programs.Values)rd.FreeRid(program.Shader);return true;});}
        finally{_gate.Release();}
    }
}
