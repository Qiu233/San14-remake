using Godot;
using System.Security.Cryptography;
using San14.Data;

public sealed record TerritoryFramePacket(ReadOnlyMemory<byte> Constants,
    ReadOnlyMemory<byte> ZoneLayers, ModelDeviceBinding Exposure,
    IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> SamplerDescriptors,
    ModelRasterInputs Raster);

/// <summary>Original PB2SM G-buffer edit owner. Copies input color storage
/// before writing AB/RS0/NS/RR0, retaining source depth and other array layers.
/// All source constants, seven zone layers, exposure and state are explicit.
/// Call under the native map scene's serial gate after terrain/models and before
/// deferred; retire target resources before the caller frees its frame.</summary>
public sealed class TerritoryRenderer : IAsyncDisposable
{
    private sealed record TargetResources(RenderTargetFrame Frame, Rid Framebuffer,
        IReadOnlyDictionary<string,Rid> Copies, ModelDeviceBinding[] Gbuffers,
        IReadOnlyList<(Rid Source,Rid Destination,uint Layers,Vector2I Extent)> CopyJobs,
        IReadOnlyList<Rid> Owned);
    private sealed record DrawResources(Rid UniformSet,Rid Pipeline,Rid Constants,Rid Zones,
        ModelDeviceBinding Exposure,string SamplerKey,string ZoneKey,ModelRasterInputs Raster,
        IReadOnlyList<Rid> Owned);
    public LoadedTerritoryResources Source { get; }
    public TerritoryShaderProgram Program { get; }
    public long AcceptedFrameCount { get; private set; }
    public long TargetCreationCount { get; private set; }
    private TargetResources? _target;
    private DrawResources? _draw;
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetime=new();
    private int _accepted;
    private bool _retiring;
    private Task? _disposal;
    private TaskCompletionSource? _drained;
    private TerritoryRenderer(LoadedTerritoryResources source,TerritoryShaderProgram program)
        =>(Source,Program)=(source,program);

    public static async Task<TerritoryRenderer> BuildAsync(LoadedTerritoryResources source,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        TerritoryResources.ValidateShader(source.Shader);
        var program=await TerritoryShaderProgram.BuildAsync(source.Shader,compiler);
        try
        {
            if(program.VertexBindings.Bindings.Count!=0||program.PixelBindings.Bindings.Count!=13||
                !program.ColorOutputLocations.SequenceEqual(new uint[]{0,1,2,3})||
                program.PixelBindings.Bindings.Single(b=>b.DescriptorKind=="UniformBuffer").ConstantBuffer?.ByteSize!=6576)
                throw new InvalidDataException("Territory source differs from the quad/13descriptor/four-MRT contract.");
            return new(source,program);
        }
        catch{await program.DisposeAsync();throw;}
    }

