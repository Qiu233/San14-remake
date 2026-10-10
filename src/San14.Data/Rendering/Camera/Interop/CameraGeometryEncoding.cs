using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Geometry fields of the renderer's camera ABI. Other field groups
/// remain in the caller's payload until their own encoder publishes them.</summary>
internal static class CameraGeometryEncoding
{
    internal static void Write(Span<byte> destination, MapCameraGeometry geometry)
    {
        WriteVector(destination[0x10..], geometry.Eye);
        WriteVector(destination[0x20..], geometry.Target);
        WriteVector(destination[0x30..], geometry.Up);
        WriteMatrices(destination, geometry.Matrices);
        var lens = geometry.Lens;
        WriteFloat(destination, 0x140, lens.Near);
        WriteFloat(destination, 0x144, lens.Far);
        WriteFloat(destination, 0x148, lens.VerticalFovRadians);
        WriteFloat(destination, 0x14C, lens.Aspect);
        WriteFloat(destination, 0x150, lens.ViewOffset);
        WriteFloat(destination, 0x154, lens.OrthographicScale);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[0x158..], lens.Flags);
        destination[0x160] = destination[0x161] = 0;
    }

    internal static byte[] UpdateMatrices(ReadOnlySpan<byte> encoded, uint clipProfile,
        NativeCameraTangentDispatch tangentDispatch)
    {
        if (encoded.Length != MapCameraConstruction.PrefixByteLength)
            throw new InvalidDataException("Current camera matrix update requires a complete camera ABI payload.");
        byte[] result = encoded.ToArray();
        var lens = CameraLens.DecodePrefix(encoded[..CameraLens.PrefixBytes]);
        var matrices = MapCameraConstructionMath.CompleteAB6C60(
            ReadVector(encoded[0x10..]), ReadVector(encoded[0x20..]), ReadVector(encoded[0x30..]),
            lens, clipProfile, tangentDispatch);
        WriteMatrices(result, matrices);
        result[0x160] = result[0x161] = 0;
        return result;
    }

    internal static void WriteMatrices(Span<byte> destination, MapCameraConstructionMatrices matrices)
    {
        if (destination.Length < 0x140)
            throw new InvalidDataException("Camera ABI destination does not include all four matrix fields.");
        WriteMatrix(destination[0x40..], matrices.RawWorldToView40);
        WriteMatrix(destination[0x80..], matrices.WorldToView80);
        WriteMatrix(destination[0xC0..], matrices.ProjectionC0);
        WriteMatrix(destination[0x100..], matrices.WorldToProjection100);
    }

    private static Vector4 ReadVector(ReadOnlySpan<byte> bytes) => new(
        ReadFloat(bytes), ReadFloat(bytes[4..]), ReadFloat(bytes[8..]), ReadFloat(bytes[12..]));

    private static void WriteMatrix(Span<byte> destination, Matrix4x4 matrix)
    {
        ReadOnlySpan<float> rows = [matrix.M11,matrix.M12,matrix.M13,matrix.M14,
            matrix.M21,matrix.M22,matrix.M23,matrix.M24,matrix.M31,matrix.M32,matrix.M33,matrix.M34,
            matrix.M41,matrix.M42,matrix.M43,matrix.M44];
        for (int i = 0; i < rows.Length; i++) WriteFloat(destination, i * 4, rows[i]);
    }

    private static float ReadFloat(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadSingleLittleEndian(bytes);
    private static void WriteFloat(Span<byte> bytes, int offset, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(bytes[offset..], value);
    private static void WriteVector(Span<byte> bytes, Vector4 value)
    {
        WriteFloat(bytes, 0, value.X); WriteFloat(bytes, 4, value.Y);
        WriteFloat(bytes, 8, value.Z); WriteFloat(bytes, 12, value.W);
    }
}
