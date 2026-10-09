namespace San14.Data;

public sealed record OriginalNativeCameraTangentConstantSource(string Name, double Value)
{
    public ulong Bits => BitConverter.DoubleToUInt64Bits(Value);
}

/// <summary>Remake tangent approximation coefficients and rounding contract.</summary>
public sealed class LoadedOriginalNativeCameraTangentSource
{
    internal LoadedOriginalNativeCameraTangentSource(string language)
    {
        Language=OriginalMainMapCameraRules.Language(language);
        Constants=Array.AsReadOnly(OriginalNativeCameraTangentConstants.Words.Select(pair =>
            new OriginalNativeCameraTangentConstantSource(pair.Key,BitConverter.UInt64BitsToDouble(pair.Value))).ToArray());
    }
    public string Language { get; }
    public string RuleVersion => "camera-tangent/v1";
    public string ResourceUri => "san14://rules/camera-tangent";
    public IReadOnlyList<OriginalNativeCameraTangentConstantSource> Constants { get; }
    public string RoundingContract => "binary32 input/result; nearest-even binary64 scalar/FMA operations; no floating exception-state emulation";
}

public static class OriginalNativeCameraTangentSource
{
    public static LoadedOriginalNativeCameraTangentSource CreateDefaults(string language) => new(language);
    public static LoadedOriginalNativeCameraTangentSource Load(OriginalGameRepository repository,string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return CreateDefaults(language);
    }
    public static LoadedOriginalNativeCameraTangentSource Load(string sourceDirectory,string language)
        => CreateDefaults(language);
}
