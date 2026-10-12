using San14.Data;

/// <summary>The first normal native session prepares WORK from this request's
/// current season and registered default-caller light. Later animation is native.</summary>
public static class D3D11AmbientWorkInitialConstants
{
    public static D3D11AmbientWorkConstants Prepare(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current,D3D11CurrentSeasonResources resources,D3D11CloudProgramMedia programs,
        LoadedSkyAmbientResources ambient, SkyMainProgramResources? skyPrograms = null)
    {
        var selected=resources.Lights.First(light=>(light.GroupMask&0x80)!=0);
        return D3D11AmbientWorkInputPreparation.Prepare(repository,current.Present,selected.DefaultCaller,programs.Resources,ambient,skyPrograms);
    }

    public static D3D11AmbientWorkConstants WriteFreshNativeInputs(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current,D3D11CurrentSeasonResources resources,string inputRoot,D3D11CloudProgramMedia programs)
    {
        var ambient=D3D11AmbientRenderResources.Load(repository,current.Present,resources.Source.Locale);
        ambient.WriteNativeInputs(inputRoot);
        var constants=Prepare(repository,current,resources,programs,ambient.Declaration);
        D3D11AmbientWorkInputPreparation.WriteNativeInputs(constants,inputRoot);
        return constants;
    }
}
