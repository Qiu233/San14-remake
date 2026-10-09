using San14.Data.Formats;

namespace San14.Data;

public sealed record MapFinalOutputToneShaderProgram(string Sha256,
    ReadOnlyMemory<byte> Bytes,DxbcContainer Container,DxbcResourceDefinitions Definitions,
    DxbcProgramSignatures Signatures);

/// <summary>The project's stock vertex and exact map tone-mapping implementation.</summary>
public sealed record LoadedMapFinalOutputToneShaderSource(string Locale,
    string SourceResourceUri,MapFinalOutputToneShaderProgram Vertex,
    MapFinalOutputToneShaderProgram Pixel,string SourceEvidence)
{
    public bool ActiveSelectionObserved=>false;
    public int PixelConstantBufferBytes=>32;
    public bool DeclaresTexture3DLut=>true;
    public MapFinalOutputProgramSource ToProgramSource()=>new(SourceResourceUri,0,0,
        Vertex.Bytes,Pixel.Bytes,Vertex.Sha256,Pixel.Sha256)
        { Kind=MapFinalOutputProgramKind.EmbeddedTone };
}

public static class MapFinalOutputToneShaderSource
{
    public static LoadedMapFinalOutputToneShaderSource ReadCurrent(string locale)
    {
        ProjectStockShaders.RequireLocale(locale);
        static MapFinalOutputToneShaderProgram Wrap(ProjectStockShaders.Program program)
            =>new(program.Sha256,program.Dxbc,program.Container,program.Definitions,program.Signatures);
        var pixel=ProjectStockShaders.Tone;
        return new(locale,pixel.ResourceUri,Wrap(ProjectStockShaders.Vertex),Wrap(pixel),
            pixel.ResourceUri+";map-tone-curves;reference-mode/exposure/gain;gamma-after-curve");
    }
}
