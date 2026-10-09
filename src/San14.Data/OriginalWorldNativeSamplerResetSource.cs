
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
    /// <summary>BF93D0 initialization submits opcode1 at BFBC1B. The handler
    /// 957AD0 calls962E90, which installs backend28A0 at all six by sixteen
    /// sampler slots. This recipe must still be joined to an owned fresh
    /// context and an exact later writer prefix; it is not current-slot proof.</summary>
    public static OriginalWorldNativeSamplerResetSource ReadWorldNativeSamplerReset(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(native);

        int resetHandler,resetSlots,initialSubmit,dispatcher,transfer;
        switch(native.Locale)
        {
            case "SC":

                resetHandler=0x958BA0;resetSlots=0x963F40;
                initialSubmit=0xBFD0DD;dispatcher=0x95BAA0;transfer=0x966CD3;
                break;
            case "TC":

                resetHandler=0x957AD0;resetSlots=0x962E90;
                initialSubmit=0xBFBC0D;dispatcher=0x95A9D0;transfer=0x965B53;
                break;
            default:throw new NotSupportedException("World native sampler reset source requires SC/TC.");
        }

        return new(native,BackendDefaultDescriptor(native),native.ResourceUri+
            $";{native.Locale}{initialSubmit:X}->{dispatcher:X}/opcode1->{resetHandler:X}/{resetSlots:X};6x16/default28A0;"+
            $"{transfer:X}:renderer3168->backend28A0;actual-six-stage-callback-tables;owned-reset-recipe-not-original-current-slots");
    }
}
