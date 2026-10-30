using San14.Data.Formats;

namespace San14.Data;

public sealed record ModelPixelShaderStage(int ProgramIndex, string SourceSha256,
    ShaderSpirv Translation, TranslatedShaderBindings Bindings);

/// <summary>Imported CPU program preparation, including the source's actual
/// absent pixel stage. It does not create a device shader or certify a draw.</summary>
public sealed record ModelShaderProgramPreparation(LoadedShaderContainer Source,
    G2ShaderCombination Combination, int VertexProgramIndex, string VertexSourceSha256,
    ShaderSpirv VertexTranslation, AdaptedVertexShader VertexAdapter,
    TranslatedShaderBindings VertexBindings, DxbcProgramSignatures VertexSignatures,
    DxbcProgramSignatures VertexDeviceTransportSignatures, ModelPixelShaderStage? PixelStage,
    IReadOnlyList<uint> ColorOutputLocations)
{
    public int ColorAttachmentCount => ColorOutputLocations.Count == 0 ? 0 : checked((int)ColorOutputLocations[^1] + 1);
}

/// <summary>CPU preparation of selected model stages and their backend interface.
/// Resource containers are parsed by the repository; device creation is owned by the renderer.</summary>
public static class ModelShaderPreparation
{
    /// <summary>Import/build boundary only. Compile the explicitly selected
    /// original combination without a device or a replacement pixel stage.</summary>
    public static ModelShaderProgramPreparation Prepare(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler, ArmySkinVertexProfile? armySkinProfile=null, bool reflectionCaptureUnusedConstantTail=false)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(compiler);
        if(reflectionCaptureUnusedConstantTail && !source.ReadConfigurations().Records.Any(value=>value.Name=="ReflectionProbeGBuffer"))
            throw new InvalidDataException("Unused source CB tail admission requires the actual reflection capture module.");
        G2ShaderCombination combination = source.ReadCombination(explicitCombinationIndex);
        if (combination.Stages.Any(s => s.Entry is not null && s.StageCode is not (0 or 1)))
            throw new InvalidDataException("Original model device profile requires only vertex and pixel stages.");
        var vs = combination.Stages.Where(s => s.StageCode == 1 && s.Entry is not null).ToArray();
        var ps = combination.Stages.Where(s => s.StageCode == 0 && s.Entry is not null).ToArray();
        if (vs.Length != 1 || ps.Length > 1)
            throw new InvalidDataException("Original model device profile requires one vertex and at most one pixel stage.");
        int vi = vs[0].Entry!.ProgramIndex;
        int? pi = ps.Length == 0 ? null : ps[0].Entry!.ProgramIndex;
        var vertex = compiler.Compile(source.Container.GetProgramBytes(source.Source.Bytes, vi), 1);
        var pixel = pi is { } pixelIndex
            ? compiler.Compile(source.Container.GetProgramBytes(source.Source.Bytes, pixelIndex)) : null;
        var vertexSignatures = source.ReadSignatures(vi);
        byte[] backendVertex = armySkinProfile is { } armyProfile
            ? ArmySkinVertexAdapter.Adapt(vertex, vertexSignatures, armyProfile).Bytes : vertex.Bytes;
        var rawInterface = SpirvShaderInterfaceReader.Read(backendVertex);
        var adapter = rawInterface.StageVariables.Any(v => v.BuiltIn == 4424)
            ? VertexShaderAdapter.Adapt(backendVertex, 0, 0, 0)
            : VertexShaderAdapter.AdaptPositionInput(backendVertex);
        var vertexBindings = TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(vi), adapter.Bytes, reflectionCaptureUnusedConstantTail);
        var pixelBindings = pi is { } actualPixelIndex
            ? TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(actualPixelIndex), pixel!.Bytes, reflectionCaptureUnusedConstantTail) : null;
        if (vertexBindings.Bindings.Any(b => b.Device.DescriptorSet != 1) ||
            pixelBindings is not null && pixelBindings.Bindings.Any(b => b.Device.DescriptorSet != 0))
            throw new InvalidDataException("Original model stages do not have isolated descriptor sets.");
        if (!vertexBindings.Interface.StageVariables.Any(v => v.StorageClass == 1 && v.Location is not null))
            throw new InvalidDataException("Original model device profile requires source vertex-buffer inputs.");
        foreach (var input in pixelBindings?.Interface.StageVariables.Where(v => v.StorageClass == 1 && v.Location is not null)
            ?? Enumerable.Empty<SpirvInterfaceVariable>())
        {
            var outputs = vertexBindings.Interface.StageVariables.Where(v => v.StorageClass == 3 && v.Location == input.Location).ToArray();
            if (outputs.Length != 1 || !Compatible(outputs[0].Type, input.Type))
                throw new InvalidDataException("Original model VS output does not satisfy the PS location/type.");
        }
        var colors = pixelBindings?.Interface.StageVariables.Where(v => v.StorageClass == 3).OrderBy(v => v.Location).ToArray()
            ?? Array.Empty<SpirvInterfaceVariable>();
        bool SupportedColor(SpirvInterfaceType type)
        {
            var scalar = type.Kind == "vector" ? type.ComponentType! : type;
            return (type.Kind != "vector" || type.ComponentCount is >= 1 and <= 4) &&
                scalar.Kind is "float" or "int" && scalar.Bits == 32;
        }
        if (colors.Length > 8 || colors.Any(v => v.Location is null or > 7 || !SupportedColor(v.Type)) ||
            colors.Select(v => v.Location).Distinct().Count() != colors.Length)
            throw new InvalidDataException("Original model device profile requires unique 32-bit numeric color outputs in locations 0..7; depth outputs are unsupported.");
        // Sparse original outputs (for example a separate state target at6)
        // retain their source locations. The caller explicitly owns all slots
        // through the highest source location, including unused attachments.
        var deviceSignatures = armySkinProfile is { } profile
            ? ArmySkinVertexAdapter.DeviceTransportSignatures(vertexSignatures, profile) : vertexSignatures;
        var pixelStage = pi is { } actualPi ? new ModelPixelShaderStage(actualPi,
            pixel!.SourceProgramSha256, pixel!, pixelBindings!) : null;
        return new(source, combination, vi, vertex.SourceProgramSha256, vertex, adapter,
            vertexBindings, vertexSignatures, deviceSignatures, pixelStage,
            Array.AsReadOnly(colors.Select(v => v.Location!.Value).ToArray()));
    }

    private static bool Compatible(SpirvInterfaceType output, SpirvInterfaceType input)
    {
        if (output.Kind != input.Kind) return false;
        if (output.Kind == "vector")
            return output.ComponentCount >= input.ComponentCount && Compatible(output.ComponentType!, input.ComponentType!);
        return output.Kind is "float" or "int" && output.Bits == input.Bits && output.Signed == input.Signed;
    }

}
