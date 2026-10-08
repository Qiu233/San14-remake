using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The actual seasonal Directional FE8 Static declaration and its
/// original legacy0500 BC1 image. A narrow source owner; no GPU-ready claim.</summary>
public static class SourceResourceRegistrationDirectionalDiffuse
{
    public const uint ObjectKey=0x73ECE15E,DefinitionResourceId=0xD2ECCCDF,TextureContextResourceId=0xDE92A1A0;
    public const string DefinitionArchive="KIDSSystemResource",TextureArchive="RRPreview";
    public const string DefinitionSha256="BEC57B5340D4FE8B4762DDA0B857BFC7717F971391E7B271EFA0728386EF99EE";
    public const string TextureSha256="7D90AB2DB1F3D98B7C5BB851F568D9A7CE6F3AC73C1AE74AE1732B7CC8661CB9";
    public const uint TextureInterfaceClassId=0xB672FAA2;
    // Paired original resource/SRV tables at pixel-format row06.
    public const uint NativeResourceFormat=70,NativeBaseViewFormat=71,NativeAlternateViewFormat=72;
    public const int BodyOffset=44,BodyBytes=2097152;
    private static readonly byte[] Header=Convert.FromHexString("47543147303530302C0020002000000001000000000000000000000000000000040000001006BB0000102100");

    public static OriginalStaticTextureBinding Load(OriginalGameRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Read(repository.LoadObjects(DefinitionArchive,DefinitionResourceId),repository.LoadRdbAsset(TextureArchive,TextureContextResourceId));
    }
    public static OriginalStaticTextureBinding Read(LoadedObjects definitions,LoadedRdbAsset texture)
    {
        ArgumentNullException.ThrowIfNull(definitions);ArgumentNullException.ThrowIfNull(texture);
        ValidateAsset(definitions.Source,DefinitionArchive,DefinitionResourceId,DefinitionSha256);
        ValidateAsset(texture,TextureArchive,TextureContextResourceId,TextureSha256);
        var definitionSource=definitions.Source with{Bytes=definitions.Source.Bytes.ToArray()};
        var textureSource=texture with{Bytes=texture.Bytes.ToArray()};
        var canonical=DokReader.Read(definitionSource.Bytes);var objects=canonical.Objects.Where(o=>o.ObjectId==ObjectKey).ToArray();
        if(objects.Length!=1||objects[0].Kind!=DokObjectKind.Inline||objects[0].ClassId!=0xFF7DBFD4||objects[0].Properties.Count!=2)
            throw new InvalidDataException("Directional diffuse source requires its exact inline Static declaration.");
        var context=Property(objects[0],0,0x6C7321D2,5,TextureContextResourceId);
        var image=Property(objects[0],1,0x00200571,4,0);
        if(textureSource.Bytes.Length!=BodyOffset+BodyBytes||!textureSource.Bytes.AsSpan(0,BodyOffset).SequenceEqual(Header))
            throw new InvalidDataException("Directional diffuse legacy0500 source header or compressed body extent differs.");
        var range=new G1EncodedRange(BodyOffset,BodyBytes);
        var mip=new G1TextureMip(0,2048,2048,BodyBytes,range);
        var metadata=new G1TextureImage(0,6,2048,2048,1,1,0,1,0,G1TextureSubresourceLayout.SingleLayerMipChain,
            new(36,textureSource.Bytes.Length-36),range,textureSource.Bytes.AsSpan(36,8).ToArray(),Array.AsReadOnly(new[]{mip}),1,"0500");
        var container=new G1TextureContainer("0500",0,textureSource.Bytes.Length,32,[4],textureSource.Bytes.AsSpan(0,36).ToArray(),Array.AsReadOnly(new[]{metadata}));
        return new(new(canonical,definitionSource),objects[0],context,image,new(container,textureSource),0);
    }
    public static void Validate(OriginalStaticTextureBinding source)
    {
        ArgumentNullException.ThrowIfNull(source);var actual=Read(source.Definitions,source.Resource.Source);
        if(source.Definition.ObjectId!=ObjectKey||source.ImageIndex!=0||source.Image.PixelFormat!=6||source.Image.Width!=2048||
            source.Image.Height!=2048||source.Image.TextureSystem!=0||source.Image.LayerCount!=1||source.Image.MipCount!=1||
            source.Image.EncodedDataRange!=actual.Image.EncodedDataRange||!source.Image.RawHeader.AsSpan().SequenceEqual(actual.Image.RawHeader))
            throw new InvalidDataException("Directional diffuse image metadata differs from its exact source.");
    }
    public static ReadOnlyMemory<byte> GetNativeBody(OriginalStaticTextureBinding source)
    {Validate(source);return source.Resource.Source.Bytes.AsMemory(BodyOffset,BodyBytes).ToArray();}
    public static byte[] SamplerKey(OriginalStaticTextureBinding source)
    {return SamplerRecipe(source).RawKey.ToArray();}
    public static G1TextureSamplerRecipe SamplerRecipe(OriginalStaticTextureBinding source)
    {
        Validate(source);
        // Original0500 has no extended bias/LOD header. Its exact base flags
        // 00211000 select linear min/mag, one mip, wrap U/V and clamp W.
        // The loader sampler circuit is independent of format06 BC1.
        return new(0x00211000,Convert.FromHexString("1014300000000000"),0x14,1,1,3,0,1,4,0,0);
    }
    private static DokProperty Property(DokObject source,int ordinal,uint hash,uint type,uint word)
    {
        var p=source.Properties[ordinal];
        if(p.Index!=ordinal||p.NameHash!=hash||p.TypeCode!=type||p.ElementCount!=1||p.ElementSize!=4||p.RawValue.Length!=4||
            BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue)!=word)
            throw new InvalidDataException("Directional diffuse Static descriptor differs.");
        return p;
    }
    private static void ValidateAsset(LoadedRdbAsset source,string archive,uint id,string hash)
    {
        if(source.ResourceUri!=GameResourceId.Rdb(archive,id).ToString()||source.FileId!=id||source.DecodedSha256!=hash||
            Convert.ToHexString(SHA256.HashData(source.Bytes))!=hash)
            throw new InvalidDataException("Directional diffuse source asset identity/hash differs.");
    }
}
