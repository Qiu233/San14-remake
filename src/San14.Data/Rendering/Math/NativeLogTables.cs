namespace San14.Data;

/// <summary>Project-owned 129-anchor binary32 logarithm rules. SC and TC use
/// the same mathematics, with separate parameter-language identities.</summary>
public sealed class NativeLogTables
{
    private static readonly NativeLogTables simplified = new("SC");
    private static readonly NativeLogTables traditional = new("TC");
    private NativeLogTables(string locale) => Locale = locale;

    public IReadOnlyList<uint> InverseCenter => NativeFloatLogConstants.Inverse;
    public IReadOnlyList<uint> LogHigh => NativeFloatLogConstants.High;
    public IReadOnlyList<uint> LogLow => NativeFloatLogConstants.Low;
    public string Locale { get; }
    public string RuleVersion => "binary32-ln-128-anchors-v1";

    public static NativeLogTables Create(string locale) => locale switch
    {
        "SC" => simplified,
        "TC" => traditional,
        _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Log parameter language must be SC or TC.")
    };

    public static NativeLogTables Load(string sourceDirectory, string locale)
        => Create(locale);
}
