using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record SkyAmbientMaterialWrite(uint Semantic, uint Destination, uint Bytes);
internal sealed record SkyAmbientMaterialStage(uint Stage, uint Slot, uint Bytes, IReadOnlyList<SkyAmbientMaterialWrite> Writes);
internal sealed record SkyAmbientResourceBinding(uint Stage, uint Role, uint Slot);

/// <summary>Actual destinations and sampled roles of one ambient operation.
/// Numerical values, source cubes and sample buffers retain their own owners.</summary>
public sealed class SkyAmbientMaterialLayout
{
    private readonly SkyAmbientMaterialStage vertex, pixel;
    private readonly SkyAmbientResourceBinding[] resources, samplers;
    private SkyAmbientMaterialLayout(SkyAmbientMaterialStage vertex, SkyAmbientMaterialStage pixel,
        SkyAmbientResourceBinding[] resources, SkyAmbientResourceBinding[] samplers)
        => (this.vertex, this.pixel, this.resources, this.samplers) = (vertex, pixel, resources, samplers);

    public uint VertexSlot => vertex.Slot;
    public uint PixelSlot => pixel.Slot;
    public uint VertexByteLength => vertex.Bytes;
    public uint PixelByteLength => pixel.Bytes;

    internal static SkyAmbientMaterialLayout Import(SkyAmbientOperation operation,
        DxbcResourceDefinitions vertex, DxbcResourceDefinitions pixel)
    {
        var textures = new List<SkyAmbientResourceBinding>();
        var samplers = new List<SkyAmbientResourceBinding>();
        ImportBindings(vertex, 1, operation, textures, samplers);
        ImportBindings(pixel, 0, operation, textures, samplers);
        return new(ImportStage(vertex, 1, operation), ImportStage(pixel, 0, operation),
            textures.ToArray(), samplers.ToArray());
    }

    private static SkyAmbientMaterialStage ImportStage(DxbcResourceDefinitions definitions,
        uint stage, SkyAmbientOperation operation)
    {
        if (definitions.RawShaderType != (stage == 1 ? 0xFFFE : 0xFFFF) || definitions.Buffers.Count != 1)
            throw new NotSupportedException("SkyAmbient material requires one constant buffer in each selected stage.");
        var buffer = definitions.Buffers[0];
        if (buffer.BufferType != 0 || buffer.ByteSize <= 0 || buffer.ByteSize > 65536 || buffer.ByteSize % 16 != 0)
            throw new NotSupportedException("SkyAmbient constant buffer storage is unsupported.");
        (string Name, uint Semantic, int Bytes) field = operation == SkyAmbientOperation.Blend
            ? stage == 1 ? ("g_mC2W", 1u, 64) : ("g_vIntInfo", 2u, 16)
            : ("g_vMipInfo", 0u, 16);
        var writes = new List<SkyAmbientMaterialWrite>();
        foreach (var variable in buffer.Variables)
        {
            if ((variable.Flags & 2) == 0) continue;
            if (variable.Name.Text != field.Name)
                throw new NotSupportedException($"SkyAmbient material has no producer for {variable.Name.Text}.");
            var type = definitions.Types.Single(value => value.Offset == variable.TypeOffset);
            bool matrix = field.Semantic == 1;
            if (variable.StartOffset < 0 || variable.StartOffset % 4 != 0 || variable.ByteSize != field.Bytes ||
                (long)variable.StartOffset + field.Bytes > buffer.ByteSize || type.BaseType != 3 ||
                type.Class != (matrix ? 2 : 1) || type.Rows != (matrix ? 4 : 1) || type.Columns != 4 ||
                type.Elements != 0 || type.Members.Count != 0)
                throw new NotSupportedException($"SkyAmbient typed field {field.Name} is unsupported.");
            writes.Add(new(field.Semantic, (uint)variable.StartOffset, (uint)field.Bytes));
        }
        var ordered = writes.OrderBy(write => write.Destination).ToArray();
        for (int index = 1; index < ordered.Length; index++)
            if (ordered[index - 1].Destination + ordered[index - 1].Bytes > ordered[index].Destination)
                throw new NotSupportedException("SkyAmbient used material fields overlap.");
        var bindings = definitions.Bindings.Where(value => value.InputType == 0).ToArray();
        if (bindings.Length != 1 || bindings[0].Name.Text != buffer.Name.Text || bindings[0].BindCount != 1 ||
            bindings[0].Dimension != 0 || bindings[0].BindPoint >= 14)
            throw new NotSupportedException("SkyAmbient constant buffer binding is unsupported.");
        return new(stage, bindings[0].BindPoint, (uint)buffer.ByteSize, Array.AsReadOnly(ordered));
    }

