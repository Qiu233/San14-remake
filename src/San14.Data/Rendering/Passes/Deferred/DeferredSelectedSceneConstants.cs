namespace San14.Data;

/// <summary>Carry the already restored native named-field values into the
/// exact original selected layout. Complete caller bytes remain authoritative
/// for padding and unwritten variables; absent declarations receive no bytes.</summary>
public static class DeferredSelectedSceneConstants
{
    public static DeferredSceneConstantPacket Apply(DeferredF103SelectedSource selected,
        ReadOnlySpan<byte> completeSelectedTemplate, DeferredSceneInputs input)
        =>Apply(selected,completeSelectedTemplate,input,null);

    /// <summary>Only PS767's exact source-owned Hatching IEEE outputs may pass
    /// the selected layout. The general canonical finite guard stays intact.</summary>
    public static DeferredSceneConstantPacket Apply(DeferredF103SelectedSource selected,
        ReadOnlySpan<byte> completeSelectedTemplate, DeferredSceneInputs input,
        DeferredSelected767HatchingIeeeProof? hatchingIeeeProof)
    {
        ArgumentNullException.ThrowIfNull(selected); ArgumentNullException.ThrowIfNull(input);

        if ((selected.Combination.Index, selected.PixelProgramIndex, selected.PixelProgramSha256) is not
            (128, 97, "FF1CE5BFD5F4A8B3397E789C59045BCC72186644AA6C7D22EE523D633CBB384F") and not
            (510, 383, "91C2A580A1D05CA6144FD7CDE4F0AC24CD7ED425F2CFB4BCAAA4E9A4F62A3AF1") and not
            (640, 481, "1FD258CD6212012BAF24E7C407FC01771C93755FDC836C2F4E050238638DAE1B") and not
            (1022, 767, "62CEB4427347D0D7E37CA096138B2F39CAEB9342134356177CFE83027E879A4C"))
            throw new NotSupportedException("Selected scene writes require one of the four independently verified original source layouts.");
        var layout=selected.Selection.Program.Prepared.SceneConstantLayout;
        layout.RequireTemplate(completeSelectedTemplate.Length);
        if(hatchingIeeeProof is not null)
        {
            if(selected.Combination.Index!=1022||selected.PixelProgramIndex!=767||
                selected.PixelProgramSha256!=DeferredSelected767Constants.PixelProgramSha256)
                throw new InvalidDataException("Hatching IEEE authority is limited to its actual selected PS767 layout.");
            hatchingIeeeProof.ValidateCurrent(selected);hatchingIeeeProof.Validate(input.Hatching);
        }
        return DeferredSceneConstants.Apply(layout,completeSelectedTemplate,input,hatchingIeeeProof is not null);
    }
}
