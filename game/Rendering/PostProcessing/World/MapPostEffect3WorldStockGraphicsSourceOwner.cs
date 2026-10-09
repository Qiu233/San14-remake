using Godot;
using San14.Data;

/// <summary>Same-attempt adapter of actual World submissions and native runtime
/// texture pointer aliases. It creates no image, performs no draw, and does not
/// supply target inheritance, raster or sampler defaults. Borrowed GPU owners
/// must outlive every executor invocation using these capabilities.</summary>
public sealed partial class MapPostEffect3WorldStockGraphicsSourceOwner
{
    private readonly NativeMapScene scene;
    private readonly MapSourceCreatedWorldNativeSession session;
    private readonly Func<NativeMapFrameContext,MapCopy97CurrentStockPacket> requireActualStock;
    private readonly NativeMapWorldDeferredSubmittedFrame submitted;
    private readonly MapSourceCreatedNativeSessionPreparedFrame prepared;
    private readonly MapPostEffect3ParameterState names;
    private MapSourceCreatedWorldPlayableHost? actualHost;
    private readonly MapOwnedGraphicsMaskAcceptedSourceSnapshot? acceptedMaskSources;
    private readonly MapPostEffect3WorldSubmittedImmutableSourceReceipt? acceptedWorldSubmission;
    public NativeMapFrameContext Context{get;}
    public PostProcessResourceBank Bank{get;}
    public MapPostEffect3TextureParameterState TextureParameters{get;}
    public MapCopy97CurrentStockPacket CurrentStock{get;}
    public string SourceEvidence=>submitted.SourceEvidence+";"+CurrentStock.SourceProvenance+
        ";same-current-World-selected-roster-and-stock-owner";

