using System.Buffers.Binary;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Native node1 producer. The original stock screen VS/default3 PS
/// draw the source32B packet into its retained2x1 FloatRGBA128 texture. Shader
/// arithmetic and source center UVs are retained; final raster Y is adapted
/// for the RenderingDevice. Its fresh stock screen state retains the original
/// constructor's unit ScreenScale/ColorScale and null custom VS. Retire
/// borrowing draws before disposing this owner.</summary>
public sealed class OriginalMapExposureOwner:IAsyncDisposable
{
    public LoadedOriginalMapExposureSource Source { get; }
    public OriginalShaderSpirv VertexTranslation { get; }
    public OriginalShaderSpirv PixelTranslation { get; }
    public OriginalAdaptedVertexShader VertexAdapter { get; }
    public Rid TextureRid { get; }
    public long AcceptedFrameNumber { get; private set; }
    public long DrawCount { get; private set; }
    public OriginalMapExposurePacket? LastPacket { get; private set; }
    private readonly Rid _shader,_framebuffer,_pipeline,_vertexSet,_vertices,_vertexArray,_pixelConstants;
    private readonly Rid[] _inputs,_pixelSets;
    private readonly IReadOnlyList<Rid> _owned;
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetime=new();
    private bool _retiring;private int _accepted;private Task? _disposal;private TaskCompletionSource? _drained;
    private long _contentsRevision;private object? _externalPublication;
    private bool _node1ContentsValid;
    private int _retiredRidCount;
    private OriginalMapExposureOwner(LoadedOriginalMapExposureSource source,OriginalShaderSpirv vertex,
        OriginalShaderSpirv pixel,OriginalAdaptedVertexShader adapter,Rid output,Rid shader,Rid framebuffer,
        Rid pipeline,Rid vertexSet,Rid vertices,Rid vertexArray,Rid pixelConstants,Rid[] inputs,Rid[] sets,List<Rid> owned)
    {Source=source;VertexTranslation=vertex;PixelTranslation=pixel;VertexAdapter=adapter;TextureRid=output;
        _shader=shader;_framebuffer=framebuffer;_pipeline=pipeline;_vertexSet=vertexSet;_vertices=vertices;
        _vertexArray=vertexArray;_pixelConstants=pixelConstants;_inputs=inputs;_pixelSets=sets;_owned=owned.AsReadOnly();}

