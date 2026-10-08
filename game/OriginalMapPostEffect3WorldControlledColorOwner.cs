using Godot;
using San14.Data;

/// <summary>The actual late World scene can be a counted external HDR target.
/// Its original pixels are initially ready from the checked World submission.
/// AddBloom invalidates that generation, and only a joined complete source
/// draw may make the new generation readable. Table aliases retain identity.
/// This owner borrows the World image; it allocates or disposes no GPU image.</summary>
public sealed partial class OriginalMapPostEffect3WorldControlledColorOwner
{
    private readonly OriginalMapPostEffect3WorldStockGraphicsSourceOwner world;
    private readonly OriginalMapPostEffect3TextureParameterState table;
    private readonly OriginalRenderTargetDeviceTexture scene;
    private readonly Rid targetView;
    private readonly uint shaderFormat, targetFormat;
    private readonly object lifetime = new();
    private long revision;
    private bool complete = true;

    public OriginalMapPostEffect3WorldControlledColorOwner(
        OriginalMapPostEffect3WorldStockGraphicsSourceOwner actualWorld)
    {
        ArgumentNullException.ThrowIfNull(actualWorld); actualWorld.RequireCurrent();
        world = actualWorld; table = world.TextureParameters;
        scene = world.Context.OutputFrame.Textures[0x064B746B];
        if (world.Context.OutputFrame.Source.ColorTargets is not [{ Texture.Object.ObjectId: 0x064B746B }] ||
            world.Context.OutputFrame.ColorViews.Count != 1)
            throw new InvalidDataException("Controlled Scene needs its actual single World color attachment view.");
        targetView = world.Context.OutputFrame.ColorViews[0];
        var native = OriginalRenderTextureFormatMath.Resolve(scene.EffectiveSource.PixelFormat,
            scene.EffectiveSource.ColorSpace, scene.EffectiveSource.OutputColorSpace);
        shaderFormat = native.ShaderViewFormat; targetFormat = native.TargetViewFormat;
    }

    public IOriginalMapPostEffect3ComputeExternalTextureLease CaptureCurrentRead(int actualLogicalParameter)
    {
        world.RequireCurrent();
        var underlying = world.LeaseWorkingScene(actualLogicalParameter);
        lock (lifetime)
        {
            RequireComplete();
            return new SceneRead(this, table.Require(actualLogicalParameter), underlying, revision);
        }
    }

    public IOriginalMapPostEffect3StockGraphicsTargetLease CaptureActualAddBloomTarget(
        OriginalMapPostEffect3Composite2BeforeBindings actualBefore,
        IOriginalMapPostEffect3FullscreenDrawContextLease actualPostBindingContext)
    {
        ArgumentNullException.ThrowIfNull(actualBefore); ArgumentNullException.ThrowIfNull(actualPostBindingContext);
        world.RequireCurrent(); actualBefore.RequireCurrent();
        actualPostBindingContext.RequireCurrentForPass(actualBefore.SourcePassIndex);
        var color = table.Require(68);
        _ = world.LeaseWorkingScene(68);
        if (actualBefore.SourcePassIndex is not (98 or 99 or 100 or 101) ||
            !actualBefore.RequiresPriorTargetContents || actualBefore.Frame.FrameNumber != table.FrameNumber ||
            !ReferenceEquals(actualBefore.Frame.Allocation, world.Bank.Source) ||
            actualPostBindingContext.FrameNumber != table.FrameNumber ||
            actualPostBindingContext.ContextOwnerIdentity != world.Bank.Source.ContextOwnerIdentity ||
            actualPostBindingContext.OwnedFrontendIdentity != world.CurrentStock.OwnedFrontendIdentity ||
            actualPostBindingContext.ViewIdentity != world.CurrentStock.ViewIdentity)
            throw new InvalidDataException("External Scene target is not this actual AddBloom source binding.");
        return new SceneTarget(this, color, actualBefore, actualPostBindingContext);
    }

    /// <summary>Called before the next pass, using the owner's sealed current
    /// coverage. An incomplete draw stays invalid rather than reusing late
    /// World readiness after a shader has changed the physical image.</summary>
    public void AcceptAddBloomCoverage(OriginalMapPostEffect3Composite2GraphicsCoverageReceipt actualCoverage)
    {
        ArgumentNullException.ThrowIfNull(actualCoverage); world.RequireCurrent(); actualCoverage.RequireCurrentSubmission();
        if (actualCoverage.ActualTarget is not SceneTarget target || !ReferenceEquals(target.Owner, this))
            throw new InvalidDataException("AddBloom receipt belongs to another external Scene owner.");
        target.RequireCurrentForPass(actualCoverage.Completed.SourcePassIndex);
        var write = actualCoverage.Completed.Writes.SingleOrDefault(w => w.SourceTargetRegister == 0);
        lock (lifetime)
        {
            if (write is null || !actualCoverage.Completed.WasPixelDrawSubmitted || write.ParameterIndex != 68 ||
                write.PhysicalAllocationParameterIndex is not null || write.InstanceIndex != 0 ||
                write.StorageRid != scene.StorageRid || write.TargetViewRid != targetView ||
                write.NativeViewFormat != targetFormat || write.WriterRevision != revision ||
                !write.PriorDestinationPixelsWereValidated)
                throw new InvalidDataException("AddBloom receipt does not join its actual source Scene write and prior pixels.");
            complete = actualCoverage.HasCompleteTargetPixelDomain;
        }
    }

