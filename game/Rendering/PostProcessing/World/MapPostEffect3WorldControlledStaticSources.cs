using San14.Data;

/// <summary>One verified static source generation. All current frames,
/// callbacks, tables, recipes, CB bytes and device receipts stay outside.</summary>
internal sealed class MapPostEffect3WorldControlledStaticSources
{
    private readonly GameResourceRepository repository;
    private readonly string sourceDirectory;
    private readonly string? overrideDirectory;
    private bool retired;
    internal LoadedDeferredSamplerNativeSource Native{get;}
    internal MapPostEffect3FreshStockDrawSource Stock{get;}
    internal MapPostEffect3NativeFullscreenFrontendSource NativeFrontend{get;}
    internal LoadedMapPostEffect3EyeGraphicsStateSource EyeGraphics{get;}
    internal LoadedMapPostEffect3InputDepthSource InputDepth{get;}
    internal LoadedMapPostEffect3EyeExposureInitializationSource ExposureInitialization{get;}
    internal LoadedMapPostEffect3EyeComputeExecutionSource EyeCompute{get;}
    internal LoadedMapPostEffect3PassRegisterResetSource RegisterReset{get;}
    internal LoadedDeferredAuxiliaryStockCopySource LensAfterStock{get;}
    internal LoadedMapPostEffect3LensFlare2ControllerTextureSource LensController{get;}
    internal LoadedMapPostEffect3LensFlare2Source Lens{get;}
    internal LoadedMapPostEffect3DepthOfField2Source DepthOfField{get;}
    internal LoadedMapPostEffect3EyeAdaptation2Source Eye{get;}
    internal LoadedMapPostEffect3Composite2Source Composite{get;}
    internal LoadedMapPostEffect3Composite2ExecutionSource CompositeExecution{get;}
    internal LoadedNativeDoubleLogSource DoubleLog{get;}
    internal LoadedMapPostEffect3Functor1FactoryReturnSource FactoryReturn{get;}
    internal LoadedMapPostEffect3Functor1WrapperSource Wrappers{get;}
    internal LoadedMapPostEffect3Functor1FrontendCursorSource Cursor{get;}
    internal LoadedMapPostEffect3Functor1FrontendLifetimeSource Lifetime{get;}
    internal LoadedMapPostEffect3TextureZeroAllocationSource TextureZero{get;}
    internal LoadedMapPostEffect3BeginFunctorCounterSource BeginCounter{get;}
    internal LoadedMapPostEffect3Functor1ExitSource Exit{get;}
    internal LoadedMapPostEffect3ControlledApplyHistorySource ApplyHistory{get;}
    private MapPostEffect3WorldControlledStaticSources(GameResourceRepository repo,string locale)
    {
        repository=repo;sourceDirectory=repo.SourceDirectory;overrideDirectory=repo.OverrideDirectory;
        Native=RenderSamplerSourceAdapters.Load(repo.SourceDirectory,locale);
        Stock=RenderSamplerSourceAdapters.ReadFreshPostEffect3StockDrawSource(Native);
        NativeFrontend=RenderSamplerSourceAdapters.ReadNativeFullscreenFrontendSource(Native);
        EyeGraphics=MapPostEffect3EyeGraphicsStateSource.Load(repo,locale);
        InputDepth=MapPostEffect3InputDepthSource.Load(repo,locale);
        ExposureInitialization=MapPostEffect3EyeExposureInitializationSource.Load(repo,locale);
        EyeCompute=MapPostEffect3EyeComputeExecutionSource.Load(repo,locale);
        RegisterReset=MapPostEffect3PassRegisterResetSource.Load(repo,locale);
        LensAfterStock=DeferredAuxiliaryStockCopySource.ReadCurrent(repo.SourceDirectory,locale);
        LensController=MapPostEffect3LensFlare2ControllerTextureSource.Load(repo,locale);
        Lens=MapPostEffect3ParameterLensFlare2.Load(repo,locale);
        DepthOfField=MapPostEffect3ParameterDepthOfField2.Load(repo,locale);
        Eye=MapPostEffect3ParameterEyeAdaptation2.Load(repo,locale);
        Composite=MapPostEffect3ParameterComposite2.Load(repo,locale);
        CompositeExecution=MapPostEffect3Composite2ExecutionSource.Load(repo,locale);
        DoubleLog=LoadedNativeDoubleLogSource.Load(repo,locale);
        FactoryReturn=MapPostEffect3Functor1FactoryReturnSource.Load(repo,Native,Eye,DoubleLog);
        Wrappers=MapPostEffect3Functor1WrapperSource.Load(repo,locale);
        Cursor=MapPostEffect3Functor1FrontendCursorSource.Load(repo,locale);
        Lifetime=MapPostEffect3Functor1FrontendLifetimeSource.Load(repo,locale);
        TextureZero=MapPostEffect3TextureZeroAllocationSource.Load(repo,locale);
        BeginCounter=MapPostEffect3BeginFunctorCounterSource.Load(repo,locale);
        Exit=MapPostEffect3Functor1ExitSource.Load(repo,locale);
        ApplyHistory=MapPostEffect3ControlledApplyHistorySource.Load(repo,locale);
        // Publish only a completely verified generation. Every native loader
        // pins the same full executable/decoded version; no GPU owner exists
        // during this constructor, including its failure paths.
        RequireRepository(repo);
    }
    internal static MapPostEffect3WorldControlledStaticSources Load(GameResourceRepository repo,string locale)=>new(repo,locale);
    internal void RequireRepository(GameResourceRepository actual)
    {
        ObjectDisposedException.ThrowIf(retired,this);
        if(!ReferenceEquals(repository,actual)||sourceDirectory!=actual.SourceDirectory||overrideDirectory!=actual.OverrideDirectory)
            throw new InvalidDataException("Static factory sources belong to another repository/native generation.");
    }
    internal void Retire()=>retired=true;
}
