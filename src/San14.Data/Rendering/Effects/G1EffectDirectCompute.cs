using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public enum G1EffectDirectKernel { Initialize, Birth, Update, ResetDraw, ExpandDraw, LinkedBirth, LinkedUpdate, Motion, Postprocess }

[Flags]
public enum G1EffectDirectBufferUsage { ShaderResource = 1, UnorderedAccess = 2, Vertex = 4, IndirectArguments = 8 }
public enum G1EffectDirectBufferFormat { Raw, UInt32, Float32, Float4 }

/// <summary>IDs are local to one compute package; no process addresses enter the package.</summary>
public sealed record G1EffectDirectBuffer(uint Id, int ByteLength, G1EffectDirectBufferUsage Usage,
    ReadOnlyMemory<byte> InitialBytes, int StructureStride = 0,
    G1EffectDirectBufferFormat Format = G1EffectDirectBufferFormat.Raw);
public sealed record G1EffectDirectProgram(G1EffectDirectKernel Kernel, string Source, string EntryPoint,
    string Profile = "cs_5_0");
public readonly record struct G1EffectDirectBinding(int Slot, uint BufferId);
public sealed record G1EffectDirectConstantBinding(int Slot, ReadOnlyMemory<byte> Bytes);
public sealed record G1EffectDirectDispatch(G1EffectDirectKernel Kernel, ReadOnlyMemory<byte> Constants,
    IReadOnlyList<G1EffectDirectBinding> ShaderResources, IReadOnlyList<G1EffectDirectBinding> UnorderedAccess,
    uint GroupsX, uint GroupsY = 1, uint GroupsZ = 1,
    IReadOnlyList<G1EffectDirectConstantBinding>? AdditionalConstants = null);
public sealed record G1EffectDirectUpload(uint BufferId, int ByteOffset, ReadOnlyMemory<byte> Bytes);
public sealed record G1EffectDirectCommands(IReadOnlyList<G1EffectDirectUpload> Uploads,
    IReadOnlyList<G1EffectDirectDispatch> Dispatches);

/// <summary>Normal-resource direct-particle GPU layout and executable owned shaders.
/// Particle words use a structure of arrays: capacity * fieldByteOffset + slot * 4.
/// The trailing 24 bytes hold draw arguments and the separate birth counter.</summary>
public sealed partial class G1EffectDirectComputePackage
{
    public const uint ParticleBuffer = 1, VertexBuffer = 2, ObjectBuffer = 3, MainParticleBuffer = 4,
        PermutationBuffer = 5, ScalarNoiseBuffer = 6, VectorNoiseBuffer = 7, LiveCountBuffer = 8;
    private readonly Dictionary<G1EffectDirectKernel, G1EffectDirectProgram> programs = [];
    public G1EffectParticleDeclaration Declaration { get; }
    public int Capacity { get; }
    public int IndirectByteOffset { get; }
    public int BirthCounterByteOffset => checked(IndirectByteOffset + 20);
    public IReadOnlyList<G1EffectDirectBuffer> Buffers { get; }
    public IReadOnlyCollection<G1EffectDirectProgram> Programs => programs.Values;
    public IReadOnlyList<G1EffectDirectKernel> RequiredKernels { get; }
    public IReadOnlyList<G1EffectDirectKernel> MissingKernels => RequiredKernels.Where(k => !programs.ContainsKey(k)).ToArray();
    public int? LinkedGenericFileOffset { get; }
    public int VertexStride => Declaration.Record.Type == 7 ? 64 : 32;
    public bool UsesIndexedInstancedDraw => Declaration.Record.Type == 7;

