using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual primitive30 type19 first-primary record and the current
/// primitive38 type20 texture CPU's retained slot directory. Null CPU and a
/// present CPU with a null/out-of-range resource preserve distinct source states.</summary>
public sealed record OriginalDeferredEarlyShadowPrimaryTexture<TTexture>(
    G1TextureRecord Primitive30, G1TextureEntry FirstPrimary, bool TextureCpuPresent,
    TTexture? Texture, float StageCount, Vector4 StageAlphaMap) where TTexture : class
{
    public float EffectivePrimaryUvSet => TextureCpuPresent ? FirstPrimary.RawValue2 : 0;
}

public sealed record OriginalDeferredEarlyShadowPixelConstants(int? PixelProgram,
    bool ClearsPixelShader, byte[]? Bytes, bool SamplesPrimaryTexture);

/// <summary>Imported generic state3 PS9 profile. Actual dimensions/pass and
/// variant remain with the renderer owner. This recipe does not select a PS,
/// make an image/sampler, or replace null resources with fixture textures.</summary>
public sealed class LoadedOriginalDeferredEarlyShadowPixelMaterialSource
{
    internal LoadedOriginalDeferredEarlyShadowPixelMaterialSource(OriginalNamedModelShader provider,
        LoadedShaderContainer shader, string programHash, int byteSize, bool curl)
        => (Provider, Shader, PixelProgramSha256, PixelConstantByteSize, HasUvCurl) =
            (provider, shader, programHash, byteSize, curl);

    public OriginalNamedModelShader Provider { get; }
    public LoadedShaderContainer Shader { get; }
    public string PixelProgramSha256 { get; }
    public int PixelConstantByteSize { get; }
    public bool HasUvCurl { get; }
    public uint TextureRegister => 0;
    public uint SamplerRegister => 0;
    public string TextureName => "sStage0";
    public string SamplerName => "__smpsStage0";
    public uint SourceTextureDescriptorFlags => 0x109;
    public uint SourceTexturePrimaryLimit => 1;
    public string SourceEvidence => Shader.Source.ResourceUri + "@" + Shader.Source.DecodedSha256 +
        ";actual-state3-PS9;981F00/texture109-count1;D288A6-A4DA10-980E00-97F630;" +
        "sbias-current-ShadowCPU138-cache-required;no-GPU-completion";

    /// <summary>The admitted normal type19 record has one primary. A present
    /// texture CPU sets nStageNum=1 even when its selected resource is null.
    /// Native97F630 resolves the first source slot and preserves null on range
    /// miss. Extra PB2 registrations have no state3 metadata consumers here.</summary>
    public OriginalDeferredEarlyShadowPrimaryTexture<TTexture> ReadCurrentPrimary<TTexture>(
        G1TextureRecord actualPrimitive30, IReadOnlyList<TTexture?>? actualPrimitive38TextureSlots)
        where TTexture : class
    {
        ArgumentNullException.ThrowIfNull(actualPrimitive30);
        if (actualPrimitive30.UnknownValue8 != 1 || actualPrimitive30.TextureEntries.Count == 0)
            throw new NotSupportedException("This normal shadow texture recipe requires its actual one-primary type19 record.");
        G1TextureEntry first = actualPrimitive30.TextureEntries[0];
        TTexture? texture = actualPrimitive38TextureSlots is not null && first.RawValue0 < actualPrimitive38TextureSlots.Count
            ? actualPrimitive38TextureSlots[first.RawValue0] : null;
        // 9813AC..9814D7: source+38=10041 excludes the legacy half-scale.
        // Vectors are the complete native literals, not fitted alpha values.
        Vector4 map = first.RawValue6 switch {
            4 => new(BitConverter.UInt32BitsToSingle(0xBF810204), 1.9921875f, 0,
                BitConverter.UInt32BitsToSingle(0x40008102)),
            2 => new(0, 0, 1, 1),
            0 => new(1, 0, 1, 0),
            _ => new(1, 1, 0, 0) };
        return new(actualPrimitive30, first, actualPrimitive38TextureSlots is not null, texture,
            actualPrimitive38TextureSlots is null ? 0 : 1, map);
    }

