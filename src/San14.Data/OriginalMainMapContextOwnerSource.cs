namespace San14.Data;

/// <summary>The fresh context owner built by the exact native Host factory.
/// Later native context mutations require a separately retained current value.</summary>
public sealed class OriginalMainMapContextOwnerSource
{
    public const string ContractId = "ce-20261002-main-map-fresh-host-context";
    public const uint FreshHostFlags3E4 = 0x20000001;

    private OriginalMainMapContextOwnerSource(string locale, string executableSha256,
        uint contextOwnerVtableRva)
    {
        Locale = locale;
        ExecutableSha256 = executableSha256;
        ContextOwnerVtableRva = contextOwnerVtableRva;
    }

    public string Locale { get; }
    public string ExecutableSha256 { get; }
    public uint ContextOwnerVtableRva { get; }
    public uint ContextFlags3E4 => FreshHostFlags3E4;
    public bool ContextOwner3E4Bit29 => (ContextFlags3E4 & 0x20000000) != 0;
    public string SourceProvenance => $"{ContractId}:{Locale}:{ExecutableSha256}:" +
        $"QVT{ContextOwnerVtableRva:X8}:HostCfg12C=1:HostCfg160=1:HostCfg161=0";

    /// <summary>Bind the verified native Host constructor recipe to a remake
    /// owned fresh context. This is a constructor source, not a live game read.</summary>
    public static OriginalMainMapContextOwnerSource LoadFreshHostDefaults(
        OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        string sha = OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(repository.SourceDirectory, locale);
        uint vtable = locale switch
        {
            "SC" => 0x13C5CF0,
            "TC" => 0x13C4ED0,
            _ => throw new InvalidDataException("Fresh Main context source requires exact SC or TC.")
        };
        return new(locale, sha, vtable);
    }
}