    /// <param name="normalObjectBytes">The real prepared module/object data supplied by its owner.
    /// Relative serialized references retain their existing coordinate system.</param>
    public G1EffectDirectComputePackage(G1EffectParticleDeclaration declaration,
        ReadOnlyMemory<byte> normalObjectBytes, int normalObjectByteOrigin = 0, G1EffectContext? context = null,
        G1EffectParticleDeclaration? linkedDeclaration = null)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (!declaration.UsesDirectBirth) throw new ArgumentException("A direct generic is required.", nameof(declaration));
        if (declaration.Record.Type is not (0 or 1 or 7 or 10 or 11))
            throw new NotSupportedException($"Direct generic {declaration.Record.Type} is not implemented.");
        Declaration = declaration;
        // GPU allocation uses the first capacity word alone, unlike the ordinary pool.
        Capacity = BinaryPrimitives.ReadInt32LittleEndian(declaration.Record.Bytes.Span[0xC..]);
        if (Capacity <= 0) throw new InvalidDataException("Direct generic has no particle capacity.");
        IndirectByteOffset = checked(declaration.Stride * Capacity);
        int link = BinaryPrimitives.ReadInt32LittleEndian(declaration.Record.Bytes.Span[0x15C..]);
        LinkedGenericFileOffset = link == 0 ? null : checked(declaration.Record.FileOffset + 0x15C + link);
        var buffers = new List<G1EffectDirectBuffer>
        {
            new(ParticleBuffer, checked(IndirectByteOffset + 24),
                G1EffectDirectBufferUsage.ShaderResource | G1EffectDirectBufferUsage.UnorderedAccess |
                G1EffectDirectBufferUsage.IndirectArguments, default),
            new(ObjectBuffer, Align(normalObjectBytes.Length), G1EffectDirectBufferUsage.ShaderResource, normalObjectBytes)
        };
        buffers.Add(new(LiveCountBuffer, 4, G1EffectDirectBufferUsage.ShaderResource |
            G1EffectDirectBufferUsage.UnorderedAccess, new byte[4], Format: G1EffectDirectBufferFormat.UInt32));
        bool linked = declaration.Record.Type is 10 or 11;
        if (!linked)
            buffers.Add(new(VertexBuffer, checked(Capacity * (declaration.Record.Type == 7 ? 64 : 192)),
                G1EffectDirectBufferUsage.UnorderedAccess | G1EffectDirectBufferUsage.Vertex, default));
        Buffers = buffers.AsReadOnly();
        RequiredKernels = linked
            ? [G1EffectDirectKernel.Initialize, G1EffectDirectKernel.LinkedBirth, G1EffectDirectKernel.LinkedUpdate]
            : [G1EffectDirectKernel.Initialize, G1EffectDirectKernel.Birth, G1EffectDirectKernel.Update,
                G1EffectDirectKernel.Motion, G1EffectDirectKernel.Postprocess,
                G1EffectDirectKernel.ResetDraw, G1EffectDirectKernel.ExpandDraw];
        string source = ReadOwnedSource("G1EffectDirect.Storage.hlsl");
        programs.Add(G1EffectDirectKernel.Initialize, new(G1EffectDirectKernel.Initialize, source, "Initialize"));
        programs.Add(G1EffectDirectKernel.ResetDraw, new(G1EffectDirectKernel.ResetDraw, source, "ResetDraw"));
        if (!linked && LinkedGenericFileOffset.HasValue)
        {
            RequiredKernels = RequiredKernels.Concat([G1EffectDirectKernel.LinkedBirth, G1EffectDirectKernel.LinkedUpdate]).ToArray();
            if (linkedDeclaration is not null) _ = PrepareLinked(linkedDeclaration, buffers);
        }
        if (declaration.Record.Type is 0 or 1)
        {
            string common = ReadOwnedSource("G1EffectDirect.Common.hlsl");
            if (G1EffectDirectValidation.BirthProgram(declaration) is null)
                programs.Add(G1EffectDirectKernel.Birth,
                    new(G1EffectDirectKernel.Birth, common + ReadOwnedSource("G1EffectDirect.Birth.hlsl"), "Birth"));
            if (G1EffectDirectUpdateValidation.GetMissing(declaration) is null)
                programs.Add(G1EffectDirectKernel.Update,
                    new(G1EffectDirectKernel.Update, common + ReadOwnedSource("G1EffectDirect.Update.hlsl"), "Update"));
            string? motionMissing = G1EffectDirectValidation.Motion(declaration, normalObjectBytes, normalObjectByteOrigin, out bool noise);
            if (motionMissing is null && (!noise || context is { VectorNoise.Length: 256, Permutation.Length: 256 }))
            {
                programs.Add(G1EffectDirectKernel.Motion,
                    new(G1EffectDirectKernel.Motion, common + ReadOwnedSource("G1EffectDirect.Motion.hlsl"), "Motion"));
                if (noise)
                {
                    byte[] permutation = new byte[1024], vectors = new byte[4096];
                    for (int i = 0; i < 256; i++)
                    {
                        Put(permutation, i * 4, context!.Permutation.Span[i]);
                        Vector4 value = context.VectorNoise.Span[i];
                        for (int lane = 0; lane < 4; lane++)
                            BinaryPrimitives.WriteSingleLittleEndian(vectors.AsSpan(i * 16 + lane * 4), value[lane]);
                    }
                    buffers.Add(new(PermutationBuffer, permutation.Length, G1EffectDirectBufferUsage.ShaderResource,
                        permutation, Format: G1EffectDirectBufferFormat.UInt32));
                    buffers.Add(new(VectorNoiseBuffer, vectors.Length, G1EffectDirectBufferUsage.ShaderResource,
                        vectors, Format: G1EffectDirectBufferFormat.Float4));
                }
            }
            uint rendering = BinaryPrimitives.ReadUInt32LittleEndian(declaration.Layout.Span[0x28..]) >> 16;
            if (rendering is 7 or 34)
                programs.Add(G1EffectDirectKernel.ExpandDraw,
                    new(G1EffectDirectKernel.ExpandDraw, ReadOwnedSource("G1EffectDirect.Expand.hlsl"), "ExpandDraw"));
            // The finite postprocess implementation requires an absent additional
            // shape/post transform, rather than silently skipping that descriptor.
            var uv = G1EffectUvAnimation.Read(declaration);
            uint uvType = BinaryPrimitives.ReadUInt32LittleEndian(uv.Span);
            uint uvCount = BinaryPrimitives.ReadUInt32LittleEndian(uv.Span[12..]);
            if (uvCount == 0 || (long)uvCount * 16 + 16 > uv.Length)
                throw new InvalidDataException("Incomplete direct particle UV table.");
            if (uvType <= 4 && BinaryPrimitives.ReadInt32LittleEndian(declaration.Record.Bytes.Span[0x21C..]) == 0)
                programs.Add(G1EffectDirectKernel.Postprocess, new(G1EffectDirectKernel.Postprocess,
                    ReadOwnedSource("G1EffectDirect.Common.hlsl") + ReadOwnedSource("G1EffectDirect.Postprocess.hlsl"), "Postprocess"));
        }
    }

    /// <summary>A provider may supply a further project-owned implementation. Its caller must
    /// compile and create the real device program before reporting storage as prepared.</summary>
    public void SupplyProgram(G1EffectDirectProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (string.IsNullOrWhiteSpace(program.Source) || string.IsNullOrWhiteSpace(program.EntryPoint))
            throw new ArgumentException("An executable shader source and entry point are required.", nameof(program));
        programs[program.Kernel] = program;
    }

    public G1EffectDirectDispatch Initialize()
    {
        byte[] constants = new byte[48];
        Put(constants, 0, Capacity); Put(constants, 4, BirthCounterByteOffset);
        Declaration.Layout.Span[..32].CopyTo(constants.AsSpan(16));
        // The source masks the serialized live-state halfword before initializing all slots.
        constants[22] = 0; constants[23] = 0;
        return new(G1EffectDirectKernel.Initialize, constants, [], [new(0, ParticleBuffer)], Groups(Capacity, 64));
    }

    public G1EffectDirectDispatch ResetDraw(bool mesh = false, uint primitiveCount = 0, uint firstIndex = 0)
    {
        byte[] constants = new byte[32];
        Put(constants, 0, IndirectByteOffset); Put(constants, 4, 1);
        if (mesh) { Put(constants, 16, 1); Put(constants, 20, unchecked((int)primitiveCount)); Put(constants, 24, unchecked((int)firstIndex)); }
        return new(G1EffectDirectKernel.ResetDraw, constants, [], [new(0, ParticleBuffer)], 1);
    }

    public G1EffectDirectDispatch Birth(int count, int startIndex, int sourceByteOffset,
        int definitionByteOffset, int genericByteOffset, uint randomNumber)
    {
        if (count < 0 || startIndex < 0 || startIndex >= Capacity)
            throw new ArgumentOutOfRangeException(nameof(count));
        byte[] constants = new byte[32];
        Put(constants, 0, Capacity); Put(constants, 4, count); Put(constants, 8, startIndex);
        Put(constants, 12, sourceByteOffset); Put(constants, 16, definitionByteOffset);
        Put(constants, 20, genericByteOffset); Put(constants, 24, unchecked((int)randomNumber));
        return new(G1EffectDirectKernel.Birth, constants, [new(0, ObjectBuffer)],
            [new(0, ParticleBuffer)], Groups(count, 64));
    }

    public G1EffectDirectDispatch Update(float seconds, int genericByteOffset)
    {
        if (!float.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        byte[] constants = new byte[16];
        Put(constants, 0, Capacity); BinaryPrimitives.WriteSingleLittleEndian(constants.AsSpan(4), seconds);
        Put(constants, 8, genericByteOffset);
        return new(G1EffectDirectKernel.Update, constants, [new(0, ObjectBuffer)],
            [new(0, ParticleBuffer)], Groups(Capacity, 64));
    }

    public G1EffectDirectDispatch Postprocess(int genericByteOffset)
    {
        byte[] constants = new byte[16]; Put(constants, 0, Capacity); Put(constants, 4, genericByteOffset);
        return new(G1EffectDirectKernel.Postprocess, constants, [new(0, ObjectBuffer)],
            [new(0, ParticleBuffer)], Groups(Capacity, 64));
    }

    public G1EffectDirectDispatch Motion(float seconds, int genericByteOffset)
    {
        var dispatch = Update(seconds, genericByteOffset);
        var resources = new List<G1EffectDirectBinding> { new(0, ObjectBuffer) };
        if (Buffers.Any(buffer => buffer.Id == PermutationBuffer))
        { resources.Add(new(125, PermutationBuffer)); resources.Add(new(127, VectorNoiseBuffer)); }
        return dispatch with { Kernel = G1EffectDirectKernel.Motion, ShaderResources = resources.AsReadOnly(),
            UnorderedAccess = [new(0, ParticleBuffer), new(1, LiveCountBuffer)] };
    }

    /// <summary>Billboard axes come from the actual current camera, in world space.
    /// Clip scale is the source camera projection scale multiplied by one half.</summary>
    public G1EffectDirectDispatch ExpandDraw(Matrix4x4 worldToClip, Vector4 billboardRight,
        Vector4 billboardUp, Vector2 cameraClipScale)
    {
        if (Declaration.Record.Type is not (0 or 1)) throw new NotSupportedException("Direct mesh/linked draw expansion is not implemented.");
        byte[] constants = new byte[128];
        float[] values = [worldToClip.M11,worldToClip.M12,worldToClip.M13,worldToClip.M14,
            worldToClip.M21,worldToClip.M22,worldToClip.M23,worldToClip.M24,
            worldToClip.M31,worldToClip.M32,worldToClip.M33,worldToClip.M34,
            worldToClip.M41,worldToClip.M42,worldToClip.M43,worldToClip.M44,
            billboardRight.X,billboardRight.Y,billboardRight.Z,billboardRight.W,
            billboardUp.X,billboardUp.Y,billboardUp.Z,billboardUp.W,
            cameraClipScale.X,cameraClipScale.Y];
        for (int i = 0; i < values.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(constants.AsSpan(i * 4), values[i]);
        Put(constants, 104, Capacity); Put(constants, 108, IndirectByteOffset);
        Put(constants, 112, checked((int)Declaration.Record.Type.Value));
        return new(G1EffectDirectKernel.ExpandDraw, constants, [],
            [new(0, VertexBuffer), new(1, ParticleBuffer)], Groups(Capacity, 32));
    }

    internal static string ReadOwnedSource(string file)
    {
        using var stream = typeof(G1EffectDirectComputePackage).Assembly.GetManifestResourceStream("San14.Data.Shaders." + file)
            ?? throw new InvalidOperationException("Owned direct compute shader is absent: " + file);
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
    internal static int Align(int value) => checked((value + 3) & ~3);
    internal static uint Groups(int count, int width) => checked((uint)((count + width - 1) / width));
    internal static void Put(Span<byte> bytes, int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes[at..], value);
}
