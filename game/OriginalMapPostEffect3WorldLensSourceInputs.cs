using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Current World/Middle producers for the original Lens family.
/// Every external alias retains its actual submitted Scene/depth/noise/light
/// producer; no substitute image or sampled-wrapper sampler is allocated.</summary>
public sealed class OriginalMapPostEffect3WorldLensSourceInputs:IOriginalMapPostEffect3LensCurrentSourceInputs
{
    private readonly OriginalMapPostEffect3FirstWorldExecutionOwner first;
    private readonly OriginalMapPostEffect3FirstWorldPreparedPrefix prefix;
    private readonly OriginalMapPostEffect3MiddleExecutor middle;
    private readonly OriginalMapPostEffect3LensExecutionOwner lens;
    private readonly OriginalMapPostEffect3RetainedModuleInvocation invocation;
    private readonly OriginalMapFirstWorldNativeSamplerState.OwnedDrawContext draw;
    public OriginalMapPostEffect3WorldLensSourceInputs(OriginalMapPostEffect3FirstWorldExecutionOwner firstWorld,
        OriginalMapPostEffect3FirstWorldPreparedPrefix actualPrefix,OriginalMapPostEffect3MiddleExecutor actualMiddle,
        OriginalMapPostEffect3LensExecutionOwner actualLens)
    {
        (first,prefix,middle,lens)=(firstWorld,actualPrefix,actualMiddle,actualLens);
        ArgumentNullException.ThrowIfNull(first);ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(middle);ArgumentNullException.ThrowIfNull(lens);
        invocation=first.CurrentRetainedInvocation??throw new InvalidDataException("Lens source inputs need the current retained module invocation.");
        draw=first.NativeSamplers.CaptureOwnedDrawContext(prefix);RequireCurrent();
    }
    public void RequireCurrent()
    {
        prefix.RequireCurrent();middle.RequireFinishedMiddlePasses();draw.RequireCurrent();
        invocation.RequireModule(first.Bank.Source);invocation.RequireInitializedFrame(prefix.Parameters);
        if(!ReferenceEquals(first.CurrentPrefix,prefix)||!ReferenceEquals(first.Bank,lens.Bank)||
            !ReferenceEquals(first.CurrentRetainedInvocation,invocation)||invocation.FrameNumber!=prefix.Context.FrameNumber||
            !ReferenceEquals(first.Bank,middle.Compute.Bank)||!ReferenceEquals(middle.Parameters,prefix.Parameters)||
            !ReferenceEquals(middle.Textures,prefix.Textures))
            throw new InvalidDataException("Lens source inputs require this actual finished Middle/World/module generation.");
    }

    public IOriginalMapPostEffect3ComputeTextureLease RequireReadableTexture(
        OriginalMapPostEffect3LensExecutionRecipe recipe,int parameter,OriginalMapPostEffect3ResolvedTextureParameter accepted)
    {
        RequireCurrent();
        if(recipe.FrameNumber!=prefix.Context.FrameNumber||recipe.ContextOwnerIdentity!=first.Bank.Source.ContextOwnerIdentity||
            recipe.SourceSha256!=first.Bank.Source.Source.Sha256||accepted.ParameterIndex!=parameter)
            throw new InvalidDataException("Lens external input belongs to another actual source/module/frame.");
        return ReadActualTexture(parameter,accepted);
    }

    internal IOriginalMapPostEffect3ComputeTextureLease ReadActualTexture(int parameter,
        OriginalMapPostEffect3ResolvedTextureParameter? supplied=null)
    {
        RequireCurrent();var accepted=supplied??prefix.Textures.Require(parameter);prefix.Textures.ValidateCurrent(accepted);
        var reference=accepted.Reference;
        if(reference.PhysicalAllocationParameterIndex is int physical)
        {
            var texture=first.Bank.RequireTexture(physical,reference.InstanceIndex);
            return first.Bank.CaptureSourceWrittenTextureAliasLease(prefix.Textures,parameter,prefix.Context.FrameNumber,
                0,checked((uint)texture.Shape.MipLevels));
        }
        var scene=prefix.Context.OutputFrame.Textures[0x064B746B].EffectiveSource;
        if(reference.ProducerOwnerIdentity==scene.OwnerIdentity)return prefix.WorldStockScope.LeaseWorkingScene(parameter);
        var depth=prefix.Context.TerrainFrame.Textures[0xE13DDB4F].EffectiveSource;
        if(reference.ProducerOwnerIdentity==depth.OwnerIdentity)return prefix.WorldStockScope.LeaseInputDepth(prefix.InputDepthSource,parameter);
        IOriginalMapPostEffect3ComputeExternalTextureLease actual;
        if(reference.ProducerOwnerIdentity==prefix.InputNoiseSource.OwnerIdentity)actual=prefix.InputNoiseSource.Lease();
        else if(prefix.InputLightSceneSource is{} light&&reference.ProducerOwnerIdentity==light.OwnerIdentity)actual=light.Lease();
        else throw new NotSupportedException($"Actual Lens/Composite texture{parameter} needs its unresolved external producer '{reference.ProducerOwnerIdentity}'.");
        if(actual.FrameNumber!=prefix.Context.FrameNumber||actual.ContextOwnerIdentity!=first.Bank.Source.ContextOwnerIdentity||
            actual.ProducerOwnerIdentity!=reference.ProducerOwnerIdentity||actual.InstanceIndex!=reference.InstanceIndex)
            throw new InvalidDataException("Copied Lens external alias does not retain its actual producer/frame/side.");
        return actual.ParameterIndex==parameter?actual:new CopiedExternalAlias(this,accepted,actual);
    }

