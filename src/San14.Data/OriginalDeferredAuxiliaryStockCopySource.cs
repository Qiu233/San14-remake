using San14.Data.Formats;

namespace San14.Data;

/// <summary>Stock screen copy stages implemented by the project's HLSL.</summary>
public sealed record LoadedOriginalDeferredAuxiliaryStockCopySource(
    LoadedOriginalDeferredSamplerNativeSource Native,
    OriginalDeferredAuxiliaryDepthCopyProgram VertexProgram,
    OriginalDeferredAuxiliaryDepthCopyProgram PixelProgram);

public static class OriginalDeferredAuxiliaryStockCopySource
{
    public const ulong EqualRectangleSamplerKey=0x0000000000300015;
    public const ulong ResizedRectangleSamplerKey=0x0000000000301415;

    public static LoadedOriginalDeferredAuxiliaryStockCopySource ReadCurrent(string sourceDirectory,string locale)
    {
        var native=OriginalDeferredSamplerSources.Load(sourceDirectory,locale);
        return new(native,OriginalDeferredAuxiliaryDepthCopySource.ToProgram(ProjectStockShaders.Vertex),
            OriginalDeferredAuxiliaryDepthCopySource.ToProgram(ProjectStockShaders.Default3));
    }

    public static DxbcResourceDefinitions ReadPixelDefinitions(ReadOnlySpan<byte> program)
        => DxbcResourceDefinitionReader.Read(program);
}
