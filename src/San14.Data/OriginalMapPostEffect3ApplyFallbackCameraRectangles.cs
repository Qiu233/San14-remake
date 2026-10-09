using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Only the fresh source constructor's rectangles, copied into the
/// owned same-frame Camera. Later viewport/capture writers remain separate
/// inputs; this receipt does not attest a current original-game Camera.</summary>
public sealed class OriginalMapPostEffect3ApplyFallbackCameraRectangles
{
    private readonly OriginalMapCameraConstructionPacket camera;
    private readonly uint zeroWord;
    private readonly OriginalMapPostEffect3FallbackCameraConstructorWords native;
    private OriginalMapPostEffect3ApplyFallbackCameraRectangles(OriginalMapCameraConstructionPacket snapshot,
        OriginalMapPostEffect3FallbackCameraConstructorWords constructor,string owner)
    {camera=snapshot;OwnerIdentity=owner;native=constructor;zeroWord=snapshot.Source.CpuWord(0x1240);}
    public long FrameNumber=>camera.FrameNumber;
    public string OwnerIdentity{get;}
    public uint SourceCameraObjectId=>camera.Source.Camera.ObjectId;
    public Vector4 Viewport1540=>new(F(zeroWord),F(zeroWord),F(native.Source1278Word),F(native.Source127CWord));
    public Vector4 SpecialRectangle152C=>new(F(zeroWord),F(zeroWord),F(zeroWord),F(zeroWord));
    public string SourceEvidence=>camera.Source.ConstructorOrigin+
        ";source125C/1270;-embedded1330"+
        "all1119-property-pointer-ranges-exclude125C..1280;fresh-source-only;"+native.SourceEvidence+";"+OwnerIdentity;
    public static OriginalMapPostEffect3ApplyFallbackCameraRectangles ConstructFreshSource(
        OriginalMapCameraConstructionPacket ownedCamera,OriginalMapPostEffect3FallbackCameraConstructorWords native,
        string actualInputOwner)
    {
        ArgumentNullException.ThrowIfNull(ownedCamera);ArgumentNullException.ThrowIfNull(native);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualInputOwner);
        if(ownedCamera.FrameNumber<=0)throw new InvalidDataException("Fallback fresh rectangles require an owned frame.");
        if(native.Locale!=ownedCamera.ExecutableLocale||ownedCamera.Source.CpuWord(0x1240)!=0)
            throw new InvalidDataException("Fallback fresh rectangles require their exact locale and constructor-clear source.");
        //CopyAcceptedSnapshot re-reads the original pinned resource and verifies
        //all1111 property descriptors/bytes against its canonical document.
        return new(ownedCamera.CopyAcceptedSnapshot(),native,actualInputOwner);
    }
    public void ValidateSameCamera(OriginalMapCameraConstructionPacket candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if(candidate.FrameNumber!=FrameNumber||candidate.Source.Camera.ObjectId!=SourceCameraObjectId||
            !candidate.Prefix.Span.SequenceEqual(camera.Prefix.Span))
            throw new InvalidDataException("Fallback rectangles belong to a different accepted Camera frame/source/prefix.");
        _=OriginalMapCameraConstructionSource.ReadPerspective(candidate.Source.Resource,candidate.Source.Camera.ObjectId);
    }
    public ReadOnlyMemory<byte> ReadCameraBytes(OriginalMapCameraConstructionPacket candidate,int offset,int count)
    {
        ValidateSameCamera(candidate);
        Vector4 value=offset switch
        {
            0x152C when count==16=>SpecialRectangle152C,
            0x1540 when count==16=>Viewport1540,
            _=>throw new NotSupportedException("Only the two complete fresh Camera rectangle fields are proved.")
        };
        byte[] bytes=new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(bytes,value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(4),value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(8),value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12),value.W);
        return bytes;
    }
    private static float F(uint word)=>BitConverter.UInt32BitsToSingle(word);
}
