using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public enum G1TextureSubresourceLayout { SingleLayerMipChain, ArrayOrderUnresolved, MipMajorArray, VolumeMipChain }

public sealed record G1EncodedRange(int Offset, int Length)
{
    public ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> source)
    {
        if (Offset < 0 || Length < 0 || Offset > source.Length - (long)Length)
            throw new InvalidDataException("G1T encoded range lies outside the supplied source.");
        return source.Slice(Offset, Length);
    }
}

public sealed record G1TextureMip(int Level, int Width, int Height, int BytesPerLayer,
    G1EncodedRange? EncodedRange, int Depth = 1);

public sealed record G1TextureImage(int Index, byte PixelFormat, int Width, int Height,
    byte RawMipCount, int MipCount, byte TextureSystem, int LayerCount,
    ushort RawFacesAndArrays, G1TextureSubresourceLayout SubresourceLayout,
    G1EncodedRange ImageRange, G1EncodedRange EncodedDataRange, byte[] RawHeader,
    IReadOnlyList<G1TextureMip> Mips, int Depth = 1, string RawContainerVersion = "")
{
    /// <summary>Gather a planar image or one physical array layer's complete
    /// encoded mip chain. Unsupported array ordering is rejected by GetEncodedMip.</summary>
    public byte[] GetEncodedLayerChain(ReadOnlySpan<byte> source, int layer = 0)
    {
        if ((uint)layer >= LayerCount) throw new ArgumentOutOfRangeException(nameof(layer));
        using var chain = new MemoryStream();
        for (int level = 0; level < MipCount; level++)
            chain.Write(GetEncodedMip(source, level, layer));
        return chain.ToArray();
    }

    /// <summary>One original system2 subresource contains all depth slices at that mip.</summary>
    public ReadOnlySpan<byte> GetEncodedVolumeMip(ReadOnlySpan<byte> source, int mipLevel)
    {
        if(SubresourceLayout != G1TextureSubresourceLayout.VolumeMipChain || TextureSystem != 2)
            throw new InvalidDataException("G1T image is not a verified volume mip chain.");
        if((uint)mipLevel >= Mips.Count) throw new ArgumentOutOfRangeException(nameof(mipLevel));
        return Mips[mipLevel].EncodedRange!.Slice(source);
    }
    /// <summary>
    /// Returns encoded bytes, without color conversion. Single-mip arrays have
    /// unambiguous layer order; supported multi-mip arrays use their parsed layout.
    /// </summary>
    public ReadOnlySpan<byte> GetEncodedMip(ReadOnlySpan<byte> source, int mipLevel, int layer = 0)
    {
        if ((uint)mipLevel >= Mips.Count || (uint)layer >= LayerCount)
            throw new ArgumentOutOfRangeException(nameof(mipLevel), "G1T mip or layer index is outside the texture.");
        if (SubresourceLayout == G1TextureSubresourceLayout.SingleLayerMipChain)
            return Mips[mipLevel].EncodedRange!.Slice(source);
        if (SubresourceLayout != G1TextureSubresourceLayout.MipMajorArray)
            throw new InvalidDataException("G1T array mip/layer ordering is unresolved; use the complete encoded body.");
        int before = 0;
        for (int mip = 0; mip < mipLevel; mip++) before = checked(before + Mips[mip].BytesPerLayer * LayerCount);
        var range = new G1EncodedRange(checked(EncodedDataRange.Offset + before + Mips[mipLevel].BytesPerLayer * layer),
            Mips[mipLevel].BytesPerLayer);
        if (range.Offset < EncodedDataRange.Offset || range.Offset > EncodedDataRange.Offset + (long)EncodedDataRange.Length - range.Length)
            throw new InvalidDataException("G1T array slice lies outside its encoded body.");
        return range.Slice(source);
    }
}

public sealed record G1TextureContainer(string RawVersion, uint Platform, int SourceByteLength,
    int TableOffset, uint[] RawOffsets, byte[] RawHeader, IReadOnlyList<G1TextureImage> Textures)
{
    public uint RawDeclaredSourceByteLength{get;init;}
    public string DeclaredLengthSourceEvidence{get;init;}="header08-equals-complete-source-length";
}

