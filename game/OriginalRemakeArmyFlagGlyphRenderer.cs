using System.Buffers.Binary;
using Godot;
using San14.Data;
using San14.Data.Formats;

public sealed record OriginalRemakeArmyFlagGlyphContext(ReadOnlyMemory<byte> CompleteVertexConstants32,
    ReadOnlyMemory<byte> CompletePixelConstants16,string CallerSource);

/// <summary>Original stock VS/mode3 PS consumer for the proven flag main-text
/// opcode4B route. RGB uses source SRC_ALPHA/INV_SRC_ALPHA/ADD; alpha preserves
/// destination. Source glyph atlas, sampler, layout command and stock context
/// remain explicit. Dispose this owner before the borrowed atlas or pane image.</summary>
public sealed class OriginalRemakeArmyFlagGlyphRenderer:IAsyncDisposable
{
    private readonly OriginalRemakeArmyFlagPaneImage _pane;
    private readonly Rid _shader,_pipeline,_vertices,_vertexArray,_indices,_indexArray,_vertexCb,_pixelCb,_vertexSet,_pixelSet;
    private readonly IReadOnlyList<Rid> _owned;
    private string? _sourceAtlasHash;
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetime=new();
    private int _accepted;private bool _retiring;private TaskCompletionSource? _drained;private Task? _disposal;
    public LoadedOriginalMapExposureSource StockSource { get; }
    public long DrawCount { get; private set; }

    private OriginalRemakeArmyFlagGlyphRenderer(LoadedOriginalMapExposureSource source,OriginalRemakeArmyFlagPaneImage pane,
        Rid shader,Rid pipeline,Rid vertices,Rid vertexArray,Rid indices,Rid indexArray,Rid vertexCb,Rid pixelCb,Rid vertexSet,Rid pixelSet,IReadOnlyList<Rid> owned)
        =>(StockSource,_pane,_shader,_pipeline,_vertices,_vertexArray,_indices,_indexArray,_vertexCb,_pixelCb,_vertexSet,_pixelSet,_owned)=
            (source,pane,shader,pipeline,vertices,vertexArray,indices,indexArray,vertexCb,pixelCb,vertexSet,pixelSet,owned);

