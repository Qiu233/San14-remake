namespace San14.Data;

/// <summary>Project-owned binary64 logarithm rules. Language selects the
/// surrounding parameter contract; the mathematical implementation is shared.</summary>
public sealed class LoadedNativeDoubleLogSource
{
    private static readonly LoadedNativeDoubleLogSource simplified = new("SC");
    private static readonly LoadedNativeDoubleLogSource traditional = new("TC");
    private LoadedNativeDoubleLogSource(string locale) => Locale = locale;

    public string Locale { get; }
    public bool CurrentNativeDispatchObserved => false;
    public string SourceEvidence => "san14-remake:binary64-log/256-anchor-split;" +
        "finite-positive-binary32-input;explicit-SSE/AVX-FMA-dispatch";

    public double LogPositiveFloat(float value, NativeLogDispatch selectedMathPath)
        => NativeDoubleLogarithm.LogPositiveFloat(value, this, selectedMathPath);

    public static LoadedNativeDoubleLogSource Create(string locale) => locale switch
    {
        "SC" => simplified,
        "TC" => traditional,
        _ => throw new NotSupportedException("Double-log parameter language must be SC or TC.")
    };

    public static LoadedNativeDoubleLogSource Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Create(locale);
    }

    public static LoadedNativeDoubleLogSource Load(string sourceDirectory, string locale)
        => Create(locale);
}
