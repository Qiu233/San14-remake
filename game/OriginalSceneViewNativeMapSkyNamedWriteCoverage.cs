using System.Numerics;
using System.Security.Cryptography;
using San14.Data;

/// <summary>The actual complete Present Sun/Moon/controller branch writes
/// every PS3/4 and VS named field before any draw. The old six caches therefore
/// supply no semantic input in this exact scope. NaN poison is owned backing,
/// not a claim about an original constructor or retained native cache.</summary>
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
            Convert.ToHexString(SHA256.HashData(actualStockSkyShader.Source.Bytes)) != actualStockSkyShader.Source.DecodedSha256 ||
            actualPixelProgram is not (3 or 4) || actual.SixCameras.Count != 6 || actual.Controller is null ||
            !actual.Lights.SunPresent || !actual.Lights.MoonPresent || actual.Lights.SourceZeroMoon is null)
            throw new NotSupportedException("Full named write coverage requires actual stock Sky PS3/4, all six current cameras, present source Sun/Moon and its complete current controller.");
        // Both seeds differ in every float bit and their old InFront state.
        // Prepare's source finite checks reject any surviving unknown semantic.
        var a = Prepare(actual, actualStockSkyShader, actualPixelProgram, 0x7FC10101, false);
        var b = Prepare(actual, actualStockSkyShader, actualPixelProgram, 0x7FC20202, true);
        if (!a.VertexPackets.Zip(b.VertexPackets).All(pair => pair.First.Bytes.AsSpan().SequenceEqual(pair.Second.Bytes)) ||
            !a.PixelPackets.Zip(b.PixelPackets).All(pair => pair.First.Bytes.AsSpan().SequenceEqual(pair.Second.Bytes)) ||
            !a.Frame.Faces.Select(f => f.SunInFront).SequenceEqual(b.Frame.Faces.Select(f => f.SunInFront)))
            throw new InvalidDataException("An unassigned prior Sky cache or InFront bit remains consumed by this current branch.");
        return a;
    }

    private static Prepared Prepare(OriginalSkySceneSourceFrame actual, LoadedShaderContainer shader,
        int pixelProgram, uint poisonBits, bool priorInFront)
    {
        float poison = BitConverter.UInt32BitsToSingle(poisonBits);
        var p = new Vector4(poison);
        var m = new Matrix4x4(poison,poison,poison,poison,poison,poison,poison,poison,
            poison,poison,poison,poison,poison,poison,poison,poison);
        var previous = Enumerable.Range(0, 6).Select(_ => new OriginalSkySceneSky2FaceCache(
            new(new(p,m,poison,m), new(p,[p,p,p,p],p,p,p,p,p,p,p,p,p,p,p)), priorInFront)).ToArray();
        var prepared = OriginalSkySceneFramePreparation.Prepare(previous, actual);
        // Owned backing zero only covers named-buffer padding; all meaningful
        // words are supplied by the actual prepared source writes above.
        var vertexDefinition = shader.ReadResourceDefinitions(0);
        var pixelDefinition = shader.ReadResourceDefinitions(pixelProgram);
        byte[] vertex = new byte[checked((int)vertexDefinition.Buffers.Single().ByteSize)];
        byte[] pixel = new byte[checked((int)pixelDefinition.Buffers.Single().ByteSize)];
        if (vertex.Length != 160 || pixel.Length != 256)
            throw new InvalidDataException("Actual stock Sky VS0/PS3-or4 buffer extent changed.");
        var vs = prepared.Faces.Select(face => OriginalSky2Constants.Vertex(vertexDefinition, vertex, face.Draw.Vertex)).ToArray();
        var ps = prepared.Faces.Select(face => OriginalSky2Constants.Pixel(pixelDefinition, pixel, face.Draw.Pixel)).ToArray();
        return new(prepared, Array.AsReadOnly(vs), Array.AsReadOnly(ps), vertex, pixel);
    }
}
