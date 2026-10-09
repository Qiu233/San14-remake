namespace San14.Data;

/// <summary>Normal App152B0 tail -> Host153EA0 overlay -> D1E410 Aux14.bit2.
/// This is a current source writer, independent of the saved FlagEX2 value.
/// It does not advance the camera or stand in for the complete Common overlay.</summary>
public static class ProceduralCurrentCommonEnableSource
{
    public const string SourceEvidence = "CE:152B0(after17BD0/currentApp288);153EA0/011040-AppFlags0;"+
        "F43360/CommonC+8-v10-overlay;D1E410-overlayword0.bit22-to-Aux14.bit2;D12A80-copy2440-to-Frame240;BB2110";

    /// <summary>App ushort0's exact post-Main tail stores. Current controller
    /// zoom index and written App288 come from the same normal preparation.</summary>
    public static ushort ApplyNormalAppTail(ushort actualFlags0,int actualMode288,float actualEcB0)
    {
        ushort flags=(ushort)(actualFlags0&0xFFFB);
        uint bit2=MathF.Floor(actualEcB0)<5f?4u:0u;
        uint bit3=actualMode288<2?8u:0u;
        return (ushort)(bit2|(uint)(flags&0xFFF7)|bit3);
    }

    /// <summary>The only Host callback write needed by this placement gate.
    /// Prior overlay or native local default remains the actual caller input.</summary>
    public static uint ApplyHostPlacementOverlayWord0(uint actualOverlayWord0,ushort currentAppFlags0)
        =>(actualOverlayWord0&0xFFBFFFFFu)|((currentAppFlags0&0x18)!=0?0x400000u:0u);

    /// <summary>Source copied Aux14 stays unchanged for a null overlay. Other
    /// Aux14 bits have their own D1E410 writers and are retained by this method.</summary>
    public static uint ApplyPlacementAuxFlags14(uint actualCopiedFlags14,bool actualOverlaySelected,uint actualOverlayWord0)
        =>actualOverlaySelected?(actualCopiedFlags14&0xFFFFFFFBu)|((actualOverlayWord0>>20)&4u):actualCopiedFlags14;
}
