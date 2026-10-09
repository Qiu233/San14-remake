using Godot;
using San14.Data;

public sealed record DeferredExtraDeviceLayer(uint TextureObjectId,uint Slice,Rid TargetView,Rid Framebuffer,
    Rid BorrowedStorage,string SourceResourceUri);

/// <summary>Borrowed RS/RR backing with owned slice views/framebuffers and the
/// exact source fallback operations. Construction never initializes pixels.
/// The parent frame must outlive this owner. Actual active producers and graph
/// ordering remain explicit responsibilities of the scene orchestrator.</summary>
public sealed class DeferredExtraLayerOwner : IAsyncDisposable
{
    public LoadedDeferredExtraLayerPlan Source { get; }
    public DeferredExtraDeviceLayer Rs3 { get; }
    public DeferredExtraDeviceLayer Rs4 { get; }
    public IReadOnlyList<DeferredExtraDeviceLayer> ReflectionLayers { get; }
    public IReadOnlyList<DeferredDeviceBinding> Inputs { get; }
    private readonly Rid _sdfPipeline;
    private readonly IReadOnlyList<Rid> _owned;
    private Task? _disposal;

    private DeferredExtraLayerOwner(LoadedDeferredExtraLayerPlan source,
        DeferredExtraDeviceLayer rs3,DeferredExtraDeviceLayer rs4,
        DeferredExtraDeviceLayer[] reflection,DeferredDeviceBinding[] inputs,Rid sdfPipeline,List<Rid> owned)
        => (Source,Rs3,Rs4,ReflectionLayers,Inputs,_sdfPipeline,_owned)=
            (source,rs3,rs4,Array.AsReadOnly(reflection),Array.AsReadOnly(inputs),sdfPipeline,owned.AsReadOnly());

