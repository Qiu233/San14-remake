using San14.Data;

/// <summary>The first normal native session prepares WORK from this request's
/// current season and registered default-caller light. Later animation is native.</summary>
public static class D3D11AmbientWorkInitialConstants
{
    public static D3D11AmbientWorkConstants Prepare(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current,D3D11CloudProgramMedia programs,
        LoadedSkyAmbientResources ambient, SkyMainProgramResources? skyPrograms = null,
        Sky2TextureBindingSnapshot? sampling = null)
    {
        var selected=current.Lights.First(light=>(light.GroupMask&0x80)!=0);
        if (selected.GroupMask != 0x80)
            throw new System.NotSupportedException("WORK requires the selected directional group 0x80.");
        var atmosphere=current.Atmosphere;
        return D3D11AmbientWorkInputPreparation.Prepare(repository,current.Present,selected.State,
            atmosphere.Controller,atmosphere.SunRgb,atmosphere.MoonRgb,atmosphere.ControllerGeometry40,
            programs.Resources,ambient,skyPrograms,sampling);
    }

    public static D3D11AmbientWorkConstants WriteFreshNativeInputs(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current,D3D11CurrentSeasonResources resources,string inputRoot,D3D11CloudProgramMedia programs,
        Sky2TextureBindingSnapshot? sampling = null)
    {
        var ambient=D3D11AmbientRenderResources.Load(repository,current.Present,resources.Source.Locale);
        ambient.WriteNativeInputs(inputRoot);
        var constants=Prepare(repository,current,programs,ambient.Declaration,sampling: sampling);
        D3D11AmbientWorkInputPreparation.WriteNativeInputs(constants,inputRoot);
        return constants;
    }
}
