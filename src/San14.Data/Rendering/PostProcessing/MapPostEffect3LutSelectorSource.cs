namespace San14.Data;

/// <summary>The exact native context50.v88 source of the owned Main Q.
/// This is not a sample of the original process's active render context.</summary>
public sealed class LoadedMapPostEffect3LutSelectorSource
{
    internal LoadedMapPostEffect3LutSelectorSource(LoadedDeferredSamplerNativeSource native,string evidence)
        =>(Native,SourceEvidence)=(native,evidence);
    public LoadedDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    public MapPostEffect3OwnedMainLutSelector ResolveOwnedMain(MainMapContextCurrentPacket actualCurrentContext)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentContext);
        actualCurrentContext.Source.Validate(actualCurrentContext);
        var host=actualCurrentContext.Source.Host;
        if(actualCurrentContext.FrameNumber<=0||host.Locale!=Native.Locale||
            host.RuleVersion!=RenderRuleIdentity.Version)
            throw new NotSupportedException("LUT selector requires the actual same-locale source-pinned Main Q/current packet.");
        return new(this,actualCurrentContext);
    }
}

/// <summary> returns zero because this exact Q.v80 is /literalfalse.
/// No external selector value, locale selection or first declaration is used.</summary>
public sealed class MapPostEffect3OwnedMainLutSelector
{
    private readonly MainMapContextCurrentPacket current;
    internal MapPostEffect3OwnedMainLutSelector(LoadedMapPostEffect3LutSelectorSource native,
        MainMapContextCurrentPacket current)=>(Native,this.current)=(native,current);
    public LoadedMapPostEffect3LutSelectorSource Native{get;}
    public uint Selector=>0;
    public long FrameNumber=>current.FrameNumber;
    public string ContextOwnerIdentity=>current.Source.OwnedHostIdentity;
    public bool ActiveContextIdentityVerified=>false;
    public bool ProvesGpuTextureReady=>false;
    public string SourceEvidence=>Native.SourceEvidence+";"+current.SourceProvenance;
    public void ValidateFor(MainMapContextCurrentPacket actualCurrentContext)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentContext);current.Source.Validate(actualCurrentContext);
        if(!ReferenceEquals(current,actualCurrentContext))
            throw new InvalidDataException("LUT selector belongs to another actual Main current packet.");
        _=Native.ResolveOwnedMain(actualCurrentContext);
    }
}

public static class MapPostEffect3LutSelectorSource
{
    public static LoadedMapPostEffect3LutSelectorSource Load(GameResourceRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return RenderSamplerSourceAdapters.ReadPostEffect3LutSelectorSource(
            RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
    }
}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3LutSelectorSource ReadPostEffect3LutSelectorSource(
        LoadedDeferredSamplerNativeSource native)
    {
        return new(native,native.RenderRuleIdentity+";owned Main context LUT selector;current source mode;not original process observation");
    }
}
