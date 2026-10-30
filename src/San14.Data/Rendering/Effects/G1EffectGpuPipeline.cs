using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record G1EffectGpuInputElement(string SemanticName, uint SemanticIndex,
    uint DxgiFormat, uint InputSlot, uint ByteOffset, uint InputClassification, uint InstanceStepRate);

/// <summary>Stage codes are original DXBC codes: zero pixel, one vertex.</summary>
public sealed record G1EffectGpuStage(uint StageCode, int ProgramIndex,
    ReadOnlyMemory<byte> Bytecode, DxbcProgramSignatures Signatures,
    DxbcResourceDefinitions ResourceDefinitions);

/// <summary>The actual twelve-word D32 request, supplied by the current view owner.</summary>
public readonly record struct G1EffectGpuFeatureRequest(uint Word0, uint Word1, uint Word2,
    uint Word3, uint Word4, uint Word5, uint Word6, uint Word7, uint Word8, uint Word9,
    uint Word10, uint Word11)
{
    public static G1EffectGpuFeatureRequest Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 48) throw new InvalidDataException("Effect feature request must contain all 48 bytes.");
        static uint U(ReadOnlySpan<byte> data, int at) => BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(at, 4));
        return new(U(bytes, 0), U(bytes, 4), U(bytes, 8), U(bytes, 12), U(bytes, 16), U(bytes, 20),
            U(bytes, 24), U(bytes, 28), U(bytes, 32), U(bytes, 36), U(bytes, 40), U(bytes, 44));
    }
    public uint[] ToWords() => [Word0, Word1, Word2, Word3, Word4, Word5, Word6,
        Word7, Word8, Word9, Word10, Word11];
    public byte[] ToBytes()
    {
        byte[] bytes = new byte[48];
        uint[] words = ToWords();
        for (int index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        return bytes;
    }
}

public readonly record struct G1EffectGpuVariant(uint Word0, uint Word1, uint Word2, uint Word3)
{
    public static G1EffectGpuVariant FromFlags(uint flags)
    {
        uint second = flags >> 14 & 3;
        if (second == 0 && (flags & 0x400) != 0) second = 1;
        return new(flags >> 16 & 3, second, flags >> 12 & 1, flags >> 7 & 1);
    }
    public byte[] ToBytes()
    {
        var bytes = new byte[16];
        uint[] words = [Word0, Word1, Word2, Word3];
        for (int index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        return bytes;
    }
}

public readonly record struct G1EffectGpuPipelineKey(int RuntimeBankIndex,
    G1EffectGpuFeatureRequest FeatureRequest, G1EffectGpuVariant Variant,
    uint Pass, int? MeshIndex, bool FullMeshLayout, int DynamicStride)
{
    public int? RetainedFeatureRow { get; init; }
}

public sealed record G1EffectGpuMissing(string Operation, string Reason);

/// <summary>A selected original pipeline, retained for one resource generation.
/// Missing identifies native capabilities or input declarations still required;
/// selecting programs does not certify a submitted GPU draw.</summary>
public sealed record G1EffectGpuPipeline(int Id, G1EffectGpuPipelineKey Key,
    LoadedShaderContainer Source, G1ShaderFeatureSelection FeatureSelection,
    int VariantRow, IReadOnlyList<uint> Dimensions, G2ShaderCombination Combination,
    G1ShaderResourceBindings ResourceBindings, IReadOnlyList<G1EffectGpuStage> Stages,
    IReadOnlyList<G1EffectGpuInputElement> InputLayout, IReadOnlyList<G1EffectGpuMissing> Missing)
{
    public int RuntimeBankIndex => Key.RuntimeBankIndex;
    public uint Pass => Key.Pass;
    public bool IsReady => Missing.Count == 0;
}

/// <summary>One 2D subresource. Arrays use D3D order: mip + slice * MipCount.</summary>
public sealed record G1EffectGpuTextureMip(int Level, int Width, int Height,
    int RowPitch, int SlicePitch, ReadOnlyMemory<byte> Bytes)
{
    public int ArraySlice { get; init; }
}

/// <summary>The resource-generation owner retains the actual image source.
/// Generated sources supply their own typed definition rather than a G1T binding.</summary>
public abstract record G1EffectGpuTextureSource;

public sealed record G1EffectGpuStaticTextureSource(StaticTextureBinding Binding)
    : G1EffectGpuTextureSource;

public sealed record G1EffectGpuTexture(int Id, int TextureSlot, G1EffectGpuTextureSource Source,
    int Width, int Height, uint ResourceFormat, uint ViewFormat,
    IReadOnlyList<G1EffectGpuTextureMip> Mips, ReadOnlyMemory<byte> SamplerDescription)
{
    public G1EffectGpuTexture(int id, int textureSlot, StaticTextureBinding source,
        int width, int height, uint resourceFormat, uint viewFormat,
        IReadOnlyList<G1EffectGpuTextureMip> mips, ReadOnlyMemory<byte> samplerDescription)
        : this(id, textureSlot, new G1EffectGpuStaticTextureSource(source), width, height,
            resourceFormat, viewFormat, mips, samplerDescription) { }

    /// <summary>DXBC resource dimension: 4 is 2D; 5 is 2DArray, including one slice.</summary>
    public uint ViewDimension { get; init; } = 4;
    public int ArraySize { get; init; } = 1;
    public int MipCount => ArraySize > 0 ? Mips.Count / ArraySize : 0;
}

public sealed record G1EffectGpuMesh(int Id, int VertexStride, int IndexFormat,
    ReadOnlyMemory<byte> VertexBytes, ReadOnlyMemory<byte> IndexBytes,
    IReadOnlyList<G1EffectModelAttribute> StaticLayout,
    IReadOnlyList<G1EffectModelAttribute> ShortLayout,
    IReadOnlyList<G1EffectModelAttribute> FullLayout);

/// <summary>One callback write to a physical stage slot. The sampler is borrowed
/// from the same selected original texture wrapper when the shader declares it.</summary>
public sealed record G1EffectGpuTextureBinding(uint StageCode, uint TextureRegister,
    uint? SamplerRegister, int EncodedToken, G1EffectGpuTexture Texture);

/// <summary>Graph-owned input required by the selected program. Its source
/// registration is retained; names are not used to fabricate a resource.</summary>
public sealed record G1EffectGpuExternalBinding(uint StageCode, DxbcRdefBinding Declaration,
    IReadOnlyList<G1ShaderResourceBinding> Registrations);

public sealed record G1EffectGpuDrawResources(G1EffectGpuPipeline Pipeline,
    IReadOnlyList<G1EffectGpuTextureBinding> Textures,
    IReadOnlyList<G1EffectGpuExternalBinding> ExternalBindings,
    IReadOnlyList<G1EffectGpuMissing> Missing)
{
    public bool IsReady => Pipeline.IsReady && Missing.Count == 0;
}
