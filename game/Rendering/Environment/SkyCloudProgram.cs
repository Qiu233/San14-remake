using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

public enum SkyCloudOperation { Pattern, Color }

/// <summary>Explicit original VCLOUD native pass5/pass0 default pairs and
/// actual applied source null-Scene/Cirrus pass0 low-cost0/1/2 pairs. Current
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

    private SkyCloudProgram(LoadedShaderContainer source, G2ShaderCombination combination,
        ShaderSpirv vertex, AdaptedVertexShader adapter, ShaderSpirv pixel,
        TranslatedShaderBindings vb, TranslatedShaderBindings pb, Rid shader, SkyCloudOperation operation)
        => (Source, Combination, VertexTranslation, VertexAdapter, PixelTranslation, VertexBindings, PixelBindings, ShaderRid, Operation)
            = (source, combination, vertex, adapter, pixel, vb, pb, shader, operation);

    public static async Task<SkyCloudProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler, SkyCloudOperation operation = SkyCloudOperation.Pattern)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(compiler);
        if (Convert.ToHexString(SHA256.HashData(source.Source.Bytes)) !=
            "C1B780D8074F46C2C90BCB3800D941024ECAEDC9C784FD9F7C12B54B5D34A2F9")
            throw new InvalidDataException("Cloud producer requires the verified 42ED52ED source.");
        var combination = source.ReadG1ResourceBindings(explicitCombinationIndex).Combination;
        if (operation is not (SkyCloudOperation.Pattern or SkyCloudOperation.Color) ||
            combination.Index % 6 != (operation == SkyCloudOperation.Pattern ? 5 : 0))
            throw new InvalidDataException("Cloud operation must match the explicit source pass.");
        if (combination.Stages.Any(s => s.Entry is not null && s.StageCode is not (0 or 1)))
            throw new InvalidDataException("Cloud producer requires one vertex/pixel source pair.");
        var vs = combination.Stages.Single(s => s.StageCode == 1 && s.Entry is not null);
        var ps = combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null);
        var vertexBytes = source.Container.GetProgramBytes(source.Source.Bytes, vs.Entry!.ProgramIndex);
        var pixelBytes = source.Container.GetProgramBytes(source.Source.Bytes, ps.Entry!.ProgramIndex);
        string expectedVertex = operation == SkyCloudOperation.Pattern ?
            "F6F570FA1D9F2E5E090D25DC2D7C119015966136DD7A3E2491E97E2CB546F2DA" :
            "7C7B170B5B42F4FE227CBDD60E411A7E000432684E48128B4A3457FAB779EAF1";
        bool appliedCirrus = operation == SkyCloudOperation.Color && explicitCombinationIndex != 0;
        var applied = appliedCirrus ? SkyCloudColorProgramSelection.ReadExplicitAppliedCombination(source,explicitCombinationIndex) : null;
        string expectedPixel = operation == SkyCloudOperation.Pattern ? PixelSourceSha :
            applied?.PixelSourceSha256 ?? ColorPixelSourceSha;
        if(applied is not null&&(applied.VertexProgram!=vs.Entry.ProgramIndex||applied.PixelProgram!=ps.Entry.ProgramIndex))
            throw new InvalidDataException("Cloud renderer pair differs from its actual source-selected low-cost contract.");
        if (Convert.ToHexString(SHA256.HashData(vertexBytes)) != expectedVertex ||
            Convert.ToHexString(SHA256.HashData(pixelBytes)) != expectedPixel)
            throw new InvalidDataException("Selected cloud program is outside the verified native default/applied low-cost Cirrus pair.");
        var vertex = compiler.Compile(vertexBytes, 1); var pixel = compiler.Compile(pixelBytes);
        var adapter = VertexShaderAdapter.Adapt(vertex.Bytes, 0, 0, 0);
        var vb = TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(vs.Entry.ProgramIndex), adapter.Bytes);
        var pb = TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(ps.Entry.ProgramIndex), pixel.Bytes);
        if (vb.Bindings.Count != 1 || pb.Bindings.Count != (operation == SkyCloudOperation.Pattern ? 7 : appliedCirrus ? 15 : 11) ||
            vb.Bindings.Any(b => b.Device.DescriptorSet != 1) || pb.Bindings.Any(b => b.Device.DescriptorSet != 0) ||
            vb.Interface.StageVariables.Any(v => v.StorageClass == 1 && v.Location is not null))
            throw new InvalidDataException("Original cloud descriptors or VertexID contract differs.");
        foreach (var pair in new[] { (Bindings: vb, Size: 160),
            (Bindings: pb, Size: operation == SkyCloudOperation.Pattern ? 192 : appliedCirrus ? 512 : 448) })
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

    public const string PixelSourceSha = "90F321AC05B492C8C2B2FAA4AB40A410ADF88BB77702BFA55DA7C659C8F56119";
    public const string ColorPixelSourceSha = "1A50E288FA3B970ED3421CE7A0236F451F3617BFB3F798225998DEAF3D281570";
    public const string AppliedCirrusColorPixelSourceSha = "5C080FC73AA9C556C60D9BFCAAA87889478C115AFBCC406C235774BF62D91884";
    public const string AppliedLowCostCirrusColorPixelSourceSha = "412C03E12CCC037BF8604BE0DB51D55DB138C4225D447C628BB7F84B6EF64A18";
    public ValueTask DisposeAsync() => new(_disposal ??= TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { rd.FreeRid(ShaderRid); return true; }));
}