    public static async Task<OriginalMapExposureOwner> BuildAsync(LoadedOriginalMapExposureSource source,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        var vertex=compiler.Compile(source.VertexProgram.Span);var pixel=compiler.Compile(source.PixelProgram.Span);
        // SM4 has no RD11 RDEF header. Inspect the translated fixed interface
        // directly; only the one source VS cb0 is moved to descriptor set1.
        var adapter=OriginalVertexShaderAdapter.AdaptPositionInput(MoveVertexSet(vertex.Bytes));
        var vi=SpirvShaderInterfaceReader.Read(adapter.Bytes);var pi=SpirvShaderInterfaceReader.Read(pixel.Bytes);
        if(vi.Descriptors.Count!=1||vi.Descriptors[0] is not{StorageClass:2,DescriptorSet:1,RequiredBufferBytes:32}||
            pi.Descriptors.Count!=3||pi.Descriptors.Any(d=>d.DescriptorSet!=0)||
            pi.Descriptors.Count(d=>d.StorageClass==2&&d.RequiredBufferBytes==16)!=1||
            pi.Descriptors.Count(d=>d.Type.Kind=="sampler")!=1||
            pi.Descriptors.Count(d=>d.Type is{Kind:"image",Dimension:1,Arrayed:false,Multisampled:false})!=1)
            throw new InvalidDataException("Exposure stock32B VS/16B PS/t0/s0 interface differs.");
        foreach(var input in pi.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null))
            if(!vi.StageVariables.Any(v=>v.StorageClass==3&&v.Location==input.Location))
                throw new InvalidDataException("Exposure stock VS COLOR/TEXCOORD does not satisfy default3 PS.");
        return await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();Rid Own(Rid r){if(!r.IsValid)throw new InvalidDataException("Exposure device allocation failed.");owned.Add(r);return r;}
            try
            {
                using var stages=new RDShaderSpirV();stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,adapter.Bytes);
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,pixel.Bytes);
                Rid shader=Own(rd.ShaderCreateFromSpirV(stages,"OriginalMapExposureDefault3"));
                Rid Texture(RenderingDevice.TextureUsageBits usage)
                {
                    using var format=new RDTextureFormat{Format=RenderingDevice.DataFormat.R32G32B32A32Sfloat,
                        TextureType=RenderingDevice.TextureType.Type2D,Width=2,Height=1,Depth=1,ArrayLayers=1,Mipmaps=1,UsageBits=usage};
                    using var view=new RDTextureView();return Own(rd.TextureCreate(format,view));
                }
                Rid output=Texture(RenderingDevice.TextureUsageBits.ColorAttachmentBit|RenderingDevice.TextureUsageBits.SamplingBit|
                    RenderingDevice.TextureUsageBits.CanCopyFromBit);
                Rid framebuffer=Own(rd.FramebufferCreate([output]));
                byte[] viewport=new byte[32];float[] constants=[1,-2,-1,1,1,1,1,1];
                for(int i=0;i<8;i++)BinaryPrimitives.WriteSingleLittleEndian(viewport.AsSpan(i*4,4),constants[i]);
                Rid vsCb=Own(rd.UniformBufferCreate(32,viewport)),psCb=Own(rd.UniformBufferCreate(16,new byte[16]));
                Rid sampler=Own(OriginalDeviceSampler.Create(rd,source.InputSampler.EncodeD3D11Descriptor()));
                Rid Set(uint set,IReadOnlyList<SpirvInterfaceVariable> declarations,Rid texture)
                {
                    var uniforms=new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach(var d in declarations)
                        {
                            var type=d.StorageClass==2?RenderingDevice.UniformType.UniformBuffer:
                                d.Type.Kind=="sampler"?RenderingDevice.UniformType.Sampler:RenderingDevice.UniformType.Texture;
                            Rid resource=d.StorageClass==2?(set==1?vsCb:psCb):d.Type.Kind=="sampler"?sampler:texture;
                            var u=new RDUniform{Binding=(int)d.Binding!.Value,UniformType=type};u.AddId(resource);uniforms.Add(u);
                        }
                        return Own(rd.UniformSetCreate(uniforms,shader,set));
                    }
                    finally{foreach(var u in uniforms)u.Dispose();}
                }
                Rid vertexSet=Set(1,vi.Descriptors,default);Rid[] inputs=new Rid[3],sets=new Rid[3];
                for(int i=0;i<3;i++){inputs[i]=Texture(RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.CanUpdateBit);sets[i]=Set(0,pi.Descriptors,inputs[i]);}
                Rid vertices=Own(rd.VertexBufferCreate(96,Quad(false)));
                var attributes=new Godot.Collections.Array<RDVertexAttribute>();
                try
                {
                    foreach(var input in vi.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null).OrderBy(v=>v.Location))
                    {
                        uint location=input.Location!.Value;
                        attributes.Add(new(){Location=location,Offset=location switch{0=>0u,1=>12u,2=>16u,_=>throw new InvalidDataException("Exposure unknown source vertex location.")},Stride=24,
                            Format=location switch{0=>RenderingDevice.DataFormat.R32G32B32Sfloat,1=>RenderingDevice.DataFormat.R8G8B8A8Unorm,_=>RenderingDevice.DataFormat.R32G32Sfloat},Frequency=RenderingDevice.VertexFrequency.Vertex});
                    }
                    long format=rd.VertexFormatCreate(attributes);Rid array=Own(rd.VertexArrayCreate(4,format,[vertices,vertices,vertices]));
                    using var raster=new RDPipelineRasterizationState{CullMode=RenderingDevice.PolygonCullMode.Disabled};
                    using var multi=new RDPipelineMultisampleState();using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
                    using var attachment=new RDPipelineColorBlendStateAttachment{EnableBlend=false};
                    using var blend=new RDPipelineColorBlendState{Attachments=[attachment]};
                    Rid pipeline=Own(rd.RenderPipelineCreate(shader,rd.FramebufferGetFormat(framebuffer),format,
                        RenderingDevice.RenderPrimitive.TriangleStrips,raster,multi,depth,blend));
                    if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("Exposure source rectangle pipeline failed.");
                    return new OriginalMapExposureOwner(source,vertex,pixel,adapter,output,shader,framebuffer,pipeline,vertexSet,vertices,array,psCb,inputs,sets,owned);
                }
                finally{foreach(var a in attributes)a.Dispose();}
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }

    /// <summary>Strict scene adapter for the accepted terrain/model Camera.
    /// The returned descriptor can be borrowed by territory and final output.</summary>
    public Task<OriginalModelDeviceBinding> PrepareAndBorrowAsync(OriginalNativeMapFrameContext frame,OriginalMapExposurePacket packet)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(packet);
        if(frame.FrameNumber!=packet.FrameNumber||frame.PreparedCamera!=packet.Camera||
            frame.TerrainFrame.Source.Graph.Source.ResourceUri!=Source.Graph.Source.ResourceUri||
            frame.TerrainFrame.Source.Graph.Source.DecodedSha256!=Source.Graph.Source.DecodedSha256)
            throw new InvalidDataException("Exposure packet differs from the accepted scene frame/Camera/source graph.");
        Accept();return DrawAcceptedAsync(packet,true);
    }

    /// <summary>Producer entry point for an explicitly prepared source frame.
    /// The owner serializes its upload/draw and retains the texture between frames.</summary>
    public Task<OriginalModelDeviceBinding> DrawPreparedAndBorrowAsync(OriginalMapExposurePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);Accept();return DrawAcceptedAsync(packet,false);
    }
    /// <summary>Recheck the actual completed node1 draw and owner lifetime.
    /// This is not a texture allocation or a CPU packet-only readiness proof.
    /// Consumers retain this owner until their draw has returned.</summary>
    public void RequireCompletedCurrentFrame(OriginalNativeMapFrameContext context,
        OriginalMapExposurePacket acceptedPacket,long acceptedDrawCount)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(acceptedPacket);
        lock(_lifetime)
        {
            ObjectDisposedException.ThrowIf(_retiring,this);
            if(!ReferenceEquals(LastPacket,acceptedPacket)||AcceptedFrameNumber!=context.FrameNumber||
                acceptedPacket.FrameNumber!=context.FrameNumber||acceptedPacket.Camera!=context.PreparedCamera||
                acceptedDrawCount<=0||DrawCount!=acceptedDrawCount||!TextureRid.IsValid||!_node1ContentsValid||_externalPublication is not null||
                context.TerrainFrame.Source.Graph.Source.ResourceUri!=Source.Graph.Source.ResourceUri||
                context.TerrainFrame.Source.Graph.Source.DecodedSha256!=Source.Graph.Source.DecodedSha256)
                throw new InvalidDataException("Exposure submission was replaced or is foreign to this exact current World/Camera/graph.");
        }
    }
    private void Accept(){lock(_lifetime){if(_retiring)throw new ObjectDisposedException(nameof(OriginalMapExposureOwner));_accepted++;}}
    /// <summary>Only the real post-Apply producer borrows this destination.
    /// Node1 uploads and retirement wait for the complete accepted draw. The
    /// callback must submit its own source draw; this method never copies or
    /// synthesizes pixels.</summary>
    internal async Task<long> PublishExternalDrawAsync(OriginalNativeMapFrameContext context,
        object actualPublicationIdentity,Func<RenderingDevice,bool> actualDraw)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(actualPublicationIdentity);
        ArgumentNullException.ThrowIfNull(actualDraw);Accept();await _gate.WaitAsync();
        try
        {
            var packet=LastPacket??throw new InvalidDataException("Exposure publication needs the actual current node1 producer.");
            RequireCompletedCurrentFrame(context,packet,DrawCount);
            // Withdraw prior pixel readiness before a target command can
            // partially execute. A failed new producer cannot leave the old
            // publication identity apparently valid.
            long writingRevision;
            lock(_lifetime){_node1ContentsValid=false;_externalPublication=null;writingRevision=++_contentsRevision;}
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                if(!rd.TextureIsValid(TextureRid))throw new InvalidDataException("Global exposure target retired before post-Apply draw.");
                if(!actualDraw(rd))throw new InvalidDataException("Post-Apply exposure draw did not return.");
                return true;
            });
            lock(_lifetime){_externalPublication=actualPublicationIdentity;return writingRevision;}
        }
        finally{_gate.Release();lock(_lifetime){if(--_accepted==0&&_retiring)_drained?.TrySetResult();}}
    }
    internal void RequireExternalPublication(OriginalNativeMapFrameContext context,object actualIdentity,long revision)
    {
        lock(_lifetime)
        {
            ObjectDisposedException.ThrowIf(_retiring,this);
            if(!ReferenceEquals(_externalPublication,actualIdentity)||revision<=0||_contentsRevision!=revision||
                AcceptedFrameNumber!=context.FrameNumber||!TextureRid.IsValid)
                throw new InvalidDataException("Global exposure publication was replaced, rewritten or retired.");
        }
    }
    private OriginalModelDeviceBinding Binding()=>new("Texture",0,TextureRid,TextureRid,Source.SourceResourceUri);
    private async Task<OriginalModelDeviceBinding> DrawAcceptedAsync(OriginalMapExposurePacket packet,bool allowSameFrame)
    {
        await _gate.WaitAsync();
        try
        {
            if(allowSameFrame&&packet.FrameNumber==AcceptedFrameNumber&&LastPacket is not null)
            {
                if(!_node1ContentsValid||_externalPublication is not null)throw new InvalidDataException("Node1 packet pixels are unknown or replaced by the actual post-Apply exposure publication.");
                if(packet.Camera!=LastPacket.Camera||packet.HalfRectangle!=LastPacket.HalfRectangle||
                    packet.SourceProvenance!=LastPacket.SourceProvenance||!packet.Bytes.Span.SequenceEqual(LastPacket.Bytes.Span))
                    throw new InvalidDataException("Exposure consumers submitted different source packets for the same accepted frame.");
                return Binding();
            }
            if(packet.FrameNumber<=AcceptedFrameNumber)throw new InvalidDataException("Exposure requires a newly accepted increasing frame number.");
            if(packet.HalfRectangle&&DrawCount==0)throw new InvalidDataException("The source half rectangle needs retained first-texel output from a prior full draw.");
            // E6DE3A clears native+4B8; EAA910 increments before wrapping3.
            int ring=(int)((DrawCount+1)%3);
            lock(_lifetime){_node1ContentsValid=false;_externalPublication=null;_contentsRevision++;}
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                byte[] pixelConstants=new byte[16];BinaryPrimitives.WriteSingleLittleEndian(pixelConstants,packet.AlphaTestThreshold);
                if(rd.TextureUpdate(_inputs[ring],0,packet.Bytes.ToArray())!=Error.Ok||
                    rd.BufferUpdate(_vertices,0,96,Quad(packet.HalfRectangle))!=Error.Ok||
                    rd.BufferUpdate(_pixelConstants,0,16,pixelConstants)!=Error.Ok)
                    throw new InvalidDataException("Exposure source packet/rectangle update failed.");
                long list=rd.DrawListBegin(_framebuffer);
                try{rd.DrawListBindRenderPipeline(list,_pipeline);rd.DrawListBindUniformSet(list,_pixelSets[ring],0);
                    rd.DrawListBindUniformSet(list,_vertexSet,1);rd.DrawListBindVertexArray(list,_vertexArray);rd.DrawListDraw(list,false,1,4);}
                finally{rd.DrawListEnd();}
                return true;
            });
            lock(_lifetime){AcceptedFrameNumber=packet.FrameNumber;LastPacket=packet;DrawCount++;_node1ContentsValid=true;}
            return Binding();
        }
        finally{_gate.Release();lock(_lifetime){_accepted--;if(_retiring&&_accepted==0)_drained?.TrySetResult();}}
    }

    private static byte[] Quad(bool half)
    {
        float left=half?1:0,u=half?.5f:0;float[][] rows=[[left,0,0,u,0],[2,0,0,1,0],[left,1,0,u,1],[2,1,0,1,1]];
        byte[] data=new byte[96];
        for(int i=0;i<4;i++){for(int c=0;c<3;c++)BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i*24+c*4,4),rows[i][c]);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(i*24+12,4),uint.MaxValue);
            for(int c=0;c<2;c++)BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i*24+16+c*4,4),rows[i][c+3]);}
        return data;
    }
    private static byte[] MoveVertexSet(byte[] source)
    {
        byte[] result=source.ToArray();int count=0;
        for(int at=20;at<result.Length;){uint first=BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(at,4));int words=(int)(first>>16);
            if(words<1||at+words*4>result.Length)throw new InvalidDataException("Exposure SPIR-V instruction differs.");
            if((first&65535)==71&&words==4&&BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(at+8,4))==34)
            {if(BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(at+12,4))!=0)throw new InvalidDataException("Exposure vertex set is not source0.");
                BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(at+12,4),1);count++;}at+=words*4;}
        if(count!=1)throw new InvalidDataException("Exposure stock VS requires exactly one descriptor-set adaptation.");return result;
    }
    public ValueTask DisposeAsync(){lock(_lifetime){_retiring=true;
        if(_disposal is null||_disposal.IsFaulted||_disposal.IsCanceled)_disposal=DisposeCoreAsync();return new(_disposal);}}
    private async Task DisposeCoreAsync()
    {
        Task pending;lock(_lifetime)pending=_accepted==0?Task.CompletedTask:(_drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        await pending;await _gate.WaitAsync();
        try{await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{
            while(_retiredRidCount<_owned.Count){Rid rid=_owned[_owned.Count-1-_retiredRidCount];
                rd.FreeRid(rid);_retiredRidCount++;}return true;});}
        finally{_gate.Release();}
    }
}
