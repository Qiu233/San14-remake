using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The sampler constructors and backend conversion implemented by
/// this project. No executable image or instruction data is loaded.</summary>
public sealed class LoadedDeferredSamplerNativeSource
{
    public string ResourceUri { get; }
    public string SourceDirectory { get; }
    public string Locale { get; }
    public string RenderRuleIdentity { get; }
    public string RuleVersion => San14.Data.RenderRuleIdentity.Version;
    internal uint ConstructorLowBits { get; }
    internal uint ConstructorMipCode { get; }
    internal uint[] Addresses { get; }
    internal uint[] Anisotropy { get; }
    internal uint[] Comparison { get; }
    internal byte[] DefaultDescriptor { get; }

    internal LoadedDeferredSamplerNativeSource(string sourceDirectory, string locale)
    {
        SourceDirectory=sourceDirectory;Locale=locale;
        RenderRuleIdentity=San14.Data.RenderRuleIdentity.For("deferred-sampler",locale);
        ResourceUri="san14://render-rules/deferred-sampler";
        ConstructorLowBits=0x1415;ConstructorMipCode=2;
        // Source packed address codes map to D3D wrap, clamp, mirror, border.
        Addresses=[1,3,2,4];Anisotropy=[1,2,4,6,8,10,12,16];Comparison=[1,2,3,4,5,6,7,8];
        uint[] descriptorWords=[0x15,3,3,3,0,1,1,
            BitConverter.SingleToUInt32Bits(1),BitConverter.SingleToUInt32Bits(1),
            BitConverter.SingleToUInt32Bits(1),BitConverter.SingleToUInt32Bits(1),
            BitConverter.SingleToUInt32Bits(-float.MaxValue),BitConverter.SingleToUInt32Bits(float.MaxValue)];
        DefaultDescriptor=new byte[52];
        for(int i=0;i<descriptorWords.Length;i++)
            BinaryPrimitives.WriteUInt32LittleEndian(DefaultDescriptor.AsSpan(i*4),descriptorWords[i]);
    }
}

public static partial class RenderSamplerSourceAdapters
{
    private static string Profile(string locale) => locale switch
    {
        "SC" or "TC" => locale,
        _ => throw new ArgumentOutOfRangeException(nameof(locale), "Native sampler layout supports SC and TC.")
    };

    public static LoadedDeferredSamplerNativeSource Load(string sourceDirectory, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        return new(Path.GetFullPath(sourceDirectory),Profile(locale));
    }

    public static byte[] BackendDefaultDescriptor(LoadedDeferredSamplerNativeSource native)
    {ArgumentNullException.ThrowIfNull(native);return native.DefaultDescriptor.ToArray();}

    /// <summary>Fresh wrapper key, before any source loader or plugin writes.</summary>
    public static byte[] RenderTextureKey(LoadedDeferredSamplerNativeSource native,int mipCount)
        => ConstructorKey(native,mipCount);
    public static byte[] CubeTextureKey(LoadedDeferredSamplerNativeSource native,int mipCount)
        => ConstructorKey(native,mipCount);

    /// <summary>CubeArrayRT04F7DD1F uses the common cube constructor and keeps
    /// its key. Its loader parses serialized sampler fields without applying
    /// the RT2D/RT2DArray postloader sampler writes.</summary>
    public static byte[] FinalCubeArrayTextureKey(LoadedDeferredSamplerNativeSource native,
        LoadedObjects source,uint objectId)
    {
        ArgumentNullException.ThrowIfNull(source);
        var original=San14.Data.Formats.DokReader.Read(source.Source.Bytes);
        var objects=original.Objects.Where(o=>o.ObjectId==objectId).ToArray();
        if(objects.Length!=1||objects[0].ClassId!=0x04F7DD1F||objects[0].Kind!=DokObjectKind.Inline)
            throw new InvalidDataException("Original cube-array sampler requires one explicit04F7DD1F source declaration.");
        var levels=objects[0].Properties.Where(p=>p.NameHash==0x7744A442).ToArray();
        if(levels.Length!=1||levels[0].TypeCode!=1||levels[0].ElementCount!=1||levels[0].RawValue.Length!=1)
            throw new InvalidDataException("Original cube-array sampler requires its actual typed LevelNum.");
        return CubeTextureKey(native,levels[0].RawValue[0]);
    }
    private static byte[] ConstructorKey(LoadedDeferredSamplerNativeSource native,int mipCount)
    {
        ArgumentNullException.ThrowIfNull(native);
        if(mipCount is <1 or >31)throw new InvalidDataException("Original constructor key requires an explicit supported mip count.");
        byte[] key=new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(key,(ushort)(native.ConstructorLowBits|(mipCount>1?native.ConstructorMipCode<<14:0)));
        BinaryPrimitives.WriteUInt16LittleEndian(key.AsSpan(2),0x30);return key;
    }