    public Task RenderAsync(RenderTargetFrame terrainFrame,TerritoryFramePacket packet)
    {
        ArgumentNullException.ThrowIfNull(terrainFrame);ArgumentNullException.ThrowIfNull(packet);
        var snapshot=Snapshot(packet);Accept();return RenderAcceptedAsync(terrainFrame,snapshot);
    }
    private async Task RenderAcceptedAsync(RenderTargetFrame frame,TerritoryFramePacket packet)
    {
        await _gate.WaitAsync();
        try
        {
            if(_target is null||!ReferenceEquals(_target.Frame,frame))
            {
                var next=await BuildTargetAsync(frame);
                try{await RetireTargetCoreAsync();_target=next;TargetCreationCount++;}
                catch{await FreeAsync(next.Owned);throw;}
            }
            string samplerKey=string.Join(';',packet.SamplerDescriptors.OrderBy(p=>p.Key).Select(p=>$"{p.Key}:{Convert.ToHexString(p.Value.Span)}"));
            string zoneKey=Convert.ToHexString(SHA256.HashData(packet.ZoneLayers.Span));
            if(_draw is null||_draw.Exposure!=packet.Exposure||_draw.SamplerKey!=samplerKey||_draw.ZoneKey!=zoneKey||!SameRaster(_draw.Raster,packet.Raster))
            {
                var next=await BuildDrawAsync(packet,samplerKey,zoneKey);
                if(_draw is not null)await FreeAsync(_draw.Owned);_draw=next;
            }
            var target=_target!;var draw=_draw!;
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                Program.RequireLive();
                if(rd.BufferUpdate(draw.Constants,0,6576,packet.Constants.ToArray())!=Error.Ok)
                    throw new InvalidDataException("Territory complete buffer update failed.");
                foreach(var job in target.CopyJobs)
                    for(uint layer=0;layer<job.Layers;layer++)
                        if(rd.TextureCopy(job.Source,job.Destination,Vector3.Zero,Vector3.Zero,
                            new Vector3(job.Extent.X,job.Extent.Y,1),0,0,layer,layer)!=Error.Ok)
                            throw new InvalidDataException("Territory input storage copy failed.");
                long list=rd.DrawListBegin(target.Framebuffer);
                try{rd.DrawListBindRenderPipeline(list,draw.Pipeline);rd.DrawListBindUniformSet(list,draw.UniformSet,0);rd.DrawListDraw(list,false,1,4);}
                finally{rd.DrawListEnd();}
                return true;
            });
            AcceptedFrameCount++;
        }
        finally{_gate.Release();Complete();}
    }

    private Task<TargetResources> BuildTargetAsync(RenderTargetFrame frame)
    {
        if(frame.Source.Graph.Source.ResourceUri!=Source.Graph.Source.ResourceUri||
            frame.Source.Graph.Source.DecodedSha256!=Source.Graph.Source.DecodedSha256)
            throw new InvalidDataException("Territory pass and terrain frame differ in source graph identity.");
        var devices=Source.Gbuffers.ToDictionary(r=>r.Role,r=>
            frame.Textures.TryGetValue(r.Texture.Object.ObjectId,out var texture)?texture:
            throw new InvalidDataException($"Territory {r.Role} source is absent from the terrain frame."));
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();Rid Own(Rid rid){if(!rid.IsValid)throw new InvalidDataException("Territory target allocation failed.");owned.Add(rid);return rid;}
            try
            {
                var copies=new Dictionary<string,Rid>();var inputs=new List<ModelDeviceBinding>();
                var jobs=new List<(Rid,Rid,uint,Vector2I)>();var outputs=new Godot.Collections.Array<Rid>();
                foreach(var role in new[]{"AB","RS","NS","RR"})
                {
                    var device=devices[role];using var original=rd.TextureGetFormat(device.StorageRid);
                    if(!original.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.CanCopyFromBit))
                        throw new InvalidDataException("Territory requires explicit GPU-copy permission on all four source colors.");
                    using var format=new RDTextureFormat{Format=device.StorageFormat,Width=original.Width,Height=original.Height,
                        Depth=1,ArrayLayers=original.ArrayLayers,Mipmaps=1,TextureType=original.TextureType,
                        UsageBits=RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.CanCopyToBit};
                    foreach(var f in new[]{device.StorageFormat,device.ShaderFormat}.Distinct())format.AddShareableFormat(f);
                    using var empty=new RDTextureView();Rid copy=Own(rd.TextureCreate(format,empty));copies.Add(role,copy);
                    Rid input=copy;
                    if(role=="RS")
                    {
                        if(original.TextureType!=RenderingDevice.TextureType.Type2DArray)
                            throw new InvalidDataException("Territory source RS must retain its native array shape.");
                        if(device.ShaderFormat!=device.StorageFormat){using var view=new RDTextureView{FormatOverride=device.ShaderFormat};input=Own(rd.TextureCreateShared(view,copy));}
                    }
                    else if(original.TextureType==RenderingDevice.TextureType.Type2DArray)
                    {using var view=new RDTextureView{FormatOverride=device.ShaderFormat};input=Own(rd.TextureCreateSharedFromSlice(view,copy,0,0,1,RenderingDevice.TextureSliceType.Slice2D));}
                    else if(device.ShaderFormat!=device.StorageFormat)
                    {using var view=new RDTextureView{FormatOverride=device.ShaderFormat};input=Own(rd.TextureCreateShared(view,copy));}
                    uint register=role switch{"AB"=>2u,"RS"=>3u,"NS"=>4u,_=>5u};
                    inputs.Add(new("Texture",register,input,copy,Source.Graph.Source.ResourceUri+"#territory-copy/"+role));
                    // Source PB2SM samples layer0 of RS and a 2D slice of RR.
                    // The caller's other array layers never enter this pass.
                    jobs.Add((device.StorageRid,copy,1,device.ExplicitExtent));
                    Rid output=device.StorageRid;
                    if(original.TextureType==RenderingDevice.TextureType.Type2DArray)
                    {using var view=new RDTextureView{FormatOverride=device.TargetFormat};output=Own(rd.TextureCreateSharedFromSlice(view,device.StorageRid,0,0,1,RenderingDevice.TextureSliceType.Slice2D));}
                    outputs.Add(output);
                }
                var depth=devices["D1"];inputs.Add(new("Texture",1,depth.ShaderViewRid,depth.StorageRid,depth.Source.Source.Source.ResourceUri+"#D1"));
                Rid fb=Own(rd.FramebufferCreate(outputs));if(!rd.FramebufferIsValid(fb))throw new InvalidDataException("Territory four-color framebuffer failed.");
                return new TargetResources(frame,fb,copies,inputs.ToArray(),jobs.AsReadOnly(),owned.AsReadOnly());
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }

    private Task<DrawResources> BuildDrawAsync(TerritoryFramePacket packet,string samplerKey,string zoneKey)
    {
        var target=_target!;
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();Rid Own(Rid rid){if(!rid.IsValid)throw new InvalidDataException("Territory draw allocation failed.");owned.Add(rid);return rid;}
            try
            {
                if(target.Frame.Textures.Values.Where(t=>t.Source.Object.ObjectId!=Source.Gbuffers.Single(g=>g.Role=="D1").Texture.Object.ObjectId)
                    .Any(t=>t.StorageRid==packet.Exposure.OwnerTexture||t.StorageRid==packet.Exposure.Resource))
                    throw new InvalidDataException("Territory exposure cannot alias any output color storage.");
                using(var exposure=rd.TextureGetFormat(packet.Exposure.Resource))
                    if(exposure.TextureType!=RenderingDevice.TextureType.Type2D||!exposure.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
                        throw new InvalidDataException("Territory exposure requires a sampleable source2D view.");
                var inputs=target.Gbuffers.Append(packet.Exposure).ToList();
                foreach(var value in packet.SamplerDescriptors)inputs.Add(new("Sampler",value.Key,Own(DeviceSampler.Create(rd,value.Value.Span)),null,Source.Shader.Source.ResourceUri+"#explicit-sampler/"+value.Key));
                Rid cb=Own(rd.UniformBufferCreate(6576,packet.Constants.ToArray()));
                byte[] zones=TerritoryConstants.ExpandZoneIndices(packet.ZoneLayers.Span);
                Rid zone=Own(rd.TextureBufferCreate((uint)(zones.Length/4),RenderingDevice.DataFormat.R32Uint,zones));
                inputs.Add(new("TextureBuffer",15,zone,null,Source.ScreenMapObjects.Source.ResourceUri+"#current-zone-layers"));
                var uniforms=new Godot.Collections.Array<RDUniform>();Rid set;
                try
                {
                    foreach(var declaration in Program.PixelBindings.Bindings)
                    {
                        var type=declaration.DescriptorKind switch{"UniformBuffer"=>RenderingDevice.UniformType.UniformBuffer,
                            "Sampler"=>RenderingDevice.UniformType.Sampler,"Texture"=>RenderingDevice.UniformType.Texture,
                            "TextureBuffer"=>RenderingDevice.UniformType.TextureBuffer,_=>throw new InvalidDataException("Unsupported territory descriptor.")};
                        Rid resource=declaration.DescriptorKind=="UniformBuffer"?cb:inputs.Single(i=>i.DescriptorKind==declaration.DescriptorKind&&i.SourceBindPoint==declaration.Source.BindPoint).Resource;
                        if(declaration.DescriptorKind=="Texture"&&!rd.TextureIsValid(resource))throw new InvalidDataException("Territory borrowed source image is no longer live.");
                        var uniform=new RDUniform{UniformType=type,Binding=(int)declaration.Device.Binding!.Value};uniform.AddId(resource);uniforms.Add(uniform);
                    }
                    set=Own(rd.UniformSetCreate(uniforms,Program.ShaderRid,0));
                }
                finally{foreach(var uniform in uniforms)uniform.Dispose();}
                using var raster=new RDPipelineRasterizationState{CullMode=packet.Raster.Cull,FrontFace=packet.Raster.FrontFace};
                using var multi=new RDPipelineMultisampleState();using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
                var attachments=new Godot.Collections.Array<RDPipelineColorBlendStateAttachment>();Rid pipeline;
                try
                {
                    foreach(var a in packet.Raster.Attachments)attachments.Add(new(){EnableBlend=a.EnableBlend,SrcColorBlendFactor=a.SourceColor,DstColorBlendFactor=a.DestinationColor,ColorBlendOp=a.ColorOperation,
                        SrcAlphaBlendFactor=a.SourceAlpha,DstAlphaBlendFactor=a.DestinationAlpha,AlphaBlendOp=a.AlphaOperation,WriteR=a.WriteR,WriteG=a.WriteG,WriteB=a.WriteB,WriteA=a.WriteA});
                    using var blend=new RDPipelineColorBlendState{Attachments=attachments};
                    pipeline=Own(rd.RenderPipelineCreate(Program.ShaderRid,rd.FramebufferGetFormat(target.Framebuffer),-1,RenderingDevice.RenderPrimitive.TriangleStrips,raster,multi,depth,blend));
                    if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("Territory original quad pipeline was rejected.");
                }
                finally{foreach(var a in attachments)a.Dispose();}
                return new DrawResources(set,pipeline,cb,zone,packet.Exposure,samplerKey,zoneKey,packet.Raster,owned.AsReadOnly());
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }

    private static TerritoryFramePacket Snapshot(TerritoryFramePacket p)
    {
        if(p.Constants.Length!=6576||p.ZoneLayers.Length!=7*48400||p.SamplerDescriptors.Count!=5||
            !p.SamplerDescriptors.Keys.Order().SequenceEqual(new uint[]{1,2,3,4,5})||p.SamplerDescriptors.Values.Any(v=>v.Length!=52)||
            p.Exposure is not {DescriptorKind:"Texture",SourceBindPoint:0,OwnerTexture:not null}||!p.Exposure.Resource.IsValid||
            !p.Exposure.OwnerTexture!.Value.IsValid||string.IsNullOrWhiteSpace(p.Exposure.SourceResourceUri)||
            p.Raster.DepthTest||p.Raster.DepthWrite||p.Raster.Attachments.Count!=4)
            throw new InvalidDataException("Territory requires the complete original buffers, five explicit52B samplers, exposure and four-color/depth-disabled state.");
        return p with{Constants=p.Constants.ToArray(),ZoneLayers=p.ZoneLayers.ToArray(),
            SamplerDescriptors=p.SamplerDescriptors.ToDictionary(v=>v.Key,v=>(ReadOnlyMemory<byte>)v.Value.ToArray()),
            Raster=p.Raster with{Attachments=Array.AsReadOnly(p.Raster.Attachments.ToArray())}};
    }
    private static bool SameRaster(ModelRasterInputs a,ModelRasterInputs b)=>a.Cull==b.Cull&&a.FrontFace==b.FrontFace&&a.Attachments.SequenceEqual(b.Attachments);
    public Task RetireTargetAsync(){Accept();return RetireAcceptedAsync();}
    private async Task RetireAcceptedAsync(){await _gate.WaitAsync();try{await RetireTargetCoreAsync();}finally{_gate.Release();Complete();}}
    private async Task RetireTargetCoreAsync(){if(_draw is not null)await FreeAsync(_draw.Owned);_draw=null;if(_target is not null)await FreeAsync(_target.Owned);_target=null;}
    private static Task<bool> FreeAsync(IReadOnlyList<Rid> owned)=>TerrainDeviceTexture.OnRenderThreadAsync(rd=>{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);return true;});
    private void Accept(){lock(_lifetime){ObjectDisposedException.ThrowIf(_retiring,this);_accepted++;}}
    private void Complete(){lock(_lifetime){if(--_accepted==0&&_retiring)_drained?.TrySetResult();}}
    public ValueTask DisposeAsync(){lock(_lifetime){_retiring=true;return new(_disposal??=DisposeCoreAsync());}}
    private async Task DisposeCoreAsync()
    {
        Task wait;lock(_lifetime){wait=_accepted==0?Task.CompletedTask:(_drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;}
        await wait;await _gate.WaitAsync();try{await RetireTargetCoreAsync();await Program.DisposeAsync();}finally{_gate.Release();}
    }
}
