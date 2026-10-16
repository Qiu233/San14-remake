using San14.Data;

/// <summary>Adapts the actual first current season frame to complete fresh
/// atmosphere constants. It advances neither World nor renderer history.</summary>
public static class AtmosphereInitialConstantsWriter
{
    public static San14.Data.AtmosphereInitialConstants WriteFreshNativeInputs(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current,SceneColorTargetResources target,float cloudElapsedSeconds,float commonClock,
        string atmosphereDirectory,string mainSkyDirectory,string mainCloudDirectory,string fogDirectory,
        D3D11CloudProgramMedia programs,D3D11CloudRenderResources resources,
        SkyMainProgramResources skyPrograms, Sky2TextureBindingSnapshot sampling,
        HeightFogProgramResources? fogPrograms = null)
    {
        var options=new D3D11AtmosphereInitialOptions(target,
            D3D11AtmosphereInitialCallerRules.FreshMainCloudClip,cloudElapsedSeconds,commonClock);
        return WriteNativeInputs(repository,current,options,atmosphereDirectory,mainSkyDirectory,mainCloudDirectory,fogDirectory,programs,resources,skyPrograms,sampling,fogPrograms);
    }

    public static San14.Data.AtmosphereInitialConstants Prepare(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current,D3D11AtmosphereInitialOptions options,
        D3D11CloudProgramMedia programs,D3D11CloudRenderResources resources,
        SkyMainProgramResources skyPrograms, Sky2TextureBindingSnapshot sampling,
        HeightFogProgramResources? fogPrograms = null)
    {
        var inputs = CreateInputs(current, options);
        var cloud = D3D11AtmosphereInitialConstantPreparation.PrepareCloudConstants(
            inputs, programs, resources);
        return D3D11AtmosphereInitialConstantPreparation.Prepare(repository, inputs, cloud,skyPrograms,sampling,fogPrograms);
    }

    public static D3D11MainCloudConstants PrepareCloud(D3D11CurrentSeasonFrame current,
        D3D11AtmosphereInitialOptions options, D3D11CloudProgramMedia programs,
        D3D11CloudRenderResources resources)
        => D3D11AtmosphereInitialConstantPreparation.PrepareCloudConstants(
            CreateInputs(current, options), programs, resources);

    private static D3D11AtmosphereInitialInputs CreateInputs(D3D11CurrentSeasonFrame current,
        D3D11AtmosphereInitialOptions options)
    {
        return new D3D11AtmosphereInitialInputs(current.Present,
            current.PreparedWords.ToDictionary(word=>word.Offset,word=>word.Bits),
            current.Lights.Select(light=>new D3D11AtmosphereInitialLight(light.GroupMask,light.State)).ToArray(),
            current.Atmosphere.Controller,current.Atmosphere.Sun,current.Atmosphere.Moon,current.Atmosphere.SunRgb,current.Atmosphere.MoonRgb,
            current.Atmosphere.ControllerGeometry40,options,current.HeightFog);
    }

    public static San14.Data.AtmosphereInitialConstants WriteNativeInputs(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current,D3D11AtmosphereInitialOptions options,
        string atmosphereDirectory,string mainSkyDirectory,string mainCloudDirectory,string fogDirectory,
        D3D11CloudProgramMedia programs,D3D11CloudRenderResources resources,
        SkyMainProgramResources skyPrograms, Sky2TextureBindingSnapshot sampling,
        HeightFogProgramResources? fogPrograms = null)
    {
        var constants=Prepare(repository,current,options,programs,resources,skyPrograms,sampling,fogPrograms);
        D3D11AtmosphereInitialConstantPreparation.WriteNativeInputs(constants,
            atmosphereDirectory,mainSkyDirectory,mainCloudDirectory,fogDirectory);
        return constants;
    }
}
