using System.Buffers.Binary;
using System.Numerics;
using San14.Data;

/// <summary>The native numeric prefix retains its static interchange fields.
/// A frame writes only the fields changed by its typed camera-light branch.</summary>
internal sealed class D3D11CurrentLightEncoding
{
    internal const int ByteLength = 0x328;
    private readonly byte[] template;

    internal D3D11CurrentLightEncoding(ReadOnlySpan<byte> temporary) =>
        template = temporary[..ByteLength].ToArray();

    internal void Write(CurrentDirectionalLightState state, ReadOnlySpan<byte> camera,
        Span<byte> destination)
    {
        template.CopyTo(destination);
        if (!state.Mode6Applied) return;
        for (int index = 0; index < 4; index++)
        {
            Vector(destination[(index * 32)..], state.Lighting.Directional[index].Direction);
            Vector(destination[(index * 32 + 16)..], state.Lighting.Directional[index].Color);
            Vector(destination[(0xA0 + index * 16)..], state.CurrentQuaternions[index]);
        }
        Vector(destination[0x120..], state.HemisphereDirection);
        // The full camera fog record includes an explicitly copied compatibility
        // tail. Numeric consumers use the typed fields; transport preserves it.
        camera.Slice(0x340, 96).CopyTo(destination[0x150..]);
    }

    private static void Vector(Span<byte> bytes, Vector4 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes, value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[4..], value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[8..], value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[12..], value.W);
    }
}
