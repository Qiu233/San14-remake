using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The actual node193 PostEffect3 declaration. Its 3SPK1000 payload
/// has a different loader and runtime descriptor from A1C83E34's _SPK4100.
/// Loading this receipt does not create its native programs or execute a pass.</summary>
public sealed class MapFinalOutputPostEffect3Source
{
    public const uint ResourceId=0x6CA7130C,ClassId=0xEA69FA13,PayloadProperty=0x7AE38475;
    public const string ResourceSha256="0E03DB0190A7C6E1905D15DF8178FCA1EC1FF97E93E1E3F188F8DD3A4513C496";
    public const string PayloadSha256="588FCC56248A90EE422971050B9D9EAB3305983F6B2AC0A33F5646C49C0623F0";
    private readonly byte[] payload;
    private MapFinalOutputPostEffect3Source(LoadedObjects source,byte[] bytes,int mode,string modeEvidence)
    {Source=source;payload=bytes;SelectedTemplateMode3C0=mode;TemplateEvidence=modeEvidence;}
    public LoadedObjects Source{get;}
    public ReadOnlyMemory<byte> Payload=>payload;
    public int SelectedTemplateMode3C0{get;}
    public string TemplateEvidence{get;}
    public uint NativeHeaderBytes=>BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(12,4));
    public string CreatorEvidence=>"post-effect-factory;caller-context-extent3E8/3EC;create-module->initialize-descriptor40;cpu-descriptor138";

    public static MapFinalOutputPostEffect3Source Load(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Read(repository.LoadRenderGraph("system",0x024496EE),repository.LoadObjects("system",ResourceId),
            repository.LoadObjects("KIDSSystemResource",0x6D011726),0x899F101B);
    }
    public static MapFinalOutputPostEffect3Source Read(LoadedRenderGraph graph,LoadedObjects effect,
        LoadedObjects selectedCommonTemplate,uint selectedCommonObjectId)
    {
        ArgumentNullException.ThrowIfNull(graph);ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(selectedCommonTemplate);
        if(graph.Document.GraphKind!=CompiledGraphKind.RenderGraph||graph.Document.Nodes.Count<=193)
            throw new InvalidDataException("PostEffect3 requires the actual node193 graph.");
        var apply=graph.Document.Nodes[193];
        var key=apply.Call.Arguments.Where(p=>p.NameHash==0xCE63C026&&p.DescriptorWord==0x05000001).ToArray();
        if(apply.Call.FactoryClassId!=MapFinalOutputResources.ApplyFactory||key.Length!=1||
            key[0].RawValue.Length!=4||BinaryPrimitives.ReadUInt32LittleEndian(key[0].RawValue)!=ResourceId)
            throw new InvalidDataException("Apply's PostEffect3 key is not the recovered source declaration.");
        if(effect.Source.FileId!=ResourceId||effect.Source.DecodedSha256!=ResourceSha256||
            Convert.ToHexString(SHA256.HashData(effect.Source.Bytes))!=ResourceSha256)
            throw new InvalidDataException("PostEffect3 source bytes differ from the installed baseline.");
        var rows=effect.Document.Objects.Where(o=>o.ObjectId==ResourceId).ToArray();
        if(rows.Length!=1||rows[0].ClassId!=ClassId||rows[0].Kind!=DokObjectKind.Inline||rows[0].Properties.Count!=1)
            throw new InvalidDataException("PostEffect3 requires its EA69FA13 inline source object.");
        var field=rows[0].Properties.Single();byte[] bytes=field.RawValue.ToArray();
        if(field.NameHash!=PayloadProperty||field.TypeCode!=1||field.ElementCount!=564068||
            bytes.Length!=564068||Convert.ToHexString(SHA256.HashData(bytes))!=PayloadSha256||
            !bytes.AsSpan(0,8).SequenceEqual("3SPK1000"u8)||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8,4))!=bytes.Length||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12,4))!=20)
            throw new InvalidDataException("PostEffect3 needs the distinct hash-pinned 3SPK1000 payload.");
        int mode=ReadSelectedTemplateMode(selectedCommonTemplate,selectedCommonObjectId);
        return new(effect,bytes,mode,selectedCommonTemplate.Source.ResourceUri+$"#object/{selectedCommonObjectId:X8}"+
            $"@{selectedCommonTemplate.Source.DecodedSha256};property31->Common3C0->current-copy;caller-selected-template");
    }

    /// <summary>Source template loading, independent of a later
    /// active View. The caller must select the template explicitly.</summary>
    public static int ReadSelectedTemplateMode(LoadedObjects source,uint objectId)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(Convert.ToHexString(SHA256.HashData(source.Source.Bytes))!=source.Source.DecodedSha256)
            throw new InvalidDataException("CommonViewSetting template bytes differ from their source identity.");
        var rows=source.Document.Objects.Where(o=>o.ObjectId==objectId).ToArray();
        if(rows.Length!=1||rows[0].ClassId!=0xEF3D849D||rows[0].Kind!=DokObjectKind.Inline||rows[0].Properties.Count!=102)
            throw new InvalidDataException("Final module requires the selected 102-property CommonViewSetting.");
        var fields=rows[0].Properties.Where(p=>p.NameHash==0x668912BD).ToArray();
        if(fields.Length!=1||fields[0].TypeCode!=4||fields[0].ElementCount!=1||fields[0].RawValue.Length!=4)
            throw new InvalidDataException("Final Common3C0 needs its typed PostEffectModule scalar.");
        return BinaryPrimitives.ReadInt32LittleEndian(fields[0].RawValue);
    }

    /// <summary>Fresh construction uses actual caller-context Q3E8/Q3EC,
    /// not file-header dimensions or the Scene declaration. Supply the owned
    /// context extent; this does not assert an active original-game descriptor.</summary>
    public MapFinalOutputPostEffect3Owner CreateFreshForOwnedContext(int width,int height,
        string actualContextOwnerIdentity,string callerEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualContextOwnerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerEvidence);
        return MapFinalOutputPostEffect3Owner.CreatePresent(actualContextOwnerIdentity+"/PostEffect3-descriptor",
            width,height,Source.Source.ResourceUri+"@"+ResourceSha256+";"+CreatorEvidence+";owned-context-extent;"+callerEvidence);
    }
}
