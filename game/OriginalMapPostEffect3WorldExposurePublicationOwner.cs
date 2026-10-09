using System.Buffers.Binary;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The returned source stock49 draw into global node1 exposure.
/// This capability is minted only after its real draw returns. It retains the
/// current104 contents and destination revision; a later producer invalidates
/// it. Host stock-history commit does not invalidate the actual pixel receipt.</summary>
public sealed class OriginalMapPostEffect3WorldExposurePublicationCompleted
{
    private readonly OriginalMapPostEffect3WorldExposurePublicationOwner owner;
    private readonly OriginalMapExposureOwner target;
    private readonly OriginalNativeMapFrameContext context;
    private readonly OriginalMapPostEffect3WorldControlledPortReturnReceipt port;
    private readonly IOriginalMapPostEffect3ComputeMipTextureLease input;
    private readonly object publicationIdentity;
    private readonly long revision;
    internal OriginalMapPostEffect3WorldExposurePublicationCompleted(
        OriginalMapPostEffect3WorldExposurePublicationOwner owner,OriginalMapExposureOwner target,
        OriginalNativeMapFrameContext context,OriginalMapPostEffect3ControlledHostTailRecipe tail,
        OriginalMapPostEffect3WorldControlledPortReturnReceipt port,
        IOriginalMapPostEffect3ComputeMipTextureLease input,object actualIdentity,long revision,string evidence)
        =>(this.owner,this.target,this.context,Tail,this.port,this.input,publicationIdentity,this.revision,SourceEvidence)=
            (owner,target,context,tail,port,input,actualIdentity,revision,evidence);
    public OriginalMapPostEffect3ControlledHostTailRecipe Tail{get;}
    public long FrameNumber=>context.FrameNumber;
    public string SourceEvidence{get;}
    public Rid SourceStorageRid=>input.StorageRid;
    public Rid TargetStorageRid=>target.TextureRid;
    public long TargetContentsRevision=>revision;
    public bool PreservesRightTexel=>true;
    /// <summary>Immutable acknowledgement of the draw that constructed this
    /// object. Current pixel readiness is checked separately by RequireCurrent.</summary>
    public bool ProvesActualDrawReturned=>true;
    public void RequireCurrent()
    {
        owner.RequireCompleted(this);Tail.RequireCurrent();port.RequireHistoricalAuthority();
        input.RequireReadableMipRange(0,1);target.RequireExternalPublication(context,publicationIdentity,revision);
        if(port.FrameNumber!=FrameNumber||Tail.FrameNumber!=FrameNumber||input.FrameNumber!=FrameNumber)
            throw new InvalidDataException("Exposure publication belongs to a different returned World frame.");
    }
}

/// <summary>Current exposure publication. A real
/// default3 stock draw updates the source-selected 1x1 rectangle of the owned
/// 2x1 RGBA32F destination. Its other texel retains the current node1 pixels.
/// No direct TextureCopy, CPU pixels, frame-ordinal algorithm or seed is used.</summary>
public sealed class OriginalMapPostEffect3WorldExposurePublicationOwner:IAsyncDisposable
{
    public LoadedOriginalMapPostEffect3ExposurePublicationSource Source{get;}
    private readonly OriginalMapExposureOwner target;
    private readonly Rid shader,framebuffer,pipeline,vertexSet,vertexConstants,pixelConstants,vertexBuffer,vertexArray,sampler;
    private readonly IReadOnlyList<SpirvInterfaceVariable> pixelInterface;
    private readonly List<Rid> owned;
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly object lifetime=new();
    private bool retiring,failed;
    private int retiredRids;
    private Task? disposal;
    private Rid pixelSet,pixelView;
    private OriginalMapPostEffect3WorldExposurePublicationCompleted? completed;
    private OriginalMapPostEffect3WorldExposurePublicationOwner(LoadedOriginalMapPostEffect3ExposurePublicationSource source,
        OriginalMapExposureOwner target,Rid shader,Rid framebuffer,Rid pipeline,Rid vertexSet,Rid vertexConstants,
        Rid pixelConstants,Rid vertexBuffer,Rid vertexArray,Rid sampler,List<Rid> owned,
        IReadOnlyList<SpirvInterfaceVariable> pixelInterface)
        =>(Source,this.target,this.shader,this.framebuffer,this.pipeline,this.vertexSet,this.vertexConstants,
            this.pixelConstants,this.vertexBuffer,this.vertexArray,this.sampler,this.owned,this.pixelInterface)=
          (source,target,shader,framebuffer,pipeline,vertexSet,vertexConstants,pixelConstants,vertexBuffer,vertexArray,sampler,owned,pixelInterface);

