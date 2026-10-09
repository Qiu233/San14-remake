namespace San14.Data;

/// <summary>The exact native context50.v88 source of the owned Main Q.
/// This is not a sample of the original process's active render context.</summary>
public sealed class LoadedOriginalMapPostEffect3LutSelectorSource
{
    internal LoadedOriginalMapPostEffect3LutSelectorSource(LoadedOriginalDeferredSamplerNativeSource native,string evidence)
        =>(Native,SourceEvidence)=(native,evidence);
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    public OriginalMapPostEffect3OwnedMainLutSelector ResolveOwnedMain(OriginalMainMapContextCurrentPacket actualCurrentContext)
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
public sealed class OriginalMapPostEffect3OwnedMainLutSelector
{
    private readonly OriginalMainMapContextCurrentPacket current;
    internal OriginalMapPostEffect3OwnedMainLutSelector(LoadedOriginalMapPostEffect3LutSelectorSource native,
        OriginalMainMapContextCurrentPacket current)=>(Native,this.current)=(native,current);
    public LoadedOriginalMapPostEffect3LutSelectorSource Native{get;}
    public uint Selector=>0;
    public long FrameNumber=>current.FrameNumber;
    public string ContextOwnerIdentity=>current.Source.OwnedHostIdentity;
    public bool ProvesOriginalActiveContext=>false;
    public bool ProvesGpuTextureReady=>false;
    public string SourceEvidence=>Native.SourceEvidence+";"+current.SourceProvenance;
    public void ValidateFor(OriginalMainMapContextCurrentPacket actualCurrentContext)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentContext);current.Source.Validate(actualCurrentContext);
        if(!ReferenceEquals(current,actualCurrentContext))
            throw new InvalidDataException("LUT selector belongs to another actual Main current packet.");
        _=Native.ResolveOwnedMain(actualCurrentContext);
    }
}

public static class OriginalMapPostEffect3LutSelectorSource
{
    public static LoadedOriginalMapPostEffect3LutSelectorSource Load(OriginalGameRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.ReadPostEffect3LutSelectorSource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3LutSelectorSource ReadPostEffect3LutSelectorSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        return new(native,native.RenderRuleIdentity+";owned Main context LUT selector;current source mode;not original process observation");
    }
}
