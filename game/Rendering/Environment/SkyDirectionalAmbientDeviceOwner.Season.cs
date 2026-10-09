using San14.Data;

public sealed partial class SkyDirectionalAmbientDeviceOwner
{
    /// <summary>The selected contribution consumes the fixed STA1 plus source
    /// null Moon/Milky. Verify the newly loaded package's actual roles before
    /// retaining the existing shaders/WORK/cubes; no current value is cached.</summary>
    public void RequireCompatibleSeasonResources(SkySeasonMainCameraSource source)
    {
        ArgumentNullException.ThrowIfNull(source);ObjectDisposedException.ThrowIf(retiring,this);
        var package=source.Selection.FirstPackage;
        _=Sky2SourceNull2D.FromAppliedSeasonOnFreshShader(package,SkySamplingRole.Moon,
            SkyTextureStateScope.AppliedSourceSeasonOnFreshShader);
        _=Sky2SourceNull2D.FromAppliedSeasonOnFreshShader(package,SkySamplingRole.MilkyWay,
            SkyTextureStateScope.AppliedSourceSeasonOnFreshShader);
        var earth=SkySeasonPackageTextures.LoadEarth(repository,package);
        var owned=textures.Selections[earth.SourceName];
        if(owned.Source.Source.ResourceUri!=earth.Resource.Source.ResourceUri||
            owned.Source.Source.DecodedSha256!=earth.Resource.Source.DecodedSha256||
            owned.ImageIndex!=earth.ImageIndex||owned.NativeViewFormat!=earth.NativeViewFormat||
            !textures.GetSamplerDescriptor(earth.SourceName).AsSpan().SequenceEqual(earth.GetSamplerDescriptor()))
            throw new NotSupportedException("Changed seasonal Earth binding requires its actual texture lifecycle.");
    }
}
