namespace San14.Data;

public sealed record NativeCameraTangentConstantSource(string Name, double Value)
{
    public ulong Bits => BitConverter.DoubleToUInt64Bits(Value);
}

/// <summary>Remake tangent approximation coefficients and rounding contract.</summary>
public sealed class LoadedNativeCameraTangentSource
{
    internal LoadedNativeCameraTangentSource(string language)
    {
        Language=MainMapCameraRules.Language(language);
        Constants=Array.AsReadOnly(NativeCameraTangentConstants.Words.Select(pair =>
            new NativeCameraTangentConstantSource(pair.Key,BitConverter.UInt64BitsToDouble(pair.Value))).ToArray());
    }
    public string Language { get; }
    public string RuleVersion => "camera-tangent/v1";
    public string ResourceUri => "san14://rules/camera-tangent";
    public IReadOnlyList<NativeCameraTangentConstantSource> Constants { get; }
    public string RoundingContract => "binary32 input/result; nearest-even binary64 scalar/FMA operations; no floating exception-state emulation";
}

public static class NativeCameraTangentSource
{
    public static LoadedNativeCameraTangentSource CreateDefaults(string language) => new(language);
    public static LoadedNativeCameraTangentSource Load(GameResourceRepository repository,string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return CreateDefaults(language);
    }
    public static LoadedNativeCameraTangentSource Load(string sourceDirectory,string language)
        => CreateDefaults(language);
}
