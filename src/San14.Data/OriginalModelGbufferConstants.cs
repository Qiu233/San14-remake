using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>All matrices are already in the selected original shader's row
/// order. Local position and normal transforms are separate source inputs.</summary>
public sealed record OriginalModelGbufferVertexInputs(float[] WorldToProjectionRows,
    float[] LocalToWorldNormalRows, float[] LocalToWorldPositionRows);
public sealed record OriginalModelGbufferPixelInputs(Vector4 Specular, float[] WorldToViewRows,
    int MaterialId, float? AlphaTest);
public sealed record OriginalModelGbufferConstantPacket(byte[] Bytes, IReadOnlyList<TerrainConstantWrite> Writes);

/// <summary>Named writes for the exact 160B/80B source G-buffer profiles.
/// Complete caller templates retain padding. This writer does not select
/// materials, calculate original CPU transforms, or infer active parameters.</summary>
public static class OriginalModelGbufferConstants
{
    public static OriginalModelGbufferConstantPacket Vertex(DxbcResourceDefinitions definitions,
        ReadOnlySpan<byte> explicitCompleteTemplate, OriginalModelGbufferVertexInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Check(input.WorldToProjectionRows, 16); Check(input.LocalToWorldNormalRows, 12); Check(input.LocalToWorldPositionRows, 12);
        var buffer = Buffer(definitions, explicitCompleteTemplate, 160);
        RequireInventory(buffer, ["mW2P", "mL2WT", "mL2W"]);
        byte[] bytes = explicitCompleteTemplate.ToArray(); var writes = new List<TerrainConstantWrite>();
        Write(definitions, buffer, bytes, writes, "mW2P", 0, input.WorldToProjectionRows.Select(BitConverter.SingleToUInt32Bits).ToArray(), 2, 3, 4, 4, 0);
        Write(definitions, buffer, bytes, writes, "mL2WT", 64, input.LocalToWorldNormalRows.Select(BitConverter.SingleToUInt32Bits).ToArray(), 1, 3, 1, 4, 3);
        Write(definitions, buffer, bytes, writes, "mL2W", 112, input.LocalToWorldPositionRows.Select(BitConverter.SingleToUInt32Bits).ToArray(), 1, 3, 1, 4, 3);
        return new(bytes, writes.AsReadOnly());
    }

    public static OriginalModelGbufferConstantPacket Pixel(DxbcResourceDefinitions definitions,
        ReadOnlySpan<byte> explicitCompleteTemplate, OriginalModelGbufferPixelInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Check(input.WorldToViewRows, 12);
        Check([input.Specular.X, input.Specular.Y, input.Specular.Z, input.Specular.W], 4);
        var buffer = Buffer(definitions, explicitCompleteTemplate, 80);
        bool alpha = buffer.Variables.Any(v => v.Name.Text == "vATest");
        if (alpha != input.AlphaTest.HasValue || input.AlphaTest is float threshold && !float.IsFinite(threshold))
            throw new InvalidDataException("Original model alpha-test parameter must match the selected source profile.");
        RequireInventory(buffer, alpha ? ["cSpec", "mWV", "vATest", "nMtrID"] : ["cSpec", "mWV", "nMtrID"]);
        byte[] bytes = explicitCompleteTemplate.ToArray(); var writes = new List<TerrainConstantWrite>();
        Write(definitions, buffer, bytes, writes, "cSpec", 0,
            new[] { input.Specular.X, input.Specular.Y, input.Specular.Z, input.Specular.W }.Select(BitConverter.SingleToUInt32Bits).ToArray(), 1, 3, 1, 4, 0);
        Write(definitions, buffer, bytes, writes, "mWV", 16, input.WorldToViewRows.Select(BitConverter.SingleToUInt32Bits).ToArray(), 1, 3, 1, 4, 3);
        if (alpha) Write(definitions, buffer, bytes, writes, "vATest", 64, [BitConverter.SingleToUInt32Bits(input.AlphaTest!.Value)], 0, 3, 1, 1, 0);
        Write(definitions, buffer, bytes, writes, "nMtrID", alpha ? 68 : 64, [unchecked((uint)input.MaterialId)], 0, 2, 1, 1, 0);
        return new(bytes, writes.AsReadOnly());
    }

    private static DxbcRdefBuffer Buffer(DxbcResourceDefinitions definitions, ReadOnlySpan<byte> template, int size)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (definitions.Buffers.Count != 1 || definitions.Buffers[0].Name.Text != "$Globals" ||
            definitions.Buffers[0].ByteSize != size || template.Length != size ||
            definitions.Bindings.Count(b => b.InputType == 0 && b.BindPoint == 0 && b.BindCount == 1 && b.Name.Text == "$Globals") != 1)
            throw new InvalidDataException("Original model G-buffer CB differs from its complete source profile.");
        return definitions.Buffers[0];
    }

    private static void RequireInventory(DxbcRdefBuffer buffer, string[] names)
    {
        if (!buffer.Variables.Select(v => v.Name.Text).Order().SequenceEqual(names.Order()))
            throw new InvalidDataException("Original model G-buffer variable inventory is unsupported.");
    }

    private static void Write(DxbcResourceDefinitions definitions, DxbcRdefBuffer buffer, byte[] bytes,
        List<TerrainConstantWrite> writes, string name, int offset, uint[] words,
        ushort variableClass, ushort baseType, ushort rows, ushort columns, ushort elements)
    {
        var variable = buffer.Variables.Single(v => v.Name.Text == name);
        var type = definitions.Types.Single(t => t.Offset == variable.TypeOffset);
        int count = Math.Max(1, (int)elements), width = rows * columns * 4;
        int stride = elements == 0 ? width : 16;
        int expectedSize = (count - 1) * stride + width;
        if (variable.StartOffset != offset || variable.ByteSize != expectedSize ||
            type.Class != variableClass || type.BaseType != baseType || type.Rows != rows || type.Columns != columns ||
            type.Elements != elements || type.Members.Count != 0 || words.Length * 4 != count * width || offset + expectedSize > bytes.Length)
            throw new InvalidDataException($"Original model G-buffer field {name} has an unsupported source RDEF layout.");
        for (int i = 0; i < count; i++) for (int word = 0; word < width / 4; word++)
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + i * stride + word * 4), words[i * width / 4 + word]);
        writes.Add(new(name, offset, count, stride, width));
    }

    private static void Check(float[]? values, int count)
    {
        if (values is null || values.Length != count || values.Any(v => !float.IsFinite(v)))
            throw new InvalidDataException("Original model G-buffer requires complete finite explicit row inputs.");
    }
}