    /// <summary>All read named fields come from the actual current owners.
    /// sbias is ShadowCPU138 cache+D0..DC, including its current W; ordinary
    /// material alpha-reference/vATest is a separate constant domain. Padding
    /// retains the supplied complete original template. A selected null PS
    /// explicitly clears the stage and has no pixel constant buffer.</summary>
    public OriginalDeferredEarlyShadowPixelConstants PrepareCurrentPixelConstants<TTexture>(
        int? actualSelectedPixelProgram, ReadOnlySpan<byte> actualCompleteTemplate,
        Vector4 actualCurrentShadowBias,
        OriginalDeferredEarlyShadowPrimaryTexture<TTexture> actualCurrentPrimary,
        Vector2? actualUvScale = null, Vector3? actualUvCurl = null) where TTexture : class
    {
        if (actualSelectedPixelProgram is null) return new(null, true, null, false);
        if (actualSelectedPixelProgram != 9)
            throw new NotSupportedException("This recipe consumes the actual selected generic PS9 or null stage.");
        ArgumentNullException.ThrowIfNull(actualCurrentPrimary);
        if (actualCompleteTemplate.Length != PixelConstantByteSize)
            throw new ArgumentException("The current PS9 requires its complete imported pixel CB0 template.", nameof(actualCompleteTemplate));
        if (HasUvCurl && (actualUvScale is null || actualUvCurl is null))
            throw new ArgumentException("The selected UV-curl PS9 needs its current uvhScale and uvhCurl writers.");
        byte[] result = actualCompleteTemplate.ToArray();
        Vector(0, actualCurrentShadowBias);
        Float(16, actualCurrentPrimary.StageCount);
        Vector(32, actualCurrentPrimary.StageAlphaMap);
        if (HasUvCurl) {
            Float(48, actualUvScale!.Value.X); Float(52, actualUvScale.Value.Y);
            Float(64, actualUvCurl!.Value.X); Float(68, actualUvCurl.Value.Y); Float(72, actualUvCurl.Value.Z);
        }
        return new(9, false, result, actualCurrentPrimary.StageCount > 0);
        void Float(int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(offset, 4), value);
        void Vector(int offset, Vector4 value) {
            Float(offset, value.X); Float(offset + 4, value.Y); Float(offset + 8, value.Z); Float(offset + 12, value.W);
        }
    }

    /// <summary>Port-owned fresh CB storage: every shader-read named field
    /// is provided by its current source owner; unread padding is zero storage,
    /// not a claim about the original backend allocation's padding bytes.</summary>
    public OriginalDeferredEarlyShadowPixelConstants PrepareCurrentPixelReadFields<TTexture>(
        int? actualSelectedPixelProgram, Vector4 actualCurrentShadowBias,
        OriginalDeferredEarlyShadowPrimaryTexture<TTexture> actualCurrentPrimary,
        Vector2? actualUvScale = null, Vector3? actualUvCurl = null) where TTexture : class
        => PrepareCurrentPixelConstants(actualSelectedPixelProgram,
            actualSelectedPixelProgram is null ? ReadOnlySpan<byte>.Empty : new byte[PixelConstantByteSize],
            actualCurrentShadowBias, actualCurrentPrimary, actualUvScale, actualUvCurl);

    public OriginalDeferredEarlyShadowPixelConstants PrepareCurrentPixelReadFields<TTexture>(
        int? actualSelectedPixelProgram, OriginalDeferredEarlyShadowCurrentBiasCache actualCurrentBiasCache,
        OriginalDeferredEarlyShadowPrimaryTexture<TTexture> actualCurrentPrimary,
        Vector2? actualUvScale = null, Vector3? actualUvCurl = null) where TTexture : class
    {
        ArgumentNullException.ThrowIfNull(actualCurrentBiasCache);
        return PrepareCurrentPixelReadFields(actualSelectedPixelProgram, actualCurrentBiasCache.ReadCurrent(),
            actualCurrentPrimary, actualUvScale, actualUvCurl);
    }
}

