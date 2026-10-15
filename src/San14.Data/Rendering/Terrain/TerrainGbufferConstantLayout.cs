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
    int PalettePrefixCount, bool UnwrittenPaletteTailIsSourceData);

/// <summary>Imported ordinary terrain constant layout. Reflection is checked
/// once; current draws provide only values and the accepted palette prefix.</summary>
public sealed class TerrainGbufferConstantLayout
{
    private sealed record Field(string Name, int Offset, int Capacity, int Stride, int ElementBytes);
    private readonly Field[] fields;
    private readonly bool vertex;
    private TerrainGbufferConstantLayout(int byteLength, bool vertex, Field[] fields)
        => (ByteLength, this.vertex, this.fields) = (byteLength, vertex, fields);
    public int ByteLength { get; }

    public static TerrainGbufferConstantLayout Import(DxbcResourceDefinitions definitions, bool vertex)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var buffers = definitions.Buffers.Where(b => b.Name.Text == "$Globals").ToArray();
        var bindings = definitions.Bindings.Where(b => b.InputType == 0).ToArray();
        if (buffers.Length != 1 || definitions.Buffers.Count != 1 || bindings.Length != 1 ||
            bindings[0].Name.Text != "$Globals" || bindings[0].BindPoint != 0 || bindings[0].BindCount != 1)
            throw new InvalidDataException("Terrain requires one complete $Globals constant buffer at slot zero.");
        var buffer = buffers[0];
        if (buffer.BufferType != 0 || buffer.ByteSize <= 0 || buffer.ByteSize > 65536 || buffer.ByteSize % 16 != 0)
            throw new InvalidDataException("Terrain constant buffer must fit an aligned D3D11 constant buffer.");
        var fields = new List<Field>();
        foreach (var variable in buffer.Variables)
        {
            var type = definitions.Types.Single(t => t.Offset == variable.TypeOffset);
            ushort expectedClass = 1, expectedBase = 3, rows = 1, columns;
            int elements = 0;
            switch (variable.Name.Text)
            {
                case "mW2P" when vertex: expectedClass = 2; rows = columns = 4; break;
                case "mWV" when !vertex: columns = 4; elements = 3; break;
                case "vEye" or "cSpec" when !vertex: columns = 4; break;
                case "nMtrID" or "nToolEnble" when !vertex: expectedClass = 0; expectedBase = 2; columns = 1; break;
                case "vHgtMapInf": columns = 2; break;
                case "vCliffInfo" or "vNerFarBld" when !vertex: columns = 2; break;
                case "vDtlTexIdx" when !vertex: expectedClass = 0; expectedBase = 2; columns = 1; elements = type.Elements; break;
                case "vDtlBldSrp" or "vDtlScaleU" or "vDtlScaleV" when !vertex:
                    expectedClass = 0; columns = 1; elements = type.Elements; break;
                default: throw new InvalidDataException($"Unresolved terrain constant {variable.Name.Text}.");
            }
            int elementBytes = rows * columns * 4;
            int stride = elements == 0 ? elementBytes : 16;
            int expectedSize = elements == 0 ? elementBytes : (elements - 1) * stride + elementBytes;
            if (variable.Name.Text.StartsWith("vDtl", StringComparison.Ordinal) && elements is < 1 or > 255 ||
                type.Class != expectedClass || type.BaseType != expectedBase || type.Rows != rows ||
                type.Columns != columns || type.Elements != elements || type.Members.Count != 0 ||
                variable.ByteSize != expectedSize || variable.StartOffset < 0 || variable.StartOffset % 4 != 0 ||
                (long)variable.StartOffset + variable.ByteSize > buffer.ByteSize)
                throw new InvalidDataException($"Terrain constant {variable.Name.Text} has an unsupported RDEF layout.");
            fields.Add(new(variable.Name.Text, variable.StartOffset, Math.Max(elements, 1), stride, elementBytes));
        }
        string[] required = vertex ? ["mW2P", "vHgtMapInf"] :
            ["cSpec", "mWV", "nMtrID", "vHgtMapInf", "vCliffInfo", "vDtlScaleU", "nToolEnble"];
        var names = fields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        if (names.Count != fields.Count || required.Any(name => !names.Contains(name)))
            throw new InvalidDataException("Terrain constants have duplicate names or lack an ordinary writer field.");
        var ordered = fields.OrderBy(f => f.Offset).ToArray();
        for (int i = 1; i < ordered.Length; i++)
            if (ordered[i - 1].Offset + (ordered[i - 1].Capacity - 1) * ordered[i - 1].Stride + ordered[i - 1].ElementBytes > ordered[i].Offset)
                throw new InvalidDataException("Terrain constant fields overlap.");
        return new(buffer.ByteSize, vertex, fields.ToArray());
    }

    public TerrainGbufferConstantPacket Pack(TerrainGbufferDrawInputs input, TerrainRendererPaletteProjection? palette = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.WorldToProjectionRows.Length != 16 || input.WorldToViewRows.Length != 12 ||
            input.WorldToProjectionRows.Concat(input.WorldToViewRows).Any(v => !float.IsFinite(v)) ||
            !Finite(input.Eye) || !Finite(input.Specular) || !Finite(input.HeightInfo) ||
            !Finite(input.CliffInfo) || !Finite(input.NearFar) || input.HeightInfo.X <= 0 || input.HeightInfo.Y <= 0)
            throw new InvalidDataException("Terrain draw requires finite explicit matrices and shader parameters.");
        if (!vertex && (palette is null || !palette.Resolved || palette.Entries.Count is < 1 or > 255 ||
            palette.Entries.Where((e, i) => e.ParameterIndex != i).Any()))
            throw new InvalidDataException("Terrain constants require a resolved, ordered palette prefix.");
        byte[] bytes = new byte[ByteLength];
        var writes = new List<TerrainConstantWrite>(fields.Length);
        foreach (var field in fields)
        {
            uint[] words = field.Name switch
            {
                "mW2P" => Bits(input.WorldToProjectionRows),
                "mWV" => Bits(input.WorldToViewRows),
                "vEye" => Bits(input.Eye),
                "cSpec" => Bits(input.Specular),
                "nMtrID" => [unchecked((uint)input.MaterialId)],
                "nToolEnble" => [unchecked((uint)input.ToolEnable)],
                "vHgtMapInf" => Bits(input.HeightInfo),
                "vCliffInfo" => Bits(input.CliffInfo),
                "vNerFarBld" => Bits(input.NearFar),
                "vDtlTexIdx" => palette!.Entries.Select(e => e.DetailTextureNameIndex).ToArray(),
                "vDtlBldSrp" => palette!.Entries.Select(e => e.BlendSharpnessBits).ToArray(),
                "vDtlScaleU" => palette!.Entries.Select(e => e.ScaleUBits).ToArray(),
                "vDtlScaleV" => palette!.Entries.Select(e => e.ScaleVBits).ToArray(),
                _ => throw new InvalidOperationException("Unknown imported terrain constant.")
            };
            int wordsPerElement = field.ElementBytes / 4;
            int count = words.Length / wordsPerElement;
            if (count > field.Capacity)
                throw new InvalidDataException($"Terrain values exceed {field.Name}'s imported array capacity.");
            for (int i = 0; i < count; i++)
                for (int word = 0; word < wordsPerElement; word++)
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(field.Offset + i * field.Stride + word * 4, 4),
                        words[i * wordsPerElement + word]);
            writes.Add(new(field.Name, field.Offset, count, field.Stride, field.ElementBytes));
        }
        return new(bytes, writes.AsReadOnly(), palette?.Entries.Count ?? 0, false);
    }

    private static uint[] Bits(IEnumerable<float> values) => values.Select(BitConverter.SingleToUInt32Bits).ToArray();
    private static uint[] Bits(Vector4 v) => Bits(new[] { v.X, v.Y, v.Z, v.W });
    private static uint[] Bits(Vector2 v) => Bits(new[] { v.X, v.Y });
    private static bool Finite(Vector4 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) && float.IsFinite(v.W);
    private static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
}
