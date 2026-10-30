using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Post-integration transforms in the installed effect motion descriptors.
/// Parent state is a view of its live transform owner, never a serialized pointer.</summary>
public static class G1EffectPostTransform
{
    public static void Initialize(Memory<byte> descriptor)
    {
        int type = I(descriptor.Span, 4);
        if (type is 9 or 10 or 11 or 12 or 16 or 17 or 18) Initialize(descriptor[0x10..]);
        else if (type is not (0 or 4 or 6))
            throw new NotSupportedException($"Effect post-transform {type} is not implemented.");
    }

    public static void Apply(ReadOnlyMemory<byte> descriptor, G1EffectRecord generic,
        Memory<byte> particle, float dt, ReadOnlyMemory<byte> parentState)
    {
        int type = I(descriptor.Span, 4);
        if (type == 0) return;
        if (type == 6)
        {
            FaceVelocity(descriptor.Span, generic, particle);
            return;
        }
        if (type == 11)
        {
            int at = I(generic.Bytes.Span, 0x34);
            Vector4 pivot = V(particle.Span, I(generic.Bytes.Span, 0x5C));
            var before = ReadMatrix(particle.Span, at);
            OffsetPivot(ref before, pivot, false);
            WriteMatrix(particle.Span, at, before);
            Apply(descriptor[0x10..], generic, particle, dt, parentState);
            var after = ReadMatrix(particle.Span, at);
            OffsetPivot(ref after, pivot, true);
            WriteMatrix(particle.Span, at, after);
            return;
        }
        if (type == 10)
        {
            Apply(descriptor[0x10..], generic, particle, dt, parentState);
            int localAt = I(generic.Bytes.Span, 0x3C), postDestination = I(generic.Bytes.Span, 0x34);
            var local = ReadMatrix(particle.Span, localAt);
            var orientation = ReadMatrix(particle.Span, I(generic.Bytes.Span, 0x50));
            var intermediate = G1EffectDistribution.Multiply(local, orientation);
            WriteMatrix(particle.Span, postDestination, intermediate);
            var parent = ReadMatrix(parentState.Span, 0x10);
            WriteMatrix(particle.Span, postDestination, G1EffectDistribution.Multiply(intermediate, parent));
            return;
        }
        if (type == 4)
        {
            int cursor = 0x1C;
            Vector4 rate = Vector(descriptor.Span, ref cursor, particle.Span);
            int localAt = I(generic.Bytes.Span, 0x3C);
            Matrix4x4 local = ReadMatrix(particle.Span, localAt);
            var rotation = new Vector3(rate.X * dt, rate.Y * dt, rate.Z * dt);
            float square = rotation.Y*rotation.Y + rotation.X*rotation.X + rotation.Z*rotation.Z;
            if (square > 0)
            {
                float inverseLength = 1f / MathF.Sqrt(square);
                float inverseHypotenuse = 1f / MathF.Sqrt(square + 1f);
                var axis = new Vector4(rotation * inverseLength, dt * rate.W * inverseLength);
                var turn = G1EffectDistribution.Rotation(axis,
                    inverseLength * square * inverseHypotenuse, inverseHypotenuse);
                for (int row = 0; row < 3; row++)
                {
                    var old = Row(local, row);
                    var rotated = old.X * Row(turn, 0) + old.Y * Row(turn, 1) + old.Z * Row(turn, 2);
                    SetRow(ref local, row, new(rotated.X, rotated.Y, rotated.Z, old.W));
                }
                WriteMatrix(particle.Span, localAt, local);
            }
            return;
        }
        if (type is not (9 or 12 or 16 or 17 or 18))
            throw new NotSupportedException($"Effect post-transform {type} is not implemented.");
        Apply(descriptor[0x10..], generic, particle, dt, parentState);
        int destination = I(generic.Bytes.Span, 0x34);
        if (type == 9)
        {
            var local = ReadMatrix(particle.Span, I(generic.Bytes.Span, 0x3C));
            WriteMatrix(particle.Span, destination,
                G1EffectDistribution.Multiply(local, ReadMatrix(parentState.Span, 0x10)));
            return;
        }
        if (type == 16)
        {
            ReadMatrixBytes(particle.Span, I(generic.Bytes.Span, 0x3C))
                .CopyTo(particle.Span.Slice(destination, 64));
            return;
        }
        var result = ReadMatrix(particle.Span, destination);
        var position = Row(result, 3);
        if (type == 12)
        {
            var offset = V(particle.Span, I(generic.Bytes.Span, 0x5C));
            result.M41 += offset.Y*result.M21 + offset.X*result.M11 + offset.Z*result.M31;
            result.M42 += offset.X*result.M12 + offset.Y*result.M22 + offset.Z*result.M32;
            result.M43 += offset.X*result.M13 + offset.Y*result.M23 + offset.Z*result.M33;
            result.M44 += offset.W;
        }
        else
        {
            if (type == 18)
            {
                var parent = ReadMatrix(parentState.Span, 0x50);
                for (int row = 0; row < 3; row++)
                {
                    var v = Row(parent, row);
                    float inverse = 1f / MathF.Sqrt(v.X*v.X + v.Y*v.Y + v.Z*v.Z);
                    SetRow(ref parent, row, new(v.X*inverse,v.Y*inverse,v.Z*inverse,v.W));
                }
                result = MultiplyBasis(result, parent);
            }
            result = MultiplyBasis(result, ReadMatrix(particle.Span, I(generic.Bytes.Span, 0x68)));
            SetRow(ref result, 3, position);
        }
        WriteMatrix(particle.Span, destination, result);
    }

