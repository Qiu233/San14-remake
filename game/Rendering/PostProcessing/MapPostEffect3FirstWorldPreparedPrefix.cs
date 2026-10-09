using San14.Data;

/// <summary>Live products of one actual first World source preparation and
/// its bounded Shaft prefix. Borrowers retain the execution owner until their
/// submissions finish. This publishes no complete Functor/history or complete
/// image-coverage claim; later reads still need their source coverage leases.</summary>
public sealed class MapPostEffect3FirstWorldPreparedPrefix
{
    private readonly MapPostEffect3FirstWorldExecutionOwner owner;
    internal MapPostEffect3FirstWorldPreparedPrefix(MapPostEffect3FirstWorldExecutionOwner owner,
        NativeMapFrameContext context,MapPostEffect3OwnedFreshCameraFrame parameters,
        MapPostEffect3TextureParameterState textures,MapPostEffect3WorldStockGraphicsSourceOwner resources,
        MapPostEffect3FirstWorldShaftResult result,int cbSide,
        MapPostEffect3BeginFunctorCounterReceipt begin,LoadedMapPostEffect3InputDepthSource depth,
        MapPostEffect3InputDepthSamplerOwner? actualDepthSampler,
        MapPostEffect3WorldCompositeSourceOwner.ParentBinding frontendParent,
        MapPostEffect3WorldInputNoiseSourceOwner noiseSource,
        MapPostEffect3WorldInputLightSceneSourceOwner? lightSceneSource)
        =>(this.owner,Context,Parameters,Textures,WorldStockScope,Result,ActualShaftConstantBufferInstance,Begin,
            InputDepthSource,ActualDepthSampler,FrontendParentBinding,InputNoiseSource,InputLightSceneSource)=(owner,context,parameters,textures,resources,result,cbSide,begin,depth,actualDepthSampler,frontendParent,noiseSource,lightSceneSource);
    public NativeMapFrameContext Context{get;}
    public MapPostEffect3OwnedFreshCameraFrame Parameters{get;}
    public MapPostEffect3TextureParameterState Textures{get;}
    public MapPostEffect3WorldStockGraphicsSourceOwner WorldStockScope{get;}
    public MapPostEffect3FirstWorldShaftResult Result{get;}
    public MapPostEffect3ComputeCompletedPass? ActualFirstComputePass1=>Result.ActualDispatch;
    public int ActualShaftConstantBufferInstance{get;}
    public MapPostEffect3BeginFunctorCounterReceipt Begin{get;}
    public LoadedMapPostEffect3InputDepthSource InputDepthSource{get;}
    public MapPostEffect3InputDepthSamplerOwner? ActualDepthSampler{get;}
    public MapPostEffect3WorldCompositeSourceOwner.ParentBinding FrontendParentBinding{get;}
    public MapPostEffect3WorldInputNoiseSourceOwner InputNoiseSource{get;}
    public MapPostEffect3WorldInputLightSceneSourceOwner? InputLightSceneSource{get;}
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
