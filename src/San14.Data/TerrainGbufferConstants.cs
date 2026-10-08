using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Explicit draw values, in original shader coordinates and row order.
/// These are caller inputs; their presence does not prove an original active frame.</summary>
public sealed record TerrainGbufferDrawInputs(float[] WorldToProjectionRows, float[] WorldToViewRows,
    Vector4 Eye, Vector4 Specular, int MaterialId, Vector2 HeightInfo, Vector2 CliffInfo,
    Vector2 NearFar, int ToolEnable);

public sealed record TerrainConstantWrite(string Name, int Offset, int ElementCount, int Stride, int ElementBytes);
public sealed record TerrainGbufferConstantPacket(byte[] Bytes, IReadOnlyList<TerrainConstantWrite> Writes,
    int PalettePrefixCount, bool UnwrittenPaletteTailIsOriginalData);

/// <summary>Packs actual RDEF variables. Scalar arrays use HLSL's 16-byte stride.
/// Allocation padding and unused palette tails are zero storage, not recovered data.</summary>
public static class TerrainGbufferConstants
{
    public static TerrainGbufferConstantPacket Vertex(DxbcResourceDefinitions definitions, TerrainGbufferDrawInputs input)
        => Pack(definitions, input, null, true);

    public static TerrainGbufferConstantPacket Pixel(DxbcResourceDefinitions definitions, TerrainGbufferDrawInputs input,
        TerrainRendererPaletteProjection palette) => Pack(definitions, input, palette, false);

