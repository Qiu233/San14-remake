using Godot;
using San14.Data;

public sealed partial class MapPostEffect3WorldStockGraphicsSourceOwner
{
    private MapPostEffect3FirstWorldExecutionOwner? retainedModuleOwner;
    internal void RequireInputDepthSamplerFirstWorld()
    {
        RequireCurrent();
        if(Context.FrameNumber==1&&scene.RenderAttemptNumber==1&&scene.FrameNumber==0)return;
        if(Context.FrameNumber!=2||retainedModuleOwner is null)
            throw new NotSupportedException("E13 sampler key retention requires a fresh World or its actual retained second attempt.");
        retainedModuleOwner.RequireRetainedWorldAttempt(Context);
    }
    /// <summary>Source ApplyCPU38 E13 getter, plus actual initialized shared
    /// geometry depth. No caller-supplied readiness bool or RefractionDepth.
    /// Every alias must already select this same original E13 wrapper owner.</summary>
    public IMapPostEffect3ComputeExternalTextureLease LeaseInputDepth(
        LoadedMapPostEffect3InputDepthSource source,int parameterIndex)
    {
        ArgumentNullException.ThrowIfNull(source);RequireCurrent();
        var host=actualHost??throw new NotSupportedException("InputDepth needs the actual World Host's private geometry completion owner.");
        var geometry=host.RequirePostEffectInputDepthGeometry(Context);
        var depth=Context.TerrainFrame.Textures.TryGetValue(0xE13DDB4F,out var t)?t:
            throw new InvalidDataException("Actual current E13 source depth allocation is absent.");
        source.RequireSelectedOwnedDepthDescriptor(depth.EffectiveSource);
        if(Context.TerrainFrame.Source.DepthTarget?.Object.ObjectId!=0xE13DDB4F||
            source.Graph.Source.ResourceUri!=Context.TerrainFrame.Source.Graph.Source.ResourceUri||
            source.Graph.Source.DecodedSha256!=Context.TerrainFrame.Source.Graph.Source.DecodedSha256||
            source.Native.Locale!=CurrentStock.Source.OwnedFrontendSource.Locale)
            throw new InvalidDataException("InputDepth factory/source graph differs from the actual same-frame E13 attachment.");
        var reference=TextureParameters.Require(parameterIndex);
        if(reference.Reference.PhysicalAllocationParameterIndex is not null||reference.Reference.InstanceIndex!=0||
            reference.Reference.ProducerOwnerIdentity!=depth.EffectiveSource.OwnerIdentity)
            throw new InvalidDataException("Native InputDepth alias does not select the actual initialized E13 wrapper.");
        var lease=new InputDepthLease(this,source,geometry,reference,depth);lease.RequireCurrent();return lease;
    }

    /// <summary>Bind the actual sampled E13 wrapper's key at
    /// the same counted texture register. The current source receipt fixes
    /// the original2D loader key; enabled TemporalAA's later mutations are
    /// deliberately outside this admitted first-frame key-retention scope.</summary>
    public Task<MapPostEffect3InputDepthSamplerOwner> BuildInputDepthSamplerAsync(
        LoadedMapPostEffect3InputDepthSource source,int parameterIndex,int nativeStageOrdinal,uint registerIndex,
        MapPostEffect3OwnedFreshCameraFrame actualParameters,
        MapPostEffect3FirstWorldExecutionOwner? retainedOwner=null)
    {
        if(retainedOwner is not null)
        {
            var invocation=retainedOwner.CurrentRetainedInvocation??throw new InvalidDataException("InputDepth sampler needs the actual retained invocation.");
            invocation.RequireModule(Bank.Source);invocation.RequireInitializedFrame(actualParameters);
            if(invocation.FrameNumber!=Context.FrameNumber||retainedModuleOwner is not null&&!ReferenceEquals(retainedModuleOwner,retainedOwner))
                throw new InvalidDataException("InputDepth sampler retained module/frame changed.");
            retainedModuleOwner=retainedOwner;
        }
        var texture=LeaseInputDepth(source,parameterIndex);
        return MapPostEffect3InputDepthSamplerOwner.BuildAsync(this,source,texture,nativeStageOrdinal,registerIndex,actualParameters);
    }

    private sealed record InputDepthLease(MapPostEffect3WorldStockGraphicsSourceOwner Owner,
        LoadedMapPostEffect3InputDepthSource Source,NativeSceneGeometryStencilCompletedFrame Geometry,
        MapPostEffect3ResolvedTextureParameter Reference,RenderTargetDeviceTexture Texture)
        :IMapPostEffect3ComputeExternalTextureLease
    {
        public long FrameNumber=>Owner.Context.FrameNumber;
        public string ContextOwnerIdentity=>Owner.Bank.Source.ContextOwnerIdentity;
        public int ParameterIndex=>Reference.ParameterIndex;
        public int InstanceIndex=>0;
        public Rid ShaderViewRid=>Texture.ShaderViewRid;
        public Rid StorageRid=>Texture.StorageRid;
        public uint NativeViewFormat=>21;
        public RenderingDevice.DataFormat DeviceFormat=>Texture.ShaderFormat;
        public Vector3I Extent=>new(Texture.ExplicitExtent.X,Texture.ExplicitExtent.Y,1);
        public string ProducerOwnerIdentity=>Texture.EffectiveSource.OwnerIdentity;
        public string SourceEvidence=>Source.SourceEvidence+";"+Owner.SourceEvidence+
            ";actual-private-shared-geometry-depth-completion;"+Reference.Reference.SourceEvidence;
        public void RequireCurrent()
        {
            Owner.RequireCurrent();Owner.TextureParameters.ValidateCurrent(Reference);
            var host=Owner.actualHost??throw new InvalidDataException("InputDepth source Host owner is absent.");
            var actual=host.RequirePostEffectInputDepthGeometry(Owner.Context);
            Geometry.ValidateCurrent(actual.Frame,actual.Population,actual.Army,actual.TerrainSubmission);
            Source.RequireSelectedOwnedDepthDescriptor(Texture.EffectiveSource);
            if(!ReferenceEquals(Owner.Context.TerrainFrame.Textures[0xE13DDB4F],Texture)||
                Texture.ShaderFormat!=RenderingDevice.DataFormat.D32SfloatS8Uint)
                throw new InvalidDataException("InputDepth current native view/geometry parent was replaced.");
        }
    }
}

