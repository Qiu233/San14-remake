using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Independent fresh source Camera fields needed by the E71690 and
/// E70780 no-controller preparations. Complements the existing prefix, Extra
/// scalars and 44-word PostEffect3 tail; no full Camera tail or defaults blob.</summary>
public sealed class OriginalMapPostEffect3ApplyUncontrolledCameraInputs
{
    private readonly Dictionary<int,uint> words;
    private readonly OriginalMapCameraConstructionPacket camera;
    private readonly OriginalMapPostEffect3ApplyFreshCameraScalars tail;
    private OriginalMapPostEffect3ApplyUncontrolledCameraInputs(OriginalMapCameraConstructionPacket snapshot,
        string owner,Dictionary<int,uint> values)
    {camera=snapshot;OwnerIdentity=owner;words=values;tail=OriginalMapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(snapshot,owner+"/tail");}
    public long FrameNumber=>camera.FrameNumber;
    public string OwnerIdentity{get;}
    public string SourceEvidence=>camera.Source.ConstructorOrigin+
        ";TC:C409C0-source-copy;21-extra-property-bound-words;"+
        (camera.CurrentSeasonPostEffectScalars?.SourceEvidence??"fresh-owned-no-later-tail-writer")+";"+OwnerIdentity;
    public uint Word(int offset)=>words.TryGetValue(offset,out uint v)?v:
        throw new NotSupportedException($"Fresh uncontrolled Camera field {offset:X} is unsupported.");
    public float Float(int offset)=>BitConverter.UInt32BitsToSingle(Word(offset));
    public IReadOnlyList<int> SupportedExtraOffsets=>Array.AsReadOnly(Bindings.Select(b=>b.CameraOffset).ToArray());
    public static OriginalMapPostEffect3ApplyUncontrolledCameraInputs ConstructFreshSource(
        OriginalMapCameraConstructionPacket currentCamera,string actualInputOwner)
    {
        ArgumentNullException.ThrowIfNull(currentCamera);ArgumentException.ThrowIfNullOrWhiteSpace(actualInputOwner);
        if(currentCamera.FrameNumber<=0)throw new InvalidDataException("Uncontrolled Camera inputs require an owned frame.");
        var snapshot=currentCamera.CopyAcceptedSnapshot();var values=new Dictionary<int,uint>();
        snapshot.CurrentSeasonPostEffectScalars?.ValidateForCamera(snapshot);
        foreach(var b in Bindings)
        {
            var field=snapshot.Source.Camera.Properties[b.Ordinal];
            if(field.Index!=b.Ordinal||field.NameHash!=b.NameHash||field.TypeCode!=b.TypeCode||
                field.ElementCount!=b.Count||field.ElementSize!=4||field.RawValue.Length!=b.Count*4||b.Component>=b.Count)
                throw new InvalidDataException("Uncontrolled Camera input differs from its exact loader/reflection binding.");
            uint value=BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue.AsSpan(b.Component*4,4));
            if(snapshot.CurrentSeasonPostEffectScalars is{}current&&current.TryReadWord(b.CameraOffset,out uint written))
                value=written;
            values.Add(b.CameraOffset,value);
        }
        var result=new OriginalMapPostEffect3ApplyUncontrolledCameraInputs(snapshot,actualInputOwner,values);
        if((result.tail.Word(0x132C)&1)!=0)throw new NotSupportedException("ObjectDistance Camera12FC selects C4436F pose-distance branch; fresh static source copy is insufficient.");
        return result;
    }
    public void ValidateSameCamera(OriginalMapCameraConstructionPacket candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        camera.RequireEquivalentCurrentPostEffectScalars(candidate);
        if(candidate.FrameNumber!=FrameNumber||candidate.Source.Camera.ObjectId!=camera.Source.Camera.ObjectId||
            !candidate.Prefix.Span.SequenceEqual(camera.Prefix.Span))
            throw new InvalidDataException("Uncontrolled inputs belong to a different Camera frame/source/prefix.");
        _=OriginalMapCameraConstructionSource.ReadPerspective(candidate.Source.Resource,candidate.Source.Camera.ObjectId);
    }
    /// <summary>Read only source-proven ranges actually consumed by the named
    /// writer. Prefix includes actual same-frame matrices/optics; tail and extra
    /// words retain their own fresh source provenance. Gaps never read as zero.</summary>
    public ReadOnlyMemory<byte> ReadCameraBytes(OriginalMapCameraConstructionPacket candidate,int offset,int count)
    {
        ValidateSameCamera(candidate);
        if(offset<0||count<=0||offset%4!=0||count%4!=0||offset>int.MaxValue-count)
            throw new InvalidDataException("Uncontrolled Camera input range is invalid.");
        if(offset+count<=candidate.Prefix.Length)return candidate.Prefix.Span.Slice(offset,count).ToArray();
        byte[] result=new byte[count];
        for(int at=0;at<count;at+=4)
        {
            int current=offset+at;uint word;
            if(words.TryGetValue(current,out uint value))word=value;
            else if(current>=0x1320&&current<=0x13DC)word=tail.Word(current);
            else if(current>=0x404&&current<=0x410)
                word=candidate.CurrentSeasonPostEffectScalars is{}selected&&selected.TryReadWord(current,out uint copied)?
                    copied:candidate.Source.CpuWord(0x1A8+current-0x404);
            else throw new NotSupportedException($"Uncontrolled Camera range contains unsupported field {current:X}.");
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(at,4),word);
        }
        return result;
    }
    private readonly record struct Binding(int CameraOffset,int Ordinal,uint NameHash,uint TypeCode,int Count,int Component);
    private static readonly Binding[] Bindings=
    [
        new(0x7A8,49,0x4226C411u,8u,1,0),
        new(0x7AC,50,0x468E5E0Au,8u,1,0),
        new(0x414, 691, 0x20196D95u, 5u, 1, 0),
        new(0x418, 1025, 0x855F598Cu, 4u, 1, 0),
        new(0x41C, 1026, 0x54A05D41u, 8u, 1, 0),
        new(0x420, 1027, 0xDF0F678Fu, 8u, 1, 0),
        new(0x424, 1028, 0xBEB97EB5u, 8u, 1, 0),
        new(0x428, 1029, 0x4DCC7F17u, 4u, 1, 0),
        new(0x42C, 1030, 0x07D11283u, 4u, 1, 0),
        new(0xE2C, 137, 0xAD7E9994u, 8u, 1, 0),
        new(0xE30, 138, 0x834DC2A6u, 8u, 1, 0),
        new(0xE34, 139, 0x7AD72492u, 8u, 1, 0),
        new(0x1300, 275, 0x995A7B0Eu, 8u, 1, 0),
        new(0x1304, 276, 0x04CFDD5Du, 8u, 1, 0),
        new(0x1308, 277, 0x6934EE7Cu, 8u, 1, 0),
        new(0x130C, 278, 0xF2B45C7Fu, 8u, 1, 0),
        new(0x1310, 279, 0x28A60BB6u, 4u, 1, 0),
        new(0x1314, 280, 0x39FE6CF0u, 8u, 1, 0),
        new(0x1318, 290, 0xEB8B5927u, 8u, 1, 0),
        new(0x131C, 292, 0xA925B2FFu, 4u, 1, 0),
        new(0x12FC, 274, 0xEE9D93F9u, 8u, 1, 0),
    ];
}
