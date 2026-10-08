using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Verified installed native constructors, sampler conversion and tables.
/// Loading reads inert executable bytes and never loads or invokes game code.</summary>
public sealed class LoadedOriginalDeferredSamplerNativeSource
{
    public string ResourceUri { get; }
    public string PhysicalPath { get; }
    public string Locale { get; }
    public string ExecutableSha256 { get; }
    public string DecodedTextSha256 { get; }
    public IReadOnlyDictionary<string, string> WindowSha256 { get; }
    internal uint ConstructorLowBits { get; }
    internal uint ConstructorMipCode { get; }
    internal uint[] Addresses { get; }
    internal uint[] Anisotropy { get; }
    internal uint[] Comparison { get; }
    internal byte[] DefaultDescriptor { get; }

    internal LoadedOriginalDeferredSamplerNativeSource(string path, string locale,
        string executableHash, string textHash, IReadOnlyDictionary<string,string> windows,
        uint low, uint mip, uint[] addresses, uint[] anisotropy, uint[] comparison,byte[] defaultDescriptor)
    {
        PhysicalPath=path;Locale=locale;ExecutableSha256=executableHash;DecodedTextSha256=textHash;
        ResourceUri=GameResourceId.File($"SAN14CE_{locale}.exe").ToString()+"#deferred-sampler-source";
        WindowSha256=windows;ConstructorLowBits=low;ConstructorMipCode=mip;
        Addresses=addresses;Anisotropy=anisotropy;Comparison=comparison;
        DefaultDescriptor=defaultDescriptor;
    }
}

public static partial class OriginalDeferredSamplerSources
{
    public const string PixelSourceSha256="B01248B54B5D09A98D850D4627520F78808EA14C1CF090F3C804753E78F1884E";
    public const string ShaderSourceSha256="95575321D097F29696A35F377B68DD91F37BB7F4EAE3061EB3CA501006021E38";
    private sealed record NativeProfile(string ExeHash,string TextHash,int Plane,int Cube,int Filter,int Creator,
        int Packet,int Ambient,int PlaneParameters,int PlaneSampler,int ArrayParameters,int ArraySampler,
        int Addresses,int Anisotropy,int Comparison,string PlaneHash,string CubeHash,string CreatorHash,
        string PlaneParametersHash,string ArrayParametersHash);
    private static NativeProfile Profile(string locale)=>locale switch
    {
        "SC"=>new("CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",
            "D7F3831C369A76148D87370D91EF0A8A43487D5E13A383886C68A6D50591E8F6",
            0x96C140,0x96C9A0,0x96A320,0x950620,0x8779B0,0xD1988E,0xC081E0,0xC08515,0xC070A0,0xC073D2,
            0x14C05C8,0x14C0740,0x14C0398,
            "294FB6B1726F8C2C9452C6FABEB7C19FAB5F3AABB7BC41BB0EBB9E52629405A5",
            "DC1FF57A2E64F66AB043E2146669D3895271C92B2E58411DB62C446BB8B3745C",
            "848F82B6FB390A423FC54ED1F616365369C33D3A339DA30F46D851830DED0869",
            "E0F96CE3C20268BA3167EF50359494A9485EBADCF5CB4D1F6BF6090D77C44C54",
            "E7FDB42628D6EE9A2B084535031182817E029C793A3994581D60DE17E4E3F624"),
        "TC"=>new("0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",
            "FFBD504FC3DEA6A218EF4E8530A65761296C17191F7D675AB3C49DF64D950766",
            0x96AFC0,0x96B820,0x9691A0,0x94F550,0x876F30,0xD18B5E,0xC06D40,0xC07075,0xC05C00,0xC05F32,
            0x14BF6E8,0x14BF860,0x14BF4B8,
            "2605FA9EBE7FD31645C180D923FCA51DAB5FC9C4CADFCCB8C0438E87D5B0F456",
            "3C8DE3350C2EA63744E122DB106F1F6E0F4B6F0CFCA8AE1920626F280BFFC606",
            "1458FAE30082306E04ABB431173B835117E90032229319351F306876F44011DC",
            "2640C19860596E8E7A7B8EDF9D7CF9E56B8DDB37523116BC714F303542C4F86D",
            "757845FCE5C2109F1986F2A35AD8DFC00C9FFF6F8C737FD207D38DBE7859FF86"),
        _=>throw new ArgumentOutOfRangeException(nameof(locale),"Native sampler source supports verified SC and TC revisions.")
    };

