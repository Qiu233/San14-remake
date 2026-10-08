using Godot;
using San14.Data;
using San14.Data.Formats;
using System.Security.Cryptography;

public enum OriginalSkyCubeOperation { Copy, Filter }

/// <summary>Caller-selected original cube copy/filter shader pair. The active
/// feature variant is not inferred from the native pass index.</summary>
public sealed class OriginalSkyCubeProgram : IAsyncDisposable
{
    public LoadedShaderContainer Source { get; }
    public G2ShaderCombination Combination { get; }
    public OriginalShaderSpirv VertexTranslation { get; }
    public OriginalAdaptedVertexShader VertexAdapter { get; }
    public OriginalShaderSpirv PixelTranslation { get; }
    public OriginalTranslatedShaderBindings VertexBindings { get; }
    public OriginalTranslatedShaderBindings PixelBindings { get; }
    public Rid ShaderRid { get; }
    public OriginalSkyCubeOperation Operation { get; }
    public OriginalSkyAmbientFilterSelection SourceSampleSelection { get; }
    public int FilterSampleCount => Operation == OriginalSkyCubeOperation.Filter ? checked((int)SourceSampleSelection.SampleCount) : 0;
    private Task? _disposal;

    private OriginalSkyCubeProgram(LoadedShaderContainer source, G2ShaderCombination combination,
        OriginalShaderSpirv vertex, OriginalAdaptedVertexShader adapter, OriginalShaderSpirv pixel,
        OriginalTranslatedShaderBindings vertexBindings, OriginalTranslatedShaderBindings pixelBindings, Rid shader,
        OriginalSkyCubeOperation operation, OriginalSkyAmbientFilterSelection sampleSelection)
    {
        (Source, Combination, VertexTranslation, VertexAdapter, PixelTranslation, VertexBindings, PixelBindings, ShaderRid,Operation)
            = (source, combination, vertex, adapter, pixel, vertexBindings, pixelBindings, shader,operation);
        SourceSampleSelection=sampleSelection;
    }

    public static async Task<OriginalSkyCubeProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler, OriginalSkyCubeOperation operation = OriginalSkyCubeOperation.Copy)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(compiler);
        if(operation is not (OriginalSkyCubeOperation.Copy or OriginalSkyCubeOperation.Filter))
            throw new InvalidDataException("Unknown sky cube operation.");
        var sampleSelection=OriginalSkyAmbientFilterSelection.FromExplicitCombination(source,
            explicitCombinationIndex,operation==OriginalSkyCubeOperation.Filter);
        sampleSelection.ValidatePrograms();
        var combination = operation==OriginalSkyCubeOperation.Filter?
            sampleSelection.FilterCombination:sampleSelection.CopyCombination;
        if (combination.Stages.Any(s => s.Entry is not null && s.StageCode is not (0 or 1)))
            throw new InvalidDataException("Sky cube source requires vertex and pixel stages only.");
        var vs = combination.Stages.Single(s => s.StageCode == 1 && s.Entry is not null);
        var ps = combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null);
        var vertexBytes = source.Container.GetProgramBytes(source.Source.Bytes, vs.Entry!.ProgramIndex);
        var pixelBytes = source.Container.GetProgramBytes(source.Source.Bytes, ps.Entry!.ProgramIndex);
        string pixelHash=operation switch {
            OriginalSkyCubeOperation.Copy=>OriginalSkyAmbientFilterSelection.CopyPixelSha256,
            OriginalSkyCubeOperation.Filter=>sampleSelection.FilterPixelSha256,
            _=>throw new InvalidDataException("Unknown sky cube operation.") };
        if (Convert.ToHexString(SHA256.HashData(vertexBytes)) != OriginalSkyAmbientFilterSelection.VertexSha256 ||
            Convert.ToHexString(SHA256.HashData(pixelBytes)) != pixelHash)
            throw new InvalidDataException("Selected source combination is outside the verified sky cube program contract.");
        var vertex = compiler.Compile(vertexBytes, 1);
        var pixel = compiler.Compile(pixelBytes);
        var adapter = OriginalVertexShaderAdapter.Adapt(vertex.Bytes, 0, 0, 0);
        var vertexBindings = OriginalTranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(vs.Entry.ProgramIndex), adapter.Bytes);
        var pixelBindings = OriginalTranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(ps.Entry.ProgramIndex), pixel.Bytes);
        if (vertexBindings.Bindings.Count != 1 || pixelBindings.Bindings.Count != (operation==OriginalSkyCubeOperation.Copy?3:5) ||
            vertexBindings.Bindings.Any(b => b.Device.DescriptorSet != 1) || pixelBindings.Bindings.Any(b => b.Device.DescriptorSet != 0) ||
            vertexBindings.Interface.StageVariables.Any(v => v.StorageClass == 1 && v.Location is not null))
            throw new InvalidDataException("Sky cube descriptors or vertex-ID geometry differ from the source contract.");
        foreach (var binding in vertexBindings.Bindings.Concat(pixelBindings.Bindings))
            if (binding.DescriptorKind == "UniformBuffer" && (binding.Source.BindPoint != 0 ||
                binding.Device.RequiredBufferBytes != 16 || binding.ConstantBuffer is not { ByteSize: 16 } buffer ||
                buffer.Variables.Count != 1 || buffer.Variables[0].Name.Text != "g_vMipInfo" ||
                buffer.Variables[0].StartOffset != 0 || buffer.Variables[0].ByteSize != 16))
                throw new InvalidDataException("Sky cube g_vMipInfo layout differs from the original 16-byte buffer.");
        Rid shader = await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            using var stages = new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, adapter.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, pixel.Bytes);
            Rid rid = rd.ShaderCreateFromSpirV(stages, "OriginalSkyCube" + operation);
            if (!rid.IsValid) throw new InvalidDataException("Device rejected the original sky cube shader pair.");
            return rid;
        });
        return new OriginalSkyCubeProgram(source, combination, vertex, adapter, pixel, vertexBindings, pixelBindings, shader,operation,sampleSelection);
    }

    public ValueTask DisposeAsync() => new(_disposal ??= OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { rd.FreeRid(ShaderRid); return true; }));
}
