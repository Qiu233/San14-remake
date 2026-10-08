using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data.Formats;

public sealed record G1DeferredGlobals(IReadOnlyList<Vector4> LightDirections,
    IReadOnlyList<Vector4> LightColors, Vector4 LightOcclusion, Vector4 AmbientLight,
    int RawNDirLit, Vector4 Eye, Vector2 OcclusionRates, Matrix4x4 ViewToWorld,
    Matrix4x4 ProjectionToWorld, byte[] RawBytes);

/// <summary>Declared 2704-byte PS1 cb0 layout in 9D8E811F. Reads explicit bytes only;
/// a shared backend staging buffer must not be attributed to this shader without
/// a separate binding proof. Unconsumed fields and padding remain in RawBytes.</summary>
public static class G1DeferredGlobalsReader
{
    public const int ByteCount = 2704;

    public static G1DeferredGlobals Read(ReadOnlySpan<byte> source)
    {
        if (source.Length != ByteCount)
            throw new InvalidDataException("Original deferred PS1 cb0 differs from its declared 2704-byte layout.");
        var directions = new Vector4[4];
        var colors = new Vector4[4];
        for (int i = 0; i < 4; i++)
        {
            directions[i] = V4(source, i * 16);
            colors[i] = V4(source, 64 + i * 16);
        }
        return new(Array.AsReadOnly(directions), Array.AsReadOnly(colors), V4(source, 128),
            V4(source, 144), BinaryPrimitives.ReadInt32LittleEndian(source.Slice(160, 4)),
            V4(source, 176), new(F32(source, 192), F32(source, 196)),
            Matrix(source, 2336), Matrix(source, 2400), source.ToArray());
    }

    private static float F32(ReadOnlySpan<byte> source, int at) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(source.Slice(at, 4)));
    private static Vector4 V4(ReadOnlySpan<byte> source, int at) =>
        new(F32(source, at), F32(source, at + 4), F32(source, at + 8), F32(source, at + 12));
    private static Matrix4x4 Matrix(ReadOnlySpan<byte> source, int at) =>
        new(F32(source, at), F32(source, at + 4), F32(source, at + 8), F32(source, at + 12),
            F32(source, at + 16), F32(source, at + 20), F32(source, at + 24), F32(source, at + 28),
            F32(source, at + 32), F32(source, at + 36), F32(source, at + 40), F32(source, at + 44),
            F32(source, at + 48), F32(source, at + 52), F32(source, at + 56), F32(source, at + 60));
}
