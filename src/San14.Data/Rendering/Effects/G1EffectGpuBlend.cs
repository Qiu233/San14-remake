using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The source renderer's packed factor/op recipe. This describes
/// factors, not the inherited render-target enables and component masks.</summary>
public static class G1EffectGpuBlend
{
    private static readonly uint[] RgbFactors = [1, 2, 3, 4, 5, 6, 9, 10, 7, 8, 11, 0];
    private static readonly uint[] AlphaFactors = [1, 2, 5, 6, 5, 6, 7, 8, 7, 8, 11, 0];
    private static readonly uint[] Operations = [1, 2, 3, 4, 5, 0];

    public static byte[] EncodePackedRecipe(uint recipe)
    {
        static uint At(uint[] table, uint index) => index < table.Length ? table[index] :
            throw new InvalidDataException("Effect blend recipe has an invalid factor or operation.");
        uint[] values = [At(RgbFactors, recipe >> 8 & 15), At(RgbFactors, recipe >> 16 & 15),
            At(Operations, recipe & 15), At(AlphaFactors, recipe >> 12 & 15),
            At(AlphaFactors, recipe >> 20 & 15), At(Operations, recipe >> 4 & 15)];
        if (values.Any(value => value == 0))
            throw new InvalidDataException("Effect blend recipe selects a reserved device enum.");
        byte[] bytes = new byte[264];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 1);
        for (int rt = 0; rt < 8; rt++)
        {
            int offset = 8 + rt * 32;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), rt == 0 ? 1u : 0u);
            for (int index = 0; index < values.Length; index++)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 4 + index * 4), values[index]);
            // The native draw scope replaces these with its actual inherited
            // masks. A null D3D blend state has the API-defined mask of fifteen.
            bytes[offset + 28] = 15;
        }
        return bytes;
    }
}