    public static Task<DeferredExtraLayerOwner> BuildAsync(LoadedDeferredExtraLayerPlan source,
        RenderTargetFrame borrowedFrame)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(borrowedFrame);
        if(source.Deferred.TextureObjects.Source.ResourceUri!=borrowedFrame.Source.TextureObjects.Source.ResourceUri ||
            source.Deferred.TextureObjects.Source.DecodedSha256!=borrowedFrame.Source.TextureObjects.Source.DecodedSha256)
            throw new InvalidDataException("Extra layers and borrowed backing differ in original texture source identity.");
        RenderTargetDeviceTexture Backing(RenderTextureDefinition original)
        {
            if(!borrowedFrame.Textures.TryGetValue(original.Object.ObjectId,out var texture) ||
                texture.Source.Object.ObjectId!=original.Object.ObjectId || texture.Source.Kind!=original.Kind ||
                texture.Source.ArrayDepth!=original.ArrayDepth || texture.Source.LevelNum!=original.LevelNum ||
                texture.Source.PixelFormat!=original.PixelFormat || texture.Source.ColorSpace!=original.ColorSpace ||
                texture.Source.OutputColorSpace!=original.OutputColorSpace || texture.ExplicitExtent.X<=0 || texture.ExplicitExtent.Y<=0)
                throw new InvalidDataException("Extra layer has no matching live declared borrowed backing.");
            return texture;
        }
        var rs=Backing(source.Rs);var rr=Backing(source.Rr);
        var bindings=new[]{"RS","RR"}.Select(role=>
        {
            var input=source.Deferred.Inputs.Single(i=>i.Role==role);var t=role=="RS"?rs:rr;
            return new DeferredDeviceBinding("Texture",input.PixelBinding.BindPoint,t.ShaderViewRid,t.StorageRid,
                input.Texture.Source.Source.ResourceUri+"#"+input.Texture.Object.ObjectId.ToString("X8"));
        }).ToArray();
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();
            Rid Own(Rid rid){if(!rid.IsValid)throw new InvalidDataException("Extra layer view/pipeline allocation failed.");owned.Add(rid);return rid;}
            try
            {
                foreach(var t in new[]{rs,rr})
                {
                    if(!rd.TextureIsValid(t.StorageRid)||!rd.TextureIsValid(t.ShaderViewRid))throw new InvalidDataException("Extra-layer borrowed texture was released.");
                    using var actual=rd.TextureGetFormat(t.StorageRid);
                    if(actual.TextureType!=RenderingDevice.TextureType.Type2DArray || actual.ArrayLayers!=t.Source.ArrayDepth ||
                        actual.Mipmaps!=1 || actual.Format!=t.TargetFormat || actual.Width!=t.ExplicitExtent.X || actual.Height!=t.ExplicitExtent.Y ||
                        !actual.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.ColorAttachmentBit))
                        throw new InvalidDataException("Extra-layer borrowed texture shape/format differs from its original source.");
                }
                DeferredExtraDeviceLayer Slice(RenderTargetDeviceTexture texture,uint slice)
                {
                    using var view=new RDTextureView{FormatOverride=texture.TargetFormat};
                    Rid target=Own(rd.TextureCreateSharedFromSlice(view,texture.StorageRid,slice,0,1,RenderingDevice.TextureSliceType.Slice2D));
                    Rid framebuffer=Own(rd.FramebufferCreate([target]));
                    if(!rd.FramebufferIsValid(framebuffer))throw new InvalidDataException("Extra-layer slice framebuffer was rejected.");
                    return new(texture.Source.Object.ObjectId,slice,target,framebuffer,texture.StorageRid,
                        texture.Source.Source.Source.ResourceUri+"#"+texture.Source.Object.ObjectId.ToString("X8")+"/slice/"+slice);
                }
                var rs3=Slice(rs,3);var rs4=Slice(rs,4);var reflection=Enumerable.Range(0,3).Select(i=>Slice(rr,(uint)i)).ToArray();
                // Backend implementation of source D1B660's proved literal fill,
                // not a guessed shader resource or a replacement active SDF PS.
                using var code=new RDShaderSource{SourceVertex="""
                    #version 450
                    void main(){vec2 p=vec2((gl_VertexIndex<<1)&2,gl_VertexIndex&2);gl_Position=vec4(p*2.0-1.0,0.0,1.0);}
                    """,SourceFragment="""
                    #version 450
                    layout(location=0)out vec4 color;
                    void main(){color=vec4(0.0,0.0,1.0,1.0);}
                    """};
                using var spirv=rd.ShaderCompileSpirVFromSource(code);
                foreach(var stage in new[]{RenderingDevice.ShaderStage.Vertex,RenderingDevice.ShaderStage.Fragment})
                    if(spirv.GetStageCompileError(stage).Length!=0)throw new InvalidDataException(spirv.GetStageCompileError(stage));
                Rid shader=Own(rd.ShaderCreateFromSpirV(spirv,"OriginalSdfConditionalFallbackFill"));
                using var raster=new RDPipelineRasterizationState{CullMode=RenderingDevice.PolygonCullMode.Disabled};
                using var multi=new RDPipelineMultisampleState();
                using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
                using var color=new RDPipelineColorBlendStateAttachment{EnableBlend=false,WriteR=true,WriteG=true,WriteB=false,WriteA=false};
                using var blend=new RDPipelineColorBlendState{Attachments=[color]};
                Rid pipeline=Own(rd.RenderPipelineCreate(shader,rd.FramebufferGetFormat(rs3.Framebuffer),-1,
                    RenderingDevice.RenderPrimitive.Triangles,raster,multi,depth,blend));
                if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("Original SDF RG-only fallback pipeline was rejected.");
                return new DeferredExtraLayerOwner(source,rs3,rs4,reflection,bindings,pipeline,owned);
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }

    public Task<DeferredReflectionDecision> ApplyReflectionAsync(DeferredReflectionState explicitSourceState)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        var decision=DeferredExtraLayerPlan.Reflection(explicitSourceState);
        if(decision.RequiresActiveProducer)throw new NotSupportedException("Original active ReflectionProbe producer must supply RR layers; fallback clear is not applicable.");
        if(decision.RequiresTargetDescriptorFallback)throw new NotSupportedException("Original null reflection target inherits the current attachment descriptor; that selected source target must be supplied explicitly.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null,this);
            foreach(var step in decision.Steps.Where(s=>s.Action==DeferredExtraAction.ClearReflectionSlice))
            {
                var layer=ReflectionLayers[checked((int)step.Slice)];Validate(rd,layer);
                // Godot's named Transparent is (1,1,1,0); the native packed
                // color is all-zero, including RGB in this alpha-less RR target.
                long list=rd.DrawListBegin(layer.Framebuffer,RenderingDevice.DrawFlags.ClearColorAll,[new Color(0,0,0,0)]);
                rd.DrawListEnd();
            }
            return decision;
        });
    }

    public Task<DeferredSdfDecision> ApplySdfAsync(DeferredSdfState explicitSourceState)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        var decision=DeferredExtraLayerPlan.Sdf(explicitSourceState);
        if(decision.Step.Action==DeferredExtraAction.RequiresActiveProducer)
            throw new NotSupportedException("Original active ViewSDF producer must supply RS3; fallback fill is not applicable.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null,this);
            if(decision.Step.Action==DeferredExtraAction.FillSdfRedGreen)
            {
                Validate(rd,Rs3);
                if(!rd.RenderPipelineIsValid(_sdfPipeline))throw new InvalidDataException("SDF fallback pipeline was released.");
                long list=rd.DrawListBegin(Rs3.Framebuffer,RenderingDevice.DrawFlags.DefaultAll);
                try{rd.DrawListBindRenderPipeline(list,_sdfPipeline);rd.DrawListDraw(list,false,1,3);}
                finally{rd.DrawListEnd();}
            }
            // Publish next once bits only after this own operation completed;
            // preserve/no-current-target branch still has the source bit result.
            return decision;
        });
    }

    /// <summary>Execute the original null-target branch using the caller's
    /// current slot0 attachment. This attachment must be a one-color framebuffer
    /// view with externally owned backing that remains live through completion.</summary>
    public Task<DeferredReflectionDecision> ApplyReflectionInheritedTargetAsync(
        DeferredReflectionState explicitSourceState,DeferredExtraDeviceLayer? explicitCurrentSlot0,
        IReadOnlyList<bool>? threeSourceBindResults)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        var decision=DeferredExtraLayerPlan.ReflectionInheritedTarget(explicitSourceState,
            explicitCurrentSlot0?.TextureObjectId,explicitCurrentSlot0?.Slice??0,threeSourceBindResults);
        if(decision.RequiresActiveProducer)throw new NotSupportedException("Original active ReflectionProbe producer must supply RR layers.");
        if(!decision.Steps.Any(s=>s.Action==DeferredExtraAction.ClearReflectionSlice))return Task.FromResult(decision);
        if(explicitSourceState.RawSliceDescriptorsPresent)
            throw new InvalidDataException("Inherited-target API cannot redirect an explicit original RR slice clear to another attachment.");
        if(explicitCurrentSlot0 is null)throw new InvalidDataException("Original inherited target is absent.");
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            ObjectDisposedException.ThrowIf(_disposal is not null,this);
            Validate(rd,explicitCurrentSlot0);
            foreach(var step in decision.Steps.Where(s=>s.Action==DeferredExtraAction.ClearReflectionSlice))
            {
                if(step.TextureObjectId!=explicitCurrentSlot0.TextureObjectId || step.Slice!=explicitCurrentSlot0.Slice)
                    throw new InvalidDataException("Original inherited clear plan and selected slot0 attachment differ.");
                long list=rd.DrawListBegin(explicitCurrentSlot0.Framebuffer,RenderingDevice.DrawFlags.ClearColorAll,[new Color(0,0,0,0)]);
                rd.DrawListEnd();
            }
            return decision;
        });
    }
    private static void Validate(RenderingDevice rd,DeferredExtraDeviceLayer layer)
    {
        if(!rd.TextureIsValid(layer.BorrowedStorage)||!rd.TextureIsValid(layer.TargetView)||!rd.FramebufferIsValid(layer.Framebuffer))
            throw new InvalidDataException("Extra layer source parent/view/framebuffer is no longer live.");
    }
    public ValueTask DisposeAsync()=>new(_disposal??=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
    {for(int i=_owned.Count-1;i>=0;i--)rd.FreeRid(_owned[i]);return true;}));
}