    public static LoadedOriginalDeferredSamplerNativeSource Load(string sourceDirectory,string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        NativeProfile profile=Profile(locale);
        string path=Path.GetFullPath(Path.Combine(sourceDirectory,$"SAN14CE_{locale}.exe"));
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,128*1024,FileOptions.SequentialScan);
        if(stream.Length is <4096 or >128*1024*1024)throw new InvalidDataException("Native sampler executable size differs.");
        byte[] exe=new byte[checked((int)stream.Length)];stream.ReadExactly(exe);
        VerifyHash(exe,profile.ExeHash,"executable");
        var image=new NativeImage(exe);
        byte[] code=image.DecodeText(out int textRva);
        VerifyHash(code,profile.TextHash,"decoded .text");
        var windows=new Dictionary<string,string>(StringComparer.Ordinal);
        void Window(string name,int rva,int count,string expected)
        {
            if(rva<textRva||rva-textRva>code.Length-count)throw new InvalidDataException("Native sampler window is outside .text.");
            VerifyHash(code.AsSpan(rva-textRva,count),expected,name);windows.Add(name,expected);
        }
        Window("renderTextureConstructor",profile.Plane,0x12E,profile.PlaneHash);
        Window("cubeTextureConstructor",profile.Cube,0x120,profile.CubeHash);
        Window("filterHelper",profile.Filter,0x19A,"D537D61B1DD60BEC34B1C29184FBD5C8C0F3D0A13CBCDE8401F5E98E0F14D5F8");
        Window("createSampler",profile.Creator,0x18A,profile.CreatorHash);
        Window("genericTexturePacket",profile.Packet,0x102,"11B515D24EE746A19EF26B044F6455DE9419A34C318AA63B36DB2E3AEEE01FF3");
        Window("ambientAddressTransform",profile.Ambient,0x7B,"F59FB70016525BC5F8082FE6CD49C0F8F8C07A1324FD7911A1AF191BBF5A8CB4");
        Window("render2DLoaderParameters",profile.PlaneParameters,0x1C4,profile.PlaneParametersHash);
        Window("render2DLoaderSampler",profile.PlaneSampler,0xE2,"9CE9C4ED0C6E0E3DF6499EDCAA4F6983162BACFFBFAD571E3B968AEDD2CA938D");
        Window("renderArrayLoaderParameters",profile.ArrayParameters,0x1E0,profile.ArrayParametersHash);
        Window("renderArrayLoaderSampler",profile.ArraySampler,0xD0,"FEC983A116F9DFB7E5895C91A4FA44547990E21D64A2570BB245E6BDA133AADD");
        int defaultRva=locale=="SC"?0x957692:0x9565C2;
        Window("backendDefaultSampler",defaultRva,0x63,locale=="SC"
            ?"F07AE853F6DC06DCC8E4ACB1A3E07287E6C866D1E809FBBE7729CECB2EFBE356"
            :"BB495E9638B4BE49510932DE5FB40AB29C8609A10999E18895CED2F5E8D890E3");
        Window("cubeArrayLoader",locale=="SC"?0xC07720:0xC06280,0x503,locale=="SC"
            ?"1F6472774DB4EB76449B9C25E622F064737AD3D1F93976AD0622DA2608CA7A6E"
            :"6E6A3E7A78BB0D02EDEB8F15FB03F65255B768FA2CF4D241B4B15FC43E19321C");
        Window("cubeArrayDeviceCreator",locale=="SC"?0x94F510:0x94E440,0x546,locale=="SC"
            ?"6FC68C082324C51B0C29643CAC80D9D6193B49132EAD6A7867725DB5D62923DF"
            :"289C924C448913EE0F903BDAFCA36D701E34879895F67AFE2FA22C68D5D55BCC");
        byte[] RipLiteral(int offset,int instructionBytes,int displacementOffset,int count)
        {
            int at=defaultRva+offset;
            int displacement=BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(at-textRva+displacementOffset,4));
            return image.Raw(checked(at+instructionBytes+displacement),count).ToArray();
        }
        byte[] first=RipLiteral(0,8,4,16),maximum=RipLiteral(0x1A,8,4,4),
            border=RipLiteral(0x3B,7,3,16),minimum=RipLiteral(0x4D,8,4,4);
        if(!first.AsSpan().SequenceEqual(Convert.FromHexString("15000000030000000300000003000000"))||
            U32(maximum,0)!=0x7F7FFFFF||U32(minimum,0)!=0xFF7FFFFF||
            !border.AsSpan().SequenceEqual(Convert.FromHexString("0000803F0000803F0000803F0000803F")))
            throw new InvalidDataException("Native direct default sampler literals differ.");
        byte[] defaultDescriptor=new byte[52];first.CopyTo(defaultDescriptor,0);border.CopyTo(defaultDescriptor,28);
        BinaryPrimitives.WriteUInt32LittleEndian(defaultDescriptor.AsSpan(20),1);
        BinaryPrimitives.WriteUInt32LittleEndian(defaultDescriptor.AsSpan(24),1);
        minimum.CopyTo(defaultDescriptor,44);maximum.CopyTo(defaultDescriptor,48);
        uint low=U32(code,profile.Plane-textRva+0x45),cubeLow=U32(code,profile.Cube-textRva+0x47);
        uint mip=U32(code,profile.Plane-textRva+0x34),cubeMip=U32(code,profile.Cube-textRva+0x36);
        if(low!=0x1415||cubeLow!=low||mip!=2||cubeMip!=mip)throw new InvalidDataException("Native sampler constructor immediates differ.");
        uint[] Table(int rva,int count)=>Enumerable.Range(0,count).Select(i=>U32(image.Raw(rva,count*4),i*4)).ToArray();
        uint[] addresses=Table(profile.Addresses,4),aniso=Table(profile.Anisotropy,8),comparison=Table(profile.Comparison,8);
        if(!addresses.SequenceEqual(new uint[]{1,3,2,4})||!aniso.SequenceEqual(new uint[]{1,2,4,6,8,10,12,16})||
            !comparison.SequenceEqual(new uint[]{1,2,3,4,5,6,7,8}))throw new InvalidDataException("Native sampler conversion tables differ.");
        return new(path,locale,profile.ExeHash,profile.TextHash,new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(windows),
            low,mip,addresses,aniso,comparison,defaultDescriptor);
    }

    /// <summary>The native direct52B default used by the binder for a null
    /// descriptor/zero-SRV command. It has no original8B key; it must not be
    /// selected just because a nonnull producer is absent in the remake.</summary>
    public static byte[] BackendDefaultDescriptor(LoadedOriginalDeferredSamplerNativeSource native)
    {ArgumentNullException.ThrowIfNull(native);return native.DefaultDescriptor.ToArray();}

    /// <summary>Fresh wrapper key, before any source loader or plugin writes.</summary>
    public static byte[] RenderTextureKey(LoadedOriginalDeferredSamplerNativeSource native,int mipCount)
        => ConstructorKey(native,mipCount);
    public static byte[] CubeTextureKey(LoadedOriginalDeferredSamplerNativeSource native,int mipCount)
        => ConstructorKey(native,mipCount);

    /// <summary>CubeArrayRT04F7DD1F uses the common cube constructor and keeps
    /// its key. Its loader parses serialized sampler fields without applying
    /// the RT2D/RT2DArray postloader sampler writes.</summary>
    public static byte[] FinalCubeArrayTextureKey(LoadedOriginalDeferredSamplerNativeSource native,
        LoadedObjects source,uint objectId)
    {
        ArgumentNullException.ThrowIfNull(source);VerifyAsset(source.Source);
        var original=San14.Data.Formats.DokReader.Read(source.Source.Bytes);
        var objects=original.Objects.Where(o=>o.ObjectId==objectId).ToArray();
        if(objects.Length!=1||objects[0].ClassId!=0x04F7DD1F||objects[0].Kind!=DokObjectKind.Inline)
            throw new InvalidDataException("Original cube-array sampler requires one explicit04F7DD1F source declaration.");
        var levels=objects[0].Properties.Where(p=>p.NameHash==0x7744A442).ToArray();
        if(levels.Length!=1||levels[0].TypeCode!=1||levels[0].ElementCount!=1||levels[0].RawValue.Length!=1)
            throw new InvalidDataException("Original cube-array sampler requires its actual typed LevelNum.");
        return CubeTextureKey(native,levels[0].RawValue[0]);
    }
    private static byte[] ConstructorKey(LoadedOriginalDeferredSamplerNativeSource native,int mipCount)
    {
        ArgumentNullException.ThrowIfNull(native);
        if(mipCount is <1 or >31)throw new InvalidDataException("Original constructor key requires an explicit supported mip count.");
        byte[] key=new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(key,(ushort)(native.ConstructorLowBits|(mipCount>1?native.ConstructorMipCode<<14:0)));
        BinaryPrimitives.WriteUInt16LittleEndian(key.AsSpan(2),0x30);return key;
    }

    /// <summary>RT2D/RT2DArray loaders replace bias/minimum/address/filter/aniso
    /// fields after construction; their declared source mip filter is retained.</summary>
    public static byte[] FinalRenderTextureKey(LoadedOriginalDeferredSamplerNativeSource native,OriginalRenderTextureDefinition source)
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

    public static G1TextureSamplerRecipe DecodeKey(LoadedOriginalDeferredSamplerNativeSource native,ReadOnlySpan<byte> originalKey)
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
    private static ulong U64(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(at,8));
    private static void VerifyHash(ReadOnlySpan<byte> bytes,string expected,string part)
    {if(Convert.ToHexString(SHA256.HashData(bytes))!=expected)throw new InvalidDataException("Original sampler "+part+" SHA-256 differs.");}

    internal sealed class NativeImage
    {
        private readonly byte[] _exe;
        private readonly PEHeaders _headers;
        private readonly PEHeader _optional;
        public NativeImage(byte[] exe)
        {
            _exe=exe;using var pe=new PEReader(new MemoryStream(exe,false));_headers=pe.PEHeaders;
            _optional=_headers.PEHeader??throw new InvalidDataException("Original sampler executable lacks PE headers.");
            if(_headers.CoffHeader.Machine!=Machine.Amd64||_optional.Magic!=PEMagic.PE32Plus||_optional.ImageBase!=0x140000000UL)
                throw new InvalidDataException("Original sampler executable PE profile differs.");
        }
        private SectionHeader Section(string name)
        {
            var found=_headers.SectionHeaders.Where(s=>s.Name==name).ToArray();
            return found.Length==1?found[0]:throw new InvalidDataException("Original sampler PE section is absent or duplicated.");
        }
        public ReadOnlySpan<byte> Raw(int rva,int count)
        {
            var found=_headers.SectionHeaders.Where(s=>rva>=s.VirtualAddress&&(long)rva+count<=(long)s.VirtualAddress+s.SizeOfRawData).ToArray();
            if(count<0||found.Length!=1)throw new InvalidDataException("Original sampler PE range is outside raw sections.");
            long at=found[0].PointerToRawData+(long)rva-found[0].VirtualAddress;
            if(at<0||at>_exe.Length-(long)count)throw new InvalidDataException("Original sampler PE range exceeds source bytes.");
            return _exe.AsSpan(checked((int)at),count);
        }
        public byte[] DecodeText(out int textRva)
        {
            SectionHeader text=Section(".text"),bind=Section(".bind");int entry=_optional.AddressOfEntryPoint;
            ReadOnlySpan<byte> signature=[0xE8,0,0,0,0,0x50,0x53,0x51,0x52,0x56,0x57,0x55,0x41,0x50];
            if(entry<bind.VirtualAddress||(long)entry+signature.Length>(long)bind.VirtualAddress+bind.SizeOfRawData||
                !Raw(entry,signature.Length).SequenceEqual(signature))throw new InvalidDataException("Original sampler static entry signature differs.");
            byte[] header=Raw(checked(entry-240),240).ToArray();uint preceding=U32(header,0);
            for(int i=4;i<header.Length;i+=4){uint encoded=U32(header,i);BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(i),encoded^preceding);preceding=encoded;}
            ulong originalEntry=U64(header,32);
            if(U32(header,4)!=0xC0DEC0DF||U64(header,8)!=_optional.ImageBase||U64(header,72)!=(ulong)text.VirtualAddress||
                U64(header,80)!=(ulong)text.SizeOfRawData||(U32(header,60)&4)!=0||originalEntry<(ulong)text.VirtualAddress||
                originalEntry>=(ulong)text.VirtualAddress+(ulong)text.VirtualSize||text.SizeOfRawData<16||text.SizeOfRawData%16!=0||text.VirtualSize>text.SizeOfRawData)
                throw new InvalidDataException("Original sampler encrypted section profile differs.");
            byte[] cipher=new byte[checked(text.SizeOfRawData+16)];header.AsSpan(136,16).CopyTo(cipher);
            Raw(text.VirtualAddress,text.SizeOfRawData).CopyTo(cipher.AsSpan(16));using Aes aes=Aes.Create();
            aes.Key=header.AsSpan(88,32).ToArray();byte[] iv=aes.DecryptEcb(header.AsSpan(120,16),PaddingMode.None);
            byte[] plain=aes.DecryptCbc(cipher,iv,PaddingMode.None);textRva=text.VirtualAddress;
            return plain.AsSpan(0,text.SizeOfRawData).ToArray();
        }
    }
}