    private static TerrainGbufferConstantPacket Pack(DxbcResourceDefinitions definitions,
        TerrainGbufferDrawInputs input, TerrainRendererPaletteProjection? palette, bool vertex)
    {
        ArgumentNullException.ThrowIfNull(definitions); ArgumentNullException.ThrowIfNull(input);
        if (input.WorldToProjectionRows.Length != 16 || input.WorldToViewRows.Length != 12 ||
            input.WorldToProjectionRows.Concat(input.WorldToViewRows).Any(v => !float.IsFinite(v)) ||
            !Finite(input.Eye) || !Finite(input.Specular) || !Finite(input.HeightInfo) ||
            !Finite(input.CliffInfo) || !Finite(input.NearFar) || input.HeightInfo.X <= 0 || input.HeightInfo.Y <= 0)
            throw new InvalidDataException("Terrain draw requires finite explicit matrices and shader parameters.");
        if (!vertex && (palette is null || !palette.Resolved || palette.Entries.Count is < 1 or > 255 ||
            palette.Entries.Where((e, i) => e.ParameterIndex != i).Any()))
            throw new InvalidDataException("Terrain constants require a resolved, ordered palette prefix.");
        DxbcRdefBuffer[] buffers = definitions.Buffers.Where(b => b.Name.Text == "$Globals").ToArray();
        if (buffers.Length != 1 || definitions.Buffers.Count != 1)
            throw new InvalidDataException("Terrain adapter expects exactly the original $Globals buffer.");
        DxbcRdefBuffer buffer = buffers[0];
        if (buffer.ByteSize != (vertex ? 80 : buffer.Variables.Any(v => v.Name.Text == "vEye") ? 16432 : 4176))
            throw new InvalidDataException("Terrain constant buffer is outside the supported ordinary variants.");
        byte[] bytes = new byte[buffer.ByteSize]; var writes = new List<TerrainConstantWrite>();
        foreach (DxbcRdefVariable variable in buffer.Variables)
        {
            DxbcRdefType type = definitions.Types.Single(t => t.Offset == variable.TypeOffset);
            uint[] words; int count = 1, stride; ushort expectedClass = 1, expectedBase = 3, rows = 1, columns;
            switch (variable.Name.Text)
            {
                case "mW2P" when vertex:
                    words = Bits(input.WorldToProjectionRows); expectedClass = 2; rows = columns = 4; break;
                case "mWV" when !vertex:
                    words = Bits(input.WorldToViewRows); count = 3; columns = 4; break;
                case "vEye" when !vertex: words = Bits(input.Eye); columns = 4; break;
                case "cSpec" when !vertex: words = Bits(input.Specular); columns = 4; break;
                case "nMtrID" when !vertex: words = [unchecked((uint)input.MaterialId)]; expectedClass = 0; expectedBase = 2; columns = 1; break;
                case "nToolEnble" when !vertex: words = [unchecked((uint)input.ToolEnable)]; expectedClass = 0; expectedBase = 2; columns = 1; break;
                case "vHgtMapInf": words = Bits(input.HeightInfo); columns = 2; break;
                case "vCliffInfo" when !vertex: words = Bits(input.CliffInfo); columns = 2; break;
                case "vNerFarBld" when !vertex: words = Bits(input.NearFar); columns = 2; break;
                case "vDtlTexIdx" when !vertex:
                    words = palette!.Entries.Select(e => e.DetailTextureNameIndex).ToArray();
                    expectedClass = 0; expectedBase = 2; columns = 1; count = words.Length; break;
                case "vDtlBldSrp" when !vertex:
                    words = palette!.Entries.Select(e => e.BlendSharpnessBits).ToArray(); expectedClass = 0; columns = 1; count = words.Length; break;
                case "vDtlScaleU" when !vertex:
                    words = palette!.Entries.Select(e => e.ScaleUBits).ToArray(); expectedClass = 0; columns = 1; count = words.Length; break;
                case "vDtlScaleV" when !vertex:
                    words = palette!.Entries.Select(e => e.ScaleVBits).ToArray(); expectedClass = 0; columns = 1; count = words.Length; break;
                default: throw new InvalidDataException($"Unresolved terrain constant {variable.Name.Text}.");
            }
            bool scalarPalette = variable.Name.Text.StartsWith("vDtl", StringComparison.Ordinal);
            int elements = scalarPalette ? 255 : variable.Name.Text == "mWV" ? 3 : 0;
            int elementBytes = rows * columns * 4;
            stride = elements == 0 ? elementBytes : 16;
            int expectedSize = elements == 0 ? elementBytes : (elements - 1) * stride + elementBytes;
            if (type.Class != expectedClass || type.BaseType != expectedBase || type.Rows != rows ||
                type.Columns != columns || type.Elements != elements || type.Members.Count != 0 ||
                variable.ByteSize != expectedSize || variable.StartOffset < 0 ||
                (long)variable.StartOffset + variable.ByteSize > bytes.Length)
                throw new InvalidDataException($"Original terrain constant {variable.Name.Text} has an unsupported RDEF layout.");
            for (int i = 0; i < count; i++)
                for (int word = 0; word < elementBytes / 4; word++)
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(variable.StartOffset + i * stride + word * 4, 4),
                        words[i * (elementBytes / 4) + word]);
            writes.Add(new(variable.Name.Text, variable.StartOffset, count, stride, elementBytes));
        }
        string[] required = vertex ? ["mW2P", "vHgtMapInf"] :
            buffer.ByteSize == 4176 ? ["cSpec", "mWV", "nMtrID", "vHgtMapInf", "vCliffInfo", "vDtlScaleU", "nToolEnble"] :
            ["vEye", "cSpec", "mWV", "nMtrID", "vHgtMapInf", "vCliffInfo", "vDtlTexIdx", "vDtlBldSrp", "vDtlScaleU", "vDtlScaleV", "vNerFarBld", "nToolEnble"];
        if (!writes.Select(w => w.Name).Order().SequenceEqual(required.Order()))
            throw new InvalidDataException("Terrain buffer variable inventory differs from its original ordinary contract.");
        return new(bytes, writes.AsReadOnly(), palette?.Entries.Count ?? 0, false);
    }

    private static uint[] Bits(IEnumerable<float> values) => values.Select(BitConverter.SingleToUInt32Bits).ToArray();
    private static uint[] Bits(Vector4 v) => Bits(new[] { v.X, v.Y, v.Z, v.W });
    private static uint[] Bits(Vector2 v) => Bits(new[] { v.X, v.Y });
    private static bool Finite(Vector4 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) && float.IsFinite(v.W);
    private static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
}
