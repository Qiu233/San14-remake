using Godot;
using San14.Data;
using San14.Data.Formats;
using System.Security.Cryptography;

/// <summary>Own RenderingDevice shader using a source-derived fullscreen adapter
/// and the translated original pixel bytecode. Creation does not bind inputs,
/// execute a pass, or replace the current map material.</summary>
public sealed partial class OriginalDeferredPixelProgram : IAsyncDisposable
{
    public Rid ShaderRid { get; }
    public LoadedShaderContainer Source { get; }
    public G2ShaderCombination Combination { get; }
    public OriginalShaderSpirv Translation { get; }
    public OriginalShaderSpirv OriginalTranslation { get; }
    public OriginalTranslatedShaderBindings SourceDescriptorBindings { get; }
    public OriginalTranslatedShaderBindings DescriptorBindings { get; }
    public OriginalDeferredNullImageSpirv? NullImageSpecialization { get; }
    public OriginalDeferredSourceLocalAmbientNullSpirv? LocalAmbientNullSpecialization { get; }
    public OriginalDeferredSourceNullIes? IesSourceNull { get; }
    public OriginalDeferredNullImageSpirv? IesNullSpecialization { get; }
    public OriginalDeferredOptionalLocalAmbientNullSpirv? OptionalLocalAmbientNullSpecialization { get; }
    public OriginalDeferredSelectedSourceNullSpirv? SelectedSourceNullSpecialization { get; }
    public string VertexAdapterSha256 { get; }
    private Task? _disposal;

