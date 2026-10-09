using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The sixteen sbias bytes owned by the same fresh compact owner.
/// This is the sole named field in its native CPU138 parameter cache, not a
/// complete native cache, Shadow CPU, parameter directory or stage binding.</summary>
public sealed class DeferredEarlyShadowCurrentBiasCache
{
    private readonly byte[] currentBytes;
    internal DeferredEarlyShadowCurrentBiasCache(
        LoadedDeferredEarlyShadowBiasSource source,
        DeferredEarlyShadowCurrentCompactFields actualCompact, byte[] bytes)
        => (Source, ActualCompact, currentBytes) = (source, actualCompact, bytes);
    public LoadedDeferredEarlyShadowBiasSource Source { get; }
    public DeferredEarlyShadowCurrentCompactFields ActualCompact { get; }
    public string ParameterName => Source.ParameterName;
    public int ParameterOrdinal => Source.ParameterOrdinal;
    public byte NativeParameterType => Source.NativeParameterType;
    public byte ComponentCount => Source.ComponentCount;
    public ReadOnlyMemory<byte> CurrentBytesD0ToDF => currentBytes;
    public string SourceEvidence => Source.SourceEvidence + ";same-freshCompact-owned-sbias16;" + ActualCompact.SourceEvidence;
    public Vector4 ReadCurrent() => DeferredEarlyShadowPixelMaterialSource.ReadOwnedCurrentShadowBias(currentBytes);

    /// <summary>Retain a value supplied by an actual later sbias producer.
    /// This does not identify or synthesize that producer. Pool reuse keeps
    /// this cache and its latest value; it does not invoke the fresh constructor.</summary>
    public void WriteCurrentFromSource(Vector4 actualCurrentSbiasWriterValue)
    {
        BinaryPrimitives.WriteSingleLittleEndian(currentBytes, actualCurrentSbiasWriterValue.X);
        BinaryPrimitives.WriteSingleLittleEndian(currentBytes.AsSpan(4), actualCurrentSbiasWriterValue.Y);
        BinaryPrimitives.WriteSingleLittleEndian(currentBytes.AsSpan(8), actualCurrentSbiasWriterValue.Z);
        BinaryPrimitives.WriteSingleLittleEndian(currentBytes.AsSpan(12), actualCurrentSbiasWriterValue.W);
    }
}

/// <summary>Imported paired compact factory/prototype/direct constructor
/// source. ConstructFreshOwnedCache performs the finite source-owned cache
/// creation, then the actual base constructor's sbias writer, in source order.</summary>
public sealed class LoadedDeferredEarlyShadowBiasSource
{
    private readonly byte[] constructorBytes;
    internal LoadedDeferredEarlyShadowBiasSource(string locale, string evidence, byte[] bytes)
        => (Locale, SourceEvidence, constructorBytes) = (locale, evidence, bytes);
    public string Locale { get; }
    public string SourceEvidence { get; }
    public string ParameterName => "sbias";
    public int ParameterOrdinal => 0;
    public byte NativeParameterType => 2;
    public byte ComponentCount => 4;
    public int InheritedParameterCount => 0;
    public int LocalParameterCount => 1;
    public static LoadedDeferredEarlyShadowBiasSource Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return RenderSamplerSourceAdapters.LoadEarlyShadowBiasSource(repository.SourceDirectory, locale);
    }

    /// <summary>Same actual fresh Compact association, original local
    /// descriptor type2/width4/ordinal0, then base constructor's D0..DF writer.
    /// Only the sixteen read bytes are owned here. Successful compact factory
    /// allocation supplies a nonnull native R8; source pool reuse skips this
    /// operation and must retain its existing cache.</summary>
    public DeferredEarlyShadowCurrentBiasCache ConstructFreshOwnedCache(
        DeferredEarlyShadowCurrentCompactFields actualCompact)
    {
        ArgumentNullException.ThrowIfNull(actualCompact);
        var own = actualCompact.OwnCamera;
        DeferredEarlyShadowCurrentMainFields.RequireActualMain(own.Main.ActualMainInputs, own.Main.PreparedMain);
        // Model only the imported sole local field. Unread cache headers and
        // other native allocation bytes are not defaulted or certified here.
        byte[] ownedField = new byte[ComponentCount * sizeof(float)];
        ApplyOwnedConstructorFragment(ownedField);
        return new(this, actualCompact, ownedField);
    }

    public void ApplyOwnedConstructorFragment(Span<byte> actualOwnedCacheD0ToDF)
    {
        if (actualOwnedCacheD0ToDF.Length != 16)
            throw new ArgumentException("The source sbias constructor fragment owns exactly sixteen bytes.", nameof(actualOwnedCacheD0ToDF));
        constructorBytes.AsSpan().CopyTo(actualOwnedCacheD0ToDF);
    }
}
