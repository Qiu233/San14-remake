using San14.Data.Formats;

namespace San14.Data;

/// <summary>Stock screen copy stages implemented by the project's HLSL.</summary>
public sealed record LoadedDeferredAuxiliaryStockCopySource(
    LoadedDeferredSamplerNativeSource Native,
    DeferredAuxiliaryDepthCopyProgram VertexProgram,
    DeferredAuxiliaryDepthCopyProgram PixelProgram);

public static class DeferredAuxiliaryStockCopySource
{
    public const ulong EqualRectangleSamplerKey=0x0000000000300015;
    public const ulong ResizedRectangleSamplerKey=0x0000000000301415;

    public static LoadedDeferredAuxiliaryStockCopySource ReadCurrent(string sourceDirectory,string locale)
    {
        var native=RenderSamplerSourceAdapters.Load(sourceDirectory,locale);
        return new(native,DeferredAuxiliaryDepthCopySource.ToProgram(ProjectStockShaders.Vertex),
            DeferredAuxiliaryDepthCopySource.ToProgram(ProjectStockShaders.Default3));
    }

    public static DxbcResourceDefinitions ReadPixelDefinitions(ReadOnlySpan<byte> program)
        => DxbcResourceDefinitionReader.Read(program);
}
