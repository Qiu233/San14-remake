using Godot;
using San14.Data;
using San14.Data.Formats;

public enum SkyCloudOperation { Pattern, Color }

/// <summary>Explicit source Pattern pass5 and typed applied Cloud WORK color
/// pass0 low-cost0/1/2 pairs. Current
/// source selection and prepared constants remain separate typed inputs.</summary>
public sealed class SkyCloudProgram : IAsyncDisposable
{
    public LoadedShaderContainer Source { get; }
    public G2ShaderCombination Combination { get; }
    public ShaderSpirv VertexTranslation { get; }
    public AdaptedVertexShader VertexAdapter { get; }
    public ShaderSpirv PixelTranslation { get; }
    public TranslatedShaderBindings VertexBindings { get; }
    public TranslatedShaderBindings PixelBindings { get; }
    public Rid ShaderRid { get; }
    public SkyCloudOperation Operation { get; }
    private Task? _disposal;
    private readonly object _disposalGate=new();

    private SkyCloudProgram(LoadedShaderContainer source, G2ShaderCombination combination,
        ShaderSpirv vertex, AdaptedVertexShader adapter, ShaderSpirv pixel,
        TranslatedShaderBindings vb, TranslatedShaderBindings pb, Rid shader, SkyCloudOperation operation)
        => (Source, Combination, VertexTranslation, VertexAdapter, PixelTranslation, VertexBindings, PixelBindings, ShaderRid, Operation)
            = (source, combination, vertex, adapter, pixel, vb, pb, shader, operation);

    public static Task<SkyCloudProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler) =>
        BuildCoreAsync(source,explicitCombinationIndex,compiler,null);

    public static Task<SkyCloudProgram> BuildAsync(SkyCloudColorProgramSelection selection,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return BuildCoreAsync(selection.Shader,selection.Combination.Index,compiler,selection);
    }

    private static async Task<SkyCloudProgram> BuildCoreAsync(LoadedShaderContainer source,
        int explicitCombinationIndex,Vkd3dShaderCompiler compiler,SkyCloudColorProgramSelection? applied)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(compiler);
        var combination = applied?.Combination ?? source.ReadG1ResourceBindings(explicitCombinationIndex).Combination;
        var operation = applied is null ? SkyCloudOperation.Pattern : SkyCloudOperation.Color;
        if (combination.Index % 6 != (operation == SkyCloudOperation.Pattern ? 5 : 0))
            throw new InvalidDataException("Cloud operation must match the explicit source pass.");
        if (combination.Stages.Any(s => s.Entry is not null && s.StageCode is not (0 or 1)))
            throw new InvalidDataException("Cloud producer requires one vertex/pixel source pair.");
        var vs = combination.Stages.Single(s => s.StageCode == 1 && s.Entry is not null);
        var ps = combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null);
        var vertexBytes = source.Container.GetProgramBytes(source.Source.Bytes, vs.Entry!.ProgramIndex);
        var pixelBytes = source.Container.GetProgramBytes(source.Source.Bytes, ps.Entry!.ProgramIndex);
        if(applied is not null&&(applied.VertexProgram!=vs.Entry.ProgramIndex||applied.PixelProgram!=ps.Entry.ProgramIndex))
            throw new InvalidDataException("Cloud renderer pair differs from its actual source-selected low-cost contract.");
        var vertex = compiler.Compile(vertexBytes, 1); var pixel = compiler.Compile(pixelBytes);
        var adapter = VertexShaderAdapter.Adapt(vertex.Bytes, 0, 0, 0);
        var vb = TranslatedShaderBindingReader.Read(applied?.VertexDefinitions ?? source.ReadResourceDefinitions(vs.Entry.ProgramIndex), adapter.Bytes);
        var pb = TranslatedShaderBindingReader.Read(applied?.PixelDefinitions ?? source.ReadResourceDefinitions(ps.Entry.ProgramIndex), pixel.Bytes);
        if (vb.Bindings.Count != 1 || pb.Bindings.Count != (operation == SkyCloudOperation.Pattern ? 7 : 15) ||
            vb.Bindings.Any(b => b.Device.DescriptorSet != 1) || pb.Bindings.Any(b => b.Device.DescriptorSet != 0) ||
            vb.Interface.StageVariables.Any(v => v.StorageClass == 1 && v.Location is not null))
            throw new InvalidDataException("Original cloud descriptors or VertexID contract differs.");
        foreach (var pair in new[] { (Bindings: vb, Size: 160),
            (Bindings: pb, Size: operation == SkyCloudOperation.Pattern ? 192 : 512) })
        {
            var cb = pair.Bindings.Bindings.Single(b => b.DescriptorKind == "UniformBuffer");
            if (cb.Source.BindPoint != 0 || cb.ConstantBuffer?.ByteSize != pair.Size || cb.Device.RequiredBufferBytes != pair.Size)
                throw new InvalidDataException("Original cloud constant buffer length differs.");
        }
        Rid shader = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            using var stages = new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, adapter.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, pixel.Bytes);
            Rid rid = rd.ShaderCreateFromSpirV(stages, "OriginalSkyCloud" + operation);
            if (!rid.IsValid) throw new InvalidDataException("Device rejected original cloud program.");
            return rid;
        });
        return new(source, combination, vertex, adapter, pixel, vb, pb, shader, operation);
    }

    public ValueTask DisposeAsync()
    {
        lock(_disposalGate)
        {
            if(_disposal is null||_disposal.IsFaulted||_disposal.IsCanceled)
                _disposal=TerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(ShaderRid);return true;});
            return new(_disposal);
        }
    }
}