    private static void ImportBindings(DxbcResourceDefinitions definitions, uint stage,
        SkyAmbientOperation operation, List<SkyAmbientResourceBinding> textures, List<SkyAmbientResourceBinding> samplers)
    {
        foreach (var binding in definitions.Bindings)
        {
            if (binding.InputType == 0) continue;
            if (stage != 0 || binding.BindCount != 1 || binding.InputType is not (2 or 3))
                throw new NotSupportedException("SkyAmbient selected resource needs another producer.");
            bool sampler = binding.InputType == 3;
            string name = binding.Name.Text;
            if (sampler)
            {
                if (!name.StartsWith("__smp", StringComparison.Ordinal))
                    throw new NotSupportedException("SkyAmbient sampler role is unsupported.");
                name = name[5..];
            }
            uint role = name switch
            {
                "sCubeMap" => 0,
                "sInterpolateCubeMap" when operation == SkyAmbientOperation.Blend => 1,
                "tLightDirBuffer" when operation == SkyAmbientOperation.Filter && !sampler => 2,
                "tWeightBuffer" when operation == SkyAmbientOperation.Filter && !sampler => 3,
                _ => throw new NotSupportedException($"SkyAmbient has no source for {binding.Name.Text}."),
            };
            if (binding.Dimension != (sampler ? 0u : role < 2 ? 9u : 1u) ||
                (!sampler && binding.ReturnType != 5) || binding.BindPoint >= (sampler ? 16u : 128u))
                throw new NotSupportedException("SkyAmbient resource dimension or binding is unsupported.");
            (sampler ? samplers : textures).Add(new(stage, role, binding.BindPoint));
        }
    }

    public byte[] PackVector(uint stage, Vector4 value)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(bytes, value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(4), value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(8), value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12), value.W);
        return Pack(stage, bytes);
    }

    public byte[] PackMatrix(uint stage, Matrix4x4 value)
    {
        float[] values = [value.M11,value.M12,value.M13,value.M14,value.M21,value.M22,value.M23,value.M24,
            value.M31,value.M32,value.M33,value.M34,value.M41,value.M42,value.M43,value.M44];
        byte[] bytes = new byte[64];
        for (int index = 0; index < values.Length; index++)
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(index * 4), values[index]);
        return PackMatrix(stage, bytes);
    }

    public byte[] PackMatrix(uint stage, ReadOnlySpan<byte> encodedMatrix)
    {
        if (encodedMatrix.Length != 64) throw new ArgumentException("A SkyAmbient matrix has sixteen floats.", nameof(encodedMatrix));
        return Pack(stage, encodedMatrix);
    }

    private byte[] Pack(uint stageCode, ReadOnlySpan<byte> value)
    {
        var stage = stageCode switch { 1 => vertex, 0 => pixel, _ => throw new ArgumentOutOfRangeException(nameof(stageCode)) };
        byte[] bytes = new byte[stage.Bytes];
        foreach (var write in stage.Writes)
        {
            if (value.Length != write.Bytes) throw new InvalidOperationException("SkyAmbient material value differs from its selected field shape.");
            value.CopyTo(bytes.AsSpan((int)write.Destination, (int)write.Bytes));
        }
        return bytes;
    }

    internal static void WriteNativeInputs(string directory, IReadOnlyList<SkyAmbientProgramSelection> programs)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(0x314C4D41u); // AML1: ambient material and resource bindings.
        writer.Write((uint)programs.Count);
        foreach (var program in programs)
        {
            var layout = program.MaterialLayout;
            writer.Write((uint)program.Operation); writer.Write(2u);
            foreach (var stage in new[] { layout.vertex, layout.pixel })
            {
                writer.Write(stage.Stage); writer.Write(stage.Slot); writer.Write(stage.Bytes); writer.Write((uint)stage.Writes.Count);
                foreach (var write in stage.Writes)
                { writer.Write(write.Semantic); writer.Write(write.Destination); writer.Write(0u); writer.Write(write.Bytes); }
            }
            foreach (var bindings in new[] { layout.resources, layout.samplers })
            {
                writer.Write((uint)bindings.Length);
                foreach (var binding in bindings)
                { writer.Write(binding.Stage); writer.Write(binding.Role); writer.Write(binding.Slot); }
            }
        }
        File.WriteAllBytes(Path.Combine(directory, "material-layout.bin"), stream.ToArray());
    }
}