    private MapPostEffect3WorldStockGraphicsSourceOwner(NativeMapScene sceneOwner,
        MapSourceCreatedWorldNativeSession sessionOwner,NativeMapFrameContext context,
        Func<NativeMapFrameContext,MapCopy97CurrentStockPacket> stockGetter,PostProcessResourceBank bank,
        MapPostEffect3TextureParameterState textureState)
    {
        scene=sceneOwner;session=sessionOwner;Context=context;requireActualStock=stockGetter;Bank=bank;TextureParameters=textureState;
        submitted=scene.LastWorldDeferredSubmittedFrame??throw new InvalidDataException("World late draws have not actually submitted.");
        prepared=session.CurrentFrame??throw new InvalidDataException("World current preparation is unavailable.");
        CurrentStock=requireActualStock(context);
        names=MapPostEffect3ParameterState.CreateUninitialized(bank.Source.Source,context.FrameNumber,bank.Source.ContextOwnerIdentity);
        RequireCurrent();
        acceptedMaskSources=CurrentStock.CaptureAcceptedImmutableMaskSources();acceptedMaskSources.RequireCurrent();
        acceptedWorldSubmission=session.CaptureAcceptedSubmittedWorld(submitted);acceptedWorldSubmission.RequireCurrent();
    }
    public static MapPostEffect3WorldStockGraphicsSourceOwner CaptureCurrent(
        NativeMapScene scene,MapSourceCreatedWorldNativeSession session,
        NativeMapFrameContext context,SceneViewNativeMapStockBackendSourceOwner stock,
        PostProcessResourceBank bank,MapPostEffect3TextureParameterState textureState)
    {
        ArgumentNullException.ThrowIfNull(scene);ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(bank);ArgumentNullException.ThrowIfNull(textureState);
        return new(scene,session,context,stock.RequirePreparedPacket,bank,textureState);
    }
    /// <summary>Default World Host adapter: the actual SourceFactory's stock
    /// backend remains private; its bound graphics owner exposes the checked
    /// packet getter. It cannot be replaced with a caller no-op delegate.</summary>
    public static MapPostEffect3WorldStockGraphicsSourceOwner CaptureCurrent(
        MapSourceCreatedWorldPlayableHost actualHost,NativeMapFrameContext context,
        PostProcessResourceBank bank,MapPostEffect3TextureParameterState textureState)
    {
        ArgumentNullException.ThrowIfNull(actualHost);ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(bank);ArgumentNullException.ThrowIfNull(textureState);
        var graphics=actualHost.GraphicsColorMaskOwner??throw new InvalidDataException("Actual World Host has no source frontend history.");
        return new(actualHost.Scene,actualHost.Session,context,graphics.RequireCurrentStockPacket,bank,textureState){actualHost=actualHost};
    }
    public void RequireCurrent()
    {
        acceptedMaskSources?.RequireCurrent();
        submitted.RequireCurrent(Context);
        if(acceptedWorldSubmission is{} accepted)accepted.RequireCurrent();else session.RequireSubmittedWorld(submitted);
        if(!ReferenceEquals(session.CurrentFrame,prepared)||!ReferenceEquals(prepared.Context,Context)||
            !ReferenceEquals(scene.OutputFrame,Context.OutputFrame)||!ReferenceEquals(scene.TerrainFrame,Context.TerrainFrame)||
            !ReferenceEquals(requireActualStock(Context),CurrentStock)||
            CurrentStock.ViewIdentity!=session.Binding.ViewIdentity||CurrentStock.FrameNumber!=Context.FrameNumber||
            prepared.Lighting.SceneInputs.Camera!=Context.PreparedCamera||
            TextureParameters.FrameNumber!=Context.FrameNumber||TextureParameters.SourceSha256!=Bank.Source.Source.Sha256||
            TextureParameters.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||Bank.OwnedRidCount<=0)
            throw new InvalidDataException("Stock graphics scope is not the live same-frame World/Camera/frontend/module/texture state.");
        CurrentStock.ValidateCurrent();
    }
    /// <summary>Complete source acceptance boundary for this actual World,
    /// including the separate Functor1 wrapper. Draw-time dynamic checks use
    /// the private accepted mask snapshot; original mutable edits still reject
    /// here before a completed invocation is accepted.</summary>
    public void RevalidateStaticMaskSourceAcceptanceBoundary()
    {
        RequireCurrent();
        CurrentStock.RevalidateAcceptedStaticSourceBoundary();acceptedWorldSubmission!.RevalidateMutableSourceBoundary();
    }

    /// <summary>Only external pointer aliases that already resolve to the
    /// actual submitted node73 working Scene are admitted. Aliases are frozen;
    /// a later native setter invalidates this lease rather than redirects it.</summary>
    public IMapPostEffect3ComputeExternalTextureLease LeaseWorkingScene(int parameterIndex)
    {
        RequireCurrent();var reference=TextureParameters.Require(parameterIndex);
        var texture=Context.OutputFrame.Textures.TryGetValue(0x064B746B,out var t)?t:
            throw new InvalidDataException("Actual node73 working Scene attachment is absent.");
        if(Context.OutputFrame.Source.ColorTargets is not [{Texture.Object.ObjectId:0x064B746B}]||
            reference.Reference.PhysicalAllocationParameterIndex is not null||reference.Reference.InstanceIndex!=0||
            reference.Reference.ProducerOwnerIdentity!=texture.EffectiveSource.OwnerIdentity)
            throw new InvalidDataException("Native pointer table does not select this actual submitted working Scene owner.");
        var native=RenderTextureFormatMath.Resolve(texture.EffectiveSource.PixelFormat,
            texture.EffectiveSource.ColorSpace,texture.EffectiveSource.OutputColorSpace);
        return new WorkingSceneLease(this,reference,texture,native.ShaderViewFormat);
    }

