using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Explicit native pass12 source pair. Retains the source program,
/// translation and RDEF; source combination remains caller-selected.</summary>
public sealed class OriginalSkyLightProgram : IAsyncDisposable
{
    public LoadedShaderContainer Source { get; }
    public G2ShaderCombination Combination { get; }
    public OriginalShaderSpirv VertexTranslation { get; }
    public OriginalAdaptedVertexShader VertexAdapter { get; }
    public OriginalShaderSpirv PixelTranslation { get; }
    public OriginalTranslatedShaderBindings VertexBindings { get; }
    public OriginalTranslatedShaderBindings PixelBindings { get; }
    public Rid ShaderRid { get; }
    private Task? _disposal;
    private OriginalSkyLightProgram(LoadedShaderContainer source, G2ShaderCombination combination,
        OriginalShaderSpirv vertex, OriginalAdaptedVertexShader adapter, OriginalShaderSpirv pixel,
        OriginalTranslatedShaderBindings vb, OriginalTranslatedShaderBindings pb, Rid shader)
        => (Source, Combination, VertexTranslation, VertexAdapter, PixelTranslation, VertexBindings, PixelBindings, ShaderRid)
            = (source, combination, vertex, adapter, pixel, vb, pb, shader);

    public static async Task<OriginalSkyLightProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(compiler);
        if (Convert.ToHexString(SHA256.HashData(source.Source.Bytes)) !=
            "A23C9FCE094C7FA40D8E173D96A48D33D043A4D31D8D8A146DB965D44A31A102")
            throw new InvalidDataException("Sky light requires the verified full 19DBE085 source.");
        var combination = source.ReadG1ResourceBindings(explicitCombinationIndex).Combination;
        if (combination.Stages.Any(s => s.Entry is not null && s.StageCode is not (0 or 1)))
            throw new InvalidDataException("Sky light requires one explicit vertex/pixel pair.");
        var vs = combination.Stages.Single(s => s.StageCode == 1 && s.Entry is not null);
        var ps = combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null);
        var vertexBytes = source.Container.GetProgramBytes(source.Source.Bytes, vs.Entry!.ProgramIndex);
        var pixelBytes = source.Container.GetProgramBytes(source.Source.Bytes, ps.Entry!.ProgramIndex);
        if (Convert.ToHexString(SHA256.HashData(vertexBytes)) != "357291D8B3BFFFFBD7996E94952ECE80BAEE70F6D85FC8AF86378851CCE1CA0B" ||
            Convert.ToHexString(SHA256.HashData(pixelBytes)) != "0B193F8F618AE3A524AD11C4E684813CB98409C993DC873825323D2A5ACD2F22")
            throw new InvalidDataException("Selected source is outside the verified native pass12 program pair.");
        var vertex = compiler.Compile(vertexBytes, 1); var pixel = compiler.Compile(pixelBytes);
        var adapter = OriginalVertexShaderAdapter.Adapt(vertex.Bytes, 0, 0, 0);
        var vb = OriginalTranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(vs.Entry.ProgramIndex), adapter.Bytes);
        var pb = OriginalTranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(ps.Entry.ProgramIndex), pixel.Bytes);
        if (vb.Bindings.Count != 1 || pb.Bindings.Count != 5 ||
            vb.Bindings.Any(b => b.Device.DescriptorSet != 1) || pb.Bindings.Any(b => b.Device.DescriptorSet != 0) ||
            vb.Interface.StageVariables.Any(v => v.StorageClass == 1 && v.Location is not null))
            throw new InvalidDataException("Sky light descriptor/VertexID contract differs.");
        foreach (var pair in new[] { (Bindings: vb, Size: 32), (Bindings: pb, Size: 192) })
        {
            var cb = pair.Bindings.Bindings.Single(b => b.DescriptorKind == "UniformBuffer");
            if (cb.Source.BindPoint != 0 || cb.Device.RequiredBufferBytes != pair.Size || cb.ConstantBuffer?.ByteSize != pair.Size)
                throw new InvalidDataException("Sky light original constant length differs.");
        }
        Rid shader = await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            using var stages = new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, adapter.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, pixel.Bytes);
            Rid rid = rd.ShaderCreateFromSpirV(stages, "OriginalSkyLightPass12");
            if (!rid.IsValid) throw new InvalidDataException("Device rejected original sky light program.");
            return rid;
        });
        return new(source, combination, vertex, adapter, pixel, vb, pb, shader);
    }

    public ValueTask DisposeAsync() => new(_disposal ??= OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { rd.FreeRid(ShaderRid); return true; }));
}