/// <summary>
/// Reads verified Windows G1T container boundaries and encoded mip sizes. Type 02
/// is only four-byte storage here; type 04 is sixteen-byte texel storage.
/// Types 5C/5E/5F use verified BC4/BC6H/BC7 block boundaries without pixel
/// decoding. This reader does not interpret float/color values.
/// Unsupported platforms, packing, formats and unmatched body lengths are rejected.
/// </summary>
public static class G1TextureContainerReader
{
    public static G1TextureContainer Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 32 || !data[..4].SequenceEqual("GT1G"u8))
            throw new InvalidDataException("G1T container header is missing.");
        string version = Encoding.ASCII.GetString(data.Slice(4, 4));
        uint platform = U32(data, 20);
        int table = I32(data, 12), count = I32(data, 16);
        bool legacyContainer = version == "0500" && platform == 0;
        if (!legacyContainer && (version is not ("0600" or "1600" or "4600") || platform != 0x0A))
            throw new InvalidDataException($"Unsupported G1T container version/platform: {version}/{platform:X8}.");
        if (legacyContainer && (table != 32 || count != 1))
            throw new InvalidDataException("Legacy0500 supports only the verified single-image directory family.");
        // Native955C20 does not consume header08 for these Windows versions:
        // it reads magic/version/count, platform and directory offset instead.
        // The original half-float LUT retains a BGRA8-sized declared length.
        // Keep that raw word, and validate every actual image/body byte below;
        // resource overrides need not reproduce one particular LUT's header.
        bool nativeIgnoresDeclaredLength=!legacyContainer;
        if ((!nativeIgnoresDeclaredLength&&I32(data,8)!=data.Length) || count < 1 || count > 10_000 || table < 28 ||
            table > 4096 || table > data.Length - 4L * count)
            throw new InvalidDataException("G1T container total length or directory is invalid.");
        var offsets = new uint[count];
        for (int i = 0; i < count; i++)
        {
            offsets[i] = U32(data, table + i * 4);
            if (offsets[i] < 4L * count || offsets[i] > data.Length - (long)table ||
                (i > 0 && offsets[i] <= offsets[i - 1]))
                throw new InvalidDataException("G1T texture directory overlaps or lies outside the file.");
        }
        // Optional records live between the directory and the first image.
        // Their ownership comes from each image's base-header record count,
        // independently of its physical texture array layers.
        _ = G1TextureOptionalMetadataReader.Read(data);
        var textures = new G1TextureImage[count];
        for (int i = 0; i < count; i++)
        {
            int start = checked(table + (int)offsets[i]);
            int end = i + 1 < count ? checked(table + (int)offsets[i + 1]) : data.Length;
            textures[i] = ReadImage(data.Slice(start, end - start), i, start, version);
        }
        return new(version, platform, data.Length, table, offsets,
            data[..(table + count * 4)].ToArray(), Array.AsReadOnly(textures))
        {
            RawDeclaredSourceByteLength=U32(data,8),
            DeclaredLengthSourceEvidence=nativeIgnoresDeclaredLength?
                "TC:955C20;Windows0600/1600/4600;header08-not-consumed;directory-and-exact-encoded-body-coverage" :
                "header08-equals-complete-source-length"
        };
    }

    private static G1TextureImage ReadImage(ReadOnlySpan<byte> image, int index, int absolute, string version)
    {
        if (image.Length < 8) throw new InvalidDataException("G1T texture header is truncated.");
        byte rawMips = (byte)(image[0] >> 4), system = (byte)(image[0] & 15);
        if(version == "0500" && (system != 0 || rawMips != 1 || image[1] is not (1 or 6) || (image[7] >> 4) != 0))
            throw new InvalidDataException("Legacy0500 requires a supported planar BGRA8 or BC1 single-mip base header.");
        int mips = Math.Max(1, (int)rawMips);
        if(system == 2 && rawMips == 0)
            throw new InvalidDataException("Original system2 volume requires an explicit nonzero mip count.");
        if (system is not (0 or 2 or 3) || (system != 2 && (image[3] & 15) != 0))
            throw new InvalidDataException("Unsupported G1T texture system, depth or external attribute headers.");
        byte format = image[1];
        int blockBytes = format switch
        {
            0x00 or 0x01 or 0x02 => 4,
            0x03 => 8,
            0x04 => 16,
            0x06 when version == "0500" => 8,
            0x59 or 0x5C => 8,
            0x5B or 0x5E or 0x5F => 16,
            _ => throw new InvalidDataException($"Unsupported G1T encoded pixel format {format:X2}.")
        };
        int width = 1 << (image[2] & 15), height = 1 << (image[2] >> 4);
        int depth = system == 2 ? 1 << (image[3] & 15) : 1;
        // Native95203D/951C90 also uploads original BGRA8 format01 volumes:
        // rowPitch=width*4, slicePitch=rowPitch*height, mip body includes depth.
        // PostEffect3Lut's real RRPreview1C45D803 is32x32x32,1mip,131072B;
        // it is not a planar LUT atlas. The existing volume mip traversal below
        // already preserves the native contiguous depth-slice ordering.
        if(system == 2 && format is not (0x01 or 0x03 or 0x5F))
            throw new InvalidDataException("Unsupported original G1T volume encoding.");
        int headerLength = 8, layers = 1;
        ushort facesAndArrays = 0;
        int extraCount = image[7] >> 4;
        if (extraCount > 1)
            throw new InvalidDataException("Multiple G1T texture extra headers are unsupported.");
        if (extraCount != 0)
        {
            if (image.Length < 12) throw new InvalidDataException("G1T texture extra header is truncated.");
            int extra = I32(image, 8);
            if (extra is not (12 or 16 or 20) || extra > image.Length - 8)
                throw new InvalidDataException("Unsupported G1T texture extra header length.");
            headerLength += extra;
            facesAndArrays = BinaryPrimitives.ReadUInt16LittleEndian(image[16..18]);
            if (system != 2 && ((facesAndArrays & 15) != 0 || image[18] != 0))
                throw new InvalidDataException("G1T texture faces or swizzle are unsupported.");
            int rawLayers = facesAndArrays >> 4;
            if (system == 3)
            {
                if (rawLayers == 0) throw new InvalidDataException("G1T texture array has no explicit layer count.");
                layers = rawLayers;
            }
            else if (system != 2 && rawLayers != 0)
                throw new InvalidDataException("G1T planar texture has an unexpected array count.");
            // Original system2 metadata uses only base header nibbles. Extended
            // width/height fields belong to the ordinary 2D/array loaders.
            if (system != 2 && extra >= 16) width = ExplicitDimension(U32(image, 20), width);
            if (system != 2 && extra >= 20) height = ExplicitDimension(U32(image, 24), height);
        }
        else if (system == 3)
            throw new InvalidDataException("G1T array requires an explicit layer count header.");

        int available = image.Length - headerLength;
        long chainBytes = 0;
        var levels = new List<(int Width, int Height, int Depth, int Bytes)>();
        int w = width, h = height, d = depth;
        for (int level = 0; level < mips; level++)
        {
            long units = format is 0x06 or 0x59 or 0x5B or 0x5C or 0x5E or 0x5F
                ? ((w + 3L) / 4) * ((h + 3L) / 4) : (long)w * h;
            // The original BC7 volume uploader does not clamp to a minimum block.
            // Support its demonstrated complete 4x4 blocks, refuse smaller mips.
            if(system == 2 && format == 0x5F && (w < 4 || h < 4))
                throw new InvalidDataException("Original BC7 volume mip below 4x4 has no supported device layout.");
            if (units > available / ((long)blockBytes * d))
                throw new InvalidDataException("G1T texture dimensions exceed its encoded body.");
            int bytes = checked((int)(units * blockBytes));
            chainBytes += (long)bytes * d;
            levels.Add((w, h, d, bytes));
            if (level + 1 < mips && w == 1 && h == 1 && d == 1)
                throw new InvalidDataException("G1T mip count repeats the final unit level.");
            w = Math.Max(1, w >> 1); h = Math.Max(1, h >> 1); d = Math.Max(1, d >> 1);
        }
        if (chainBytes * layers != available)
            throw new InvalidDataException("G1T mip/layer byte sizes do not exactly cover the encoded body.");
        var layout = system == 2 ? G1TextureSubresourceLayout.VolumeMipChain
            : layers == 1 ? G1TextureSubresourceLayout.SingleLayerMipChain
            : G1TextureSubresourceLayout.ArrayOrderUnresolved;
        // With a single mip, mip-major and layer-major storage have the same
        // physical order. No multi-mip ordering assumption is needed to slice it.
        if (layers > 1 && mips == 1)
            layout = G1TextureSubresourceLayout.MipMajorArray;
        // Current CE's native system-3 BC3 reader traverses mip then layer, writing
        // subresource=mip+layer*mipCount. The verified Windows header family retains
        // that order independently of dimensions/count; other families stay unresolved.
        if (layers > 1 && version == "1600" && system == 3 && format == 0x5B &&
            headerLength == 20 &&
            image[4] == 0 && image[5] == 0x10 && image[6] == 0x21 && image[7] == 0x10 &&
            U32(image, 12) == 0 && image[19] is 0 or 1)
            layout = G1TextureSubresourceLayout.MipMajorArray;
        var mipRecords = new G1TextureMip[mips];
        int position = absolute + headerLength;
        for (int level = 0; level < mips; level++)
        {
            var value = levels[level];
            int mipBytes = checked(value.Bytes * value.Depth);
            mipRecords[level] = new(level, value.Width, value.Height, value.Bytes,
                layers == 1 ? new G1EncodedRange(position, mipBytes) : null, value.Depth);
            if (layers == 1) position += mipBytes;
        }
        return new(index, format, width, height, rawMips, mips, system, layers,
            facesAndArrays, layout, new(absolute, image.Length), new(absolute + headerLength, available),
            image[..headerLength].ToArray(), Array.AsReadOnly(mipRecords), depth, version);
    }

    private static int ExplicitDimension(uint value, int fallback)
    {
        if (value > int.MaxValue) throw new InvalidDataException("G1T explicit dimension exceeds the supported integer range.");
        return value == 0 ? fallback : (int)value;
    }

    private static uint U32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
    private static int I32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));
}
