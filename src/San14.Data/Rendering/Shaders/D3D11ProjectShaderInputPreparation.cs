namespace San14.Data;

/// <summary>Writes the project's built-in stages once when mounting the native map inputs.
/// Resource shader programs and retained render state have separate owners.</summary>
public static class D3D11ProjectShaderInputPreparation
{
    private static readonly Lazy<byte[]> GgxVertex = new(() =>
        ProjectHlslCompiler.CompileEmbedded("ProjectStockShader.GgxVertex.hlsl", "main", "vs_5_0"));

    public static void WriteNativeInputs(string inputDirectory, string locale)
    {
        ProjectStockShaders.RequireLocale(locale);
        string inputs = Path.GetDirectoryName(Path.GetFullPath(inputDirectory))!;
        byte[] vertex = ProjectStockShaders.Vertex.Dxbc;
        byte[] copy = ProjectStockShaders.Default3.Dxbc;

        void Write(string relativePath, byte[] program)
        {
            string path = Path.Combine(inputs, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, program);
        }

        Write("lighting-input/ggx-pixel.dxbc", GgxShaderCompiler.Load(locale).Dxbc);
        Write("lighting-input/vertex.dxbc", GgxVertex.Value);
        Write("map-exposure-input/vs.dxbc", vertex);
        Write("map-exposure-input/ps.dxbc", copy);
        Write("territory-visual-input/stock-copy-vs.dxbc", vertex);
        Write("territory-visual-input/stock-copy-ps.dxbc", ProjectStockShaders.Default5.Dxbc);
        Write("refraction-source-input/copy97/vs.dxbc", vertex);
        Write("refraction-source-input/copy97/ps.dxbc", ProjectStockShaders.Depth.Dxbc);
        Write("refraction-source-input/copy-scene97/vs.dxbc", vertex);
        Write("refraction-source-input/copy-scene97/ps.dxbc", copy);
        Write("army-visual-input/glyph/common/glyph-vs.dxbc", vertex);
        Write("army-visual-input/glyph/common/glyph-ps.dxbc", copy);
        Write("army-visual-input/army-material-bank/common/glyph-vs.dxbc", vertex);
        Write("army-visual-input/army-material-bank/common/glyph-ps.dxbc", copy);
        Write("city-accessory-next-input/material-source/common/glyph-vs.dxbc", vertex);
        Write("city-accessory-next-input/material-source/common/glyph-ps.dxbc", copy);
    }
}