    private static void OffsetPivot(ref Matrix4x4 matrix, Vector4 pivot, bool add)
    {
        float x = pivot.Y*matrix.M21 + pivot.X*matrix.M11 + pivot.Z*matrix.M31;
        float y = pivot.Y*matrix.M22 + pivot.X*matrix.M12 + pivot.Z*matrix.M32;
        float z = pivot.Y*matrix.M23 + pivot.X*matrix.M13 + pivot.Z*matrix.M33;
        matrix.M41 = add ? x + matrix.M41 : matrix.M41 - x;
        matrix.M42 = add ? y + matrix.M42 : matrix.M42 - y;
        matrix.M43 = add ? z + matrix.M43 : matrix.M43 - z;
        matrix.M44 = add ? pivot.W + matrix.M44 : matrix.M44 - pivot.W;
    }

    private static void FaceVelocity(ReadOnlySpan<byte> descriptor, G1EffectRecord generic, Memory<byte> particle)
    {
        Vector4 velocity = V(particle.Span, I(generic.Bytes.Span, 0x48));
        float square = velocity.X*velocity.X + velocity.Y*velocity.Y + velocity.Z*velocity.Z;
        if (!(square >= 1.1920929e-05f)) return;
        Range(descriptor, 0x10, 3);
        int axisIndex = descriptor[0x10], oppositeIndex = descriptor[0x12];
        if (axisIndex > 2 || oppositeIndex > 2)
            throw new InvalidDataException("Velocity-facing transform references an invalid basis axis.");
        int localAt = I(generic.Bytes.Span, 0x3C);
        var local = ReadMatrix(particle.Span, localAt);
        Vector4 axis = Row(local, axisIndex);
        float inverse = 1f / MathF.Sqrt((axis.X*axis.X + axis.Y*axis.Y + axis.Z*axis.Z) * square);
        float cosine = (axis.X*velocity.X + axis.Y*velocity.Y + axis.Z*velocity.Z) * inverse;
        if (!(1f - cosine >= 0.0011920929f)) return;
        if (cosine + 1f >= 0.0011920929f)
        {
            float x = (axis.Y*velocity.Z - axis.Z*velocity.Y) * inverse;
            float y = (axis.Z*velocity.X - axis.X*velocity.Z) * inverse;
            float z = (axis.X*velocity.Y - axis.Y*velocity.X) * inverse;
            float sine = MathF.Sqrt(y*y + x*x + z*z), normalize = 1f / sine;
            var rotation = G1EffectDistribution.Rotation(new(x*normalize, y*normalize, z*normalize, 0), sine, cosine);
            for (int row = 0; row < 3; row++)
            {
                Vector4 value = Row(local, row);
                SetRow(ref local, row, new(
                    rotation.M11*value.X + rotation.M21*value.Y + rotation.M31*value.Z,
                    rotation.M12*value.X + rotation.M22*value.Y + rotation.M32*value.Z,
                    rotation.M13*value.X + rotation.M23*value.Y + rotation.M33*value.Z, value.W));
            }
        }
        else
        {
            SetRow(ref local, axisIndex, -Row(local, axisIndex));
            SetRow(ref local, oppositeIndex, -Row(local, oppositeIndex));
        }
        WriteMatrix(particle.Span, localAt, local);
    }

