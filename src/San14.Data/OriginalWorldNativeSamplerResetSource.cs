
namespace San14.Data;

/// <summary>Verified backend reset recipe, not an observation of a running
/// game's sampler slots. Its default has no wrapper eight-byte key.</summary>
public sealed class OriginalWorldNativeSamplerResetSource
{
    private readonly byte[] descriptor;
    internal OriginalWorldNativeSamplerResetSource(LoadedOriginalDeferredSamplerNativeSource native,
        byte[] defaultDescriptor, string evidence)
        => (Native, descriptor, SourceEvidence) = (native, defaultDescriptor, evidence);
    public LoadedOriginalDeferredSamplerNativeSource Native { get; }
    public byte[] DefaultDescriptor => descriptor.ToArray();
    public string SourceEvidence { get; }
    public int StageCount => 6;
    public int RegisterCount => 16;
    public ulong ResetCachedKey => ulong.MaxValue;
}

public static partial class OriginalDeferredSamplerSources
{
    /// <summary>Initialization submits a reset command, which installs the
    /// backend default at all six by sixteen sampler slots. This recipe must still be joined to an owned fresh
    /// context and an exact later writer prefix; it is not current-slot proof.</summary>
    public static OriginalWorldNativeSamplerResetSource ReadWorldNativeSamplerReset(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(native);

        return new(native,BackendDefaultDescriptor(native),native.ResourceUri+
            $";{RenderRuleIdentity.For("sampler-reset",native.Locale)};initial-reset->dispatch->reset-slots;6x16/default28A0;"+
            "renderer3168->backend28A0;actual-six-stage-callback-tables;owned-reset-recipe-not-original-current-slots");
    }
}
