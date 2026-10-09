using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

public enum MapFinalOutputNativeProgramKind { Spk,StockScreenDefault3,StockScreenDefault1 }
public sealed record MapFinalOutputNativeProgram(string Key,MapFinalOutputNativeProgramKind Kind,int SourcePassIndex);
public sealed record MapFinalOutputNativeTarget(int Id,int SourceTextureIndex,int Width,int Height,string SourceEvidence)
{
    public RenderTextureDefinition? SourceDeclaration { get; init; }
}
public sealed record MapFinalOutputNativeDraw(string ProgramKey,IReadOnlyList<int> Outputs,
    IReadOnlyDictionary<uint,int> Inputs,MapFinalOutputPassDrawPacket Packet,Color? ClearColor,string SourceEvidence)
{
    public Rect2I? Scissor { get; init; }
}
public sealed record MapFinalOutputNativeRecipeFrame(long AcceptedFrameNumber,
    IReadOnlyList<MapFinalOutputNativeTarget> Targets,IReadOnlyDictionary<int,ModelDeviceBinding> ExternalTextures,
    IReadOnlyList<MapFinalOutputNativeDraw> Draws,string SourceEvidence);

public sealed partial class MapFinalOutputPassOwner
{
    /// <summary>Original native custom draw recipes: ordered MRT/ping-pong
    /// targets and original SPK or stock screen programs. Every real draw is
    /// submitted; private scratch images have explicit source allocation clones.
    /// This does not replace a custom executor by one generic quad.</summary>
    public sealed class NativeRecipeOwner:IAsyncDisposable
    {
        private sealed record Bind(uint Stage,uint Register,uint DeviceBinding,string Kind,int Bytes);
        private sealed record Shader(MapFinalOutputNativeProgram Source,Rid Rid,IReadOnlyList<Bind> Bindings,bool Stock,int ColorOutputs);
        private sealed record Target(MapFinalOutputNativeTarget Source,Rid Storage,RenderingDevice.DataFormat Format);
        private sealed record Draw(string ProgramKey,Rid Pipeline,Rid VertexBuffer,Rid Vertices,Rid Indices,Rid VertexSet,Rid PixelSet,
            IReadOnlyDictionary<(uint,uint),Rid> Constants,IReadOnlyList<ModelDeviceBinding> Images,string Samplers,
            ModelRasterInputs Raster,IReadOnlyList<int> Outputs,IReadOnlyList<Rid> Owned);
        public MapFinalOutputPassGraph Graph { get; }
        public IReadOnlyDictionary<int,ModelDeviceBinding>? LatestTextures { get; private set; }
        public long SubmittedDrawCount { get; private set; }
        public long AcceptedFrameCount { get; private set; }
        private readonly IReadOnlyDictionary<string,Shader> _programs;private readonly bool _readback;
        private readonly Dictionary<int,Target> _targets=new();private readonly Dictionary<string,Rid> _framebuffers=new();
        private readonly Dictionary<int,Draw> _draws=new();private readonly List<Rid> _targetRids=new();
        private readonly SemaphoreSlim _gate=new(1,1);private readonly object _lifetime=new();
        private int _accepted;private bool _retiring;private Task? _disposal;private TaskCompletionSource? _drained;
        private NativeRecipeOwner(MapFinalOutputPassGraph graph,IReadOnlyDictionary<string,Shader> programs,bool readback)
            =>(Graph,_programs,_readback)=(graph,programs,readback);
        public static async Task<NativeRecipeOwner> BuildAsync(MapFinalOutputPassGraph graph,
            IReadOnlyList<MapFinalOutputNativeProgram> programs,Vkd3dShaderCompiler compiler,bool allowReadback=false)
        {
            ArgumentNullException.ThrowIfNull(graph);ArgumentNullException.ThrowIfNull(programs);ArgumentNullException.ThrowIfNull(compiler);
            if(programs.Count==0||programs.Any(p=>string.IsNullOrWhiteSpace(p.Key))||programs.Select(p=>p.Key).Distinct().Count()!=programs.Count)
                throw new InvalidDataException("Native recipe requires unique exact source program keys.");
            var shaders=new Dictionary<string,Shader>();
            try
            {
                foreach(var source in programs)
                {
                    if(source.Kind==MapFinalOutputNativeProgramKind.Spk)
                    {
                        if(source.SourcePassIndex<0||source.SourcePassIndex>=graph.Passes.Count)throw new InvalidDataException("Native recipe SPK pass is absent.");
                        var built=await BuildProgramAsync(graph.Passes[source.SourcePassIndex],compiler);
                        var bindings=built.VertexBindings.Bindings.Select(b=>BindFrom(b,1)).Concat(built.PixelBindings.Bindings.Select(b=>BindFrom(b,0))).ToArray();
                        shaders.Add(source.Key,new(source,built.Shader,Array.AsReadOnly(bindings),false,built.Pass.Outputs.Count));
                    }
                    else if(source.Kind is MapFinalOutputNativeProgramKind.StockScreenDefault3 or MapFinalOutputNativeProgramKind.StockScreenDefault1)
                    {
                        var stock=graph.Source.StockScreen;var vs=compiler.Compile(stock.VertexProgram.Span);
                        byte[] pixelBytes=source.Kind==MapFinalOutputNativeProgramKind.StockScreenDefault1?MapFinalOutputStockScreenPrograms.ReadMode1():stock.PixelProgram.ToArray();
                        var ps=compiler.Compile(pixelBytes);
                        var vertex=VertexShaderAdapter.AdaptPositionInput(MoveStockVertexSet(vs.Bytes));
                        var vi=SpirvShaderInterfaceReader.Read(vertex.Bytes);var pi=SpirvShaderInterfaceReader.Read(ps.Bytes);
                        if(vi.Descriptors.Count!=1||vi.Descriptors[0] is not{StorageClass:2,RequiredBufferBytes:32,DescriptorSet:1}||
                            pi.Descriptors.Count!=3||pi.Descriptors.Any(d=>d.DescriptorSet!=0)||pi.Descriptors.Count(d=>d.StorageClass==2&&d.RequiredBufferBytes==16)!=1||
                            pi.Descriptors.Count(d=>d.Type.Kind=="sampler")!=1||pi.Descriptors.Count(d=>d.Type is{Kind:"image",Dimension:1,Arrayed:false})!=1)
                            throw new InvalidDataException("Native stock recipe interface changed.");
                        var bindings=vi.Descriptors.Select(d=>new Bind(1,0,d.Binding!.Value,"UniformBuffer",32)).Concat(pi.Descriptors.Select(d=>
                            new Bind(0,0,d.Binding!.Value,d.StorageClass==2?"UniformBuffer":d.Type.Kind=="sampler"?"Sampler":"Texture",d.StorageClass==2?16:0))).ToArray();
                        Rid rid=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{using var spirv=new RDShaderSpirV();
                            spirv.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,vertex.Bytes);spirv.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,ps.Bytes);
                            var shader=rd.ShaderCreateFromSpirV(spirv,"OriginalNativeStockRecipe");if(!shader.IsValid)throw new InvalidDataException("Native stock recipe shader failed.");return shader;});
                        shaders.Add(source.Key,new(source,rid,Array.AsReadOnly(bindings),true,1));
                    }
                    else throw new InvalidDataException("Native recipe program kind is unsupported.");
                }
                return new(graph,shaders,allowReadback);
            }
            catch{await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{foreach(var shader in shaders.Values)rd.FreeRid(shader.Rid);return true;});throw;}
        }
        private static Bind BindFrom(TranslatedShaderBinding source,uint stage)
            =>new(stage,source.Source.BindPoint,source.Device.Binding!.Value,source.DescriptorKind,source.ConstantBuffer?.ByteSize??0);
        public Task<IReadOnlyDictionary<int,ModelDeviceBinding>> RenderAsync(MapFinalOutputNativeRecipeFrame frame)
        {
            var copy=Snapshot(frame);Accept();return RenderAcceptedAsync(copy);
        }
        private async Task<IReadOnlyDictionary<int,ModelDeviceBinding>> RenderAcceptedAsync(MapFinalOutputNativeRecipeFrame frame)
        {
            await _gate.WaitAsync();
            try{return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                if(_targets.Count!=frame.Targets.Count||frame.Targets.Any(t=>!_targets.TryGetValue(t.Id,out var prior)||prior.Source!=t))
                    BuildTargets(rd,frame.Targets);
                var available=frame.ExternalTextures.ToDictionary(p=>p.Key,p=>p.Value);
                var published=frame.ExternalTextures.Keys.ToHashSet();
                foreach(var target in _targets.Values)available.Add(target.Source.Id,new("Texture",0,target.Storage,target.Storage,target.Source.SourceEvidence));
                for(int ordinal=0;ordinal<frame.Draws.Count;ordinal++)
                {
                    var source=frame.Draws[ordinal];var shader=_programs[source.ProgramKey];var packet=source.Packet;
                    if(source.Inputs.Values.Any(id=>!published.Contains(id)))throw new InvalidDataException("Native recipe consumes a target before its actual producer draw.");
                    var images=source.Inputs.OrderBy(p=>p.Key).Select(p=>available.TryGetValue(p.Value,out var image)?image with{SourceBindPoint=p.Key}:
                        throw new InvalidDataException("Native recipe dependency is absent before draw.")).ToArray();
                    var outputRids=source.Outputs.Select(id=>_targets[id].Storage).ToHashSet();
                    foreach(var image in images)
                    {
                        if(image is not{DescriptorKind:"Texture",OwnerTexture:{IsValid:true}}||!rd.TextureIsValid(image.Resource)||!rd.TextureIsValid(image.OwnerTexture.Value)||
                            outputRids.Contains(image.Resource)||outputRids.Contains(image.OwnerTexture.Value))throw new InvalidDataException("Native recipe input is retired or aliases a current output.");
                        using var shape=rd.TextureGetFormat(image.Resource);if(shape.TextureType!=RenderingDevice.TextureType.Type2D||!shape.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
                            throw new InvalidDataException("Native recipe image violates its source2D shape.");
                    }
                    string fbKey=string.Join(',',source.Outputs),samplerKey=string.Join(';',packet.SamplerDescriptors.OrderBy(p=>p.Key).Select(p=>$"{p.Key}:{Convert.ToHexString(p.Value.Span)}"));
                    if(!_framebuffers.TryGetValue(fbKey,out var framebuffer))
                    {
                        var outputs=source.Outputs.Select(i=>_targets[i]).ToArray();if(outputs.Select(t=>(t.Source.Width,t.Source.Height)).Distinct().Count()!=1)throw new InvalidDataException("Native MRT extent mismatch.");
                        framebuffer=rd.FramebufferCreate(new Godot.Collections.Array<Rid>(outputs.Select(t=>t.Storage)));if(!framebuffer.IsValid||!rd.FramebufferIsValid(framebuffer))throw new InvalidDataException("Native recipe framebuffer failed.");
                        _framebuffers.Add(fbKey,framebuffer);_targetRids.Add(framebuffer);
                    }
                    if(!_draws.TryGetValue(ordinal,out var draw)||draw.ProgramKey!=source.ProgramKey||!draw.Images.SequenceEqual(images)||draw.Samplers!=samplerKey||!SameRaster(draw.Raster,packet.Raster)||!draw.Outputs.SequenceEqual(source.Outputs))
                    {
                        var next=BuildNativeDraw(rd,shader,packet,images,samplerKey,source.Outputs,framebuffer);if(draw is not null)Free(rd,draw.Owned);_draws[ordinal]=draw=next;
                    }
                    foreach(var stage in new[]{(1u,packet.VertexConstants),(0u,packet.PixelConstants)})foreach(var cb in stage.Item2)
                        if(rd.BufferUpdate(draw.Constants[(stage.Item1,cb.Key)],0,(uint)cb.Value.Length,cb.Value.ToArray())!=Error.Ok)throw new InvalidDataException("Native recipe CB upload failed.");
                    if(rd.BufferUpdate(draw.VertexBuffer,0,96,packet.Quad.ToArray())!=Error.Ok)throw new InvalidDataException("Native recipe quad upload failed.");
                    var targetExtent=_targets[source.Outputs[0]].Source;
                    var viewport=new Rect2I(0,0,targetExtent.Width,targetExtent.Height);
                    Rect2I? effectiveScissor=source.Scissor is{} nativeScissor?nativeScissor.Intersection(viewport):null;
                    bool hasPixelDomain=effectiveScissor is not{} clipped||clipped.Size.X>0&&clipped.Size.Y>0;
                    // Godot ignores an empty scissor and retains its old
                    // rectangle. Preserve the native zero-pixel semantics by
                    // omitting Draw; any requested attachment clear still runs.
                    long list=source.ClearColor is Color color?rd.DrawListBegin(framebuffer,RenderingDevice.DrawFlags.ClearAll,Enumerable.Repeat(color,source.Outputs.Count).ToArray()):rd.DrawListBegin(framebuffer);
                    try{rd.DrawListBindRenderPipeline(list,draw.Pipeline);rd.DrawListBindVertexArray(list,draw.Vertices);
                        if(hasPixelDomain&&effectiveScissor is Rect2I scissor)rd.DrawListEnableScissor(list,scissor);
                        if(draw.Indices.IsValid)rd.DrawListBindIndexArray(list,draw.Indices);
                        if(draw.VertexSet.IsValid)rd.DrawListBindUniformSet(list,draw.VertexSet,1);if(draw.PixelSet.IsValid)rd.DrawListBindUniformSet(list,draw.PixelSet,0);
                        if(hasPixelDomain)rd.DrawListDraw(list,draw.Indices.IsValid,1,draw.Indices.IsValid?0u:4u);}
                    finally{rd.DrawListEnd();}
                    if(hasPixelDomain)SubmittedDrawCount++;packet.DrawSucceeded?.Invoke();
                    if(hasPixelDomain||source.ClearColor is not null)foreach(int id in source.Outputs)published.Add(id);
                }
                AcceptedFrameCount++;return LatestTextures=available.Where(p=>published.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value);
            });}
            finally{_gate.Release();Complete();}
        }
        private void BuildTargets(RenderingDevice rd,IReadOnlyList<MapFinalOutputNativeTarget> targets)
        {
            var next=new Dictionary<int,Target>();var owned=new List<Rid>();
            try
            {
                foreach(var source in targets)
                {
                    var native=source.SourceDeclaration is not null?RenderTextureFormatMath.Resolve(source.SourceDeclaration):Graph.ResolveTempFormats(source.SourceTextureIndex);
                    if(native.ShaderViewFormat!=native.TargetViewFormat)throw new InvalidDataException("Native recipe requires the exact same sampled and render view format.");
                    var formatCode=native.TargetViewFormat switch{87=>RenderingDevice.DataFormat.B8G8R8A8Unorm,10=>RenderingDevice.DataFormat.R16G16B16A16Sfloat,
                        41=>RenderingDevice.DataFormat.R32Sfloat,26=>RenderingDevice.DataFormat.B10G11R11UfloatPack32,_=>throw new NotSupportedException("Native recipe target has no source view adapter.")};
                    var usage=RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.ColorAttachmentBit;if(_readback)usage|=RenderingDevice.TextureUsageBits.CanCopyFromBit;
                    using var format=new RDTextureFormat{Format=formatCode,Width=(uint)source.Width,Height=(uint)source.Height,Depth=1,ArrayLayers=1,Mipmaps=1,TextureType=RenderingDevice.TextureType.Type2D,UsageBits=usage};
                    using var view=new RDTextureView();var storage=rd.TextureCreate(format,view);if(!storage.IsValid)throw new InvalidDataException("Native recipe target failed.");owned.Add(storage);next.Add(source.Id,new(source,storage,formatCode));
                }
                RetireCore(rd);foreach(var pair in next)_targets.Add(pair.Key,pair.Value);_targetRids.AddRange(owned);
            }
            catch{Free(rd,owned);throw;}
        }
        private static Draw BuildNativeDraw(RenderingDevice rd,Shader shader,MapFinalOutputPassDrawPacket packet,
            ModelDeviceBinding[] images,string samplerKey,IReadOnlyList<int> outputs,Rid framebuffer)
        {
            var owned=new List<Rid>();Rid Own(Rid rid){if(!rid.IsValid)throw new InvalidDataException("Native recipe draw allocation failed.");owned.Add(rid);return rid;}
            try
            {
                var cbs=new Dictionary<(uint,uint),Rid>();foreach(var stage in new[]{(1u,packet.VertexConstants),(0u,packet.PixelConstants)})foreach(var cb in stage.Item2)cbs.Add((stage.Item1,cb.Key),Own(rd.UniformBufferCreate((uint)cb.Value.Length,cb.Value.ToArray())));
                var samplers=packet.SamplerDescriptors.ToDictionary(p=>p.Key,p=>Own(DeviceSampler.Create(rd,p.Value.Span)));
                Rid Set(uint stage)
                {
                    var bindings=shader.Bindings.Where(b=>b.Stage==stage).ToArray();if(bindings.Length==0)return default;
                    var uniforms=new Godot.Collections.Array<RDUniform>();try
                    {
                        foreach(var b in bindings){var u=new RDUniform{Binding=(int)b.DeviceBinding,UniformType=b.Kind switch{"UniformBuffer"=>RenderingDevice.UniformType.UniformBuffer,"Sampler"=>RenderingDevice.UniformType.Sampler,_=>RenderingDevice.UniformType.Texture}};uniforms.Add(u);
                            u.AddId(b.Kind=="UniformBuffer"?cbs[(stage,b.Register)]:b.Kind=="Sampler"?samplers[b.Register]:images.Single(i=>i.SourceBindPoint==b.Register).Resource);}
                        var set=Own(rd.UniformSetCreate(uniforms,shader.Rid,stage));if(!rd.UniformSetIsValid(set))throw new InvalidDataException("Native recipe uniform set failed.");return set;
                    }finally{foreach(var u in uniforms)u.Dispose();}
                }
                Rid vs=Set(1),ps=Set(0),vb=Own(rd.VertexBufferCreate(96,packet.Quad.ToArray()));var attributes=new Godot.Collections.Array<RDVertexAttribute>();long vertexFormat;
                try
                {
                    attributes.Add(new(){Location=0,Offset=0,Stride=24,Format=shader.Stock?RenderingDevice.DataFormat.R32G32B32Sfloat:RenderingDevice.DataFormat.R32G32B32A32Sfloat,Frequency=RenderingDevice.VertexFrequency.Vertex});
                    if(shader.Stock)attributes.Add(new(){Location=1,Offset=12,Stride=24,Format=RenderingDevice.DataFormat.R8G8B8A8Unorm,Frequency=RenderingDevice.VertexFrequency.Vertex});
                    attributes.Add(new(){Location=shader.Stock?2u:1u,Offset=16,Stride=24,Format=RenderingDevice.DataFormat.R32G32Sfloat,Frequency=RenderingDevice.VertexFrequency.Vertex});vertexFormat=rd.VertexFormatCreate(attributes);
                }finally{foreach(var a in attributes)a.Dispose();}
                Rid vertices=Own(rd.VertexArrayCreate(4,vertexFormat,new Godot.Collections.Array<Rid>(Enumerable.Repeat(vb,shader.Stock?3:2))));
                // Original stock vertices are TL,BL,BR,TR and its six source
                // indices preserve signed endpoint reversal in Flare draws.
                Rid indices=default;if(shader.Stock){byte[] indexBytes=[0,0,1,0,2,0,0,0,2,0,3,0];
                    Rid buffer=Own(rd.IndexBufferCreate(6,RenderingDevice.IndexBufferFormat.Uint16,indexBytes));indices=Own(rd.IndexArrayCreate(buffer,0,6));}
                using var raster=new RDPipelineRasterizationState{CullMode=packet.Raster.Cull,FrontFace=packet.Raster.FrontFace};using var multi=new RDPipelineMultisampleState();
                using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};var attachments=new Godot.Collections.Array<RDPipelineColorBlendStateAttachment>();
                try
                {
                    foreach(var a in packet.Raster.Attachments)attachments.Add(new(){EnableBlend=a.EnableBlend,SrcColorBlendFactor=a.SourceColor,DstColorBlendFactor=a.DestinationColor,
                        ColorBlendOp=a.ColorOperation,SrcAlphaBlendFactor=a.SourceAlpha,DstAlphaBlendFactor=a.DestinationAlpha,AlphaBlendOp=a.AlphaOperation,WriteR=a.WriteR,WriteG=a.WriteG,WriteB=a.WriteB,WriteA=a.WriteA});
                    using var blend=new RDPipelineColorBlendState{Attachments=attachments};Rid pipeline=Own(rd.RenderPipelineCreate(shader.Rid,rd.FramebufferGetFormat(framebuffer),vertexFormat,shader.Stock?RenderingDevice.RenderPrimitive.Triangles:RenderingDevice.RenderPrimitive.TriangleStrips,raster,multi,depth,blend));
                    if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("Native recipe pipeline failed.");return new(shader.Source.Key,pipeline,vb,vertices,indices,vs,ps,cbs,Array.AsReadOnly(images),samplerKey,packet.Raster,Array.AsReadOnly(outputs.ToArray()),owned.AsReadOnly());
                }finally{foreach(var a in attachments)a.Dispose();}
            }catch{Free(rd,owned);throw;}
        }
        private MapFinalOutputNativeRecipeFrame Snapshot(MapFinalOutputNativeRecipeFrame frame)
        {
            if(frame.AcceptedFrameNumber<=0||frame.Targets.Count==0||frame.Draws.Count==0||string.IsNullOrWhiteSpace(frame.SourceEvidence)||frame.Targets.Select(t=>t.Id).Distinct().Count()!=frame.Targets.Count||
                frame.Targets.Any(t=>t.Width<=0||t.Height<=0||string.IsNullOrWhiteSpace(t.SourceEvidence)||frame.ExternalTextures.ContainsKey(t.Id)))throw new InvalidDataException("Native recipe requires distinct positive source targets and accepted context.");
            var ids=frame.Targets.Select(t=>t.Id).ToHashSet();var draws=new List<MapFinalOutputNativeDraw>();
            foreach(var target in frame.Targets.Where(t=>t.SourceDeclaration is not null))
            {
                var original=RenderTextureResources.Read(Graph.Source.TextureObjects,MapFinalOutputShaft.LightSceneObjectId);
                if(target.Id!=23||target.SourceDeclaration!=original||original.PixelFormat!=MapFinalOutputShaft.LightScenePixelFormat||
                    original.Width!=0||original.Height!=0||original.LevelNum!=1)
                    throw new InvalidDataException("Native recipe external declaration must be its exact source LightScene23 object.");
            }
            foreach(var draw in frame.Draws)
            {
                if(!_programs.TryGetValue(draw.ProgramKey,out var program)||draw.Outputs.Count!=program.ColorOutputs||draw.Outputs.Any(id=>!ids.Contains(id))||string.IsNullOrWhiteSpace(draw.SourceEvidence)||
                    draw.Packet.Quad.Length!=96||draw.Packet.Raster.DepthTest||draw.Packet.Raster.DepthWrite||draw.Packet.Raster.Attachments.Count!=draw.Outputs.Count)throw new InvalidDataException("Native recipe draw/source layout is incomplete.");
                IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> Constants(uint stage,IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> values)
                {
                    var expected=program.Bindings.Where(b=>b.Stage==stage&&b.Kind=="UniformBuffer").ToArray();if(values.Count!=expected.Length||expected.Any(b=>!values.TryGetValue(b.Register,out var data)||data.Length!=b.Bytes))throw new InvalidDataException("Native recipe requires every complete source CB.");
                    return values.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray());
                }
                var samplerRegisters=program.Bindings.Where(b=>b.Kind=="Sampler").Select(b=>b.Register).ToArray();var textureRegisters=program.Bindings.Where(b=>b.Kind=="Texture").Select(b=>b.Register).ToArray();
                if(draw.Packet.SamplerDescriptors.Count!=samplerRegisters.Length||samplerRegisters.Any(r=>!draw.Packet.SamplerDescriptors.TryGetValue(r,out var s)||s.Length!=52)||
                    draw.Inputs.Count!=textureRegisters.Length||textureRegisters.Any(r=>!draw.Inputs.ContainsKey(r)))throw new InvalidDataException("Native recipe requires every actual source image/sampler.");
                var packet=draw.Packet with{VertexConstants=Constants(1,draw.Packet.VertexConstants),PixelConstants=Constants(0,draw.Packet.PixelConstants),Quad=draw.Packet.Quad.ToArray(),
                    SamplerDescriptors=draw.Packet.SamplerDescriptors.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray()),Raster=draw.Packet.Raster with{Attachments=Array.AsReadOnly(draw.Packet.Raster.Attachments.ToArray())}};
                draws.Add(draw with{Outputs=Array.AsReadOnly(draw.Outputs.ToArray()),Inputs=draw.Inputs.ToDictionary(p=>p.Key,p=>p.Value),Packet=packet});
            }
            return frame with{Targets=Array.AsReadOnly(frame.Targets.ToArray()),ExternalTextures=frame.ExternalTextures.ToDictionary(p=>p.Key,p=>p.Value),Draws=draws.AsReadOnly()};
        }
        private static byte[] MoveStockVertexSet(byte[] source)
        {
            byte[] result=source.ToArray();int count=0;for(int at=20;at<result.Length;){uint first=System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(at,4));int length=(int)(first>>16);
                if(length<1||at+length*4>result.Length)throw new InvalidDataException("Stock recipe SPIR-V is truncated.");
                if((first&65535)==71&&length==4&&System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(at+8,4))==34){if(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(at+12,4))!=0)throw new InvalidDataException("Stock vertex descriptor set changed.");System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(at+12,4),1);count++;}at+=length*4;}
            if(count!=1)throw new InvalidDataException("Stock source recipe requires exactly one vertex CB set.");return result;
        }
        public Task RetireTargetAsync(){Accept();return RetireAcceptedAsync();}
        private async Task RetireAcceptedAsync(){await _gate.WaitAsync();try{await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{RetireCore(rd);return true;});}finally{_gate.Release();Complete();}}
        private void RetireCore(RenderingDevice rd){foreach(var draw in _draws.Values)Free(rd,draw.Owned);_draws.Clear();Free(rd,_targetRids);_targetRids.Clear();_framebuffers.Clear();_targets.Clear();LatestTextures=null;}
        private void Accept(){lock(_lifetime){ObjectDisposedException.ThrowIf(_retiring,this);_accepted++;}}
        private void Complete(){lock(_lifetime){if(--_accepted==0&&_retiring)_drained?.TrySetResult();}}
        public ValueTask DisposeAsync(){lock(_lifetime){_retiring=true;return new(_disposal??=DisposeCoreAsync());}}
        private async Task DisposeCoreAsync(){Task wait;lock(_lifetime){wait=_accepted==0?Task.CompletedTask:(_drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;}await wait;await _gate.WaitAsync();
            try{await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{RetireCore(rd);foreach(var shader in _programs.Values)rd.FreeRid(shader.Rid);return true;});}finally{_gate.Release();}}
    }
}
