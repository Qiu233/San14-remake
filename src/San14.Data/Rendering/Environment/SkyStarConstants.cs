using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record SkyStarConstantPacket(byte[] VertexBytes, byte[] GeometryBytes, byte[] PixelBytes);

/// <summary>Complete explicit original Sky2Star native-pass0 CBs. The GS packet
/// remains separate although this backend executes it in the vertex stage.</summary>
public static class SkyStarConstants
{
    public static SkyStarConstantPacket Read(DxbcResourceDefinitions vertex,
        DxbcResourceDefinitions geometry, DxbcResourceDefinitions pixel,
        ReadOnlySpan<byte> vertexBytes, ReadOnlySpan<byte> geometryBytes, ReadOnlySpan<byte> pixelBytes)
    {
        Validate(vertex, vertexBytes, 352, [("g_mE2C", 0, 64, 2, 4, 4, 0),
            ("g_vPhaze", 64, 16, 1, 1, 4, 0), ("g_vTable", 80, 256, 1, 1, 4, 16), ("g_vStarInfo", 336, 16, 1, 1, 4, 0)]);
        Validate(geometry, geometryBytes, 16, [("g_vPointScale", 0, 16, 1, 1, 4, 0)]);
        Validate(pixel, pixelBytes, 16, [("g_vStarInfo", 0, 16, 1, 1, 4, 0)]);
        // The original VS dynamically indexes its 16 table entries. This
        // supported packet domain bounds that access; it does not change values.
        float phaseCount = BinaryPrimitives.ReadSingleLittleEndian(vertexBytes.Slice(68, 4));
        if (phaseCount < 0 || phaseCount > 16 || MathF.Truncate(phaseCount) != phaseCount)
            throw new InvalidDataException("Star phase table count is outside the bounded 0..16 integer domain.");
        return new(vertexBytes.ToArray(), geometryBytes.ToArray(), pixelBytes.ToArray());
    }

    private static void Validate(DxbcResourceDefinitions definitions, ReadOnlySpan<byte> bytes, int size,
        (string Name, int Offset, int Size, int Class, int Rows, int Columns, int Elements)[] fields)
    {
        if (bytes.Length != size || definitions.Buffers.Count != 1 || definitions.Buffers[0].Name.Text != "$Globals" ||
            definitions.Buffers[0].ByteSize != size || definitions.Buffers[0].Variables.Count != fields.Length)
            throw new InvalidDataException("Star packet differs from the complete original RDEF buffer.");
        for (int i = 0; i < fields.Length; i++)
        {
            var expected = fields[i]; var actual = definitions.Buffers[0].Variables[i];
            var type = definitions.Types.Single(t => t.Offset == actual.TypeOffset);
            if (actual.Name.Text != expected.Name || actual.StartOffset != expected.Offset || actual.ByteSize != expected.Size ||
                type.BaseType != 3 || type.Class != expected.Class || type.Rows != expected.Rows || type.Columns != expected.Columns ||
                type.Elements != expected.Elements || type.Members.Count != 0)
                throw new InvalidDataException("Star field layout differs: " + expected.Name);
            for (int at = expected.Offset; at < expected.Offset + expected.Size; at += 4)
                if (!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(at, 4))))
                    throw new InvalidDataException("Star field is outside the supported finite domain: " + expected.Name);
        }
    }
}
