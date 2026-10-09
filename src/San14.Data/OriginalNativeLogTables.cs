namespace San14.Data;

/// <summary>Project-owned 129-anchor binary32 logarithm rules. SC and TC use
/// the same mathematics, with separate parameter-language identities.</summary>
public sealed class OriginalNativeLogTables
{
    private static readonly OriginalNativeLogTables simplified = new("SC");
    private static readonly OriginalNativeLogTables traditional = new("TC");
    private OriginalNativeLogTables(string locale) => Locale = locale;

    public IReadOnlyList<uint> InverseCenter => OriginalNativeFloatLogConstants.Inverse;
    public IReadOnlyList<uint> LogHigh => OriginalNativeFloatLogConstants.High;
    public IReadOnlyList<uint> LogLow => OriginalNativeFloatLogConstants.Low;
    public string Locale { get; }
    public string RuleVersion => "binary32-ln-128-anchors-v1";

    public static OriginalNativeLogTables Create(string locale) => locale switch
    {
        "SC" => simplified,
        "TC" => traditional,
        _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Log parameter language must be SC or TC.")
    };

    public static OriginalNativeLogTables Load(string sourceDirectory, string locale)
        => Create(locale);
}
