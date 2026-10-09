using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Fresh scalar copies into the owned prepared Camera tail.
/// Only 44 native property-bound words are present; the four intervening
/// resource/blend words have no implicit zero. No full tail blob is invented.
/// Later LensPreset or per-view callbacks require their own writer receipt.</summary>
public sealed class MapPostEffect3ApplyFreshCameraScalars
{
    private readonly Dictionary<int,uint> words;
    private readonly MapCameraConstructionPacket camera;
    private MapPostEffect3ApplyFreshCameraScalars(MapCameraConstructionPacket snapshot,
        string owner,Dictionary<int,uint> values)
    {camera=snapshot;OwnerIdentity=owner;words=values;}
    public long FrameNumber=>camera.FrameNumber;
    public uint CameraObjectId=>camera.Source.Camera.ObjectId;
    public string OwnerIdentity{get;}
    public string SourceEvidence=>camera.Source.ConstructorOrigin+
        "TC;native-reflection-;loader-"+
        (camera.CurrentSeasonPostEffectScalars?.SourceEvidence??"fresh-source-no-later-tail-writer")+";"+OwnerIdentity;
    public IReadOnlyList<int> SupportedCameraOffsets=>Array.AsReadOnly(Bindings.Select(b=>b.CameraOffset).ToArray());
    public uint Word(int cameraOffset)=>words.TryGetValue(cameraOffset,out uint value)?value:
        throw new InvalidDataException($"Fresh source Camera tail offset {cameraOffset:X} has no admitted writer.");
    public float Float(int cameraOffset)=>BitConverter.UInt32BitsToSingle(Word(cameraOffset));
    public int Int(int cameraOffset)=>unchecked((int)Word(cameraOffset));
    public static MapPostEffect3ApplyFreshCameraScalars ConstructFreshSource(
        MapCameraConstructionPacket currentCamera,string actualTailOwner)
    {
        ArgumentNullException.ThrowIfNull(currentCamera);ArgumentException.ThrowIfNullOrWhiteSpace(actualTailOwner);
        if(currentCamera.FrameNumber<=0)throw new InvalidDataException("Fresh tail requires an owned frame.");
        var snapshot=currentCamera.CopyAcceptedSnapshot();
        var source=snapshot.Source;var values=new Dictionary<int,uint>();
        snapshot.CurrentSeasonPostEffectScalars?.ValidateForCamera(snapshot);
        foreach(var b in Bindings)
        {
            var field=source.Camera.Properties[b.Ordinal];
            if(field.Index!=b.Ordinal||field.NameHash!=b.NameHash||field.TypeCode!=b.TypeCode||
                field.ElementCount!=1||field.ElementSize!=4||field.RawValue.Length!=4)
                throw new InvalidDataException("Fresh PostEffect3 Camera field differs from its native loader/reflection binding.");
            uint value=BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
            if(snapshot.CurrentSeasonPostEffectScalars is{}current&&current.TryReadWord(b.CameraOffset,out uint written))
                value=written;
            values.Add(b.CameraOffset,value);
        }
        return new(snapshot,actualTailOwner,values);
    }
    public void ValidateSameCamera(MapCameraConstructionPacket candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        camera.RequireEquivalentCurrentPostEffectScalars(candidate);
        if(candidate.FrameNumber!=FrameNumber||candidate.Source.Camera.ObjectId!=CameraObjectId||
            !candidate.Prefix.Span.SequenceEqual(camera.Prefix.Span)||
            candidate.Source.Resource.Source.DecodedSha256!=camera.Source.Resource.Source.DecodedSha256)
            throw new InvalidDataException("Fresh tail cannot be reused by a different Camera frame/source/prefix.");
        _=MapCameraConstructionSource.ReadPerspective(candidate.Source.Resource,CameraObjectId);
    }
    private readonly record struct Binding(int CameraOffset,int Ordinal,uint NameHash,uint TypeCode);
    private static readonly Binding[] Bindings=
    [
        new(0x1320, 929, 0x03C0227Eu, 4u),
        new(0x1324, 955, 0x8CC2593Au, 8u),
        new(0x1328, 984, 0x182ED000u, 4u),
        new(0x132C, 315, 0xC651B23Au, 4u),
        new(0x1330, 418, 0x4DCEB3FCu, 8u),
        new(0x1334, 419, 0xCC53256Au, 8u),
        new(0x1338, 420, 0x0E8A7670u, 8u),
        new(0x133C, 421, 0xB406C48Au, 4u),
        new(0x1340, 454, 0x3278E4A0u, 8u),
        new(0x1344, 959, 0x6B7FBFDCu, 4u),
        new(0x1348, 970, 0x4C1DAA58u, 4u),
        new(0x134C, 455, 0x60B60FF7u, 8u),
        new(0x1350, 456, 0x8130A3ACu, 8u),
        new(0x1354, 949, 0xC81F204Du, 8u),
        new(0x1358, 950, 0x119CC5BAu, 8u),
        new(0x135C, 951, 0x69C17F10u, 8u),
        new(0x1360, 952, 0x80119D80u, 8u),
        new(0x1364, 457, 0x7729E0F8u, 8u),
        new(0x1368, 605, 0x752C9273u, 4u),
        new(0x136C, 943, 0x3F80B302u, 8u),
        new(0x1370, 944, 0x67E49830u, 8u),
        new(0x1374, 945, 0xD66E4FA5u, 8u),
        new(0x1378, 946, 0x4C05D973u, 8u),
        new(0x137C, 947, 0x6DA5B0FCu, 4u),
        new(0x1380, 948, 0x0286381Au, 8u),
        new(0x1384, 953, 0x0D04C5ACu, 8u),
        new(0x1388, 966, 0x84CB7A7Bu, 8u),
        new(0x139C, 1067, 0x3A35E6F5u, 8u),
        new(0x13A0, 1068, 0x2AF009C7u, 8u),
        new(0x13A4, 1069, 0x9DC863D7u, 8u),
        new(0x13A8, 1070, 0xE7F79A0Fu, 8u),
        new(0x13AC, 1071, 0x356841BCu, 8u),
        new(0x13B0, 1092, 0xBC94863Eu, 4u),
        new(0x13B4, 1093, 0xF9B44A5Du, 4u),
        new(0x13B8, 1094, 0xDD69E8A8u, 8u),
        new(0x13BC, 1095, 0xC33E91F7u, 8u),
        new(0x13C0, 1103, 0xF2555D35u, 8u),
        new(0x13C4, 1104, 0xC7585A9Du, 8u),
        new(0x13C8, 1105, 0x48ECAF7Eu, 8u),
        new(0x13CC, 1075, 0x2F101A92u, 5u),
        new(0x13D0, 1076, 0x1658638Bu, 5u),
        new(0x13D4, 1077, 0x0B3B7FADu, 5u),
        new(0x13D8, 1078, 0xBA41042Eu, 8u),
        new(0x13DC, 1079, 0x7547C33Bu, 8u),
    ];
}
