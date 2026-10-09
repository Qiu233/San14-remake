namespace San14.Data;

/// <summary>Project-owned binary64 logarithm rules. Language selects the
/// surrounding parameter contract; the mathematical implementation is shared.</summary>
public sealed class LoadedOriginalNativeDoubleLogSource
{
    private static readonly LoadedOriginalNativeDoubleLogSource simplified = new("SC");
    private static readonly LoadedOriginalNativeDoubleLogSource traditional = new("TC");
    private LoadedOriginalNativeDoubleLogSource(string locale) => Locale = locale;

    public string Locale { get; }
    public bool CurrentNativeDispatchObserved => false;
    public string SourceEvidence => "san14-remake:binary64-log/256-anchor-split;" +
        "finite-positive-binary32-input;explicit-SSE/AVX-FMA-dispatch";

    public double LogPositiveFloat(float value, OriginalNativeLogDispatch selectedOriginalPath)
        => OriginalNativeDoubleLogarithm.LogPositiveFloat(value, this, selectedOriginalPath);

    public static LoadedOriginalNativeDoubleLogSource Create(string locale) => locale switch
    {
        "SC" => simplified,
        "TC" => traditional,
        _ => throw new NotSupportedException("Double-log parameter language must be SC or TC.")
    };

    public static LoadedOriginalNativeDoubleLogSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Create(locale);
    }

    public static LoadedOriginalNativeDoubleLogSource Load(string sourceDirectory, string locale)
        => Create(locale);
}
