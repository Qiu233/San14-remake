using San14.Data;

/// <summary>The first normal native session prepares WORK from this request's
/// current season and registered default-caller light. Later animation is native.</summary>
public static class OriginalD3D11AmbientWorkInitialConstants
{
    public static OriginalD3D11AmbientWorkConstants WriteFreshNativeInputs(OriginalGameRepository repository,
        OriginalD3D11CurrentSeasonFrame current,OriginalD3D11CurrentSeasonResources resources,string inputRoot)
    {
        var selected=resources.Lights.First(light=>(light.GroupMask&0x80)!=0);
        var constants=OriginalD3D11AmbientWorkInputPreparation.Prepare(repository,current.Present,selected.DefaultCaller);
        OriginalD3D11AmbientWorkInputPreparation.WriteNativeInputs(repository,constants,inputRoot,resources.Source.Locale);
        return constants;
    }
}
