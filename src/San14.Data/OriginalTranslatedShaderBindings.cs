using System.Globalization;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalTranslatedShaderBinding(SpirvInterfaceVariable Device,
    DxbcRdefBinding Source, DxbcRdefBuffer? ConstantBuffer, string DescriptorKind);
public sealed record OriginalTranslatedShaderBindings(SpirvShaderInterface Interface,
    DxbcResourceDefinitions OriginalDefinitions, IReadOnlyList<OriginalTranslatedShaderBinding> Bindings);

/// <summary>Matches the verified compiler's register names to every original RDEF
/// declaration. Keeps the actual output set/binding instead of assuming sequential
/// slots. No runtime values, sampler states or texture contents are synthesized.</summary>
public static class OriginalTranslatedShaderBindingReader
{
    public static OriginalTranslatedShaderBindings Read(DxbcResourceDefinitions original, ReadOnlySpan<byte> spirv)
    {
        ArgumentNullException.ThrowIfNull(original);
        SpirvShaderInterface module = SpirvShaderInterfaceReader.Read(spirv);
        uint stage = module.EntryPoints[0].ExecutionModel;
        if (stage is not (0 or 4) || module.EntryPoints[0].Name != "main" ||
            (stage == 4 && !module.ExecutionModes.Any(m => m.FunctionId == module.EntryPoints[0].FunctionId && m.Mode == 7)))
            throw new InvalidDataException("Original graphics binding requires Vertex/main or Fragment/main with OriginUpperLeft.");
        var rows = new List<OriginalTranslatedShaderBinding>(); var matched = new HashSet<int>();
        foreach (SpirvInterfaceVariable variable in module.Descriptors)
        {
            string name = variable.Name ?? throw new InvalidDataException("Compiler descriptor lacks its register name.");
            uint inputType; string register;
            if (name.StartsWith("cb", StringComparison.Ordinal))
            {
                int separator = name.IndexOf('_', 2);
                if (separator < 0 || !uint.TryParse(name[(separator + 1)..], NumberStyles.None,
                    CultureInfo.InvariantCulture, out _))
                    throw new InvalidDataException("Unknown compiler constant buffer register representation.");
                inputType = 0; register = name[2..separator];
            }
            else if (name.StartsWith('s')) { inputType = 3; register = name[1..]; }
            else if (name.StartsWith('t')) { inputType = 2; register = name[1..]; }
            else throw new InvalidDataException("Unknown compiler descriptor register name.");
            if (!uint.TryParse(register, NumberStyles.None, CultureInfo.InvariantCulture, out uint index))
                throw new InvalidDataException("Invalid compiler descriptor register number.");
            DxbcRdefBinding[] candidates = original.Bindings.Where(b => b.InputType == inputType && b.BindPoint == index).ToArray();
            if (candidates.Length != 1 || candidates[0].BindCount != 1 || !matched.Add(candidates[0].Index))
                throw new InvalidDataException("Compiler descriptor does not uniquely match one original RDEF declaration.");
            DxbcRdefBinding source = candidates[0]; SpirvInterfaceType type = variable.Type;
            DxbcRdefBuffer? buffer = null; string kind;
            if (inputType == 0)
            {
                DxbcRdefBuffer[] buffers = original.Buffers.Where(b => b.Name.Text == source.Name.Text).ToArray();
                if (variable.StorageClass != 2 || type.Kind != "struct" || !type.Block || buffers.Length != 1 ||
                    variable.RequiredBufferBytes != buffers[0].ByteSize)
                    throw new InvalidDataException("Translated constant buffer byte layout differs from original RDEF.");
                buffer = buffers[0]; kind = "UniformBuffer";
            }
            else if (inputType == 3)
            {
                if (variable.StorageClass != 0 || type.Kind != "sampler")
                    throw new InvalidDataException("Translated sampler has an incompatible descriptor type.");
                kind = "Sampler";
            }
            else
            {
                (uint dimension, bool arrayed) = source.Dimension switch
                {
                    1 => (5u, false), 4 => (1u, false), 5 => (1u, true),
                    8 => (2u, false), 9 => (3u, false), 10 => (3u, true),
                    _ => throw new InvalidDataException("Original texture dimension is outside the verified translation contract.")
                };
                bool component = source.ReturnType switch
                {
                    4 => type.ComponentType is { Kind: "int", Bits: 32, Signed: false },
                    5 => type.ComponentType is { Kind: "float", Bits: 32 },
                    _ => throw new InvalidDataException("Original texture return type is outside the verified translation contract.")
                };
                if (variable.StorageClass != 0 || type.Kind != "image" || type.Dimension != dimension ||
                    type.Arrayed != arrayed || type.Multisampled != false || type.SampledCode != 1 ||
                    type.ImageFormat != 0 || !component)
                    throw new InvalidDataException("Translated texture dimension or return type differs from original RDEF.");
                kind = dimension == 5 ? "TextureBuffer" : "Texture";
            }
            rows.Add(new(variable, source, buffer, kind));
        }
        if (matched.Count != original.Bindings.Count)
            throw new InvalidDataException("Original RDEF contains declarations not resolved by the translated interface.");
        return new(module, original, rows.AsReadOnly());
    }
}
