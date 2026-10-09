using San14.Data;

/// <summary>The actual complete Present Sun/Moon/controller branch writes
/// every PS3/4 and VS named field before any draw. Prepare those values once;
/// buffer padding is owned device backing rather than a source cache.</summary>
public static class OriginalSceneViewNativeMapSkyNamedWriteCoverage
{
    public sealed record Prepared(OriginalSkyScenePreparedFrame Frame,
        IReadOnlyList<OriginalSky2ConstantPacket> VertexPackets,
        IReadOnlyList<OriginalSky2ConstantPacket> PixelPackets,
        byte[] OwnedVertexPaddingTemplate, byte[] OwnedPixelPaddingTemplate);

    public static Prepared PrepareCompleteCurrent(OriginalSkySceneSourceFrame actual,
        LoadedShaderContainer actualStockSkyShader, int actualPixelProgram = 3)
    {
        ArgumentNullException.ThrowIfNull(actual); ArgumentNullException.ThrowIfNull(actualStockSkyShader);
        if (actualStockSkyShader.Source.OverrideSource is not null ||
            actualStockSkyShader.Source.FileId != 0x340AC335 ||
            actualStockSkyShader.Source.DecodedSha256 != "CE4CEC6271BDE59E40D5B97746A7A1877188E133550A0C608D41C353A4999424" ||
            actualPixelProgram is not (3 or 4) || actual.SixCameras.Count != 6 || actual.Controller is null ||
            !actual.Lights.SunPresent || !actual.Lights.MoonPresent || actual.Lights.SourceZeroMoon is null)
            throw new NotSupportedException("Full named write coverage requires actual stock Sky PS3/4, all six current cameras, present source Sun/Moon and its complete current controller.");
        var prepared = OriginalSkySceneFramePreparation.PrepareFreshFullyWritten(actual);
        // Owned backing zero only covers named-buffer padding; all meaningful
        // words are supplied by the actual prepared source writes above.
        var vertexDefinition = actualStockSkyShader.ReadResourceDefinitions(0);
        var pixelDefinition = actualStockSkyShader.ReadResourceDefinitions(actualPixelProgram);
        byte[] vertex = new byte[checked((int)vertexDefinition.Buffers.Single().ByteSize)];
        byte[] pixel = new byte[checked((int)pixelDefinition.Buffers.Single().ByteSize)];
        if (vertex.Length != 160 || pixel.Length != 256)
            throw new InvalidDataException("Actual stock Sky VS0/PS3-or4 buffer extent changed.");
        var vs = prepared.Faces.Select(face => OriginalSky2Constants.Vertex(vertexDefinition, vertex, face.Draw.Vertex)).ToArray();
        var ps = prepared.Faces.Select(face => OriginalSky2Constants.Pixel(pixelDefinition, pixel, face.Draw.Pixel)).ToArray();
        return new(prepared, Array.AsReadOnly(vs), Array.AsReadOnly(ps), vertex, pixel);
    }
}