    private static Matrix4x4 MultiplyBasis(Matrix4x4 value, Matrix4x4 basis)
    {
        for (int row = 0; row < 3; row++)
        {
            var v = Row(value, row);
            SetRow(ref value, row, new(v.Y*basis.M21+v.X*basis.M11+v.Z*basis.M31,
                basis.M22*v.Y+basis.M12*v.X+basis.M32*v.Z,
                basis.M23*v.Y+basis.M13*v.X+basis.M33*v.Z,0));
        }
        value.M41=0; value.M42=0; value.M43=0; value.M44=1;
        return value;
    }

    private static Vector4 Vector(ReadOnlySpan<byte> expression, ref int cursor, ReadOnlySpan<byte> context)
    {
        int opcode = I(expression,cursor); cursor+=4;
        if (opcode == 0) return Vector(expression,ref cursor,context);
        int at = I(expression,cursor);
        if (opcode == 1) at=checked(at+cursor);
        cursor+=4;
        return opcode switch { 1=>V(expression,at), 2=>V(context,at),
            _=>throw new NotSupportedException($"Post-transform vector expression {opcode} is not implemented.") };
    }
    private static int I(ReadOnlySpan<byte> b,int at)
    { Range(b,at,4); return BinaryPrimitives.ReadInt32LittleEndian(b[at..]); }
    private static float F(ReadOnlySpan<byte> b,int at)
    { Range(b,at,4); return BinaryPrimitives.ReadSingleLittleEndian(b[at..]); }
    private static Vector4 V(ReadOnlySpan<byte> b,int at)=>new(F(b,at),F(b,at+4),F(b,at+8),F(b,at+12));
    private static void Range(ReadOnlySpan<byte>b,int at,int size)
    { if(at<0||at>b.Length-size)throw new InvalidDataException("Effect post-transform leaves its live owner."); }
    private static ReadOnlySpan<byte> ReadMatrixBytes(ReadOnlySpan<byte>b,int at)
    { Range(b,at,64);return b.Slice(at,64); }
    private static Matrix4x4 ReadMatrix(ReadOnlySpan<byte>b,int at)
    { Range(b,at,64); var a=V(b,at);var c=V(b,at+16);var d=V(b,at+32);var e=V(b,at+48);
      return new(a.X,a.Y,a.Z,a.W,c.X,c.Y,c.Z,c.W,d.X,d.Y,d.Z,d.W,e.X,e.Y,e.Z,e.W); }
    private static void WriteMatrix(Span<byte>b,int at,Matrix4x4 m)
    { Range(b,at,64);for(int row=0;row<4;row++){var v=Row(m,row);int p=at+row*16;
      BinaryPrimitives.WriteSingleLittleEndian(b[p..],v.X);BinaryPrimitives.WriteSingleLittleEndian(b[(p+4)..],v.Y);
      BinaryPrimitives.WriteSingleLittleEndian(b[(p+8)..],v.Z);BinaryPrimitives.WriteSingleLittleEndian(b[(p+12)..],v.W);} }
    private static Vector4 Row(Matrix4x4 m,int row)=>row switch {0=>new(m.M11,m.M12,m.M13,m.M14),
      1=>new(m.M21,m.M22,m.M23,m.M24),2=>new(m.M31,m.M32,m.M33,m.M34),_=>new(m.M41,m.M42,m.M43,m.M44)};
    private static void SetRow(ref Matrix4x4 m,int row,Vector4 v)
    { switch(row){case 0:m.M11=v.X;m.M12=v.Y;m.M13=v.Z;m.M14=v.W;break;
      case 1:m.M21=v.X;m.M22=v.Y;m.M23=v.Z;m.M24=v.W;break;
      case 2:m.M31=v.X;m.M32=v.Y;m.M33=v.Z;m.M34=v.W;break;
      default:m.M41=v.X;m.M42=v.Y;m.M43=v.Z;m.M44=v.W;break;} }
}
