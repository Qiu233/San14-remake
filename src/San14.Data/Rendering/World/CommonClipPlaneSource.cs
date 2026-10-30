using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Common's six axis coordinates, in +X,-X,+Y,-Y,+Z,-Z order.
/// These are source positions, not already transformed plane equations.</summary>
public readonly record struct CommonClipPlaneCoordinates(float PositiveX, float NegativeX,
    float PositiveY, float NegativeY, float PositiveZ, float NegativeZ)
{
    internal float At(int index) => index switch
    {
        0 => PositiveX, 1 => NegativeX, 2 => PositiveY,
        3 => NegativeY, 4 => PositiveZ, 5 => NegativeZ,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
}

public sealed record CommonClipPlaneConfiguration(uint Mask, CommonClipPlaneCoordinates Coordinates);

/// <summary>Effective ksgClipPlane values after the actual Common call and
/// frontend cache preparation. Disabled slots are neutral zero; this does
/// not claim that the frontend's underlying raw plane slots were cleared.</summary>
public sealed class CommonClipPlanePacket
{
    internal CommonClipPlanePacket(MapCommonRenderState common, uint mask,
        uint frontendMask, Vector4[] planes, bool preparesRawPlanes)
    {
        Common = common; Mask = mask; FrontendMask = frontendMask;
        Planes = Array.AsReadOnly(planes); PreparesRawPlanes = preparesRawPlanes;
    }
    public MapCommonRenderState Common { get; }
    public uint Mask { get; }
    public uint FrontendMask { get; }
    public IReadOnlyList<Vector4> Planes { get; }
    public bool PreparesRawPlanes { get; }
    public string SourceProvenance => Common.ProducerProvenance +
        ";D4A7A0-Common-axis-planes/current-View100-inverse;95EA50-enabled-nibbles-to-ksgClipPlane";
}

/// <summary>Resource/current Common -> six effective vertex clip equations.
/// Import follows the Common loader's explicit zero initialization. Current
/// calls consume the selected typed Common state, never reread resources.</summary>
public static class CommonClipPlaneSource
{
    public const uint MaskProperty = 0x880FE0E8;
    public const uint CoordinatesProperty = 0x356FF2E7;

    public static CommonClipPlaneConfiguration ReadInitialSettings(LoadedObjects source)
    {
        ArgumentNullException.ThrowIfNull(source);
        DokObject[] objects = source.Document.Objects.Where(item =>
            item.ObjectId == CommonRenderResources.SourceObjectId).ToArray();
        if (objects.Length != 1 || objects[0].Kind != DokObjectKind.Inline || objects[0].ClassId != 0xEF3D849D)
            throw new InvalidDataException("Clip settings require the actual CommonViewSetting declaration.");
        ReadOnlySpan<byte> Read(uint hash, uint type, uint count)
        {
            DokProperty[] fields = objects[0].Properties.Where(item => item.NameHash == hash).ToArray();
            if (fields.Length == 0) return ReadOnlySpan<byte>.Empty;
            if (fields.Length != 1 || fields[0].TypeCode != type || fields[0].ElementCount != count ||
                fields[0].ElementSize != 4 || fields[0].RawValue.Length != checked((int)count * 4))
                throw new InvalidDataException($"Common clip setting {hash:X8} requires its exact source type/count.");
            return fields[0].RawValue;
        }
        ReadOnlySpan<byte> maskBytes = Read(MaskProperty, 4, 1);
        uint mask = maskBytes.IsEmpty ? 0u : BinaryPrimitives.ReadUInt32LittleEndian(maskBytes);
        ReadOnlySpan<byte> coordinates = Read(CoordinatesProperty, 8, 6);
        // D20 loader zeroes all six local floats before B3E980. Absent source
        // properties preserve those actual loader defaults, independently.
        return new(mask, coordinates.IsEmpty ? default : ReadCoordinates(coordinates));
    }

    internal static CommonClipPlaneCoordinates ReadCoordinates(ReadOnlySpan<byte> source)
    {
        if (source.Length != 24) throw new InvalidDataException("Common clip coordinates require six float32 values.");
        return new(Float(source, 0), Float(source, 4), Float(source, 8),
            Float(source, 12), Float(source, 16), Float(source, 20));
    }

    public static CommonClipPlanePacket Prepare(MapCommonRenderState actualCommon,
        Matrix4x4 actualCameraWorldToProjection)
    {
        ArgumentNullException.ThrowIfNull(actualCommon);
        CommonAuxiliaryState auxiliary = actualCommon.Auxiliary ??
            throw new InvalidDataException("The Common clip call requires its actual current auxiliary owner.");
        uint mask = auxiliary.ClipPlaneMask ??
            throw new InvalidDataException("The current Common clip mask is unresolved.");
        var planes = new Vector4[6];
        uint active = mask & 0x3Fu;
        // D4A7A0 disables only the frontend mask, leaving raw planes alone.
        // 95EA50 then writes zero for every disabled cache slot, without
        // reading the View matrix or Common coordinates on this branch.
        if (active == 0) return new(actualCommon, mask, 0, planes, false);
        CommonClipPlaneCoordinates coordinates = auxiliary.ClipPlanes ??
            throw new InvalidDataException("Enabled current Common clip coordinates are unresolved.");
        Matrix4x4 inverse = NativeMatrixMath.Invert(actualCameraWorldToProjection, out _);
        uint frontendMask = 0;
        for (int index = 0; index < 6; index++)
        {
            float coordinate = coordinates.At(index);
            if (!float.IsFinite(coordinate)) throw new InvalidDataException("Common clip coordinate is nonfinite.");
            float sign = (index & 1) == 0 ? 1f : -1f;
            float x = index < 2 ? sign : 0f;
            float y = index is 2 or 3 ? sign : 0f;
            float z = index >= 4 ? sign : 0f;
            // Source forms a point on the corresponding axis, then negates
            // its three-product dot with the fixed normal (including zeros).
            float px = index < 2 ? coordinate : 0f;
            float py = index is 2 or 3 ? coordinate : 0f;
            float pz = index >= 4 ? coordinate : 0f;
            float w = -NativeFloat.Add(NativeFloat.Add(M(x, px), M(y, py)), M(z, pz));
            Vector4 transformed = new(
                Sum(inverse.M12, y, inverse.M11, x, inverse.M13, z, inverse.M14, w),
                Sum(inverse.M21, x, inverse.M22, y, inverse.M23, z, inverse.M24, w),
                Sum(inverse.M32, y, inverse.M31, x, inverse.M33, z, inverse.M34, w),
                Sum(inverse.M42, y, inverse.M41, x, inverse.M43, z, inverse.M44, w));
            // The source writes all six raw planes. 95EA50's selected cache
            // copies only enabled slots; disabled equations are literal zero.
            if ((active & (1u << index)) != 0)
            {
                planes[index] = transformed;
                frontendMask |= 2u << (index * 4);
            }
        }
        return new(actualCommon, mask, frontendMask, planes, true);
    }

    private static float M(float left, float right) => NativeFloat.Multiply(left, right);
    private static float Float(ReadOnlySpan<byte> source, int offset) => BitConverter.UInt32BitsToSingle(
        BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]));
    private static float Sum(float a, float x, float b, float y, float c, float z, float d, float w)
        => NativeFloat.Add(NativeFloat.Add(NativeFloat.Add(M(a, x), M(b, y)), M(c, z)), M(d, w));
}
