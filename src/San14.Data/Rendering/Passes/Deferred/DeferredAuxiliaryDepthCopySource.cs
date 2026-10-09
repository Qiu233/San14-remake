using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record DeferredAuxiliaryDepthCopyProgram(byte[] Dxbc,
    string Sha256, DxbcContainer Container, byte[] ResourceDefinitionBytes,
    DxbcResourceDefinitions Definitions, DxbcProgramSignatures Signatures);

/// <summary>The project's screen vertex and integer depth-copy programs.</summary>
public sealed record LoadedDeferredAuxiliaryDepthCopySource(string Locale,
    string ResourceUri, DeferredAuxiliaryDepthCopyProgram VertexProgram,
    DeferredAuxiliaryDepthCopyProgram PixelProgram)
{
    public uint SourceDepthObjectId => 0xE13DDB4F;
    public uint DestinationDepthObjectId => 0xB6169FDB;
    public int SourceDepthMip => 0;
    public int NativeTextureStage => 4;
    public int TextureSlot => 0;
    public int StockVertexConstantByteCount => 32;
    public bool PixelUsesSampler => false;
    public bool PixelWritesDepth => true;
}

public static class DeferredAuxiliaryDepthCopySource
{
    /// <summary>The current screen scales, color scale and viewport are caller state.</summary>
    public static byte[] BuildStockVertexConstants(int currentViewportWidth, int currentViewportHeight,
        float currentScreenScale29E0, float currentScreenScale29E4, Vector4 currentColorScale29F0)
    {
        if (currentViewportWidth <= 0 || currentViewportHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(currentViewportWidth), "Depth-copy viewport dimensions must be positive.");
        float[] values = [(currentScreenScale29E0 + currentScreenScale29E0) / (float)currentViewportWidth,
            (currentScreenScale29E4 * -2.0f) / (float)currentViewportHeight, -1.0f, 1.0f,
            currentColorScale29F0.X,currentColorScale29F0.Y,currentColorScale29F0.Z,currentColorScale29F0.W];
        byte[] bytes = new byte[32];
        for (int i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4), BitConverter.SingleToInt32Bits(values[i]));
        return bytes;
    }

    public static LoadedDeferredAuxiliaryDepthCopySource ReadCurrent(string locale)
    {
        ProjectStockShaders.RequireLocale(locale);
        return new(locale, ProjectStockShaders.Depth.ResourceUri,
            ToProgram(ProjectStockShaders.Vertex), ToProgram(ProjectStockShaders.Depth));
    }

    internal static DeferredAuxiliaryDepthCopyProgram ToProgram(ProjectStockShaders.Program program)
        => new(program.Dxbc, program.Sha256, program.Container, program.ResourceDefinitionBytes,
            program.Definitions, program.Signatures);

    public static DxbcResourceDefinitions ReadVertexDefinitions(ReadOnlySpan<byte> program)
        => DxbcResourceDefinitionReader.Read(program);
}
