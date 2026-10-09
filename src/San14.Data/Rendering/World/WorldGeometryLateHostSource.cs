using System.Numerics;

namespace San14.Data;

/// <summary>Actual field accessor of the accepted Main renderer type. A
/// field accessor reads its caller's retained owner; it is not a null proof.</summary>
public readonly record struct WorldGeometryLateHostGetter(
    int VirtualSlot, int? HostFieldOffset, bool IntrinsicNull);

/// <summary>Only the initialized fields of the source 50-byte Q58 owner.
/// Offset28 padding and native allocation metadata are deliberately absent.</summary>
public readonly record struct WorldGeometryLateHostColourParameters(
    Vector4 Scalars18, Vector4 Values30, Vector4 Values40);

public sealed record WorldGeometryLateHostNativeSource(
    string Locale, string RuleVersion,
    IReadOnlyList<WorldGeometryLateHostGetter> Getters,
    WorldGeometryLateHostColourParameters FreshColourParameters)
{
    public string SourceProvenance => $"{RenderRuleIdentity.For("WorldGeometryLateHost",Locale)};" +
        "Q-v120=58;v140-intrinsic-null;v198=88;v1A8=80;" +
        "Q58-successful-creator-owned-50B;current-Q80/Q88-publications-required";
}

/// <summary>Project-owned rules for node58 Host dependencies. Runtime
/// code keeps Q58, Q80 and Q88 as actual owned references and uses the
/// successful Host begin reset and later publications in their source order.</summary>
public sealed class LoadedWorldGeometryLateHostSource
{
    private LoadedWorldGeometryLateHostSource(WorldGeometryLateHostNativeSource native)
        => Native = native;
    public WorldGeometryLateHostNativeSource Native { get; }
    public static LoadedWorldGeometryLateHostSource Load(
        GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(RenderSamplerSourceAdapters.LoadWorldGeometryLateHostNativeSource(
            repository.SourceDirectory, locale));
    }
    public WorldGeometryLateHostColourParameters ConstructFreshColourParameters()
        => Native.FreshColourParameters;

    /// <summary>The Main renderer's v140 accessor returns null.
    /// This result does not depend on fresh constructor or caller state.</summary>
    public object? ReadIntrinsicVirtual140() => null;
}
