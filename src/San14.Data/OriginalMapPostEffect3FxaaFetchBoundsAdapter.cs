using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalMapPostEffect3FxaaFetchBoundsProof(
    uint OriginalResultId, uint OriginalImageId, uint OriginalCoordinateId, uint OriginalLodId,
    uint ImageOperandsMask, uint? ConstOffsetId, int OffsetX, int OffsetY,
    uint QueryResultId, uint ConditionId, uint InBoundsBlockId, uint OutOfBoundsBlockId,
    uint MergeBlockId, uint GuardedFetchResultId, uint ZeroFloat4Id);

public sealed record OriginalMapPostEffect3FxaaFetchBoundsAdaptation(
    byte[] Bytes, string SourceProgramSha256, string TranslatedSpirvSha256,
    string AdaptedSpirvSha256, uint TextureVariableId,
    IReadOnlyList<OriginalMapPostEffect3FxaaFetchBoundsProof> FetchBoundsProofs,
    int RetainedInstructions, string SourceEvidence);

/// <summary>Exact original PS55 default translation only. D3D Texture2D.Load
/// returns all-zero outside the bound view. Vulkan's unprotected ImageFetch
/// cannot guarantee that result. Each fetch is therefore dominated by a real
/// view-size query and signed effective-coordinate bounds check. The original
/// image, coordinate, LOD and ConstOffset operands remain intact in the valid
/// branch; its result ID is retained by a float4 Phi. Nothing returns the pixel
/// early or changes original filtering, output, gamma, sampler or descriptors.
/// This is a device representation adaptation, not a GPU completion receipt.</summary>
public static class OriginalMapPostEffect3FxaaFetchBoundsAdapter
{
    public const string SourceSha256 = "80E7576F6AADB40E85B2B3EFCAC81CAC315CEE28568310B82545692586BF44E6";
    public const string DefaultTranslationSha256 = "8014C0D959BFCC0BE36C1F9EE8076BBA921C200A64DDD44A3D776A54B24DB806";

    public static OriginalMapPostEffect3FxaaFetchBoundsAdaptation Adapt(
        OriginalMapPostEffect3Program source, OriginalShaderSpirv translation)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(translation);
        if (source.StageCode != 0 || source.StageIndex != 55 ||
            source.Sha256 != SourceSha256 || Hash(source.Bytes.Span) != SourceSha256 ||
            translation.SourceProgramSha256 != SourceSha256 ||
            translation.SpirvSha256 != DefaultTranslationSha256 || Hash(translation.Bytes) != DefaultTranslationSha256 ||
            translation.ExplicitDescriptorSet is not null || translation.BufferUavsAsStorageBuffers)
            throw new InvalidDataException("FXAA bounds adaptation requires exact original PS55 and its default, non-null translation.");

        var before = SpirvShaderInterfaceReader.Read(translation.Bytes);
        var texture = before.Descriptors.Single(d => d.VariableId == 25);
        if (before.EntryPoints is not [{ ExecutionModel: 4, FunctionId: 2, Name: "main" }] ||
            texture is not { Name: "t0", StorageClass: 0, DescriptorSet: 0, Binding: 2,
                Type: { Kind: "image", Dimension: 1, Arrayed: false, Multisampled: false,
                    SampledCode: 1, ImageFormat: 0, ComponentType: { Kind: "float", Bits: 32 } } })
            throw new InvalidDataException("FXAA source texture or fragment interface changed.");

        var original = Read(translation.Bytes);
        // This complete pinned module has no source Phi or switch. Splitting
        // its plain fetch blocks does not require predecessor-ID rewrites.
        // Reject a different graph instead of silently reusing this contract.
        if (original.Any(i => i.Op is 245 or 251 || i.Op is >= 101 and <= 107) ||
            original.Count(i => i.Op == 54) != 1 || original.Count(i => i.Op == 95) != 10)
            throw new InvalidDataException("FXAA source control flow/image query domain changed.");
        uint[][] expected = [
            [6, 96, 89, 92, 2, 95], [6, 110, 101, 104, 10, 107, 109],
            [6, 123, 114, 117, 10, 120, 122], [6, 135, 127, 130, 10, 133, 134],
            [6, 147, 139, 142, 10, 145, 146], [6, 383, 376, 379, 2, 382],
            [6, 408, 401, 404, 2, 407],
            [6, 421, 414, 417, 2, 420], [6, 434, 427, 430, 2, 433],
            [6, 537, 530, 533, 2, 536]
        ];
        var fetches = original.Where(i => i.Op == 95).ToArray();
        if (!fetches.Zip(expected).All(pair => pair.First.A.SequenceEqual(pair.Second)) ||
            fetches.Any(f => !original.Any(i => i.Op == 61 && i.A.SequenceEqual(new uint[] { 23, f.A[2], texture.VariableId }))))
            throw new InvalidDataException("FXAA original fetch operand or texture origin changed.");

