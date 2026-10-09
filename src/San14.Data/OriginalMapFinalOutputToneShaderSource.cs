using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalMapFinalOutputToneShaderProgram(string Sha256,
    ReadOnlyMemory<byte> Bytes,DxbcContainer Container,DxbcResourceDefinitions Definitions,
    DxbcProgramSignatures Signatures);

/// <summary>The project's stock vertex and exact map tone-mapping implementation.</summary>
public sealed record LoadedOriginalMapFinalOutputToneShaderSource(string Locale,
    string SourceResourceUri,OriginalMapFinalOutputToneShaderProgram Vertex,
    OriginalMapFinalOutputToneShaderProgram Pixel,string SourceEvidence)
{
    public bool ActiveOriginalSelectionObserved=>false;
    public int PixelConstantBufferBytes=>32;
    public bool DeclaresTexture3DLut=>true;
    public OriginalMapFinalOutputProgramSource ToProgramSource()=>new(SourceResourceUri,0,0,
        Vertex.Bytes,Pixel.Bytes,Vertex.Sha256,Pixel.Sha256)
        { Kind=OriginalMapFinalOutputProgramKind.EmbeddedTone };
}

public static class OriginalMapFinalOutputToneShaderSource
{
    public static LoadedOriginalMapFinalOutputToneShaderSource ReadCurrent(string locale)
    {
        ProjectStockShaders.RequireLocale(locale);
        static OriginalMapFinalOutputToneShaderProgram Wrap(ProjectStockShaders.Program program)
            =>new(program.Sha256,program.Dxbc,program.Container,program.Definitions,program.Signatures);
        var pixel=ProjectStockShaders.Tone;
        return new(locale,pixel.ResourceUri,Wrap(ProjectStockShaders.Vertex),Wrap(pixel),
            pixel.ResourceUri+";map-tone-curves;reference-mode/exposure/gain;gamma-after-curve");
    }
}
