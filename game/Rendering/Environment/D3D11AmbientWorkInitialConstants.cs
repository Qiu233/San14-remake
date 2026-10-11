using San14.Data;

/// <summary>The first normal native session prepares WORK from this request's
/// current season and registered default-caller light. Later animation is native.</summary>
public static class D3D11AmbientWorkInitialConstants
{
    public static D3D11AmbientWorkConstants Prepare(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current,D3D11CurrentSeasonResources resources,D3D11CloudProgramMedia programs)
    {
        var selected=resources.Lights.First(light=>(light.GroupMask&0x80)!=0);
        return D3D11AmbientWorkInputPreparation.Prepare(repository,current.Present,selected.DefaultCaller,programs.Resources);
    }

    public static D3D11AmbientWorkConstants WriteFreshNativeInputs(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current,D3D11CurrentSeasonResources resources,string inputRoot,D3D11CloudProgramMedia programs)
    {
        var constants=Prepare(repository,current,resources,programs);
        D3D11AmbientWorkInputPreparation.WriteNativeInputs(repository,constants,inputRoot,resources.Source.Locale);
        return constants;
    }
}
