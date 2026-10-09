namespace San14.Data;

/// <summary>Fresh camera rectangle defaults implemented by the project.
/// Current viewport changes are supplied separately by the frame owner.</summary>
public sealed class OriginalMapPostEffect3FallbackCameraConstructorWords
{
    internal OriginalMapPostEffect3FallbackCameraConstructorWords(string locale)
    {Locale=locale;SourceEvidence=RenderRuleIdentity.For("fresh-camera-rectangles",locale);}
    public string Locale{get;}
    public string RuleVersion=>RenderRuleIdentity.Version;
    public uint Source1278Word=>BitConverter.SingleToUInt32Bits(1f);
    public uint Source127CWord=>BitConverter.SingleToUInt32Bits(1f);
    public string SourceEvidence{get;}
}

public static partial class OriginalDeferredSamplerSources
{
    public static OriginalMapPostEffect3FallbackCameraConstructorWords ReadFallbackCameraRectangleNativeSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(native);
        return new(native.Locale);
    }
}