    /// <summary>RT2D/RT2DArray loaders replace bias/minimum/address/filter/aniso
    /// fields after construction; their declared source mip filter is retained.</summary>
    public static byte[] FinalRenderTextureKey(LoadedDeferredSamplerNativeSource native,RenderTextureDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.LevelNum==0||!float.IsFinite(source.MipLodBias))throw new InvalidDataException("Final render sampler needs finite bias and explicit nonzero mip count.");
        byte[] key=RenderTextureKey(native,source.LevelNum);
        uint lo=(uint)((source.AddressU&3)|((source.AddressV&3)<<2)|(1<<4)|((source.MinMipLevel&15)<<6)|
            ((source.MagFilter&3)<<10)|((source.MinFilter&3)<<12)|((source.MipFilter&3)<<14));
        BinaryPrimitives.WriteUInt16LittleEndian(key,(ushort)lo);
        BinaryPrimitives.WriteUInt16LittleEndian(key.AsSpan(2),(ushort)(0x30|(source.AnisoSampleNum&7)));
        BinaryPrimitives.WriteUInt32LittleEndian(key.AsSpan(4),BitConverter.SingleToUInt32Bits(source.MipLodBias));return key;
    }

    public static byte[] AmbientSpecularKey(ReadOnlySpan<byte> originalKey)
    {
        if(originalKey.Length!=8)throw new InvalidDataException("Ambient sampler requires the complete original key.");
        byte[] key=originalKey.ToArray();ushort lo=BinaryPrimitives.ReadUInt16LittleEndian(key);
        BinaryPrimitives.WriteUInt16LittleEndian(key,(ushort)((lo&0xFFD5)|0x15));return key;
    }

    public static G1TextureSamplerRecipe DecodeKey(LoadedDeferredSamplerNativeSource native,ReadOnlySpan<byte> originalKey)
    {
        ArgumentNullException.ThrowIfNull(native);
        if(originalKey.Length!=8)throw new InvalidDataException("Native sampler key must contain all eight bytes.");
        uint lo=BinaryPrimitives.ReadUInt16LittleEndian(originalKey),hi=BinaryPrimitives.ReadUInt16LittleEndian(originalKey[2..]);
        uint mag=lo>>10&3,min=lo>>12&3,mip=lo>>14&3;
        if(mag==3||min==3||mip==3)throw new InvalidDataException("Native sampler uses an unresolved filter code3.");
        uint filter=mag==2||min==2?0x55u:(min==1?0x10u:0u)|(mag==1?4u:0u)|(mip==2?1u:0u);
        if((hi&8)!=0)filter|=0x80;
        uint maximum=mip==0?0u:0x7F7FFFFFu;
        uint bias=BinaryPrimitives.ReadUInt32LittleEndian(originalKey[4..]);
        if(!float.IsFinite(BitConverter.UInt32BitsToSingle(bias)))throw new InvalidDataException("Native sampler bias is nonfinite.");
        uint minimum=maximum==0?0u:BitConverter.SingleToUInt32Bits((float)(lo>>6&15));
        return new(0,originalKey.ToArray(),filter,native.Addresses[lo&3],native.Addresses[lo>>2&3],native.Addresses[lo>>4&3],
            bias,native.Anisotropy[hi&7],native.Comparison[hi>>4&7],minimum,maximum);
    }

    private static uint U32(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(at,4));
}
