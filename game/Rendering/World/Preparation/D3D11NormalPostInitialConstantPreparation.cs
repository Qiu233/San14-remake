using San14.Data;

/// <summary>Join the actual same-frame native season owner to the ordinary
/// fresh Post3 numeric preparation. Called once before native session creation.</summary>
public static class D3D11NormalPostInitialConstantPreparation
{
    public static D3D11NormalPostInitialConstants WriteNativeInputs(
        GameResourceRepository repository,D3D11CurrentSeasonFrame current,
        string inputRoot,int outputWidth,int outputHeight)
    {
        D3D11CurrentSeasonLight? selected=current.ExposureLight.Present
            ?current.Lights.Single(light=>light.ObjectId==current.ExposureLight.ObjectId):null;
        var prepared=D3D11NormalPostInitialInputPreparation.Prepare(repository,current.Present,
            selected?.State.LensFlareSourceVector,selected is null?null:current.Present.FrameNumber,outputWidth,outputHeight);
        prepared.WriteNativeInputs(inputRoot);
        prepared.Resources.WriteNativeInputs(inputRoot);
        return prepared;
    }
}