    private OriginalDeferredPixelProgram(Rid shader, LoadedShaderContainer source,
        G2ShaderCombination combination, OriginalShaderSpirv translation, OriginalTranslatedShaderBindings descriptorBindings,
        OriginalShaderSpirv originalTranslation, OriginalTranslatedShaderBindings sourceBindings,
        OriginalDeferredNullImageSpirv? nullImages,
        OriginalDeferredSourceLocalAmbientNullSpirv? localAmbientNull = null,
        OriginalDeferredSourceNullIes? ies = null, OriginalDeferredNullImageSpirv? iesNull = null,
        OriginalDeferredOptionalLocalAmbientNullSpirv? optionalLocal = null,
        OriginalDeferredF103SelectedSource? selected = null,
        OriginalDeferredSelectedSourceNullSpirv? selectedNull = null)
    {
        ShaderRid = shader; Source = source; Combination = combination; Translation = translation;
        DescriptorBindings = descriptorBindings;
        OriginalTranslation = originalTranslation; SourceDescriptorBindings = sourceBindings;
        NullImageSpecialization = nullImages;
        LocalAmbientNullSpecialization = localAmbientNull;
        IesSourceNull = ies; IesNullSpecialization = iesNull;
        OptionalLocalAmbientNullSpecialization = optionalLocal;
        SourceSelection = selected; SelectedSourceNullSpecialization = selectedNull;
        VertexAdapterSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(OriginalDeferredFullscreenShader.VertexSource)));
    }

    public static Task<OriginalDeferredPixelProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler)
        => BuildCoreAsync(source, explicitCombinationIndex, compiler, []);

    /// <summary>Specializes only explicitly proven fresh source-null Hatching/Ramp
    /// samples. Retains all42 source RDEF declarations and the unmodified compiler
    /// output; the device shader omits the proven null descriptor pairs.</summary>
    public static Task<OriginalDeferredPixelProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler,
        IReadOnlyList<OriginalDeferredSourceNull2D> explicitFreshSourceNulls)
    {
        ArgumentNullException.ThrowIfNull(explicitFreshSourceNulls);
        return BuildCoreAsync(source, explicitCombinationIndex, compiler, explicitFreshSourceNulls.ToArray());
    }

    private static async Task<OriginalDeferredPixelProgram> BuildCoreAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler,
        IReadOnlyList<OriginalDeferredSourceNull2D> sourceNulls,
        OriginalDeferredSourceLocalAmbient? sourceLocalAmbient = null,
        OriginalDeferredSourceNullIes? sourceIes = null,
        OriginalDeferredF103SelectedSource? selected = null)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(compiler);
        G1ShaderResourceBindings bindings = source.ReadG1ResourceBindings(explicitCombinationIndex);
        G2ShaderCombination combination = bindings.Combination;
        G2ShaderCombinationStage[] pixels = combination.Stages.Where(s => s.StageCode == 0 && s.Entry is not null).ToArray();
        G2ShaderCombinationStage[] vertices = combination.Stages.Where(s => s.StageCode == 1 && s.Entry is not null).ToArray();
        if (pixels.Length != 1 || vertices.Length != 1)
            throw new InvalidDataException("Deferred shader requires explicit vertex and pixel source programs.");
        ReadOnlyMemory<byte> vertex = source.Container.GetProgramBytes(source.Source.Bytes, vertices[0].Entry!.ProgramIndex).ToArray();
        // Adapter is specific to the original 784B fullscreen VS0 already
        // recovered independently. Do not silently use it for other layouts.
        if (Convert.ToHexString(SHA256.HashData(vertex.Span)) != "C51D71A59DC89E4B23B68395180A41473B35914AD33BCE80C92AF636A379757D")
            throw new InvalidDataException("Selected vertex program is outside the verified fullscreen adapter contract.");
        OriginalShaderSpirv original = compiler.Compile(source.Container.GetProgramBytes(source.Source.Bytes, pixels[0].Entry!.ProgramIndex));
        OriginalTranslatedShaderBindings sourceBindings = OriginalTranslatedShaderBindingReader.Read(
            source.ReadResourceDefinitions(pixels[0].Entry!.ProgramIndex), original.Bytes);
        OriginalDeferredSelectedSourceNullSpirv? selectedNull = selected is not null &&
            (sourceNulls.Count != 0 || sourceLocalAmbient is not null || sourceIes is not null)
            ? OriginalDeferredSelectedSourceNullAdapter.Adapt(original, sourceBindings, selected, sourceNulls, sourceIes, sourceLocalAmbient) : null;
        OriginalDeferredSourceLocalAmbientNullSpirv? localAmbientNull = selected is not null || sourceLocalAmbient is null ? null
            : OriginalDeferredSourceLocalAmbientNullAdapter.Adapt(original, sourceBindings, sourceLocalAmbient, sourceNulls);
        OriginalDeferredNullImageSpirv? nullImages = selected is not null ? null : localAmbientNull is not null ? localAmbientNull.Preceding2D : sourceNulls.Count == 0 ? null
            : OriginalDeferredNullImageAdapter.Adapt(original, sourceBindings, sourceNulls);
        OriginalDeferredOptionalLocalAmbientNullSpirv? optionalLocal = selected is null && sourceIes is not null && sourceLocalAmbient is not null
            ? OriginalDeferredOptionalNullImageAdapter.Adapt(original, sourceBindings, sourceIes, sourceLocalAmbient, sourceNulls) : null;
        OriginalDeferredNullImageSpirv? iesNull = selected is null && sourceIes is not null && sourceLocalAmbient is null
            ? OriginalDeferredOptionalNullImageAdapter.Adapt(original, sourceBindings, sourceIes, sourceNulls) : null;
        if (optionalLocal is not null)
        { localAmbientNull = optionalLocal.LocalAmbient; nullImages = localAmbientNull.Preceding2D; }
        OriginalShaderSpirv translated = selectedNull is not null
            ? original with { Bytes = selectedNull.Bytes, SpirvSha256 = selectedNull.Sha256 } : optionalLocal is not null
            ? original with { Bytes = optionalLocal.Bytes, SpirvSha256 = optionalLocal.Sha256 } : iesNull is not null
            ? original with { Bytes = iesNull.Bytes, SpirvSha256 = iesNull.Sha256 } : localAmbientNull is not null
            ? original with { Bytes = localAmbientNull.Bytes, SpirvSha256 = localAmbientNull.Sha256 } : nullImages is null ? original
            : original with { Bytes = nullImages.Bytes, SpirvSha256 = nullImages.Sha256 };
        OriginalTranslatedShaderBindings descriptorBindings = selectedNull is not null
            ? OriginalDeferredSelectedSourceNullAdapter.ReadDeviceBindings(sourceBindings, selectedNull) : optionalLocal is not null
            ? OriginalDeferredOptionalNullImageAdapter.ReadDeviceBindings(sourceBindings, optionalLocal) : iesNull is not null
            ? OriginalDeferredOptionalNullImageAdapter.ReadDeviceBindings(sourceBindings, iesNull) : localAmbientNull is not null
            ? OriginalDeferredSourceLocalAmbientNullAdapter.ReadDeviceBindings(sourceBindings, localAmbientNull) : nullImages is null ? sourceBindings
            : OriginalDeferredNullImageAdapter.ReadDeviceBindings(sourceBindings, nullImages);
        Rid shader = await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            using var vertexSource = new RDShaderSource { SourceVertex = OriginalDeferredFullscreenShader.VertexSource };
            using RDShaderSpirV adapter = rd.ShaderCompileSpirVFromSource(vertexSource);
            string error = adapter.GetStageCompileError(RenderingDevice.ShaderStage.Vertex);
            if (error.Length != 0) throw new InvalidDataException("Original fullscreen adapter compilation failed: " + error);
            using var combined = new RDShaderSpirV();
            combined.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, adapter.GetStageBytecode(RenderingDevice.ShaderStage.Vertex));
            combined.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, translated.Bytes);
            Rid created = rd.ShaderCreateFromSpirV(combined, "OriginalDeferredPixelProgram");
            if (!created.IsValid) throw new InvalidDataException("RenderingDevice rejected the translated original pixel shader.");
            return created;
        });
        return new(shader, source, combination, translated, descriptorBindings, original, sourceBindings, nullImages,
            localAmbientNull, selected is null ? sourceIes : null, iesNull, optionalLocal, selected, selectedNull);
    }

    public ValueTask DisposeAsync() => new(_disposal ??= OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { rd.FreeRid(ShaderRid); return true; }));
}
