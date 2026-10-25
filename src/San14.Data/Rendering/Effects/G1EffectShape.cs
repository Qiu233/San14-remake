using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Prepared outer-emitter shape. Sampling consumes the shared effect
/// stream supplied by the live owner, and returns a homogeneous position after
/// the current shape transform. The caller owns subsequent particle transforms.</summary>
public sealed class G1EffectShape
{
    private readonly float innerRadius;
    private readonly float outerRadius;
    private readonly float angleStart;
    private readonly float angleEnd;
    private readonly float height;
    private readonly float lowerCosine;
    private readonly float upperCosine;
    private readonly float radiusCubeDifference;

    public uint ShapeType { get; }
    public ReadOnlyMemory<byte> SourceBytes { get; }
    public Matrix4x4 Transform { get; set; }
    public int Dimension => ShapeType switch { 0 => 3, 3 or 6 => 2, _ => 0 };

    private G1EffectShape(uint type, ReadOnlyMemory<byte> bytes)
    {
        ShapeType = type;
        SourceBytes = bytes;
        Transform = new(F(0x10), F(0x14), F(0x18), F(0x1C),
            F(0x20), F(0x24), F(0x28), F(0x2C),
            F(0x30), F(0x34), F(0x38), F(0x3C),
            F(0x40), F(0x44), F(0x48), F(0x4C));
        if (type == 0)
        {
            innerRadius = F(0x50);
            outerRadius = F(0x54);
            angleStart = F(0x58);
            angleEnd = F(0x5C);
            // The initializer derives these from the two polar angles, rather
            // than treating the serialized derivative fields as live state.
            lowerCosine = MathF.Cos(F(0x64));
            upperCosine = MathF.Cos(F(0x60));
            radiusCubeDifference = outerRadius * outerRadius * outerRadius -
                innerRadius * innerRadius * innerRadius;
        }
        else if (type == 3)
        {
            outerRadius = F(0x50);
            angleStart = F(0x54);
            angleEnd = F(0x58);
            lowerCosine = MathF.Cos(F(0x60));
            upperCosine = MathF.Cos(F(0x5C));
        }
        else if (type == 6)
        {
            outerRadius = F(0x50);
            angleStart = F(0x54);
            angleEnd = F(0x58);
            height = F(0x5C);
        }
    }

    public static G1EffectShape Read(G1EffectEmitterDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return Read(declaration.ShapeType, declaration.Shape);
    }

    public static G1EffectShape Read(uint type, ReadOnlyMemory<byte> bytes)
    {
        int minimum = type switch
        {
            0 => 0x80, 3 => 0x70, 6 => 0x60, 10 => 0x50,
            _ => throw new NotSupportedException($"G1FX shape {type} is not implemented.")
        };
        if (bytes.Length < minimum || BinaryPrimitives.ReadUInt32LittleEndian(bytes.Span) != bytes.Length ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.Span.Slice(4, 4)) != type)
            throw new InvalidDataException("Incomplete or inconsistent G1FX shape declaration.");
        return new(type, bytes);
    }

    /// <summary>The source shape measure used by rate mode two. A cylinder
    /// surface uses its declared radius, angular span and height; its source
    /// measure does not multiply by the transform determinant.</summary>
    public float Measure
    {
        get
        {
            if (ShapeType == 10) return 1;
            if (ShapeType == 3) return MathF.Abs((upperCosine - lowerCosine) * (angleEnd - angleStart)) *
                (outerRadius * outerRadius);
            if (ShapeType == 6) return MathF.Abs(height * outerRadius * (angleEnd - angleStart));
            Matrix4x4 m = Transform;
            float determinant = (m.M21 * m.M13 - m.M11 * m.M23) * m.M32 +
                (m.M12 * m.M23 - m.M13 * m.M22) * m.M31 +
                (m.M11 * m.M22 - m.M21 * m.M12) * m.M33;
            return MathF.Abs(determinant * radiusCubeDifference * (upperCosine - lowerCosine) *
                (angleEnd - angleStart) / 3f);
        }
    }

    /// <summary>One sample per scheduled birth batch, before iterating its
    /// particle definitions. Shape zero consumes azimuth, polar cosine, then
    /// radius; shape three consumes azimuth then polar cosine; shape six
    /// consumes azimuth then height; shape ten consumes none.</summary>
    public Vector4 Sample(G1EffectRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (ShapeType == 10)
            return new(Transform.M41, Transform.M42, Transform.M43, Transform.M44);

        float angle = random.NextUnit() * (angleEnd - angleStart) + angleStart;
        (float sine, float cosine) = G1EffectShapeMath.SinCos(angle);
        Vector4 position;
        if (ShapeType == 6)
        {
            float y = (random.NextUnit() - 0.5f) * height;
            position = new(outerRadius * sine, y, outerRadius * cosine, 1);
        }
        else
        {
            float polarCosine = random.NextUnit() * (upperCosine - lowerCosine) + lowerCosine;
            float radius = outerRadius;
            if (ShapeType == 0)
            {
                float fraction = innerRadius / outerRadius;
                fraction = fraction * fraction * fraction;
                radius = outerRadius * G1EffectShapeMath.CubeRoot((1f - fraction) * random.NextUnit() + fraction);
            }
            float y = radius * polarCosine;
            float horizontal = MathF.Sqrt(1f - polarCosine * polarCosine) * radius;
            position = new(horizontal * sine, y, horizontal * cosine, 1);
        }
        return ApplyTransform(position, Transform);
    }

    private float F(int offset) => BinaryPrimitives.ReadSingleLittleEndian(SourceBytes.Span.Slice(offset, 4));

    // Keep the source scalar operation order rather than applying a normalized
    // direction or discarding the homogeneous lane.
    private static Vector4 ApplyTransform(Vector4 v, Matrix4x4 m) => new(
        v.Y * m.M21 + v.X * m.M11 + v.Z * m.M31 + v.W * m.M41,
        v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32 + v.W * m.M42,
        v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33 + v.W * m.M43,
        v.X * m.M14 + v.Y * m.M24 + v.Z * m.M34 + v.W * m.M44);
}
