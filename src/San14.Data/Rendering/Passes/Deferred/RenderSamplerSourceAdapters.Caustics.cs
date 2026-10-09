using System.Numerics;

namespace San14.Data;

/// <summary>Project caustics initialization rules. Current texture handles and
/// transforms remain owned by the caller.</summary>
public sealed class LoadedDeferredEarlyCausticsSource
{
    internal LoadedDeferredEarlyCausticsSource(string locale,string ruleVersion,Matrix4x4 initialTransform)
    {
        Locale=locale;RuleVersion=ruleVersion;
        IdentityBytes=RenderRuleNumericEncoding.Matrix(initialTransform);
    }
    public string Locale { get; }
    public string RuleVersion { get; }
    internal byte[] IdentityBytes { get; }
    public string SourceProvenance=>RuleVersion;
}

public static partial class RenderSamplerSourceAdapters
{
    public static LoadedDeferredEarlyCausticsSource LoadEarlyCaustics(string sourceDirectory,string locale)
        => new(locale,RenderRuleIdentity.For("caustics",locale),Matrix4x4.Identity);
}
