using System.Buffers.Binary;

namespace San14.Data.Formats;

/// <summary>Original untrimmed loader key and complete D3D11 sampler descriptor.</summary>
public sealed record G1TextureSamplerRecipe(uint SourceFlags, byte[] RawKey,
    uint Filter, uint AddressU, uint AddressV, uint AddressW, uint MipLodBiasBits,
    uint MaxAnisotropy, uint ComparisonFunction, uint MinimumLodBits, uint MaximumLodBits)
{
    public byte[] EncodeD3D11Descriptor()
    {
        byte[] bytes = new byte[52];
        uint[] values = [Filter,AddressU,AddressV,AddressW,MipLodBiasBits,MaxAnisotropy,
            ComparisonFunction,0,0,0,0,MinimumLodBits,MaximumLodBits];
        for(int i=0;i<values.Length;i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4,4),values[i]);
        return bytes;
    }
}

/// <summary>
/// Recovers verified point/linear CE G1T recipes with source mips retained.
/// Runtime mip trimming and unknown native filter branches stay explicit errors.
/// Reads actual flags, bias and minimum LOD rather than naming all textures alike.
/// </summary>
public static class G1TextureSamplerReader
{
    private static readonly uint[] Addresses = [1,3,2,4];
    private static readonly uint[] Anisotropy = [1,2,4,6,8,10,12,16];

    public static G1TextureSamplerRecipe ReadRetainedSource(G1TextureImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        ReadOnlySpan<byte> header=image.RawHeader;
        bool legacy=image.RawContainerVersion=="0500" && image.TextureSystem==0 &&
            image.PixelFormat==1 && image.MipCount==1 && header.Length==8 && (header[7]>>4)==0;
        if(!legacy && (image.TextureSystem is not (0 or 2 or 3) || header.Length<20 || (header[7]>>4)!=1 || image.MipCount<1))
            throw Error("requires a supported system0/2/3 image with one explicit extra header");
        // Original0500 skips extended fields and supplies bias/minLOD zero;
        // base-header filter/address flags still use the ordinary decoder.
        uint flags=U32(header,4),bias=legacy?0:U32(header,12);
        if(!float.IsFinite(BitConverter.UInt32BitsToSingle(bias)))
            throw Error("nonfinite LOD bias is unresolved");
        uint min=legacy?0:(uint)(BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(16,2))&15);
        uint addressU=flags&3, addressV=(flags>>4)&3;
        uint addressW=image.TextureSystem is 2 or 3 ? (flags>>8)&3 : 1;
        uint magnification=(flags>>12)&3, minification=(flags>>16)&3;
        uint mip=image.MipCount>1 ? ((flags>>20)&15)&3 : 0;
        bool point=magnification==0 && minification==0 && mip==0;
        bool linear=magnification==1 && minification==1 && mip is 0 or 1 or 2;
        // Native filter helper TC9691D1/9692CD keeps anisotropic filtering
        // when mip code0 is selected, and writes both LOD bounds as zero.
        bool anisotropic=magnification==2 && minification==2 && mip is 0 or 2;
        if(!point && !linear && !anisotropic)
            throw Error($"native filter codes ({magnification},{minification},{mip}) are unresolved");
        ushort key0=(ushort)(addressU|(addressV<<2)|(addressW<<4)|(min<<6)|
            (magnification<<10)|(minification<<12)|(mip<<14));
        ushort key2=(ushort)(((flags>>24)&7)|0x30);
        byte[] key=new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(key,key0);
        BinaryPrimitives.WriteUInt16LittleEndian(key.AsSpan(2,2),key2);
        BinaryPrimitives.WriteUInt32LittleEndian(key.AsSpan(4,4),bias);
        uint maxLod=mip==0 ? 0u : 0x7F7FFFFF;
        uint minLod=mip==0 ? 0u : BitConverter.SingleToUInt32Bits(min);
        // Native111 returns MIN_MAG_LINEAR_MIP_POINT and retains FLT_MAX;
        // native110 uses the same filter but restricts both LOD bounds to zero.
        return new(flags,key,anisotropic ? 0x55u : point ? 0u : mip==2 ? 0x15u : 0x14u,Addresses[addressU],Addresses[addressV],Addresses[addressW],
            bias,Anisotropy[flags>>24&7],4,minLod,maxLod);
    }
    private static uint U32(ReadOnlySpan<byte> source,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(at,4));
    private static InvalidDataException Error(string detail)=>new($"Original G1T sampler: {detail}.");
}