    public IOriginalMapPostEffect3ComputeSamplerLease RequireWrapperSampler(
        OriginalMapPostEffect3LensExecutionRecipe recipe,int stage,uint register)
    {
        RequireCurrent();
        // A shader-visible manager selection is supplied by the original
        // Before writer. Unknown sampled-wrapper states cannot become defaults.
        if(recipe.DescriptorTextureSubmissions.Any(s=>s.NativeStageCode==stage&&s.RegisterIndex==register&&
            s.DescriptorTextureOffset is 0x30 or 0x38))return lens.LeaseCurrentManagerSampler(recipe,stage,register);
        throw new NotSupportedException($"Lens declared sampler stage{stage}/s{register} has no actual current native wrapper producer.");
    }

    public OriginalMapPostEffect3LensGhostFrameInputs PrepareGhostDraw(
        OriginalMapPostEffect3LensExecutionRecipe recipe,IOriginalMapPostEffect3ComputeBufferLease sprite,
        IOriginalMapPostEffect3ComputeTextureLease star)
    {
        RequireCurrent();
        var pixel=first.Bank.Source.Source.GetProgram(0,2);var declared=DxbcResourceDefinitionReader.Read(pixel.Bytes.Span).Bindings;
        var samplers=declared.Where(b=>b.InputType==3).Select(b=>
        {
            if(b.BindCount!=1)throw new NotSupportedException("Original Ghost sampler array has no current producer.");
            return RequireWrapperSampler(recipe,2,b.BindPoint);
        }).ToArray();
        // Native Before writes its8x8 manager wrapper pointers even when the
        // selected PS has no t7/t8 declaration. Such unconsumed pointers are
        // not invented texture inputs or device descriptors.
        foreach(var input in declared.Where(b=>b.InputType==2&&b.BindPoint!=0))
            throw new NotSupportedException($"Original Ghost PS reads t{input.BindPoint} without its source descriptor texture producer.");
        var target=lens.CaptureCurrentGhostTarget(recipe,prefix.Textures);
        return draw.PrepareGhostDrawInputs(recipe,sprite,star,Array.AsReadOnly(samplers),
            Array.Empty<OriginalMapPostEffect3LensDescriptorTextureInput>(),
            invocation.ConstantBufferInstances[6],target);
    }

    private sealed record CopiedExternalAlias(OriginalMapPostEffect3WorldLensSourceInputs Owner,
        OriginalMapPostEffect3ResolvedTextureParameter Accepted,IOriginalMapPostEffect3ComputeExternalTextureLease Producer)
        :IOriginalMapPostEffect3ComputeExternalTextureLease
    {
        public long FrameNumber=>Producer.FrameNumber;
        public string ContextOwnerIdentity=>Producer.ContextOwnerIdentity;
        public int ParameterIndex=>Accepted.ParameterIndex;
        public int InstanceIndex=>Producer.InstanceIndex;
        public Rid ShaderViewRid=>Producer.ShaderViewRid;
        public Rid StorageRid=>Producer.StorageRid;
        public uint NativeViewFormat=>Producer.NativeViewFormat;
        public RenderingDevice.DataFormat DeviceFormat=>Producer.DeviceFormat;
        public Vector3I Extent=>Producer.Extent;
        public string ProducerOwnerIdentity=>Producer.ProducerOwnerIdentity;
        public string SourceEvidence=>Accepted.Reference.SourceEvidence+";actual-copied-external-wrapper;"+Producer.SourceEvidence;
        public void RequireCurrent()
        {
            Owner.RequireCurrent();Owner.prefix.Textures.ValidateCurrent(Accepted);Producer.RequireCurrent();
            if(Accepted.Reference.PhysicalAllocationParameterIndex is not null||
                Accepted.Reference.ProducerOwnerIdentity!=ProducerOwnerIdentity||Accepted.Reference.InstanceIndex!=InstanceIndex)
                throw new InvalidDataException("Actual external Lens alias no longer copies this exact current wrapper producer.");
        }
    }
}
