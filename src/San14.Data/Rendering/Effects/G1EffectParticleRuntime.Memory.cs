using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public sealed partial class G1EffectGenericRuntime
{
    private static float Float(ReadOnlyMemory<byte> data, int at) =>
        BinaryPrimitives.ReadSingleLittleEndian(data.Span[at..]);
    private static int Word(ReadOnlyMemory<byte> data, int at) =>
        BinaryPrimitives.ReadInt32LittleEndian(data.Span[at..]);
    private static void PutFloat(Memory<byte> data, int at, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(data.Span[at..], value);
    private static void PutWord(Memory<byte> data, int at, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(data.Span[at..], value);
    private static Vector4 Vector(ReadOnlyMemory<byte> data, int at) =>
        new(Float(data, at), Float(data, at + 4), Float(data, at + 8), Float(data, at + 12));
    private static void PutVector(Memory<byte> data, int at, Vector4 value)
    {
        PutFloat(data, at, value.X); PutFloat(data, at + 4, value.Y);
        PutFloat(data, at + 8, value.Z); PutFloat(data, at + 12, value.W);
    }
    private static Matrix4x4 Matrix(ReadOnlyMemory<byte> data, int at) =>
        Matrix(Vector(data, at), Vector(data, at + 16), Vector(data, at + 32), Vector(data, at + 48));
    private static Matrix4x4 Matrix(Vector4 x, Vector4 y, Vector4 z, Vector4 w) => new(
        x.X, x.Y, x.Z, x.W, y.X, y.Y, y.Z, y.W,
        z.X, z.Y, z.Z, z.W, w.X, w.Y, w.Z, w.W);
    private static Vector4 Row(Matrix4x4 m, int row) => row switch
    {
        0 => new(m.M11, m.M12, m.M13, m.M14), 1 => new(m.M21, m.M22, m.M23, m.M24),
        2 => new(m.M31, m.M32, m.M33, m.M34), _ => new(m.M41, m.M42, m.M43, m.M44)
    };
    private static void PutMatrix(Memory<byte> data, int at, Matrix4x4 value)
    {
        for (int row = 0; row < 4; row++) PutVector(data, at + row * 16, Row(value, row));
    }
    private static Matrix4x4 Multiply(Matrix4x4 a, Matrix4x4 b)
    {
        Vector4 R(Vector4 r) => (r.Z * Row(b, 2) + r.X * Row(b, 0)) + (r.W * Row(b, 3) + r.Y * Row(b, 1));
        return Matrix(R(Row(a, 0)), R(Row(a, 1)), R(Row(a, 2)), R(Row(a, 3)));
    }
    private static Vector4 Transform(Vector4 p, Matrix4x4 m) => new(
        p.Y*m.M21+p.X*m.M11+p.Z*m.M31+p.W*m.M41,
        p.X*m.M12+p.Y*m.M22+p.Z*m.M32+p.W*m.M42,
        p.X*m.M13+p.Y*m.M23+p.Z*m.M33+p.W*m.M43,
        p.X*m.M14+p.Y*m.M24+p.Z*m.M34+p.W*m.M44);
    private static Matrix4x4 Basis(Matrix4x4 value)
    {
        Vector4 N(Vector4 row)
        {
            float scale = 1f / MathF.Sqrt(row.X*row.X+row.Y*row.Y+row.Z*row.Z);
            return new(row.X*scale, row.Y*scale, row.Z*scale, row.W);
        }
        return Matrix(N(Row(value, 0)), N(Row(value, 1)), N(Row(value, 2)), new(0, 0, 0, 1));
    }
    private static Matrix4x4 Inverse(Matrix4x4 value) => Matrix4x4.Invert(value, out var inverse)
        ? inverse : throw new InvalidDataException("Particle transform is singular.");
    private int Field(int binding)
    {
        int field = Int(binding);
        if (field < 0) throw new InvalidDataException($"Particle declaration has no field {binding:X}.");
        return field;
    }
    private float TemplateFloat(int particleOffset)
    {
        int offset = particleOffset;
        if (Int(0x150) < offset) offset -= Int(0x154);
        return Float(Declaration.Layout, offset);
    }
    private static Matrix4x4 AxisRotation(Vector4 axis, float angle)
    {
        if (MathF.Abs(angle) > 6.2831855f) angle -= (int)(angle / 6.2831855f) * 6.2831855f;
        var (sine, cosine) = G1EffectShapeMath.SinCos(angle);
        float x=axis.X,y=axis.Y,z=axis.Z,square=x*x+y*y+z*z;
        float inverse = square < 0.9999881f || square > 1.0000119f ? 1f/square : 1f;
        float normal=MathF.Sqrt(inverse), c=1f-cosine;
        float xy=y*x*inverse,xz=z*x*inverse,yz=z*y*inverse;
        float sx=x*sine*normal,sy=y*sine*normal,sz=z*sine*normal;
        return new(x*x*inverse*c+cosine,xy*c+sz,xz*c-sy,0,
            xy*c-sz,y*y*inverse*c+cosine,yz*c+sx,0,
            xz*c+sy,yz*c-sx,z*z*inverse*c+cosine,0,0,0,0,1);
    }
    private Vector4 SampleDefinitionShape(G1EffectParticleDefinition definition, int referenceField)
    {
        var data=definition.Record.Bytes;
        int at=checked(referenceField+Word(data,referenceField)), length=Word(data,at), type=Word(data,at+4);
        if (at < 0 || length < 0x50 || at > data.Length-length)
            throw new InvalidDataException("Definition shape escapes its owning record.");
        var shape=data.Slice(at,length);
        if (type != 3) return G1EffectShape.Read(unchecked((uint)type),shape).Sample(context.Random);
        // Prepared type-three shell: azimuth, then cosine of the polar angle.
        float angle=context.Random.NextUnit()*(Float(shape,0x58)-Float(shape,0x54))+Float(shape,0x54);
        var (sine,cosine)=G1EffectShapeMath.SinCos(angle);
        float lower=MathF.Cos(Float(shape,0x60)),upper=MathF.Cos(Float(shape,0x5C));
        float polar=context.Random.NextUnit()*(upper-lower)+lower;
        float radius=Float(shape,0x50), horizontal=MathF.Sqrt(1f-polar*polar)*radius;
        return Transform(new(horizontal*sine,radius*polar,horizontal*cosine,1),Matrix(shape,0x10));
    }
    private Matrix4x4 DefinitionRotation(G1EffectParticleDefinition definition, G1EffectParticleBirthInputs inputs)
    {
        float angle=definition.EvaluateScalar(0x34,inputs.SourceContext,context.Random);
        return MathF.Abs(angle)>1.1920929e-5f
            ? AxisRotation(SampleDefinitionShape(definition,0x30),angle) : Matrix4x4.Identity;
    }
}
