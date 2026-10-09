using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

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

/// <summary>An explicitly selected source model VS and optional PS. Selection of a
/// combination and original raster/material state remains a separate source
/// input. No preview lighting or replacement shader is used.</summary>
public sealed class ModelShaderProgram : IAsyncDisposable
{
    public LoadedShaderContainer Source { get; }
    public G2ShaderCombination Combination { get; }
    public int VertexProgramIndex { get; }
    public ModelPixelShaderStage? PixelStage { get; }
    // Existing color consumers require a pixel stage; absent PS is not an
    // empty replacement shader. Shadow consumers inspect PixelStage directly.
    public int PixelProgramIndex => RequirePixelStage().ProgramIndex;
    public string VertexSourceSha256 { get; }
    public string PixelSourceSha256 => RequirePixelStage().SourceSha256;
    public ShaderSpirv VertexTranslation { get; }
    public AdaptedVertexShader VertexAdapter { get; }
    public ShaderSpirv PixelTranslation => RequirePixelStage().Translation;
    public TranslatedShaderBindings VertexBindings { get; }
    public TranslatedShaderBindings PixelBindings => RequirePixelStage().Bindings;
    public DxbcProgramSignatures VertexSignatures { get; }
    public DxbcProgramSignatures VertexDeviceTransportSignatures { get; }
    public IReadOnlyList<uint> ColorOutputLocations { get; }
    public int ColorAttachmentCount { get; }
    public Rid ShaderRid { get; }
    private Task? _disposal;

    private ModelShaderProgram(ModelShaderProgramPreparation prepared, Rid shader)
    {
        Source = prepared.Source; Combination = prepared.Combination;
        VertexProgramIndex = prepared.VertexProgramIndex; VertexSourceSha256 = prepared.VertexSourceSha256;
        VertexTranslation = prepared.VertexTranslation; VertexAdapter = prepared.VertexAdapter;
        VertexBindings = prepared.VertexBindings; VertexSignatures = prepared.VertexSignatures;
        VertexDeviceTransportSignatures = prepared.VertexDeviceTransportSignatures;
        PixelStage = prepared.PixelStage; ColorOutputLocations = prepared.ColorOutputLocations;
        ColorAttachmentCount = prepared.ColorAttachmentCount; ShaderRid = shader;
    }

