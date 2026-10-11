using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The declared PB2 wave-water shader bank. Near and far water share
/// this single-pass pair; their material callbacks remain separate. This is a
/// resource import, not a general current Ocean shader selector.</summary>
public sealed class OceanShaderResources
{
    private readonly byte[] vertex, pixel;

    private OceanShaderResources(LoadedRdbAsset source, int vertexIndex, int pixelIndex,
        byte[] vertex, byte[] pixel)
    {
        SourceIdentity = $"{source.ResourceUri}@{source.DecodedSha256}";
        VertexProgramIndex = vertexIndex; PixelProgramIndex = pixelIndex;
        this.vertex = vertex; this.pixel = pixel;
    }

    public string SourceIdentity { get; }
    public int VertexProgramIndex { get; }
    public int PixelProgramIndex { get; }

    /// <summary>Import the named Renderer's explicit PB2/WV declaration. The
    /// supported Native water path uses selector1, PB2 context, WV accessory
    /// and no external override; other selection routes need their own owner.</summary>
    public static OceanShaderResources LoadPb2Wave(GameResourceRepository repository,
        NamedViewRendererResources renderer)
    {
        if (renderer.OceanWaveShaderFileId == 0)
            throw new NotSupportedException("The named Renderer declares no PB2 wave-water shader.");
        var shader = repository.LoadShaderContainer("system", renderer.OceanWaveShaderFileId);
        byte[] bytes = shader.Source.Bytes.ToArray();
        var container = G2ShaderContainerReader.Read(bytes);
        // This physical pair has no binding variants (and a zero variant width).
        // Import its stage combinations directly; no feature/variant selector
        // is needed for the supported single combination.
        var combinations = G2ShaderCombinationReader.Read(bytes);
        uint passes = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x3C, 4));
        bool hasSelection = Enumerable.Range(0, 7).Any(dimension =>
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x48 + dimension * 4, 4)) > 1);
        if (passes != 1 || hasSelection || combinations.Rows.Count != 1)
            throw new NotSupportedException("Ocean PB2 wave preparation requires a single-pass program pair.");
        var combination = combinations.Rows[0];
        if (combination.Stages.Count(stage => stage.Entry is not null) != 2)
            throw new NotSupportedException("Ocean PB2 wave preparation supports vertex and pixel stages only.");
        int vertexIndex = combination.Stages.Single(stage => stage.StageCode == 1).Entry?.ProgramIndex
            ?? throw new InvalidDataException("Ocean wave shader has no vertex stage.");
        int pixelIndex = combination.Stages.Single(stage => stage.StageCode == 0).Entry?.ProgramIndex
            ?? throw new InvalidDataException("Ocean wave shader has no pixel stage.");
        byte[] vertex = container.GetProgramBytes(bytes, vertexIndex).ToArray();
        byte[] pixel = container.GetProgramBytes(bytes, pixelIndex).ToArray();
        OceanShaderAbi.RequireSupported(vertex, pixel);
        return new(shader.Source, vertexIndex, pixelIndex, vertex, pixel);
    }

    public void WriteNativeInputs(string waterDirectory)
    {
        string directory = Path.Combine(waterDirectory, "shaders");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "vs.dxbc"), vertex);
        File.WriteAllBytes(Path.Combine(directory, "ps.dxbc"), pixel);
    }

}