public static class OriginalDeferredEarlyShadowPixelMaterialSource
{
    /// <summary>Numeric fragment of actual9FFCA0's nonnull R8 parameter cache.
    /// Fresh compact D0C8D0 -> B92870 allocates its cache at CPU+D30;
    /// cascade D0C7F0 -> ABDC10 uses CPU+1410. Both publish it as CPU138;
    /// the sole local descriptor is type2 width4 "sbias". Call only
    /// when that actual owner constructs its fresh cache. Pool reuse preserves
    /// the existing current cache and must not reapply this constructor fragment.</summary>
    public static void ApplyOwnedConstructorBiasFragment(Span<byte> actualFreshCacheD0ToDF)
    {
        if (actualFreshCacheD0ToDF.Length != 16)
            throw new ArgumentException("The owned sbias constructor fragment is exactly16 bytes.", nameof(actualFreshCacheD0ToDF));
        uint[] words = [0x3F000000, 0, 0x3F800000, 0x3F4CCCCD];
        for (int i = 0; i < words.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(actualFreshCacheD0ToDF.Slice(i * 4, 4), words[i]);
    }

    /// <summary>Read after the actual constructor and any later writers,
    /// from the cache associated with the currently bound ShadowCPU. This
    /// getter does not infer that the cache exists or that all ShadowCPU fields
    /// and current shader stages have been prepared.</summary>
    public static Vector4 ReadOwnedCurrentShadowBias(ReadOnlySpan<byte> actualCurrentCacheD0ToDF)
    {
        if (actualCurrentCacheD0ToDF.Length != 16)
            throw new ArgumentException("The current sbias field is exactly16 bytes.", nameof(actualCurrentCacheD0ToDF));
        return new(BinaryPrimitives.ReadSingleLittleEndian(actualCurrentCacheD0ToDF),
            BinaryPrimitives.ReadSingleLittleEndian(actualCurrentCacheD0ToDF[4..]),
            BinaryPrimitives.ReadSingleLittleEndian(actualCurrentCacheD0ToDF[8..]),
            BinaryPrimitives.ReadSingleLittleEndian(actualCurrentCacheD0ToDF[12..]));
    }

    /// <summary>Import/reload only. Retained canonical shader/profile data is
    /// reused by subsequent current packets; no per-request source hashes.</summary>
    public static LoadedOriginalDeferredEarlyShadowPixelMaterialSource Load(
        OriginalNamedModelShader actualState3Provider, LoadedShaderContainer actualShader)
    {
        ArgumentNullException.ThrowIfNull(actualState3Provider); ArgumentNullException.ThrowIfNull(actualShader);
        if (actualState3Provider.Object.ClassId != 0x39E93192 ||
            actualState3Provider.ShaderFileHash != actualShader.Source.FileId ||
            actualShader.Source.ResourceUri != GameResourceId.Rdb("system", actualState3Provider.ShaderFileHash).ToString() ||
            Convert.ToHexString(SHA256.HashData(actualShader.Source.Bytes)) != actualShader.Source.DecodedSha256)
            throw new InvalidDataException("Shadow pixel import requires the actual state3 generic provider and original G1S.");
        ReadOnlySpan<byte> source = actualShader.Source.Bytes;
        uint descriptorOffset = BinaryPrimitives.ReadUInt32LittleEndian(source[0x88..]);
        if (BinaryPrimitives.ReadUInt32LittleEndian(source[0x38..]) != 0x10041 ||
            BinaryPrimitives.ReadUInt32LittleEndian(source[0x54..]) != 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(source[0x58..]) != 1 ||
            descriptorOffset < actualShader.Container.MetadataRange.Offset ||
            descriptorOffset + 16L > actualShader.Container.BytecodeRange.Offset ||
            BinaryPrimitives.ReadUInt32LittleEndian(source[(int)descriptorOffset..]) != 0x109 ||
            BinaryPrimitives.ReadUInt32LittleEndian(source[((int)descriptorOffset + 4)..]) != 1)
            throw new NotSupportedException("Shadow pixel import has no audited modern109/one-primary/no-material-mode source descriptor.");
        string hash = Convert.ToHexString(SHA256.HashData(actualShader.Container.GetProgramBytes(source, 9)));
        bool curl = hash is "44BA842A974E99689969989ED32AC9495FC6AC2CB314AEE5FAB36366CA150066" or
            "7393F3BFED93EF090BBA458FEB6998C6232CC56286E09AEEA5D812FA154F6031";
        if (!curl && hash != "0422B6E14A248D7070B17437CFA3CC1D06BAD164728859220BA86BA2C8D8B0C7")
            throw new NotSupportedException("The actual state3 PS9 bytecode is outside the imported source profiles.");
        var rdef = actualShader.ReadResourceDefinitions(9);
        int size = curl ? 80 : 48;
        string[] names = curl ? ["sbias", "nStageNum", "vSAMap", "uvhScale", "uvhCurl"] : ["sbias", "nStageNum", "vSAMap"];
        if (rdef.Buffers.Count != 1 || rdef.Buffers[0].ByteSize != size ||
            !rdef.Buffers[0].Variables.Select(v => v.Name.Text).SequenceEqual(names) ||
            !rdef.Bindings.Where(b => b.InputType == 2).Select(b => (b.Name.Text, b.BindPoint, b.BindCount, b.Dimension))
                .SequenceEqual(new[] { ("sStage0", 0u, 1u, 4u) }) ||
            !rdef.Bindings.Where(b => b.InputType == 3).Select(b => (b.Name.Text, b.BindPoint, b.BindCount))
                .SequenceEqual(new[] { ("__smpsStage0", 0u, 1u) }))
            throw new InvalidDataException("Shadow PS9 declarations differ from its complete imported profile.");
        var bindings = actualShader.ReadG1Bindings();
        foreach (var combination in bindings.Combinations.Rows.Where(c => c.Stages.Any(s => s.StageCode == 0 && s.Entry?.ProgramIndex == 9)))
            if (actualShader.ReadG1ResourceBindings(combination.Index).Entries.Count != 0)
                throw new NotSupportedException("The imported generic PS9 has unaudited extra texture registrations.");
        return new(actualState3Provider, actualShader, hash, size, curl);
    }
}
