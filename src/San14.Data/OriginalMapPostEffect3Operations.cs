namespace San14.Data;

public enum OriginalMapPostEffect3ConstantBufferWriter
{
    TemporalAA,BloomLoop,BloomMain,Exposure,CompositeLegacy,LensLegacy,LightShaft,
    DepthOfFieldLegacy,MotionBlur,Lens2,DepthOfField2,Composite2,ToneInfo,LightInfo
}
public enum OriginalMapPostEffect3ResourceSetter {ShaderResource,UnorderedAccess,RenderTarget}
public enum OriginalMapPostEffect3BufferWriter {LensInitialization,Compute}
public enum OriginalMapPostEffect3PassWrapper {Standard,EyeRepeat}
public enum OriginalMapPostEffect3Hook
{
    AlwaysTrue,PrepareMiddle,InitializeLightShaft,InitializeDepthOfFieldLegacy,
    InitializeMotionBlur,InitializeLensLegacy,InitializeCompositeLegacy,
    BeforeTemporalAA,BeforeMiddle,BeforeLensLegacy,ExecuteLensLegacy,
    PrepareLens2,InitializeLens2,BeforeLens2,ExecuteLens2,AfterLens2,
    PrepareDepthOfField2,InitializeDepthOfField2,FinishDepthOfField2,BeforeDepthOfField2,ExecuteDepthOfField2,
    PrepareEye,InitializeEye,BeforeEye,ExecuteEye,
    PrepareComposite2,InitializeComposite2,BeforeComposite2,ExecuteComposite2
}

public enum OriginalMapPostEffect3ChildOperation
{
    LensConstants,LensTextures,DofShape,DofConstants,DofInitialize,
    ExposureConstants,BloomConstants,BloomLevels,EyeInitialize,CachedScalarLookup,
    CompositeConstants,ToneInfo,LightInfo,CompositeInitialize
}
