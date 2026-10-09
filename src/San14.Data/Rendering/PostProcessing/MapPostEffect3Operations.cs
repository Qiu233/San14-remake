namespace San14.Data;

public enum MapPostEffect3ConstantBufferWriter
{
    TemporalAA,BloomLoop,BloomMain,Exposure,CompositeLegacy,LensLegacy,LightShaft,
    DepthOfFieldLegacy,MotionBlur,Lens2,DepthOfField2,Composite2,ToneInfo,LightInfo
}
public enum MapPostEffect3ResourceSetter {ShaderResource,UnorderedAccess,RenderTarget}
public enum MapPostEffect3BufferWriter {LensInitialization,Compute}
public enum MapPostEffect3PassWrapper {Standard,EyeRepeat}
public enum MapPostEffect3Hook
{
    AlwaysTrue,PrepareMiddle,InitializeLightShaft,InitializeDepthOfFieldLegacy,
    InitializeMotionBlur,InitializeLensLegacy,InitializeCompositeLegacy,
    BeforeTemporalAA,BeforeMiddle,BeforeLensLegacy,ExecuteLensLegacy,
    PrepareLens2,InitializeLens2,BeforeLens2,ExecuteLens2,AfterLens2,
    PrepareDepthOfField2,InitializeDepthOfField2,FinishDepthOfField2,BeforeDepthOfField2,ExecuteDepthOfField2,
    PrepareEye,InitializeEye,BeforeEye,ExecuteEye,
    PrepareComposite2,InitializeComposite2,BeforeComposite2,ExecuteComposite2
}

public enum MapPostEffect3ChildOperation
{
    LensConstants,LensTextures,DofShape,DofConstants,DofInitialize,
    ExposureConstants,BloomConstants,BloomLevels,EyeInitialize,CachedScalarLookup,
    CompositeConstants,ToneInfo,LightInfo,CompositeInitialize
}