/// <summary>One actual counted InputDepth sampler selection. Its owner must
/// retire after queued users; retirement rejects all leases immediately.</summary>
public sealed class MapPostEffect3InputDepthSamplerOwner:IMapPostEffect3ComputeSamplerLease,IAsyncDisposable
{
    private readonly MapPostEffect3WorldStockGraphicsSourceOwner world;
    private readonly IMapPostEffect3ComputeExternalTextureLease texture;
    private readonly MapPostEffect3OwnedFreshCameraFrame parameters;
    private readonly int sourcePass;
    private readonly string evidence;
    private readonly object lifetime=new();private bool retiring;private Task? disposal;
    private MapPostEffect3InputDepthSamplerOwner(MapPostEffect3WorldStockGraphicsSourceOwner owner,
        IMapPostEffect3ComputeExternalTextureLease lease,MapPostEffect3OwnedFreshCameraFrame values,
        int pass,int stage,uint register,Rid sampler,string source)
        =>(world,texture,parameters,sourcePass,NativeStageOrdinal,SourceRegister,SamplerRid,evidence)=
            (owner,lease,values,pass,stage,register,sampler,source);
    public long FrameNumber=>texture.FrameNumber;
    public string ContextOwnerIdentity=>texture.ContextOwnerIdentity;
    public int NativeStageOrdinal{get;}
    public uint SourceRegister{get;}
    public Rid SamplerRid{get;}
    public string SourceEvidence=>evidence;
    internal static Task<MapPostEffect3InputDepthSamplerOwner> BuildAsync(
        MapPostEffect3WorldStockGraphicsSourceOwner world,LoadedMapPostEffect3InputDepthSource source,
        IMapPostEffect3ComputeExternalTextureLease texture,int stage,uint register,
        MapPostEffect3OwnedFreshCameraFrame parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        int pass=stage==0?1:throw new NotSupportedException("This exact first-Shaft InputDepth sampler selection is CS stage0 only.");
        if(register!=2)throw new NotSupportedException("First Shaft InputDepth source register is2.");
        world.RequireInputDepthSamplerFirstWorld();
        texture.RequireCurrent();parameters.RequireCurrent();_=parameters.RequirePreparedFactories();
        if(parameters.FrameNumber!=world.Context.FrameNumber||parameters.OwnerIdentity!=world.Bank.Source.ContextOwnerIdentity||
            parameters.Allocation.Source.Sha256!=world.Bank.Source.Source.Sha256||
            System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(parameters.State.RequireValue("TemporalAAEnable").Span)!=0)
            throw new NotSupportedException("Initial E13 key retention requires this owned first-frame disabled TemporalAA source branch.");
        var rows=world.Bank.Source.Passes[pass].ActiveBindings.Where(b=>b.Role==MapPostEffect3BindingRole.ShaderResourceRead&&
            b.RegisterIndex==register&&(b.StageMask&(1<<stage))!=0).ToArray();
        if(rows.Length!=2||rows.Any(b=>b.ParameterIndex!=texture.ParameterIndex||b.NativeViewKind!=1||b.MipSelector!=-1||b.SliceSelector!=-1))
            throw new InvalidDataException("InputDepth sampler is not selected by this exact counted native texture setter.");
        var depth=world.Context.TerrainFrame.Textures[0xE13DDB4F];
        var packed=RenderTexturePackedFlags.ConstructAfterSuccessful2DLoader(source.Native,depth.EffectiveSource,
            texture.SourceEvidence+";actual-owned-E13-device-wrapper-selected");
        var recipe=RenderSamplerSourceAdapters.DecodeKey(source.Native,packed.CompleteSamplerKey38.Span);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            texture.RequireCurrent();parameters.RequireCurrent();
            var sampler=DeviceSampler.Create(rd,recipe);
            return new MapPostEffect3InputDepthSamplerOwner(world,texture,parameters,pass,stage,register,sampler,
                packed.SourceEvidence+";descriptor38-same-register;counted-CS-pass1/t2/s2;disabled-TAA-preserves-E13-key");
        });
    }
    public void RequireCurrent()
    {
        lock(lifetime)ObjectDisposedException.ThrowIf(retiring,this);
        world.RequireInputDepthSamplerFirstWorld();
        texture.RequireCurrent();parameters.RequireCurrent();_=parameters.RequirePreparedFactories();
        if(parameters.FrameNumber!=FrameNumber||parameters.OwnerIdentity!=ContextOwnerIdentity||sourcePass!=1||
            System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(parameters.State.RequireValue("TemporalAAEnable").Span)!=0)
            throw new InvalidDataException("InputDepth sampler retention was replaced, mutated or belongs to another frame.");
    }
    public ValueTask DisposeAsync()
    {
        lock(lifetime){if(disposal is not null)return new(disposal);retiring=true;
            return new(disposal=TerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(SamplerRid);return true;}));}
    }
}
