using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Owned fresh source Camera's two Apply LDR lookup inputs. Source
/// key933 drives CPU2ED0; CAAEE0 copies it to embedded2B60/Camera1830 and
/// explicitly clears embedded2B70/Camera1840. This bounded default receipt
/// admits source key0 only; a nonzero key requires actual resource resolution.
/// It does not describe later native Camera edits or captured native memory.</summary>
public sealed class MapFinalOutputCameraBloomSource
{
    private readonly byte[] prefix;
    private readonly MapCameraConstructionPacket snapshot;
    private MapFinalOutputCameraBloomSource(MapCameraConstructionPacket camera,string owner,float scalar)
    {
        snapshot=camera.CopyAcceptedSnapshot();prefix=snapshot.Prefix.ToArray();
        CameraTailOwnerIdentity=owner;WholeCamera1384=scalar;
    }
    public string CameraTailOwnerIdentity{get;}
    public long FrameNumber=>snapshot.FrameNumber;
    public uint CameraObjectId=>snapshot.Source.Camera.ObjectId;
    public uint SourcePrimaryLdrLutKey=>0;
    public bool ResolvedCamera1830Present=>false;
    public bool ResolvedCamera1840Present=>false;
    public float WholeCamera1384{get;}
    /// <summary>No literal/current writer has been supplied for1398. Both
    /// null lookups skip its blend branches; this receipt never invents it.</summary>
    public float? WholeCamera1398=>null;
    public bool WholeCamera1398RequiredForBloom=>false;
    public float CurrentMaxLum=>0f;
    public string SourceEvidence=>"owned-fresh-no-later-resource-writer;"+snapshot.Source.ConstructorOrigin+
        ";TC:C4F6F0/C57C3A/C57C7A:key933=0=>CPU2ED0=null;"+
        "CAAEE0/CAB26E/CAB275:2ED0=>2B60=1830;CAB27C/CAB285:2B70=1840=null;"+
        "C43B92/C43B98:CPU10D0=>26B4=1384;"+
        "EB6CF1/EB712D:both-resolved-null=>BloomMaxLum0-without1398;"+CameraTailOwnerIdentity;

    /// <summary>Construct a separate owned tail receipt for the existing fresh
    /// Camera packet. Source is canonically verified. Later resource callbacks
    /// need another receipt; their values cannot reuse this fresh constructor.</summary>
    public static MapFinalOutputCameraBloomSource ConstructFreshSource(
        MapCameraConstructionPacket camera,string cameraTailOwnerIdentity)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentException.ThrowIfNullOrWhiteSpace(cameraTailOwnerIdentity);
        if(camera.FrameNumber<=0)throw new InvalidDataException("Fresh Bloom Camera requires its owned frame.");
        var verified=MapCameraConstructionSource.ReadPerspective(camera.Source.Resource,camera.Source.Camera.ObjectId);
        uint ldr=Word(verified,933,0xDF7EBEED,5);
        if(ldr!=0)throw new InvalidDataException("Nonzero source PostEffect3LdrLut needs its actual typed resource lookup; no null fallback is admitted.");
        float scalar=BitConverter.UInt32BitsToSingle(Word(verified,953,0x0D04C5AC,8));
        if(!float.IsFinite(scalar))throw new InvalidDataException("Source whole-Camera1384 is not finite.");
        return new(camera,cameraTailOwnerIdentity,scalar);
    }
    public void ValidateSameCamera(MapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var verified=MapCameraConstructionSource.ReadPerspective(camera.Source.Resource,camera.Source.Camera.ObjectId);
        if(camera.FrameNumber!=FrameNumber||camera.Source.Camera.ObjectId!=CameraObjectId||
            !camera.Prefix.Span.SequenceEqual(prefix)||Word(verified,933,0xDF7EBEED,5)!=0||
            Word(verified,953,0x0D04C5AC,8)!=BitConverter.SingleToUInt32Bits(WholeCamera1384))
            throw new InvalidDataException("Fresh Bloom receipt cannot be moved to a different Camera frame/source or later resource/scalar state.");
    }
    private static uint Word(LoadedMapCameraConstructionSource source,int ordinal,uint hash,uint type)
    {
        var property=source.Camera.Properties[ordinal];
        if(property.Index!=ordinal||property.NameHash!=hash||property.TypeCode!=type||property.ElementCount!=1||
            property.ElementSize!=4||property.RawValue.Length!=4)
            throw new InvalidDataException("Camera Bloom source property differs from its native loader binding.");
        return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
    }
}
