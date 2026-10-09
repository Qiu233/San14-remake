namespace San14.Data;

/// <summary>The fresh context owner built by the exact native Host factory.
/// Later native context mutations require a separately retained current value.</summary>
public sealed class OriginalMainMapContextOwnerSource
{
    public const string ContractId = "ce-20261002-main-map-fresh-host-context";
    public const uint FreshHostFlags3E4 = 0x20000001;

    private OriginalMainMapContextOwnerSource(string locale) => Locale = locale;

    public string Locale { get; }
    public string RuleVersion => RenderRuleIdentity.Version;
    public uint ContextFlags3E4 => FreshHostFlags3E4;
    public bool ContextOwner3E4Bit29 => (ContextFlags3E4 & 0x20000000) != 0;
    public string SourceProvenance => RenderRuleIdentity.For("fresh-host-context", Locale) +
        ";HostCfg12C=1:HostCfg160=1:HostCfg161=0";

    /// <summary>Bind the verified native Host constructor recipe to a remake
    /// owned fresh context. This is a constructor source, not a live game read.</summary>
    public static OriginalMainMapContextOwnerSource LoadFreshHostDefaults(string locale)
    {
        _ = RenderRuleIdentity.For("fresh-host-context", locale);
        return new(locale);
    }
}
