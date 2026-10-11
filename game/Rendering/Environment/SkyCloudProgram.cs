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
    private readonly SkyCloudPatternMaterialLayout? patternLayout;
    public SkyCloudPatternMaterialLayout PatternLayout => patternLayout ??
        throw new InvalidOperationException("A Color program has no Pattern material layout.");
    private Task? _disposal;
    private readonly object _disposalGate=new();

    private SkyCloudProgram(LoadedShaderContainer source, G2ShaderCombination combination,
        ShaderSpirv vertex, AdaptedVertexShader adapter, ShaderSpirv pixel,
        TranslatedShaderBindings vb, TranslatedShaderBindings pb, Rid shader, SkyCloudOperation operation,
        SkyCloudPatternMaterialLayout? patternLayout)
        => (Source, Combination, VertexTranslation, VertexAdapter, PixelTranslation, VertexBindings, PixelBindings, ShaderRid, Operation, this.patternLayout)
            = (source, combination, vertex, adapter, pixel, vb, pb, shader, operation, patternLayout);

    public static Task<SkyCloudProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler) =>
        BuildCoreAsync(source,explicitCombinationIndex,compiler,null,null);

    public static Task<SkyCloudProgram> BuildAsync(SkyCloudColorProgramSelection selection,Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return BuildCoreAsync(selection.Shader,selection.Combination.Index,compiler,selection,null);
    }

    public static Task<SkyCloudProgram> BuildAsync(SkyCloudPatternProgramSelection selection, Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return BuildCoreAsync(selection.Shader, selection.Combination.Index, compiler, null, selection);
    }

    private static async Task<SkyCloudProgram> BuildCoreAsync(LoadedShaderContainer source,
        int explicitCombinationIndex,Vkd3dShaderCompiler compiler,SkyCloudColorProgramSelection? applied,
        SkyCloudPatternProgramSelection? pattern)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(compiler);
        var combination = applied?.Combination ?? pattern?.Combination ?? source.ReadG1ResourceBindings(explicitCombinationIndex).Combination;
        var operation = applied is null ? SkyCloudOperation.Pattern : SkyCloudOperation.Color;
        if (applied is null && pattern is null)
        {
            uint passCount = source.ReadG1Bindings().PassCount;
            if (passCount <= 5 || (uint)combination.Index % passCount != 5)
                throw new InvalidDataException("Explicit Pattern combination must belong to actual pass five.");
        }
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
        var vb = TranslatedShaderBindingReader.Read(applied?.VertexDefinitions ?? pattern?.VertexDefinitions ?? source.ReadResourceDefinitions(vs.Entry.ProgramIndex), adapter.Bytes);
        var pb = TranslatedShaderBindingReader.Read(applied?.PixelDefinitions ?? pattern?.PixelDefinitions ?? source.ReadResourceDefinitions(ps.Entry.ProgramIndex), pixel.Bytes);
        var patternLayout = operation == SkyCloudOperation.Pattern
            ? SkyCloudPatternMaterialLayout.Import(vb.SourceDefinitions, pb.SourceDefinitions) : null;
        if ((operation == SkyCloudOperation.Color && (vb.Bindings.Count != 1 || pb.Bindings.Count != 15)) ||
            vb.Bindings.Any(b => b.Device.DescriptorSet != 1) || pb.Bindings.Any(b => b.Device.DescriptorSet != 0) ||
            vb.Interface.StageVariables.Any(v => v.StorageClass == 1 && v.Location is not null))
            throw new InvalidDataException("Original cloud descriptors or VertexID contract differs.");
        foreach (var pair in new[] { (Bindings: vb, Size: patternLayout?.VertexByteLength ?? 160, Slot: patternLayout?.VertexSlot ?? 0),
            (Bindings: pb, Size: patternLayout?.PixelByteLength ?? 512, Slot: patternLayout?.PixelSlot ?? 0) })
        {
            var cb = pair.Bindings.Bindings.Single(b => b.DescriptorKind == "UniformBuffer");
            if (cb.Source.BindPoint != pair.Slot || cb.ConstantBuffer?.ByteSize != pair.Size ||
                cb.Device.RequiredBufferBytes > pair.Size || (operation == SkyCloudOperation.Color && cb.Device.RequiredBufferBytes != pair.Size))
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
        return new(source, combination, vertex, adapter, pixel, vb, pb, shader, operation, patternLayout);
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
