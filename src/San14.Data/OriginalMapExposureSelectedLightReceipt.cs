using System.Buffers.Binary;

namespace San14.Data;

public enum OriginalMapExposureLuminanceBranch
{
    RetainedSameCamera17C,
    SelectedRecordBaselineRgbFA0
}

/// <summary>Exposure's selected View1B20/record8 input, separated from the
/// F103 H-modified copied packet. The owned ApplyLuminance reference supplies
/// the nonnull global branch; no original global/process pointer is claimed.</summary>
public sealed class OriginalMapExposureSelectedLightReceipt
{
    private readonly OriginalMapSourceCreatedLightingPacket lighting;
    private readonly byte[] selectedData;
    private OriginalMapExposureSelectedLightReceipt(OriginalSkySceneSelectedViewLightPacket selected,
        OriginalMapSourceCreatedLightingPacket lighting,string applyReference)
    {
        this.lighting=lighting.CopyAcceptedSnapshot();selectedData=selected.CompleteLightData.ToArray();
        FrameNumber=lighting.FrameNumber;SelectedViewIdentity=selected.Identity.SelectedViewOwner;
        RecordOwnerIdentity=selected.Identity.ProviderRecordOwner;LightOwnerIdentity=selected.Identity.ProviderPacketOwner;
        OwnedApplyReferenceIdentity=applyReference;
        UsesSelectedBaselineRgb=selectedData[0x10B0] is 3 or 4 or 5||(selectedData[0xFDC]&1)!=0;
        SelectedExternalLuminance=OriginalMapExposureSource.ResolveSelectedLuminance(true,selectedData);
        Branch=UsesSelectedBaselineRgb?OriginalMapExposureLuminanceBranch.SelectedRecordBaselineRgbFA0:
            OriginalMapExposureLuminanceBranch.RetainedSameCamera17C;
        SameCameraLuminance17C=BinaryPrimitives.ReadSingleLittleEndian(lighting.PreparedCameraPrefix.Span.Slice(0x17C,4));
        if(!float.IsFinite(SameCameraLuminance17C))throw new InvalidDataException("Exposure's selected same Camera17C is not finite.");
        SourceProvenance=selected.SourceProvenance+";"+lighting.SourceProvenance+
            $";owned-nonnull-ApplyLuminance-reference={applyReference};TC-EA9784/D59120:View1B20-record8;"+
            $"EA9792..EA97DE:type345-or-FDCbit0-RGB-else-same-Camera17C;branch={Branch}";
    }
    public long FrameNumber { get; }
    public string SelectedViewIdentity { get; }
    public string RecordOwnerIdentity { get; }
    public string LightOwnerIdentity { get; }
    public string OwnedApplyReferenceIdentity { get; }
    public bool UsesSelectedBaselineRgb { get; }
    public OriginalMapExposureLuminanceBranch Branch { get; }
    public float? SelectedExternalLuminance { get; }
    public float SameCameraLuminance17C { get; }
    public string SourceProvenance { get; }
    public static OriginalMapExposureSelectedLightReceipt CaptureOwnedSelectedView(
        OriginalSkySceneSelectedViewLightPacket selected,OriginalMapSourceCreatedLightingPacket lighting,
        string ownedApplyReferenceIdentity)
    {
        ArgumentNullException.ThrowIfNull(selected);ArgumentNullException.ThrowIfNull(lighting);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedApplyReferenceIdentity);lighting.ValidateCurrentSourceFrame();
        if(selected.Identity.FrameNumber!=lighting.FrameNumber||selected.Identity.SelectedViewOwner!=lighting.Binding.ViewIdentity||
            selected.Identity.ProviderPacketOwner!=lighting.Binding.LightOwnerIdentity||
            string.IsNullOrWhiteSpace(selected.Identity.ProviderRecordOwner)||
            selected.Identity.ProviderRecordOwner==lighting.Binding.LightOwnerIdentity||
            selected.Identity.ProviderRecordOwner.StartsWith("0x",StringComparison.OrdinalIgnoreCase)||
            !selected.CompleteLightData.Span.SequenceEqual(lighting.LocalLightData.Span))
            throw new InvalidDataException("Exposure requires the actual selected owned View record8 and unchanged current source payload.");
        return new(selected,lighting,ownedApplyReferenceIdentity);
    }
    public void ValidateFor(OriginalMapSourceCreatedLightingPacket current)
    {
        ArgumentNullException.ThrowIfNull(current);lighting.ValidateCurrentSourceFrame();current.ValidateCurrentSourceFrame();
        if(current.FrameNumber!=FrameNumber||current.Binding!=lighting.Binding||current.SourceIdentity!=lighting.SourceIdentity||
            !current.PreparedCameraPrefix.Span.SequenceEqual(lighting.PreparedCameraPrefix.Span)||
            !current.LocalLightData.Span.SequenceEqual(selectedData)||
            OriginalMapExposureSource.ResolveSelectedLuminance(true,selectedData)!=SelectedExternalLuminance)
            throw new InvalidDataException("Exposure selected View record receipt belongs to another same-frame Camera/source LightData generation.");
    }
}
