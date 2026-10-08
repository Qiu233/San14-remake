using San14.Data;

/// <summary>Live products of one actual first World source preparation and
/// its bounded Shaft prefix. Borrowers retain the execution owner until their
/// submissions finish. This publishes no complete Functor/history or complete
/// image-coverage claim; later reads still need their source coverage leases.</summary>
public sealed class OriginalMapPostEffect3FirstWorldPreparedPrefix
{
    private readonly OriginalMapPostEffect3FirstWorldExecutionOwner owner;
    internal OriginalMapPostEffect3FirstWorldPreparedPrefix(OriginalMapPostEffect3FirstWorldExecutionOwner owner,
        OriginalNativeMapFrameContext context,OriginalMapPostEffect3OwnedFreshCameraFrame parameters,
        OriginalMapPostEffect3TextureParameterState textures,OriginalMapPostEffect3WorldStockGraphicsSourceOwner resources,
        OriginalMapPostEffect3FirstWorldShaftResult result,int cbSide,
        OriginalMapPostEffect3BeginFunctorCounterReceipt begin,LoadedOriginalMapPostEffect3InputDepthSource depth,
        OriginalMapPostEffect3InputDepthSamplerOwner? actualDepthSampler,
        OriginalMapPostEffect3WorldCompositeSourceOwner.ParentBinding frontendParent,
        OriginalMapPostEffect3WorldInputNoiseSourceOwner noiseSource,
        OriginalMapPostEffect3WorldInputLightSceneSourceOwner? lightSceneSource)
        =>(this.owner,Context,Parameters,Textures,WorldStockScope,Result,ActualShaftConstantBufferInstance,Begin,
            InputDepthSource,ActualDepthSampler,FrontendParentBinding,InputNoiseSource,InputLightSceneSource)=(owner,context,parameters,textures,resources,result,cbSide,begin,depth,actualDepthSampler,frontendParent,noiseSource,lightSceneSource);
    public OriginalNativeMapFrameContext Context{get;}
    public OriginalMapPostEffect3OwnedFreshCameraFrame Parameters{get;}
    public OriginalMapPostEffect3TextureParameterState Textures{get;}
    public OriginalMapPostEffect3WorldStockGraphicsSourceOwner WorldStockScope{get;}
    public OriginalMapPostEffect3FirstWorldShaftResult Result{get;}
    public OriginalMapPostEffect3ComputeCompletedPass? ActualFirstComputePass1=>Result.ActualDispatch;
    public int ActualShaftConstantBufferInstance{get;}
    public OriginalMapPostEffect3BeginFunctorCounterReceipt Begin{get;}
    public LoadedOriginalMapPostEffect3InputDepthSource InputDepthSource{get;}
    public OriginalMapPostEffect3InputDepthSamplerOwner? ActualDepthSampler{get;}
    public OriginalMapPostEffect3WorldCompositeSourceOwner.ParentBinding FrontendParentBinding{get;}
    public OriginalMapPostEffect3WorldInputNoiseSourceOwner InputNoiseSource{get;}
    public OriginalMapPostEffect3WorldInputLightSceneSourceOwner? InputLightSceneSource{get;}
    public void RequireCurrent(){owner.RequireCurrentPrefix(this);InputNoiseSource.RequireCurrent();}
    public IReadOnlyDictionary<string,int> ReadCurrentSourceFlags()
    {
        RequireCurrent();
        var flags=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(string name in new[]{"TemporalAAEnable","DofEnable","MotionBlurEnable","LensFlareEnable","LightShaftEnable","IntoSunLight"})
        {
            var value=Parameters.State.RequireValue(name);
            if(value.Length!=4)throw new InvalidDataException("Actual source effect flag has another native extent: "+name);
            flags.Add(name,System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(value.Span));
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string,int>(flags);
    }
}