    public static async Task<OriginalRemakeArmyFlagGlyphRenderer> BuildAsync(LoadedOriginalMapExposureSource source,
        Vkd3dShaderCompiler compiler,OriginalRemakeArmyFlagPaneImage pane,OriginalModelDeviceBinding atlas,
        ReadOnlyMemory<byte> explicitSourceAtlasSampler52)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);ArgumentNullException.ThrowIfNull(pane);
        if(atlas.DescriptorKind!="Texture"||!atlas.Resource.IsValid||atlas.OwnerTexture is not{IsValid:true}||
            string.IsNullOrWhiteSpace(atlas.SourceResourceUri)||explicitSourceAtlasSampler52.Length!=52)
            throw Error("requires the actual owned font atlas and complete source sampler");
        var vertex=compiler.Compile(source.VertexProgram.Span);var pixel=compiler.Compile(source.PixelProgram.Span);
        if(vertex.SourceProgramSha256!=OriginalMapExposureSource.VertexSha256||pixel.SourceProgramSha256!=OriginalMapExposureSource.PixelSha256)
            throw Error("stock source programs differ from the pinned glyph default route");
        var adapter=OriginalVertexShaderAdapter.AdaptPositionInput(MoveVertexSet(vertex.Bytes));
        var vi=SpirvShaderInterfaceReader.Read(adapter.Bytes);var pi=SpirvShaderInterfaceReader.Read(pixel.Bytes);
        if(vi.Descriptors.Count!=1||vi.Descriptors[0] is not{StorageClass:2,DescriptorSet:1,RequiredBufferBytes:32}||
            pi.Descriptors.Count!=3||pi.Descriptors.Any(d=>d.DescriptorSet!=0)||
            pi.Descriptors.Count(d=>d.StorageClass==2&&d.RequiredBufferBytes==16)!=1||pi.Descriptors.Count(d=>d.Type.Kind=="sampler")!=1||
            pi.Descriptors.Count(d=>d.Type is{Kind:"image",Dimension:1,Arrayed:false,Multisampled:false})!=1)
            throw Error("source stock glyph interface differs from32B/16B/t0/s0");
        byte[] samplerBytes=explicitSourceAtlasSampler52.ToArray();
        return await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();Rid Own(Rid rid){if(!rid.IsValid)throw Error("device allocation failed");owned.Add(rid);return rid;}
            try
            {
                using var stages=new RDShaderSpirV();stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,pixel.Bytes);
                Rid shader=Own(rd.ShaderCreateFromSpirV(stages,"OriginalArmyFlagStockGlyph"));
                Rid vsCb=Own(rd.UniformBufferCreate(32,new byte[32])),psCb=Own(rd.UniformBufferCreate(16,new byte[16]));
                Rid sampler=Own(OriginalDeviceSampler.Create(rd,samplerBytes));
                Rid Set(uint set,IReadOnlyList<SpirvInterfaceVariable> declarations)
                {
                    var uniforms=new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach(var declaration in declarations)
                        {
                            var uniform=new RDUniform{Binding=(int)declaration.Binding!.Value,
                                UniformType=declaration.StorageClass==2?RenderingDevice.UniformType.UniformBuffer:
                                declaration.Type.Kind=="sampler"?RenderingDevice.UniformType.Sampler:RenderingDevice.UniformType.Texture};
                            uniform.AddId(declaration.StorageClass==2?(set==1?vsCb:psCb):declaration.Type.Kind=="sampler"?sampler:atlas.Resource);
                            uniforms.Add(uniform);
                        }
                        Rid value=Own(rd.UniformSetCreate(uniforms,shader,set));
                        if(!rd.UniformSetIsValid(value))throw Error("stock source uniform set rejected");return value;
                    }
                    finally{foreach(var uniform in uniforms)uniform.Dispose();}
                }
                Rid vsSet=Set(1,vi.Descriptors),psSet=Set(0,pi.Descriptors);
                Rid vertices=Own(rd.VertexBufferCreate(96,new byte[96]));
                Rid indices=Own(rd.IndexBufferCreate(6,RenderingDevice.IndexBufferFormat.Uint16,OriginalArmyFlagGlyphQuad.CopyNativeIndices()));
                Rid indexArray=Own(rd.IndexArrayCreate(indices,0,6));
                var attributes=new Godot.Collections.Array<RDVertexAttribute>();long format;
                try
                {
                    foreach(var input in vi.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null).OrderBy(v=>v.Location))
                    {
                        uint location=input.Location!.Value;
                        attributes.Add(new(){Location=location,Offset=location switch{0=>0u,1=>12u,2=>16u,_=>throw Error("stock vertex location differs")},Stride=24,
                            Format=location switch{0=>RenderingDevice.DataFormat.R32G32B32Sfloat,1=>RenderingDevice.DataFormat.R8G8B8A8Unorm,_=>RenderingDevice.DataFormat.R32G32Sfloat},
                            Frequency=RenderingDevice.VertexFrequency.Vertex});
                    }
                    format=rd.VertexFormatCreate(attributes);
                }
                finally{foreach(var attribute in attributes)attribute.Dispose();}
                Rid vertexArray=Own(rd.VertexArrayCreate(4,format,[vertices,vertices,vertices]));
                using var raster=new RDPipelineRasterizationState{CullMode=RenderingDevice.PolygonCullMode.Disabled};
                using var multisample=new RDPipelineMultisampleState();
                using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
                using var attachment=new RDPipelineColorBlendStateAttachment{EnableBlend=true,
                    SrcColorBlendFactor=RenderingDevice.BlendFactor.SrcAlpha,DstColorBlendFactor=RenderingDevice.BlendFactor.OneMinusSrcAlpha,
                    ColorBlendOp=RenderingDevice.BlendOperation.Add,SrcAlphaBlendFactor=RenderingDevice.BlendFactor.Zero,
                    DstAlphaBlendFactor=RenderingDevice.BlendFactor.One,AlphaBlendOp=RenderingDevice.BlendOperation.Add,
                    WriteR=true,WriteG=true,WriteB=true,WriteA=true};
                using var blend=new RDPipelineColorBlendState{Attachments=[attachment]};
                Rid pipeline=Own(rd.RenderPipelineCreate(shader,rd.FramebufferGetFormat(pane.Framebuffer),format,
                    RenderingDevice.RenderPrimitive.Triangles,raster,multisample,depth,blend));
                if(!rd.RenderPipelineIsValid(pipeline))throw Error("native glyph source pipeline rejected");
                return new OriginalRemakeArmyFlagGlyphRenderer(source,pane,shader,pipeline,vertices,vertexArray,indices,indexArray,vsCb,psCb,vsSet,psSet,owned.AsReadOnly());
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }

    public static async Task<OriginalRemakeArmyFlagGlyphRenderer> BuildAsync(LoadedOriginalMapExposureSource source,
        Vkd3dShaderCompiler compiler,OriginalRemakeArmyFlagPaneImage pane,OriginalRemakeArmyFlagGlyphAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        var renderer=await BuildAsync(source,compiler,pane,atlas.Borrow(),atlas.NativeSamplerDescriptor52);
        renderer._sourceAtlasHash=atlas.Source.WrittenAtlasSha256;return renderer;
    }

    public async Task DrawFreshSourceLayoutAsync(LoadedOriginalArmyFlagFreshPaneLayout layout,OriginalRemakeArmyFlagGlyphContext context)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if(_sourceAtlasHash is null||_sourceAtlasHash!=layout.Atlas.WrittenAtlasSha256)
            throw Error("fresh layout requires its actual source font atlas owner");
        await DrawAsync(layout.Quad,context);
        _pane.PublishCompletedSourceGlyph(layout);
    }

    public Task DrawAsync(OriginalArmyFlagGlyphQuad command,OriginalRemakeArmyFlagGlyphContext context)
    {
        ArgumentNullException.ThrowIfNull(command);ArgumentNullException.ThrowIfNull(context);
        if(context.CompleteVertexConstants32.Length!=32||context.CompletePixelConstants16.Length!=16||string.IsNullOrWhiteSpace(context.CallerSource))
            throw Error("requires complete caller stock context and provenance");
        byte[] vertices=command.CopyNativeVertexBytes(),vs=context.CompleteVertexConstants32.ToArray(),ps=context.CompletePixelConstants16.ToArray();
        foreach(int offset in Enumerable.Range(0,8).Select(i=>i*4))
            if(!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(vs.AsSpan(offset,4))))throw Error("stock vertex context must be finite");
        if(!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(ps)))throw Error("stock alpha context must be finite");
        lock(_lifetime){if(_retiring)throw new ObjectDisposedException(nameof(OriginalRemakeArmyFlagGlyphRenderer));_accepted++;}
        return DrawAcceptedAsync(vertices,vs,ps);
    }
    private async Task DrawAcceptedAsync(byte[] vertices,byte[] vs,byte[] ps)
    {
        await _gate.WaitAsync();
        try
        {
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                if(rd.BufferUpdate(_vertices,0,96,vertices)!=Godot.Error.Ok||rd.BufferUpdate(_vertexCb,0,32,vs)!=Godot.Error.Ok||rd.BufferUpdate(_pixelCb,0,16,ps)!=Godot.Error.Ok)
                    throw Error("native quad/context upload failed");
                long list=rd.DrawListBegin(_pane.Framebuffer);
                try{rd.DrawListBindRenderPipeline(list,_pipeline);rd.DrawListBindUniformSet(list,_vertexSet,1);rd.DrawListBindUniformSet(list,_pixelSet,0);
                    rd.DrawListBindVertexArray(list,_vertexArray);rd.DrawListBindIndexArray(list,_indexArray);rd.DrawListDraw(list,true,1);}
                finally{rd.DrawListEnd();}return true;
            });
            DrawCount++;
        }
        finally{_gate.Release();lock(_lifetime){_accepted--;if(_retiring&&_accepted==0)_drained?.TrySetResult();}}
    }
    private static byte[] MoveVertexSet(byte[] bytes)
    {
        byte[] result=bytes.ToArray();int count=0;
        for(int at=20;at<result.Length;)
        {
            uint first=BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(at,4));int words=(int)(first>>16);
            if(words<1||at+words*4>result.Length)throw Error("SPIRV instruction differs");
            if((first&65535)==71&&words==4&&BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(at+8,4))==34)
            {
                if(BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(at+12,4))!=0)throw Error("source vertex descriptor set differs");
                BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(at+12,4),1);count++;
            }
            at+=words*4;
        }
        if(count!=1)throw Error("stock VS requires its single descriptor set adaptation");return result;
    }
    public ValueTask DisposeAsync(){lock(_lifetime){_retiring=true;return new(_disposal??=DisposeCoreAsync());}}
    private async Task DisposeCoreAsync()
    {
        Task pending;lock(_lifetime)pending=_accepted==0?Task.CompletedTask:(_drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        await pending;await _gate.WaitAsync();
        try{await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{for(int i=_owned.Count-1;i>=0;i--)rd.FreeRid(_owned[i]);return true;});}
        finally{_gate.Release();}
    }
    private static InvalidDataException Error(string message)=>new("Original Army flag stock glyph "+message+".");
}
