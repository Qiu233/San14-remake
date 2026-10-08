using San14.Data;

public sealed partial class OriginalSkyDirectionalAmbientDeviceOwner
{
    /// <summary>The selected contribution consumes the fixed STA1 plus source
    /// null Moon/Milky. Verify the newly loaded package's actual roles before
    /// retaining the existing shaders/WORK/cubes; no current value is cached.</summary>
    public void RequireCompatibleSeasonResources(OriginalSkySeasonMainCameraSource source)
    {
        ArgumentNullException.ThrowIfNull(source);ObjectDisposedException.ThrowIf(retiring,this);
        var package=source.Selection.FirstPackage;
        _=OriginalSky2SourceNull2D.FromAppliedSeasonOnFreshShader(package,OriginalSkySamplingRole.Moon,
            OriginalSkyTextureStateScope.AppliedSourceSeasonOnFreshShader);
        _=OriginalSky2SourceNull2D.FromAppliedSeasonOnFreshShader(package,OriginalSkySamplingRole.MilkyWay,
            OriginalSkyTextureStateScope.AppliedSourceSeasonOnFreshShader);
        var earth=OriginalSkySeasonPackageTextures.LoadEarth(repository,package);
        var owned=textures.Selections[earth.SourceName];
        if(owned.Source.Source.ResourceUri!=earth.Resource.Source.ResourceUri||
            owned.Source.Source.DecodedSha256!=earth.Resource.Source.DecodedSha256||
            owned.ImageIndex!=earth.ImageIndex||owned.NativeViewFormat!=earth.NativeViewFormat||
            !textures.GetSamplerDescriptor(earth.SourceName).AsSpan().SequenceEqual(earth.GetSamplerDescriptor()))
            throw new NotSupportedException("Changed seasonal Earth binding requires its actual texture lifecycle.");
    }
}
