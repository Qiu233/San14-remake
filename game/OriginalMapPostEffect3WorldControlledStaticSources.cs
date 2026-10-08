using San14.Data;

/// <summary>One verified static source generation. All current frames,
/// callbacks, tables, recipes, CB bytes and device receipts stay outside.</summary>
internal sealed class OriginalMapPostEffect3WorldControlledStaticSources
{
    private readonly OriginalGameRepository repository;
    private readonly string sourceDirectory;
    private readonly string? overrideDirectory;
    private bool retired;
    internal LoadedOriginalDeferredSamplerNativeSource Native{get;}
    internal OriginalMapPostEffect3FreshStockDrawSource Stock{get;}
    internal OriginalMapPostEffect3NativeFullscreenFrontendSource NativeFrontend{get;}
    internal LoadedOriginalMapPostEffect3EyeGraphicsStateSource EyeGraphics{get;}
    internal LoadedOriginalMapPostEffect3InputDepthSource InputDepth{get;}
    internal LoadedOriginalMapPostEffect3EyeExposureInitializationSource ExposureInitialization{get;}
    internal LoadedOriginalMapPostEffect3EyeComputeExecutionSource EyeCompute{get;}
    internal LoadedOriginalMapPostEffect3PassRegisterResetSource RegisterReset{get;}
    internal LoadedOriginalDeferredAuxiliaryStockCopySource LensAfterStock{get;}
    internal LoadedOriginalMapPostEffect3LensFlare2ControllerTextureSource LensController{get;}
    internal LoadedOriginalMapPostEffect3LensFlare2Source Lens{get;}
    internal LoadedOriginalMapPostEffect3DepthOfField2Source DepthOfField{get;}
    internal LoadedOriginalMapPostEffect3EyeAdaptation2Source Eye{get;}
    internal LoadedOriginalMapPostEffect3Composite2Source Composite{get;}
    internal LoadedOriginalMapPostEffect3Composite2ExecutionSource CompositeExecution{get;}
    internal LoadedOriginalNativeDoubleLogSource DoubleLog{get;}
    internal LoadedOriginalMapPostEffect3Functor1FactoryReturnSource FactoryReturn{get;}
    internal LoadedOriginalMapPostEffect3Functor1WrapperSource Wrappers{get;}
    internal LoadedOriginalMapPostEffect3Functor1FrontendCursorSource Cursor{get;}
    internal LoadedOriginalMapPostEffect3Functor1FrontendLifetimeSource Lifetime{get;}
    internal LoadedOriginalMapPostEffect3TextureZeroAllocationSource TextureZero{get;}
    internal LoadedOriginalMapPostEffect3BeginFunctorCounterSource BeginCounter{get;}
    internal LoadedOriginalMapPostEffect3Functor1ExitSource Exit{get;}
    internal LoadedOriginalMapPostEffect3ControlledApplyHistorySource ApplyHistory{get;}
    private OriginalMapPostEffect3WorldControlledStaticSources(OriginalGameRepository repo,string locale)
    {
        repository=repo;sourceDirectory=repo.SourceDirectory;overrideDirectory=repo.OverrideDirectory;
        Native=OriginalDeferredSamplerSources.Load(repo.SourceDirectory,locale);
        Stock=OriginalDeferredSamplerSources.ReadFreshPostEffect3StockDrawSource(Native);
        NativeFrontend=OriginalDeferredSamplerSources.ReadNativeFullscreenFrontendSource(Native);
        EyeGraphics=OriginalMapPostEffect3EyeGraphicsStateSource.Load(repo,locale);
        InputDepth=OriginalMapPostEffect3InputDepthSource.Load(repo,locale);
        ExposureInitialization=OriginalMapPostEffect3EyeExposureInitializationSource.Load(repo,locale);
        EyeCompute=OriginalMapPostEffect3EyeComputeExecutionSource.Load(repo,locale);
        RegisterReset=OriginalMapPostEffect3PassRegisterResetSource.Load(repo,locale);
        LensAfterStock=OriginalDeferredAuxiliaryStockCopySource.ReadCurrent(repo.SourceDirectory,locale);
        LensController=OriginalMapPostEffect3LensFlare2ControllerTextureSource.Load(repo,locale);
        Lens=OriginalMapPostEffect3ParameterLensFlare2.Load(repo,locale);
        DepthOfField=OriginalMapPostEffect3ParameterDepthOfField2.Load(repo,locale);
        Eye=OriginalMapPostEffect3ParameterEyeAdaptation2.Load(repo,locale);
        Composite=OriginalMapPostEffect3ParameterComposite2.Load(repo,locale);
        CompositeExecution=OriginalMapPostEffect3Composite2ExecutionSource.Load(repo,locale);
        DoubleLog=LoadedOriginalNativeDoubleLogSource.Load(repo,locale);
        FactoryReturn=OriginalMapPostEffect3Functor1FactoryReturnSource.Load(repo,Native,Eye,DoubleLog);
        Wrappers=OriginalMapPostEffect3Functor1WrapperSource.Load(repo,locale);
        Cursor=OriginalMapPostEffect3Functor1FrontendCursorSource.Load(repo,locale);
        Lifetime=OriginalMapPostEffect3Functor1FrontendLifetimeSource.Load(repo,locale);
        TextureZero=OriginalMapPostEffect3TextureZeroAllocationSource.Load(repo,locale);
        BeginCounter=OriginalMapPostEffect3BeginFunctorCounterSource.Load(repo,locale);
        Exit=OriginalMapPostEffect3Functor1ExitSource.Load(repo,locale);
        ApplyHistory=OriginalMapPostEffect3ControlledApplyHistorySource.Load(repo,locale);
        // Publish only a completely verified generation. Every native loader
        // pins the same full executable/decoded version; no GPU owner exists
        // during this constructor, including its failure paths.
        RequireRepository(repo);
    }
    internal static OriginalMapPostEffect3WorldControlledStaticSources Load(OriginalGameRepository repo,string locale)=>new(repo,locale);
    internal void RequireRepository(OriginalGameRepository actual)
    {
        ObjectDisposedException.ThrowIf(retired,this);
        if(!ReferenceEquals(repository,actual)||sourceDirectory!=actual.SourceDirectory||overrideDirectory!=actual.OverrideDirectory)
            throw new InvalidDataException("Static factory sources belong to another repository/native generation.");
    }
    internal void Retire()=>retired=true;
}