    public static async Task<OriginalMapPostEffect3WorldExposurePublicationOwner> BuildAsync(
        OriginalGameRepository repository,OriginalMapExposureOwner actualGlobalExposure,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(actualGlobalExposure);
        var source=OriginalMapPostEffect3ExposurePublicationSource.Load(repository,locale);
        // The shared legacy node1 producer reads its stock pair from SC;
        // each post-Apply caller uses its actual locale. Their original VS/PS
        // bytecode hashes are identical. Keep each installed PE provenance,
        // rather than compare two intentionally different executable hashes.
        var targetSource=actualGlobalExposure.Source;
        var vs=actualGlobalExposure.VertexAdapter;var ps=actualGlobalExposure.PixelTranslation;
        if(targetSource.Texture.Object.ObjectId!=0x6C5630BB)
            throw new InvalidDataException("Exposure publication needs its actual node1 target and stock-default3 source owner.");
        var vi=SpirvShaderInterfaceReader.Read(vs.Bytes);var pi=SpirvShaderInterfaceReader.Read(ps.Bytes);
        if(vi.Descriptors.Count!=1||vi.Descriptors[0] is not{StorageClass:2,DescriptorSet:1,RequiredBufferBytes:32}||
            pi.Descriptors.Count!=3||pi.Descriptors.Any(d=>d.DescriptorSet!=0)||
            pi.Descriptors.Count(d=>d.StorageClass==2&&d.RequiredBufferBytes==16)!=1||
            pi.Descriptors.Count(d=>d.Type.Kind=="sampler")!=1||
            pi.Descriptors.Count(d=>d.Type is{Kind:"image",Dimension:1,Arrayed:false,Multisampled:false})!=1)
            throw new InvalidDataException("Exposure publication stock shader interface differs from the exact32B/16B/t0/s0 source.");
        return await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();
            Rid Own(Rid rid){if(!rid.IsValid)throw new InvalidDataException("Exposure publication GPU allocation failed.");owned.Add(rid);return rid;}
            try
            {
                RequireTarget(rd,actualGlobalExposure);
                using var stages=new RDShaderSpirV();stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex,vs.Bytes);
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment,ps.Bytes);
                Rid shader=Own(rd.ShaderCreateFromSpirV(stages,"OriginalPostApplyExposureStock3"));
                Rid framebuffer=Own(rd.FramebufferCreate([actualGlobalExposure.TextureRid]));
                Rid vc=Own(rd.UniformBufferCreate(32,new byte[32]));
                byte[] alpha=new byte[16];BinaryPrimitives.WriteSingleLittleEndian(alpha,-float.MaxValue);
                Rid pc=Own(rd.UniformBufferCreate(16,alpha));
                Rid sampler=Own(OriginalDeviceSampler.Create(rd,source.InputSampler));
                var uniforms=new Godot.Collections.Array<RDUniform>();Rid vertexSet;
                try
                {
                    var u=new RDUniform{UniformType=RenderingDevice.UniformType.UniformBuffer,Binding=(int)vi.Descriptors[0].Binding!.Value};
                    u.AddId(vc);uniforms.Add(u);vertexSet=Own(rd.UniformSetCreate(uniforms,shader,1));
                }
                finally{foreach(var u in uniforms)u.Dispose();}
                Rid vb=Own(rd.VertexBufferCreate(96,source.BuildQuad()));
                var attributes=new Godot.Collections.Array<RDVertexAttribute>();long format;
                try
                {
                    foreach(var input in vi.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null).OrderBy(v=>v.Location))
                    {
                        uint location=input.Location!.Value;
                        attributes.Add(new(){Location=location,Offset=location switch{0=>0u,1=>12u,2=>16u,_=>throw new InvalidDataException("Exposure stock vertex layout differs.")},Stride=24,
                            Format=location switch{0=>RenderingDevice.DataFormat.R32G32B32Sfloat,1=>RenderingDevice.DataFormat.R8G8B8A8Unorm,_=>RenderingDevice.DataFormat.R32G32Sfloat},Frequency=RenderingDevice.VertexFrequency.Vertex});
                    }
                    format=rd.VertexFormatCreate(attributes);
                }
                finally{foreach(var a in attributes)a.Dispose();}
                Rid array=Own(rd.VertexArrayCreate(4,format,[vb,vb,vb]));
                using var raster=new RDPipelineRasterizationState{CullMode=RenderingDevice.PolygonCullMode.Disabled};
                using var multi=new RDPipelineMultisampleState();using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
                using var attachment=new RDPipelineColorBlendStateAttachment{EnableBlend=false,WriteR=true,WriteG=true,WriteB=true,WriteA=true};
                using var blend=new RDPipelineColorBlendState{Attachments=[attachment]};
                Rid pipeline=Own(rd.RenderPipelineCreate(shader,rd.FramebufferGetFormat(framebuffer),format,
                    RenderingDevice.RenderPrimitive.TriangleStrips,raster,multi,depth,blend));
                if(!rd.RenderPipelineIsValid(pipeline)||!rd.UniformSetIsValid(vertexSet))
                    throw new InvalidDataException("Exposure publication source pipeline/set was rejected.");
                return new OriginalMapPostEffect3WorldExposurePublicationOwner(source,actualGlobalExposure,shader,framebuffer,
                    pipeline,vertexSet,vc,pc,vb,array,sampler,owned,pi.Descriptors);
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }

    public async Task<OriginalMapPostEffect3WorldExposurePublicationCompleted?> PublishAsync(
        OriginalMapPostEffect3WorldControlledFactoryFrame frame,OriginalMapPostEffect3ControlledAuxFields actualAux,
        OriginalMapPostEffect3ControlledHostTailRecipe actualTail,OriginalMapPostEffect3WorldControlledPortReturnReceipt actualPortReturn)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(actualAux);
        ArgumentNullException.ThrowIfNull(actualTail);ArgumentNullException.ThrowIfNull(actualPortReturn);
        await serial.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);if(failed)throw new InvalidOperationException("Exposure publication contains a failed draw.");
            frame.RequirePortReturned(frame.ActualDirectoryReturn);actualTail.RequireCurrent();
            actualAux.ValidateFrame(frame.Camera.FrameNumber);
            if(!ReferenceEquals(frame.PortReturn,actualPortReturn)||actualTail.FrameNumber!=frame.Camera.FrameNumber)
                throw new InvalidDataException("Exposure publication needs the exact current module return and caller tail.");
            if(!actualTail.RequiresExposureCopy)return null;
            var pointer=actualTail.ActualModuleExposurePointer??throw new InvalidDataException("Exposure copy has no actual current61 lookup.");
            frame.Textures.ValidateCurrent(pointer);
            if(pointer.Reference.PhysicalAllocationParameterIndex!=61||pointer.Reference.InstanceIndex!=0||pointer.Reference.HasSourceSelectedMipView||
                !ReferenceEquals(actualTail.ActualGlobalExposureWrapperIdentity,target)||
                !ReferenceEquals(actualTail.ActualModuleWrapperIdentity,frame.World.Bank.RequireTexture(61,0)))
                throw new InvalidDataException("Exposure caller copy wrapper/owner differs from the actual global node1 and module61.");
            Source.RequireCurrent(actualAux,frame.World.CurrentStock);
            Source.RequireAllocation(frame.Camera.Allocation);
            var input=frame.World.Bank.CaptureSourceWrittenTextureLease(61,0,frame.Camera.FrameNumber,0,1);
            RequireInput(input);
            byte[] constants=OriginalDeferredAuxiliaryDepthCopySource.BuildStockVertexConstants(2,1,
                frame.World.CurrentStock.CurrentScreenScale29E0.X,frame.World.CurrentStock.CurrentScreenScale29E0.Y,
                frame.World.CurrentStock.CurrentColorScale29F0);
            object identity=new();
            long revision=await target.PublishExternalDrawAsync(frame.World.Context,identity,rd=>
            {
                frame.RequirePortReturned(frame.ActualDirectoryReturn);actualTail.RequireCurrent();
                Source.RequireCurrent(actualAux,frame.World.CurrentStock);RequireInput(input);RequireTarget(rd,target);
                if(!rd.TextureIsValid(input.StorageRid)||!rd.TextureIsValid(input.ShaderViewRid))
                    throw new InvalidDataException("Actual completed104 exposure source retired before stock49.");
                using(var shape=rd.TextureGetFormat(input.ShaderViewRid))
                    if(shape.Format!=RenderingDevice.DataFormat.R32Sfloat||shape.Width!=2||shape.Height!=1||shape.Mipmaps!=1||
                        shape.ArrayLayers!=1||shape.TextureType!=RenderingDevice.TextureType.Type2D||!shape.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
                        throw new InvalidDataException("Exposure publication source view is not the actual2x1 R32_FLOAT mip0.");
                BindInput(rd,input.ShaderViewRid);
                if(rd.BufferUpdate(vertexConstants,0,32,constants)!=Error.Ok)
                    throw new InvalidDataException("Exposure publication actual stock constants upload failed.");
                // DefaultAll is LOAD/STORE. The source rectangle covers only
                // left texel; the current node1 right texel must survive.
                long list=rd.DrawListBegin(framebuffer,RenderingDevice.DrawFlags.DefaultAll,region:new Rect2(0,0,2,1));
                try
                {
                    rd.DrawListBindRenderPipeline(list,pipeline);rd.DrawListBindUniformSet(list,vertexSet,1);
                    rd.DrawListBindUniformSet(list,pixelSet,0);rd.DrawListBindVertexArray(list,vertexArray);
                    rd.DrawListDraw(list,false,1,4);
                }
                finally{rd.DrawListEnd();}
                return true;
            });
            completed=new(this,target,frame.World.Context,actualTail,actualPortReturn,input,identity,revision,
                Source.SourceEvidence+";node1-stock-native="+target.Source.Native.RenderRuleIdentity+
                ";"+input.SourceEvidence+";"+frame.World.CurrentStock.SourceProvenance+
                ";actual-stock49-return;RT0-RGBA15;ScreenBegin/End-clip0;target-pop;1x1-left-texel/LOAD-retains-right-node1;");
            completed.RequireCurrent();return completed;
        }
        catch{failed=true;throw;}
        finally{serial.Release();}
    }
    private void BindInput(RenderingDevice rd,Rid view)
    {
        if(pixelView==view&&pixelSet.IsValid&&rd.UniformSetIsValid(pixelSet))return;
        var uniforms=new Godot.Collections.Array<RDUniform>();Rid next=default;
        try
        {
            foreach(var d in pixelInterface)
            {
                var u=new RDUniform{Binding=(int)d.Binding!.Value,UniformType=d.StorageClass==2?RenderingDevice.UniformType.UniformBuffer:
                    d.Type.Kind=="sampler"?RenderingDevice.UniformType.Sampler:RenderingDevice.UniformType.Texture};
                u.AddId(d.StorageClass==2?pixelConstants:d.Type.Kind=="sampler"?sampler:view);uniforms.Add(u);
            }
            next=rd.UniformSetCreate(uniforms,shader,0);
            if(!next.IsValid||!rd.UniformSetIsValid(next))throw new InvalidDataException("Exposure publication source binding failed.");
            if(pixelSet.IsValid){rd.FreeRid(pixelSet);owned.Remove(pixelSet);}
            pixelSet=next;pixelView=view;owned.Add(next);
        }
        catch{if(next.IsValid)rd.FreeRid(next);throw;}
        finally{foreach(var u in uniforms)u.Dispose();}
    }
    private static void RequireInput(IOriginalMapPostEffect3ComputeMipTextureLease input)
    {
        input.RequireReadableMipRange(0,1);
        if(input.ParameterIndex!=61||input.InstanceIndex!=0||input.NativeViewFormat!=41||
            input.DeviceFormat!=RenderingDevice.DataFormat.R32Sfloat||input.Extent!=new Vector3I(2,1,1))
            throw new InvalidDataException("Post-Apply exposure input needs completed same-frame104 R32_FLOAT2x1 mip0.");
    }
    private static void RequireTarget(RenderingDevice rd,OriginalMapExposureOwner target)
    {
        if(!rd.TextureIsValid(target.TextureRid))throw new InvalidDataException("Global node1 exposure target is not live.");
        using var shape=rd.TextureGetFormat(target.TextureRid);
        if(shape.Format!=RenderingDevice.DataFormat.R32G32B32A32Sfloat||shape.Width!=2||shape.Height!=1||shape.Mipmaps!=1||
            shape.ArrayLayers!=1||shape.TextureType!=RenderingDevice.TextureType.Type2D||!shape.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.ColorAttachmentBit))
            throw new InvalidDataException("Global exposure target differs from source6C5630BB2x1 RGBA32_FLOAT mip0.");
    }
    internal void RequireCompleted(OriginalMapPostEffect3WorldExposurePublicationCompleted actual)
    {
        lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);
            if(failed||!ReferenceEquals(completed,actual))throw new InvalidDataException("Exposure publication receipt was replaced or its owner failed.");}
    }
    public ValueTask DisposeAsync()
    {
        lock(lifetime){retiring=true;if(disposal is null||disposal.IsFaulted||disposal.IsCanceled)disposal=RetireAsync();return new(disposal);}
    }
    private async Task RetireAsync()
    {
        await serial.WaitAsync();
        try
        {
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {while(retiredRids<owned.Count){rd.FreeRid(owned[owned.Count-1-retiredRids]);retiredRids++;}return true;});
            completed=null;
        }
        finally{serial.Release();}
    }
}
