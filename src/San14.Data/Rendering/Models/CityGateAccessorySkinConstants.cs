using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Named writes for the native category-2 four-influence material.
/// Inactive palette storage and alignment padding have no source-value claim.
/// Actual groups replace the active rows with their retained current pose.</summary>
internal static class CityGateAccessorySkinConstants
{
    internal static uint[] SkinControls(G1ModelGeometry geometry, G1DrawRecord draw,
        ModelMatrixBindingInput matrix)
    {
        if (ArmySkinVertexAdapter.Select(geometry, draw.Index) != ArmySkinVertexProfile.FourInfluenceUnsigned8 ||
            matrix.CurrentWords[1] != 3)
            throw new NotSupportedException("Accessory geometry requires its supported four-influence skin mode.");
        return [1, 1, 0];
    }

    internal static byte[] Vertex(DxbcResourceDefinitions definitions, Matrix4x4 worldToProjection,
        IReadOnlyList<Vector4> activePalette, IReadOnlyList<uint> controls)
    {
        var buffer = definitions.Buffers.Single();
        if (buffer.ByteSize != 6256 || buffer.Name.Text != "$Globals" || controls.Count != 3 ||
            activePalette.Count > 384 || activePalette.Count % 3 != 0 ||
            definitions.Bindings.Count(b => b.InputType == 0 && b.Name.Text == "$Globals" &&
                b.BindPoint == 0 && b.BindCount == 1) != 1 ||
            !buffer.Variables.Select(v => v.Name.Text).Order().SequenceEqual(new[] { "bSkin", "mL2W", "mW2P" }))
            throw new NotSupportedException("Accessory vertex material has an unsupported native constant interface.");
        byte[] bytes = new byte[buffer.ByteSize];
        Write("mW2P", 0, 2, 3, 4, 4, 0, MatrixWords(worldToProjection));
        // Only actual palette entries are meaningful. The remaining storage is
        // not an invented bind pose or a source constructor initialization.
        uint[] palette = new uint[384 * 4];
        for (int i = 0; i < activePalette.Count; ++i)
        {
            var row = activePalette[i];
            float[] values = [row.X, row.Y, row.Z, row.W];
            for (int j = 0; j < 4; ++j) palette[i * 4 + j] = FloatWord(values[j]);
        }
        Write("mL2W", 64, 1, 3, 1, 4, 384, palette);
        Write("bSkin", 6208, 0, 1, 1, 1, 3, controls.ToArray());
        return bytes;

        void Write(string name, int offset, ushort variableClass, ushort baseType,
            ushort rows, ushort columns, ushort elements, uint[] words)
        {
            var field = buffer.Variables.Single(v => v.Name.Text == name);
            var type = definitions.Types.Single(t => t.Offset == field.TypeOffset);
            int count = Math.Max(1, (int)elements), width = rows * columns * 4;
            int stride = elements == 0 ? width : Math.Max(16, width);
            int length = (count - 1) * stride + width;
            if (field.StartOffset != offset || field.ByteSize != length || type.Class != variableClass ||
                type.BaseType != baseType || type.Rows != rows || type.Columns != columns ||
                type.Elements != elements || type.Members.Count != 0 || words.Length != count * width / 4 ||
                offset + length > bytes.Length)
                throw new NotSupportedException($"Accessory vertex field {name} has an unsupported native layout.");
            for (int i = 0; i < count; ++i)
                for (int j = 0; j < width / 4; ++j)
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + i * stride + j * 4), words[i * width / 4 + j]);
        }
    }

    static uint[] MatrixWords(Matrix4x4 value) => new[]
    {
        value.M11, value.M12, value.M13, value.M14, value.M21, value.M22, value.M23, value.M24,
        value.M31, value.M32, value.M33, value.M34, value.M41, value.M42, value.M43, value.M44,
    }.Select(FloatWord).ToArray();

    static uint FloatWord(float value) => float.IsFinite(value) ? BitConverter.SingleToUInt32Bits(value) :
        throw new InvalidDataException("Accessory initial vertex material contains a nonfinite input.");
}
