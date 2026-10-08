using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>The native no-controller preparation ABI is selected by an actual
/// header word carried into descriptor58. This is independent of functor index,
/// shader version, stage counts and the preparation's returned predicate.</summary>
public sealed class OriginalMapPostEffect3ApplyPreparationSelection
{
    private OriginalMapPostEffect3ApplyPreparationSelection(uint selector,string owner)
    {Descriptor58=selector;OwnerIdentity=owner;}
    public uint Descriptor58{get;}
    public string OwnerIdentity{get;}
    public OriginalMapPostEffect3ApplyUncontrolledVariant Variant=>Descriptor58==6
        ?OriginalMapPostEffect3ApplyUncontrolledVariant.NativeE70780
        :OriginalMapPostEffect3ApplyUncontrolledVariant.NativeE71690;
    public string SourceEvidence=>"TC:1147218-header44/remainder24;114722A-arg6;1147510/1147517-descriptor58;EB7E32-selector6;"+OwnerIdentity;
    public static OriginalMapPostEffect3ApplyPreparationSelection ReadActualSource(
        OriginalMapFinalOutputPostEffect3Source source,OriginalMapPostEffect3Container container,string descriptorOwner)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(container);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptorOwner);
        if(source.SelectedTemplateMode3C0!=1||container.Sha256!=OriginalMapFinalOutputPostEffect3Source.PayloadSha256||
            Convert.ToHexString(SHA256.HashData(container.Bytes.Span))!=container.Sha256||
            !source.Payload.Span.SequenceEqual(container.Bytes.Span))
            throw new InvalidDataException("Preparation selection requires the exact owned actual PostEffect3 payload.");
        return new(BinaryPrimitives.ReadUInt32LittleEndian(container.Bytes.Span.Slice(0x44,4)),descriptorOwner);
    }
}