    private void RequireComplete()
    {
        if (!complete) throw new InvalidDataException("Controlled Scene generation has no complete current pixel producer.");
    }
    private sealed record SceneRead(OriginalMapPostEffect3WorldControlledColorOwner Owner,
        OriginalMapPostEffect3ResolvedTextureParameter Accepted, IOriginalMapPostEffect3ComputeExternalTextureLease Underlying,
        long Revision) : IOriginalMapPostEffect3ComputeExternalTextureLease, IOriginalMapPostEffect3ComputeMipTextureLease,
        IOriginalMapPostEffect3SharedSampledViewLease
    {
        public long FrameNumber => Underlying.FrameNumber;
        public string ContextOwnerIdentity => Underlying.ContextOwnerIdentity;
        public int ParameterIndex => Accepted.ParameterIndex;
        public int InstanceIndex => Underlying.InstanceIndex;
        public Rid StorageRid => Underlying.StorageRid;
        public Rid ShaderViewRid => Underlying.ShaderViewRid;
        public uint NativeViewFormat => Owner.shaderFormat;
        public RenderingDevice.DataFormat DeviceFormat => Underlying.DeviceFormat;
        public Vector3I Extent => Underlying.Extent;
        public string ProducerOwnerIdentity => Underlying.ProducerOwnerIdentity;
        public string SourceEvidence => Underlying.SourceEvidence + ";actual-controlled-Scene-content-generation=" + Revision;
        public void RequireCurrent()
        {
            Underlying.RequireCurrent(); Owner.table.ValidateCurrent(Accepted);
            lock (Owner.lifetime)
            {
                Owner.RequireComplete();
                if (Owner.revision != Revision) throw new InvalidDataException("Controlled Scene read predates the actual current shader write.");
            }
        }
        public void RequireReadableMipRange(uint firstMipLevel,uint mipLevelCount)
        {
            // OriginalRenderTargetFrame admits and allocates only the
            // effective single-mip attachment. RequireCurrent joins its
            // actual World submission or complete subsequent Scene writer
            // and rejects every older content generation.
            RequireCurrent();
            if(firstMipLevel!=0||mipLevelCount!=1||Owner.scene.EffectiveSource.LevelNum!=1||
                Owner.scene.EffectiveSource.MinMipLevel!=0||Accepted.Reference.HasSourceSelectedMipView)
                throw new NotSupportedException("Controlled World Scene proves only its actual complete unsliced mip0.");
        }
        public OriginalMapPostEffect3SharedSampledViewProof RequireSharedSampledViewProof()
        {
            RequireCurrent();
            return OriginalMapPostEffect3SharedSampledViewProof.CaptureWorkingScene(Owner.world.Context.OutputFrame,
                Owner.scene, RequireCurrent);
        }
    }
    private sealed record SceneTarget(OriginalMapPostEffect3WorldControlledColorOwner Owner,
        OriginalMapPostEffect3ResolvedTextureParameter Accepted, OriginalMapPostEffect3Composite2BeforeBindings Before,
        IOriginalMapPostEffect3FullscreenDrawContextLease Context)
        : IOriginalMapPostEffect3FullscreenExternalWritableTargetLease, IOriginalMapPostEffect3StockGraphicsReadableTargetLease
    {
        public long FrameNumber => Before.Frame.FrameNumber;
        public string ContextOwnerIdentity => Before.Frame.OwnerIdentity;
        public string OwnedFrontendIdentity => Context.OwnedFrontendIdentity;
        public string ViewIdentity => Context.ViewIdentity;
        public int? ParameterIndex => 68;
        public int InstanceIndex => Accepted.Reference.InstanceIndex;
        public string ProducerOwnerIdentity => Accepted.Reference.ProducerOwnerIdentity;
        public Rid StorageRid => Owner.scene.StorageRid;
        public Rid TargetViewRid => Owner.targetView;
        public uint NativeViewFormat => Owner.targetFormat;
        public RenderingDevice.DataFormat DeviceFormat => Owner.scene.TargetFormat;
        public Vector2I Extent => Owner.scene.ExplicitExtent;
        public string SourceEvidence => Before.SourceEvidence + ";actual-World-Scene68-external-AddBloom-target;" + Context.SourceEvidence;
        public void RequireCurrentForPass(int pass)
        {
            Owner.world.RequireCurrent(); Before.RequireCurrent(); Owner.table.ValidateCurrent(Accepted);
            Context.RequireCurrentForPass(pass);
            if (pass != Before.SourcePassIndex || Accepted.Reference.PhysicalAllocationParameterIndex is not null ||
                Accepted.Reference.ProducerOwnerIdentity != Owner.scene.EffectiveSource.OwnerIdentity ||
                Accepted.Reference.InstanceIndex != 0)
                throw new InvalidDataException("AddBloom target no longer selects its actual current external Scene wrapper.");
        }
        public void RequireReadableForPass(int pass)
        {
            RequireCurrentForPass(pass); lock (Owner.lifetime) Owner.RequireComplete();
        }
        public long InvalidateContentsBeforePixelWrite(int pass)
        {
            RequireCurrentForPass(pass);
            lock (Owner.lifetime) { Owner.complete = false; return Owner.revision = checked(Owner.revision + 1); }
        }
    }
}
