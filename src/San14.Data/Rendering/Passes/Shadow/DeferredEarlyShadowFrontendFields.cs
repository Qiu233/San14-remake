namespace San14.Data;

public sealed record DeferredEarlyShadowFrontendWrite(byte Alpha1BE, byte RenderTarget0Blend1C0,
    uint? CommandWord, string SourceEvidence);

/// <summary>The two small, backend-lived fields read by generic shadow v80.
/// C0 is RT0 blend-enable; it is not a second alpha reference or a matrix word.
/// This owner is created at a real fresh backend construction, then actual
/// setters update it across ordinary rendering scopes.</summary>
public sealed class DeferredEarlyShadowFrontendFields
{
    internal DeferredEarlyShadowFrontendFields(LoadedDeferredEarlyShadowSelectionSource source)
    { Source=source; } //966EA0: word1BE=0100 and loop byte1C0=0.
    public LoadedDeferredEarlyShadowSelectionSource Source { get; }
    public byte Alpha1BE { get; private set; }
    public byte RenderTarget0Blend1C0 { get; private set; }
    public string SourceEvidence => Source.SourceEvidence+
        ";fresh966EA0-two-byte-owned-fragment;actual-setters-retained;not-full-backend-or-GPU-state";

    /// <summary>D362B0 tests its actual caller disable-alpha argument and
    /// SETE. Only a changed value emits opcode17. It preserves current1C0.</summary>
    public DeferredEarlyShadowFrontendWrite ApplyPrimitiveEntry(bool actualDisableAlphaArgument)
        => ApplyActualAlphaSetter(!actualDisableAlphaArgument);

    public DeferredEarlyShadowFrontendWrite ApplyActualAlphaSetter(bool actualEnabled)
    {
        byte value=actualEnabled?(byte)1:(byte)0;
        uint? command=Alpha1BE==value?null:0x17u|((uint)value<<16);
        Alpha1BE=value;return new(Alpha1BE,RenderTarget0Blend1C0,command,SourceEvidence+";actual-alpha-setter");
    }

    /// <summary>Publish a real source blend setter's RT0 result. The caller
    /// supplies that executed operation; target count0 does not force this byte0.</summary>
    public void PublishAfterActualRenderTarget0BlendSetter(bool actualEnabled)
        => RenderTarget0Blend1C0=actualEnabled?(byte)1:(byte)0;

    /// <summary>After the actual generic v80 reads these current fields,
    /// nonzero1C0 emits opcodeB and is cleared. Alpha1BE remains unchanged.</summary>
    public DeferredEarlyShadowFrontendWrite ApplyAfterGenericV80()
    {
        uint? command=RenderTarget0Blend1C0!=0?0xBu:null;
        RenderTarget0Blend1C0=0;
        return new(Alpha1BE,RenderTarget0Blend1C0,command,SourceEvidence+";1053F80-current-RT0blend-reset");
    }

    /// <summary>D170B8..D17277 clears alpha only after the actual extra-target
    /// binding returned successfully and its current SSSTransmit lookup plus
    /// three current parameter inputs are all nonnull. No material-name guess
    /// or generic renderer default supplies these source branch conditions.</summary>
    public DeferredEarlyShadowFrontendWrite ApplyAfterActualExtraTargetParameterBranch(
        bool actualTarget0Present,bool actualTarget1Present,bool actualSssTransmitLookupPresent,
        bool actualParameter18Present,bool actualParameterMinus18Present,bool actualParameter10Present,
        bool actualTargetBindAccepted,bool actualSavedAlphaEnabledBeforeParameterTail)
    {
        bool suppress=actualTarget0Present&&actualTarget1Present&&actualSssTransmitLookupPresent&&
            actualParameter18Present&&actualParameterMinus18Present&&actualParameter10Present&&actualTargetBindAccepted&&
            actualSavedAlphaEnabledBeforeParameterTail;
        return suppress?ApplyActualAlphaSetter(false):new(Alpha1BE,RenderTarget0Blend1C0,null,
            SourceEvidence+";actual-extra-target-branch-preserves-alpha");
    }
}
