using System.Globalization;
using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ReflectionCaptureFilterShaderInterface(SpirvShaderInterface Module,
    IReadOnlyList<ReflectionComputeBinding> Bindings);

/// <summary>Complete source interfaces for PS3/4/5 and CS9/12/13. Structured
/// SH resources use the existing official uint32 storage-buffer translation.</summary>
public static class ReflectionCaptureFilterShader
{
    /// <summary>The official CS12 storage translation repeats one identical
    /// ArrayStride4 decoration when both SH UAVs share their runtime-array
    /// type. Remove only that redundant annotation, retaining all instructions,
    /// bindings and IDs. Conflicting or other duplicate annotations still fail
    /// the unchanged common interface reader.</summary>
    public static byte[] PrepareDeviceBytes(ReflectionShaderStage stage, ReadOnlySpan<byte> spirv)
    {
        if (stage.ProgramIndex != 12) return spirv.ToArray();
        if (spirv.Length < 20 || spirv.Length % 4 != 0) throw Invalid("device_module");
        var bytes = new List<byte>(spirv.Length); bytes.AddRange(spirv[..20].ToArray());
        var strides = new HashSet<uint>();
        for (int at = 20; at < spirv.Length;)
        {
            uint first = BinaryPrimitives.ReadUInt32LittleEndian(spirv[at..]); int words = (int)(first >> 16);
            if (words < 1 || words > (spirv.Length - at) / 4) throw Invalid("device_instruction");
            bool skip = false;
            if ((first & 0xFFFF) == 71 && words == 4 &&
                BinaryPrimitives.ReadUInt32LittleEndian(spirv[(at + 8)..]) == 6 &&
                BinaryPrimitives.ReadUInt32LittleEndian(spirv[(at + 12)..]) == 4)
                skip = !strides.Add(BinaryPrimitives.ReadUInt32LittleEndian(spirv[(at + 4)..]));
            if (!skip) bytes.AddRange(spirv.Slice(at, words * 4).ToArray());
            at += words * 4;
        }
        return bytes.ToArray();
    }

    public static ReflectionCaptureFilterShaderInterface Read(ReflectionShaderStage stage, ReadOnlySpan<byte> spirv)
    {
        bool compute = stage.ProgramIndex is 9 or 12 or 13;
        if (stage.ProgramIndex is not (3 or 4 or 5 or 9 or 12 or 13) || stage.StageCode != (compute ? 5u : 0u))
            throw Invalid("stage");
        var module = SpirvShaderInterfaceReader.Read(spirv); var entry = module.EntryPoints.Single();
        if (entry.ExecutionModel != (compute ? 5u : 4u) || entry.Name != "main" ||
            !compute && !module.ExecutionModes.Any(mode => mode.FunctionId == entry.FunctionId && mode.Mode == 7)) throw Invalid("entry");
        if (compute)
        {
            var mode = module.ExecutionModes.Single(value => value.FunctionId == entry.FunctionId && value.Mode == 17);
            if (mode.Operands.Count != 3 || mode.Operands.Any(value => value == 0)) throw Invalid("compute_group");
        }
        var used = new HashSet<int>(); var bindings = new List<ReflectionComputeBinding>();
        foreach (var variable in module.Descriptors)
        {
            string name = variable.Name ?? throw Invalid("register_name"); uint index; uint kind;
            if (name.StartsWith("cb", StringComparison.Ordinal))
            {
                int separator = name.IndexOf('_', 2);
                if (separator < 0 || !uint.TryParse(name.AsSpan(2, separator - 2), NumberStyles.None, CultureInfo.InvariantCulture, out index))
                    throw Invalid("constant_register");
                kind = 0;
            }
            else
            {
                if (name.Length < 2 || !uint.TryParse(name.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out index))
                    throw Invalid("register");
                kind = name[0] switch { 't' => 2u, 's' => 3u, 'u' => 4u, _ => throw Invalid("register_kind") };
            }
            var source = stage.Definitions.Bindings.Single(value => value.BindPoint == index &&
                (kind == 4 ? value.InputType is 4 or 6 : value.InputType == kind));
            if (source.BindCount != 1 || !used.Add(source.Index) || variable.DescriptorSet != 0 || variable.Binding is null)
                throw Invalid("binding");
            var type = variable.Type; string descriptor; DxbcRdefBuffer? cb = null;
            if (kind == 0)
            {
                cb = stage.Definitions.Buffers.Single(value => value.Name.Text == source.Name.Text);
                if (variable.StorageClass != 2 || type is not { Kind: "struct", Block: true } || variable.RequiredBufferBytes != cb.ByteSize)
                    throw Invalid("constant_layout");
                descriptor = "UniformBuffer";
            }
            else if (kind == 3)
            {
                if (variable.StorageClass != 0 || type.Kind != "sampler") throw Invalid("sampler");
                descriptor = "Sampler";
            }
            else if (source.InputType == 6)
            {
                if (source.Dimension != 1 || source.ReturnType != 6 || variable.StorageClass != 2 ||
                    type is not { Kind: "struct", BufferBlock: true, Block: false } ||
                    type.Members is not [{ Offset: 0, Type: { Kind: "runtime-array", ArrayStride: 4,
                        ElementType: { Kind: "int", Bits: 32, Signed: false } } }] ||
                    stage.Definitions.Buffers.Single(value => value.Name.Text == source.Name.Text).ByteSize != 108)
                    throw Invalid("structured_coefficients");
                descriptor = "StorageBuffer";
            }
            else
            {
                if (source.Dimension is not (4 or 5) || source.ReturnType != 5 || variable.StorageClass != 0 ||
                    type is not { Kind: "image", Dimension: 1, Multisampled: false, ComponentType: { Kind: "float", Bits: 32 } } ||
                    type.Arrayed != (source.Dimension == 5) || type.SampledCode != (kind == 2 ? 1u : 2u)) throw Invalid("image");
                descriptor = kind == 2 ? "Texture" : "Image";
            }
            bindings.Add(new(variable, source, cb, descriptor));
        }
        if (used.Count != stage.Definitions.Bindings.Count) throw Invalid("complete_interface");
        return new(module, bindings.AsReadOnly());
    }
    private static InvalidDataException Invalid(string field) => new("reflection.filter_shader." + field);
}
