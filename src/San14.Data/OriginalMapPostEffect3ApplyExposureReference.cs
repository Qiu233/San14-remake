using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Owned node1 completed Exposure publication, with no subsequent
/// temporary/retained reference50 override. This represents the remake owner's
/// reference object; it does not observe the original27DDDE0 global pointer.</summary>
public sealed class OriginalMapPostEffect3ApplyExposureReference
{
    private OriginalMapPostEffect3ApplyExposureReference(long frame,string owner,
        OriginalMapPostEffect3ApplyView exposure,float luminance,string evidence)
    {FrameNumber=frame;OwnerIdentity=owner;ExposureTexture50=exposure;ActualReferenceLuminance18=luminance;SourceEvidence=evidence;}
    public long FrameNumber{get;}
    public string OwnerIdentity{get;}
    public uint TextureObjectId=>0x6C5630BB;
    public OriginalMapPostEffect3ApplyView ExposureTexture50{get;}
    public float ActualReferenceLuminance18{get;}
    public float SourceConstructorLuminance18=>1;
    public string SourceEvidence{get;}
    public static OriginalMapPostEffect3ApplyExposureReference CaptureCompletedSourceNode1(
        LoadedOriginalMapExposureSource source,OriginalMapExposurePacket exposure,
        OriginalMapExposureSelectedLightReceipt selectedView,
        string actualOwnedReferenceIdentity,OriginalMapPostEffect3ApplyView actualExposureOutputOwner,
        bool actualNode1CompletedExposurePublication,bool actualNoLaterReference50Override,string frameOwnerEvidence)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(exposure);
        ArgumentNullException.ThrowIfNull(selectedView);ArgumentNullException.ThrowIfNull(actualExposureOutputOwner);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOwnedReferenceIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualExposureOutputOwner.OwnerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameOwnerEvidence);
        if(!actualNode1CompletedExposurePublication||!actualNoLaterReference50Override)
            throw new NotSupportedException("Reference50 requires actual completed node1 publication with no later override.");
        if(source.Native.Locale is not ("SC" or "TC")||source.Texture.Object.ObjectId!=0x6C5630BB||
            source.Graph.Document.Nodes.Count<=1||source.Graph.Document.Nodes[1].Call.FactoryClassId!=0x5F0A8446||
            Convert.ToHexString(SHA256.HashData(source.Graph.Source.Bytes))!=source.Graph.Source.DecodedSha256)
            throw new InvalidDataException("Reference publication requires the exact paired SC/TC Exposure node1 source.");
        var args=source.Graph.Document.Nodes[1].Call.Arguments;
        var view=args.Single(a=>a.NameHash==0x9075EDA0);
        var flag=args.Single(a=>a.NameHash==0x05D92ACC);
        var target=args.Single(a=>a.NameHash==0x5C1A1454);
        if(view.RawValue.Length!=1||view.RawValue[0]!=0||flag.RawValue.Length!=2||
            BinaryPrimitives.ReadUInt16LittleEndian(flag.RawValue)!=3||target.RawValue.Length!=4||
            BinaryPrimitives.ReadUInt32LittleEndian(target.RawValue)!=TextureObjectIdConstant)
            throw new InvalidDataException("Exposure publication needs actual node1 View0/Flag3/target6C5630BB.");
        if(exposure.FrameNumber<=0||exposure.FrameNumber!=selectedView.FrameNumber||
            exposure.FrameNumber!=actualExposureOutputOwner.FrameNumber||
            selectedView.OwnedApplyReferenceIdentity!=actualOwnedReferenceIdentity)
            throw new InvalidDataException("Exposure publication owners/reference belong to different frames.");
        float raw=selectedView.SelectedExternalLuminance??selectedView.SameCameraLuminance17C;
        if(!float.IsFinite(raw)||BitConverter.SingleToUInt32Bits(raw)!=BitConverter.SingleToUInt32Bits(exposure.Luminance))
            throw new InvalidDataException("Reference18 input differs from the actual selected View exposure generation.");
        // EA97EA MAXSS with the exact positive source epsilon. Finite inputs
        // retain IEEE32 bits; signed zero selects the positive epsilon.
        float epsilon=BitConverter.UInt32BitsToSingle(0x358637BD);
        float clamped=raw>epsilon?raw:epsilon;
        string nativeEvidence=source.Native.Locale=="TC"?
            "TC:9A4A77-constructor18=3F800000;9A4AA7-constructor50=null;"+
            "factory5F0A8446/E81200-param3-5C1A1454=6C5630BB;E813EC..E814B4-targetlookup;"+
            "E817AB/E817B0-arg18;E6CBCE/E6CBD2-CPU10;EA98D5/EA990B-C0BB30-outputwrapper;"+
            "E6CCFE/E6CD0C-Flag3;EA96C6/EA96D4-localflag;EA9C7C-bit2/EA9C82-output;"+
            "EA9CD4/EA9CDA-D6F710;D6F73D-reference50;EA97EA-epsilon358637BD/EA98A3-reference18;":
            "SC:9A5A07-constructor18=3F800000;9A5A37-constructor50=null;"+
            "factory5F0A8446/16E7A40-v58-E822B0;E8251F/E8252F-targetlookup;"+
            "E8285B/E82860-arg18;E6DC7E/E6DC82-CPU10;EAA8C5/EAA8FB-C0D000-outputwrapper;"+
            "E6DDAE/E6DDBC-Flag3;EAA6B6/EAA6C4-localflag;EAAC6C-bit2/EAAC72-output;"+
            "EAACC4/EAACCA-E487B0;E487DD-reference50;EAA7DA-epsilon358637BD/EAA893-reference18;";
        string evidence=nativeEvidence+
            "exe="+source.Native.ExecutableSha256+";text="+source.Native.DecodedTextSha256+";"+
            "owned-completed-source-node1-no-later-reference50-override;"+
            source.SourceResourceUri+";"+selectedView.SourceProvenance+";"+frameOwnerEvidence;
        return new(exposure.FrameNumber,actualOwnedReferenceIdentity,actualExposureOutputOwner,clamped,evidence);
    }
    private const uint TextureObjectIdConstant=0x6C5630BB;
}
