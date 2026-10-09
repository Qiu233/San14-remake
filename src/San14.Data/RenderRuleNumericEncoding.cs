using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Encode project numeric parameters for existing shader/controller
/// transports. No original image or instruction data is used.</summary>
internal static class RenderRuleNumericEncoding
{
    internal static byte[] Matrix(Matrix4x4 value)
        => Vectors(new(value.M11,value.M12,value.M13,value.M14),
            new(value.M21,value.M22,value.M23,value.M24),
            new(value.M31,value.M32,value.M33,value.M34),
            new(value.M41,value.M42,value.M43,value.M44));

    internal static byte[] Vectors(params Vector4[] values)
    {
        byte[] bytes=new byte[values.Length*16];
        for(int i=0;i<values.Length;i++)
        {
            var v=values[i];Span<byte> row=bytes.AsSpan(i*16,16);
            BinaryPrimitives.WriteSingleLittleEndian(row,v.X);
            BinaryPrimitives.WriteSingleLittleEndian(row[4..],v.Y);
            BinaryPrimitives.WriteSingleLittleEndian(row[8..],v.Z);
            BinaryPrimitives.WriteSingleLittleEndian(row[12..],v.W);
        }
        return bytes;
    }
}