    public static async Task<ModelShaderProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler, ArmySkinVertexProfile? armySkinProfile=null)
    {
        var prepared = PrepareSource(source, explicitCombinationIndex, compiler, armySkinProfile);
        return await BuildPreparedAsync(prepared);
    }

    // Device components may supply a source-pinned backend input adaptation
    // after PrepareSource. The ordinary public preparation path stays intact.
    internal static async Task<ModelShaderProgram> BuildPreparedAsync(ModelShaderProgramPreparation prepared)
    {
        Rid shader = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            using var stages = new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, prepared.VertexAdapter.Bytes);
            if (prepared.PixelStage is { } pixel)
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, pixel.Translation.Bytes);
            Rid value = rd.ShaderCreateFromSpirV(stages, "OriginalModelSourceProgram");
            if (!value.IsValid) throw new InvalidDataException("RenderingDevice rejected the selected original model stages.");
            return value;
        });
        return new(prepared, shader);
    }

    /// <summary>Import/build boundary only. Compile the explicitly selected
    /// original combination without a device or a replacement pixel stage.</summary>
    public static ModelShaderProgramPreparation PrepareSource(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler, ArmySkinVertexProfile? armySkinProfile=null)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(compiler);
        byte[] snapshot = source.Source.Bytes.ToArray();
        if (!Convert.ToHexString(SHA256.HashData(snapshot)).Equals(source.Source.DecodedSha256,
            StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Original model shader bytes differ from their source hash.");
        // Reparse these exact bytes rather than trusting a caller's modified
        // container offsets. Overrides are supported with the same validation.
        var canonical = source with { Source = source.Source with { Bytes = snapshot }, Container = G2ShaderContainerReader.Read(snapshot) };
        G2ShaderCombination combination = canonical.ReadG1ResourceBindings(explicitCombinationIndex).Combination;
        if (combination.Stages.Any(s => s.Entry is not null && s.StageCode is not (0 or 1)))
            throw new InvalidDataException("Original model device profile requires only vertex and pixel stages.");
        var vs = combination.Stages.Where(s => s.StageCode == 1 && s.Entry is not null).ToArray();
        var ps = combination.Stages.Where(s => s.StageCode == 0 && s.Entry is not null).ToArray();
        if (vs.Length != 1 || ps.Length > 1)
            throw new InvalidDataException("Original model device profile requires one vertex and at most one pixel stage.");
        int vi = vs[0].Entry!.ProgramIndex;
        int? pi = ps.Length == 0 ? null : ps[0].Entry!.ProgramIndex;
        byte[] vertexBytes = canonical.Container.GetProgramBytes(canonical.Source.Bytes, vi).ToArray();
        byte[]? pixelBytes = pi is { } pixelIndex ? canonical.Container.GetProgramBytes(canonical.Source.Bytes, pixelIndex).ToArray() : null;
        var vertex = compiler.Compile(vertexBytes, 1);
        var pixel = pixelBytes is null ? null : compiler.Compile(pixelBytes);
        byte[] backendVertex=vertex.Bytes;
        if(armySkinProfile is { } armyProfile)
        {
            bool soldierSource=canonical.Source.ResourceUri==GameResourceId.Rdb("system",0xB90AEDD3).ToString()&&
                canonical.Source.DecodedSha256==ArmySkinVertexAdapter.ShaderSha256;
            bool flagVariationSource=canonical.Source.ResourceUri==GameResourceId.Rdb("system",ArmyFlagMaterialVariations.GbufferShaderId).ToString()&&
                canonical.Source.DecodedSha256==ArmyFlagSkinVertexAdapter.ShaderSha256&&
                armyProfile==ArmySkinVertexProfile.FourInfluenceUnsigned8&&pi==2;
            if((!soldierSource&&!flagVariationSource)||explicitCombinationIndex!=1||vi!=1||pi is null)
                throw new InvalidDataException("Army optional input transport requires its exact source G-buffer shader/combination.");
            backendVertex=flagVariationSource?ArmyFlagSkinVertexAdapter.Adapt(vertex,armyProfile).Bytes:
                ArmySkinVertexAdapter.Adapt(vertex,armyProfile).Bytes;
        }
        var rawInterface = SpirvShaderInterfaceReader.Read(backendVertex);
        var adapter = rawInterface.StageVariables.Any(v => v.BuiltIn == 4424)
            ? VertexShaderAdapter.Adapt(backendVertex, 0, 0, 0)
            : VertexShaderAdapter.AdaptPositionInput(backendVertex);
        var vertexBindings = TranslatedShaderBindingReader.Read(canonical.ReadResourceDefinitions(vi), adapter.Bytes);
        var pixelBindings = pi is { } actualPixelIndex
            ? TranslatedShaderBindingReader.Read(canonical.ReadResourceDefinitions(actualPixelIndex), pixel!.Bytes) : null;
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
        var vertexSignatures = canonical.ReadSignatures(vi);
        var deviceSignatures = armySkinProfile is { } profile
            ? ArmySkinVertexAdapter.DeviceTransportSignatures(vertexSignatures, profile) : vertexSignatures;
        var pixelStage = pi is { } actualPi ? new ModelPixelShaderStage(actualPi,
            Convert.ToHexString(SHA256.HashData(pixelBytes!)), pixel!, pixelBindings!) : null;
        return new(canonical, combination, vi, Convert.ToHexString(SHA256.HashData(vertexBytes)), vertex, adapter,
            vertexBindings, vertexSignatures, deviceSignatures, pixelStage,
            Array.AsReadOnly(colors.Select(v => v.Location!.Value).ToArray()));
    }

    private ModelPixelShaderStage RequirePixelStage() => PixelStage
        ?? throw new InvalidOperationException("The selected original model combination has no pixel stage.");

    private static bool Compatible(SpirvInterfaceType output, SpirvInterfaceType input)
    {
        if (output.Kind != input.Kind) return false;
        if (output.Kind == "vector")
            return output.ComponentCount >= input.ComponentCount && Compatible(output.ComponentType!, input.ComponentType!);
        return output.Kind is "float" or "int" && output.Bits == input.Bits && output.Signed == input.Signed;
    }

    public ValueTask DisposeAsync() => new(_disposal ??= TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { rd.FreeRid(ShaderRid); return true; }));

    internal void RequireLive() => ObjectDisposedException.ThrowIf(_disposal is not null, this);
}
