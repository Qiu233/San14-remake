using System.Globalization;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Complete original CS7/8/14 interfaces. Sky's writable R32_FLOAT
/// texel buffer uses the mature official compiler storage-buffer option,
/// preserving original DXBC accesses without another SPIR-V rewrite.</summary>
public static class ReflectionCaptureContentShader
{
    public static ReflectionComputeBindings ReadBindings(ReflectionShaderStage stage, ReadOnlySpan<byte> spirv)
    {
        if (stage.StageCode != 5 || stage.ProgramIndex is not (7 or 8 or 14)) throw Invalid("stage");
        var module = SpirvShaderInterfaceReader.Read(spirv, allowFloat32StorageRuntimeArrays: stage.ProgramIndex == 14);
        var entry = module.EntryPoints.Single();
        var mode = module.ExecutionModes.Single(value => value.FunctionId == entry.FunctionId && value.Mode == 17);
        if (entry.ExecutionModel != 5 || entry.Name != "main" || mode.Operands.Count != 3 || mode.Operands.Any(value => value == 0))
            throw Invalid("compute_entry");
        if (stage.ProgramIndex is 7 or 8 && !mode.Operands.SequenceEqual(new uint[] { 8, 8, 1 }))
            throw Invalid("capture_threads");
        var bindings = new List<ReflectionComputeBinding>(); var used = new HashSet<int>();
        foreach (var variable in module.Descriptors)
        {
            string name = variable.Name ?? throw Invalid("register_name");
            uint type, index; string kind; DxbcRdefBuffer? cb = null;
            if (name.StartsWith("cb", StringComparison.Ordinal))
            {
                int separator = name.IndexOf('_', 2);
                if (separator < 0 || !uint.TryParse(name.AsSpan(2, separator - 2), NumberStyles.None,
                    CultureInfo.InvariantCulture, out index)) throw Invalid("constant_register");
                type = 0; kind = "UniformBuffer";
            }
            else
            {
                if (name.Length < 2 || !uint.TryParse(name.AsSpan(1), NumberStyles.None,
                    CultureInfo.InvariantCulture, out index)) throw Invalid("register");
                type = name[0] switch { 't' => 2u, 's' => 3u, 'u' => 4u, _ => throw Invalid("register_kind") };
                kind = type switch { 2 => "Texture", 3 => "Sampler", _ => index == 4 ? "ImageBuffer" : "Image" };
            }
            var source = stage.Definitions.Bindings.Single(value => value.InputType == type && value.BindPoint == index);
            if (source.BindCount != 1 || !used.Add(source.Index) || variable.DescriptorSet != 0 || variable.Binding is null)
                throw Invalid("binding");
            var actual = variable.Type;
            if (type == 0)
            {
                cb = stage.Definitions.Buffers.Single(value => value.Name.Text == source.Name.Text);
                if (variable.StorageClass != 2 || actual.Kind != "struct" || !actual.Block || variable.RequiredBufferBytes != cb.ByteSize)
                    throw Invalid("constant_layout");
            }
            else if (type == 3)
            {
                if (variable.StorageClass != 0 || actual.Kind != "sampler") throw Invalid("sampler");
            }
            else if (kind == "ImageBuffer")
            {
                if (stage.ProgramIndex != 14 || source.Name.Text != "sky_occ" || source.Dimension != 1 ||
                    source.ReturnType != 5 || variable.StorageClass != 2 || actual.Kind != "struct" || !actual.BufferBlock ||
                    actual.Members is not [{ Offset: 0, Type: { Kind: "runtime-array", ArrayStride: 4,
                        ElementType: { Kind: "float", Bits: 32 } } }])
                    throw Invalid("sky_storage_buffer");
            }
            else
            {
                uint dimension = source.Dimension switch { 1 => 5u, 5 => 1u, 9 => 3u, _ => throw Invalid("source_dimension") };
                if (variable.StorageClass != 0 || actual.Kind != "image" || actual.Dimension != dimension ||
                    actual.Arrayed != (source.Dimension == 5) || actual.Multisampled != false ||
                    actual.SampledCode != (type == 2 ? 1u : 2u) || source.ReturnType != 5 ||
                    actual.ComponentType is not { Kind: "float", Bits: 32 }) throw Invalid("image_type");
            }
            bindings.Add(new(variable, source, cb, kind));
        }
        if (used.Count != stage.Definitions.Bindings.Count) throw Invalid("complete_interface");
        return new(module, bindings.AsReadOnly(), mode.Operands[0], mode.Operands[1], mode.Operands[2]);
    }

    private static InvalidDataException Invalid(string field) => new("reflection.capture_shader." + field);
}