        uint next = before.IdBound;
        uint NewId() => checked(next++);
        const uint float4 = 6, floatZero = 40, intType = 67, int2 = 68, intZero = 75, boolType = 49;
        uint zeroFloat4 = NewId();
        var output = new List<Instruction>();
        var proofs = new List<OriginalMapPostEffect3FxaaFetchBoundsProof>();
        bool capabilityAdded = false;
        int retained = 0;
        foreach (var i in original)
        {
            if (!capabilityAdded && i.Op != 17)
            {
                output.Add(Make(17, 50)); // ImageQuery capability
                capabilityAdded = true;
            }
            if (i.Op == 54)
                output.Add(Make(44, float4, zeroFloat4, floatZero, floatZero, floatZero, floatZero));
            if (i.Op != 95) { output.Add(i); retained++; continue; }
            uint[] a = i.A;
            uint Binary(ushort op, uint type, uint left, uint right)
            {
                uint id = NewId(); output.Add(Make(op, type, id, left, right)); return id;
            }
            uint query = NewId();
            // Keep original LOD dataflow. Full pinned source initializes and
            // preserves the register W used by all these loads at LOD0.
            output.Add(Make(103, int2, query, a[2], a[5]));
            uint effective = a.Length == 7 ? Binary(128, int2, a[3], a[6]) : a[3];
            uint x = NewId(), y = NewId(), width = NewId(), height = NewId();
            output.Add(Make(81, intType, x, effective, 0));
            output.Add(Make(81, intType, y, effective, 1));
            output.Add(Make(81, intType, width, query, 0));
            output.Add(Make(81, intType, height, query, 1));
            uint nonnegative = Binary(167, boolType,
                Binary(175, boolType, x, intZero), Binary(175, boolType, y, intZero));
            uint belowExtent = Binary(167, boolType,
                Binary(177, boolType, x, width), Binary(177, boolType, y, height));
            uint condition = Binary(167, boolType, nonnegative, belowExtent);
            uint good = NewId(), bad = NewId(), merge = NewId(), fetched = NewId();
            output.Add(Make(247, merge, 0));
            output.Add(Make(250, condition, good, bad));
            output.Add(Make(248, good));
            uint[] guarded = a.ToArray(); guarded[1] = fetched;
            output.Add(new(95, guarded));
            output.Add(Make(249, merge));
            output.Add(Make(248, bad));
            output.Add(Make(249, merge));
            output.Add(Make(248, merge));
            output.Add(Make(245, float4, a[1], fetched, good, zeroFloat4, bad));
            (int dx, int dy) = a.Length != 7 ? (0, 0) : a[6] switch
            { 109 => (-1, -1), 122 => (1, -1), 134 => (-1, 1), 146 => (1, 1),
                _ => throw new InvalidDataException("Unknown FXAA offset origin.") };
            proofs.Add(new(a[1], a[2], a[3], a[5], a[4], a.Length == 7 ? a[6] : null,
                dx, dy, query, condition, good, bad, merge, fetched, zeroFloat4));
        }
        byte[] bytes = Serialize(translation.Bytes.AsSpan(0, 20), next, output);
        var after = SpirvShaderInterfaceReader.Read(bytes);
        if (JsonSerializer.Serialize(before.Descriptors) != JsonSerializer.Serialize(after.Descriptors) ||
            JsonSerializer.Serialize(before.StageVariables) != JsonSerializer.Serialize(after.StageVariables) ||
            JsonSerializer.Serialize(before.EntryPoints) != JsonSerializer.Serialize(after.EntryPoints) ||
            JsonSerializer.Serialize(before.ExecutionModes) != JsonSerializer.Serialize(after.ExecutionModes))
            throw new InvalidDataException("FXAA bounds adaptation changed a descriptor, stage input/output or entry point.");
        return new(bytes, SourceSha256, DefaultTranslationSha256, Hash(bytes), texture.VariableId,
            proofs.AsReadOnly(), retained,
            "exact-original-3SPK-PS55/default-vkd3d;ten-bound-view-queries-at-original-LOD0;effective-signed-coord-plus-original-four-ConstOffsets;pre-fetch-structured-guard;OOB-full-float4-zero;all-unrelated-instructions-byte-identical;no-pixel-return/coordinate-clamp/sampler-or-filter-change;no-GPU-completion-claim");
    }

    private sealed record Instruction(ushort Op, uint[] A);
    private static Instruction Make(ushort op, params uint[] args) => new(op, args);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static List<Instruction> Read(ReadOnlySpan<byte> bytes)
    {
        var result = new List<Instruction>();
        for (int at = 20; at < bytes.Length;)
        {
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
            int count = checked((int)(word >> 16));
            if (count == 0 || count > (bytes.Length - at) / 4)
                throw new InvalidDataException("Truncated FXAA SPIR-V instruction.");
            var args = new uint[count - 1];
            for (int n = 0; n < args.Length; n++)
                args[n] = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 4 + n * 4)..]);
            result.Add(new((ushort)word, args)); at += count * 4;
        }
        return result;
    }
    private static byte[] Serialize(ReadOnlySpan<byte> header, uint bound, List<Instruction> instructions)
    {
        var bytes = new byte[20 + instructions.Sum(i => 4 + i.A.Length * 4)];
        header.CopyTo(bytes); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), bound);
        int at = 20;
        foreach (var i in instructions)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), checked((uint)(i.A.Length + 1) << 16) | i.Op); at += 4;
            foreach (uint a in i.A) { BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), a); at += 4; }
        }
        return bytes;
    }
}
