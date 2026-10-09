using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>The native no-controller preparation ABI is selected by an actual
/// header word carried into descriptor58. This is independent of functor index,
/// shader version, stage counts and the preparation's returned predicate.</summary>
public sealed class MapPostEffect3ApplyPreparationSelection
{
    private MapPostEffect3ApplyPreparationSelection(uint selector,string owner)
    {Descriptor58=selector;OwnerIdentity=owner;}
    public uint Descriptor58{get;}
    public string OwnerIdentity{get;}
    public MapPostEffect3ApplyUncontrolledVariant Variant=>Descriptor58==6
        ?MapPostEffect3ApplyUncontrolledVariant.LegacyCameraFields
        :MapPostEffect3ApplyUncontrolledVariant.LensPresetCameraFields;
    public string SourceEvidence=>";header44/remainder24;-arg6;-descriptor58;-selector6"+OwnerIdentity;
    public static MapPostEffect3ApplyPreparationSelection ReadActualSource(
        MapFinalOutputPostEffect3Source source,MapPostEffect3Container container,string descriptorOwner)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(container);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptorOwner);
        if(source.SelectedTemplateMode3C0!=1||container.Sha256!=MapFinalOutputPostEffect3Source.PayloadSha256||
            Convert.ToHexString(SHA256.HashData(container.Bytes.Span))!=container.Sha256||
            !source.Payload.Span.SequenceEqual(container.Bytes.Span))
            throw new InvalidDataException("Preparation selection requires the exact owned actual PostEffect3 payload.");
        return new(BinaryPrimitives.ReadUInt32LittleEndian(container.Bytes.Span.Slice(0x44,4)),descriptorOwner);
    }
}