    /// <summary>Actual temporary EA2OutputExposureScale replacement only.
    /// In this module its primary declaration is61, not LFOutputToneMapInfo5.
    /// A still-owned61 or a missing native setter rejects; no alias is invented.</summary>
    public IMapPostEffect3ComputeExternalTextureLease LeaseCompletedExposureScale(
        MapExposureOwner actualOwner,MapPostEffect3ApplyExposureReference actualPublication)
    {
        ArgumentNullException.ThrowIfNull(actualOwner);ArgumentNullException.ThrowIfNull(actualPublication);
        RequireCurrent();
        int index=names.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,"EA2OutputExposureScale")
            ??throw new InvalidDataException("Original exposure scale declaration is absent.");
        var reference=TextureParameters.Require(index);
        var packet=actualOwner.LastPacket??throw new InvalidDataException("Actual exposure has no completed packet.");
        long drawCount=actualOwner.DrawCount;
        actualOwner.RequireCompletedCurrentFrame(Context,packet,drawCount);
        if(actualOwner.Source.Texture.Object.ObjectId!=0x6C5630BB||actualPublication.FrameNumber!=Context.FrameNumber||
            actualPublication.ExposureTexture50.FrameNumber!=Context.FrameNumber||
            reference.Reference.PhysicalAllocationParameterIndex is not null||reference.Reference.InstanceIndex!=0||
            reference.Reference.ProducerOwnerIdentity!=actualPublication.ExposureTexture50.OwnerIdentity)
            throw new InvalidDataException("Exposure lease needs the original temporary setter's current external owner and actual node1 completion.");
        return new ExposureLease(this,reference,actualOwner,actualPublication,packet,drawCount);
    }

    public IMapPostEffect3StockGraphicsTargetLease RequirePass7Target()
    {
        RequireCurrent();var reference=TextureParameters.Require(11);
        if(reference.Reference.PhysicalAllocationParameterIndex!=11)
            throw new NotSupportedException("Original pass7 target11 does not select its actual owned physical allocation.");
        var target=Bank.RequireTexture(11,reference.Reference.InstanceIndex);
        var rows=Bank.Source.Passes[7].ActiveBindings.Where(b=>b.Role==MapPostEffect3BindingRole.RenderTargetWrite).ToArray();
        if(rows.Length!=2||rows.Any(b=>b.ParameterIndex!=11||b.RegisterIndex!=0||b.NativeViewKind!=3||
            b.MipSelector!=-1||b.SliceSelector!=-1)||!rows.Select(b=>b.SourceRowIndex).SequenceEqual(new[]{56,59})||
            target.Shape.Dimension!=MapPostEffect3TextureDimension.Texture2D||target.Shape.MipLevels!=1||
            !target.Shape.HasRenderTargetPath||target.Shape.Formats.RenderTargetFormat0==0)
            throw new NotSupportedException("Pass7 target differs from the original counted single2D RT0 setter.");
        return new Pass7Target(this,reference,target);
    }

    /// <summary>Execute the actual Composite Before's sampler selection recipe
    /// at pass44. These are manager7/8 source slots only, not sampler0..3 from
    /// sampled wrappers and not the preceding compute-stage context defaults.</summary>
    public IReadOnlyList<IMapPostEffect3ManagerSamplerSelection> SelectCompositeBeforeSamplers(
        MapPostEffect3OwnedFreshCameraFrame actualParameters)
    {
        ArgumentNullException.ThrowIfNull(actualParameters);RequireCurrent();actualParameters.RequireCurrent();
        var initialization=actualParameters.RequirePreparedFactories();
        var composite=initialization.Composite;
        if(actualParameters.FrameNumber!=Context.FrameNumber||actualParameters.OwnerIdentity!=Bank.Source.ContextOwnerIdentity||
            actualParameters.Allocation.Source.Sha256!=Bank.Source.Source.Sha256||
            actualParameters.Allocation.ContextWidth!=Bank.Source.ContextWidth||actualParameters.Allocation.ContextHeight!=Bank.Source.ContextHeight||
            composite.FrameNumber!=Context.FrameNumber||composite.OwnerIdentity!=Bank.Source.ContextOwnerIdentity||
            composite.ConstantBuffer.SourceSha256!=Bank.Source.Source.Sha256||
            composite.ConstantBuffer.Writer!=MapPostEffect3ParameterComposite.InitializationWriter||
            composite.ConstantBuffer.Declaration.Index!=16||composite.ConstantBuffer.Bytes.Length!=160||
            composite.BeforeSamplerRequirements is not [{NativeStageOrdinal:2,RegisterIndex:7,NativeManagerSource:"ShaderManager30/source-linear"},
                {NativeStageOrdinal:2,RegisterIndex:8,NativeManagerSource:"ShaderManager38/source-point"}])
            throw new InvalidDataException("Composite Before sampler source differs from this exact initialized owned Functor0 frame.");
        return Array.AsReadOnly<IMapPostEffect3ManagerSamplerSelection>([
            new CompositeSelection(this,actualParameters,initialization,2,7,MapPostEffect3ManagerSamplerKind.Linear),
            new CompositeSelection(this,actualParameters,initialization,2,8,MapPostEffect3ManagerSamplerKind.Point)]);
    }

    private sealed record CompositeSelection(MapPostEffect3WorldStockGraphicsSourceOwner Owner,
        MapPostEffect3OwnedFreshCameraFrame Parameters,MapPostEffect3OwnedFactoryInitialization Initialization,
        int NativeStageOrdinal,uint SourceRegister,MapPostEffect3ManagerSamplerKind Kind)
        :IMapPostEffect3ManagerSamplerSelection
    {
        public long FrameNumber=>Owner.Context.FrameNumber;
        public string ContextOwnerIdentity=>Owner.Bank.Source.ContextOwnerIdentity;
        public string SourceEvidence=>Owner.SourceEvidence+";CompositeBefore;stage2/slot"+SourceRegister+
            ";actual-private-factory-initialization16;manager"+(Kind==MapPostEffect3ManagerSamplerKind.Linear?"30/linear":"38/point");
        public void RequireCurrent()
        {
            Owner.RequireCurrent();Parameters.RequireCurrent();
            if(!ReferenceEquals(Parameters.RequirePreparedFactories(),Initialization)||
                Parameters.FrameNumber!=FrameNumber||Parameters.OwnerIdentity!=ContextOwnerIdentity)
                throw new InvalidDataException("Composite Before selection was replaced, failed, disposed or belongs to another owned frame.");
        }
    }

    private sealed record WorkingSceneLease(MapPostEffect3WorldStockGraphicsSourceOwner Owner,
        MapPostEffect3ResolvedTextureParameter Reference,RenderTargetDeviceTexture Texture,uint NativeViewFormat)
        :IMapPostEffect3ComputeExternalTextureLease,IMapPostEffect3SharedSampledViewLease
    {
        public long FrameNumber=>Owner.Context.FrameNumber;
        public string ContextOwnerIdentity=>Owner.Bank.Source.ContextOwnerIdentity;
        public int ParameterIndex=>Reference.ParameterIndex;
        public int InstanceIndex=>0;
        public Rid ShaderViewRid=>Texture.ShaderViewRid;
        public Rid StorageRid=>Texture.StorageRid;
        public RenderingDevice.DataFormat DeviceFormat=>Texture.ShaderFormat;
        public Vector3I Extent=>new(Texture.ExplicitExtent.X,Texture.ExplicitExtent.Y,1);
        public string ProducerOwnerIdentity=>Texture.EffectiveSource.OwnerIdentity;
        public string SourceEvidence=>Owner.SourceEvidence+";actual-working-wrapper-alias;"+Reference.Reference.SourceEvidence;
        public MapPostEffect3SharedSampledViewProof RequireSharedSampledViewProof()
            =>MapPostEffect3SharedSampledViewProof.CaptureWorkingScene(Owner.Context.OutputFrame,Texture,RequireCurrent);
        public void RequireCurrent()
        {
            Owner.RequireCurrent();Owner.TextureParameters.ValidateCurrent(Reference);
            if(!ReferenceEquals(Owner.Context.OutputFrame.Textures[0x064B746B],Texture))
                throw new InvalidDataException("Submitted working Scene allocation was replaced.");
        }
    }
    private sealed record ExposureLease(MapPostEffect3WorldStockGraphicsSourceOwner Owner,
        MapPostEffect3ResolvedTextureParameter Reference,MapExposureOwner Exposure,
        MapPostEffect3ApplyExposureReference Publication,MapExposurePacket Packet,long DrawCount)
        :IMapPostEffect3ComputeExternalTextureLease
    {
        public long FrameNumber=>Owner.Context.FrameNumber;
        public string ContextOwnerIdentity=>Owner.Bank.Source.ContextOwnerIdentity;
        public int ParameterIndex=>Reference.ParameterIndex;
        public int InstanceIndex=>0;
        public Rid ShaderViewRid=>Exposure.TextureRid;
        public Rid StorageRid=>Exposure.TextureRid;
        public uint NativeViewFormat=>2;
        public RenderingDevice.DataFormat DeviceFormat=>RenderingDevice.DataFormat.R32G32B32A32Sfloat;
        public Vector3I Extent=>new(2,1,1);
        public string ProducerOwnerIdentity=>Publication.ExposureTexture50.OwnerIdentity;
        public string SourceEvidence=>Owner.SourceEvidence+";"+Publication.SourceEvidence+";actual-exposure-scale-swap;"+Reference.Reference.SourceEvidence;
        public void RequireCurrent()
        {
            Owner.RequireCurrent();Owner.TextureParameters.ValidateCurrent(Reference);
            Exposure.RequireCompletedCurrentFrame(Owner.Context,Packet,DrawCount);
        }
    }
    private sealed record Pass7Target(MapPostEffect3WorldStockGraphicsSourceOwner Owner,
        MapPostEffect3ResolvedTextureParameter Reference,MapPostEffect3DeviceTexture Texture)
        :IMapPostEffect3StockGraphicsTargetLease
    {
        public long FrameNumber=>Owner.Context.FrameNumber;
        public string ContextOwnerIdentity=>Owner.Bank.Source.ContextOwnerIdentity;
        public string OwnedFrontendIdentity=>Owner.CurrentStock.OwnedFrontendIdentity;
        public string ViewIdentity=>Owner.CurrentStock.ViewIdentity;
        public int? ParameterIndex=>11;
        public int InstanceIndex=>Reference.Reference.InstanceIndex;
        public string ProducerOwnerIdentity=>Reference.Reference.ProducerOwnerIdentity;
        public Rid StorageRid=>Texture.StorageRid;
        public Rid TargetViewRid=>Texture.NativeFormatViews[NativeViewFormat];
        public uint NativeViewFormat=>Texture.Shape.Formats.RenderTargetFormat0;
        public RenderingDevice.DataFormat DeviceFormat=>Texture.StorageFormat;
        public Vector2I Extent=>new(checked((int)Texture.Shape.Width),checked((int)Texture.Shape.Height));
        public string SourceEvidence=>Owner.SourceEvidence+";pass7-rows56/59-RT0-parameter11;"+Reference.Reference.SourceEvidence;
        public void RequireCurrentForPass(int sourcePassIndex)
        {
            if(sourcePassIndex!=7)throw new NotSupportedException("This target capability proves pass7 only; no target inheritance is implied.");
            Owner.RequireCurrent();Owner.TextureParameters.ValidateCurrent(Reference);
            if(!ReferenceEquals(Owner.Bank.RequireTexture(11,InstanceIndex),Texture))
                throw new InvalidDataException("Actual pass7 allocated target retired or changed.");
        }
    }
}
