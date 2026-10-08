using System.Numerics;

namespace San14.Data;

/// <summary>Actual field accessor of the accepted Main renderer type. A
/// field accessor reads its caller's retained owner; it is not a null proof.</summary>
public readonly record struct OriginalWorldGeometryLateHostGetter(
    int VirtualSlot, int GetterRva, int? HostFieldOffset, bool IntrinsicNull);

/// <summary>Only the initialized fields of the source 50-byte Q58 owner.
/// Offset28 padding and native allocation metadata are deliberately absent.</summary>
public readonly record struct OriginalWorldGeometryLateHostColourParameters(
    Vector4 Scalars18, Vector4 Values30, Vector4 Values40);

public sealed record OriginalWorldGeometryLateHostNativeSource(
    string Locale, string ExecutableSha256, string DecodedTextSha256,
    int HostVtableRva, IReadOnlyList<OriginalWorldGeometryLateHostGetter> Getters,
    IReadOnlyDictionary<string, string> WindowSha256,
    OriginalWorldGeometryLateHostColourParameters FreshColourParameters)
{
    public string SourceProvenance => $"late-Host-getters/{Locale}@{ExecutableSha256};" +
        "Q-v120=58;v140-intrinsic-null;v198=88;v1A8=80;" +
        "Q58-successful-creator-owned-50B;current-Q80/Q88-publications-required";
}

/// <summary>Load-only source acceptance for node58 Host dependencies. Runtime
/// code keeps Q58, Q80 and Q88 as actual owned references and uses the
/// successful Host begin reset and later publications in their source order.</summary>
public sealed class LoadedOriginalWorldGeometryLateHostSource
{
    private LoadedOriginalWorldGeometryLateHostSource(OriginalWorldGeometryLateHostNativeSource native)
        => Native = native;
    public OriginalWorldGeometryLateHostNativeSource Native { get; }
    public static LoadedOriginalWorldGeometryLateHostSource Load(
        OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(OriginalDeferredSamplerSources.LoadWorldGeometryLateHostNativeSource(
            repository.SourceDirectory, locale));
    }
    public OriginalWorldGeometryLateHostColourParameters ConstructFreshColourParameters()
        => Native.FreshColourParameters;

    /// <summary>The Main renderer's actual v140 body is xor eax,eax;ret.
    /// This result does not depend on fresh constructor or caller state.</summary>
    public object? ReadIntrinsicVirtual140() => null;
}
