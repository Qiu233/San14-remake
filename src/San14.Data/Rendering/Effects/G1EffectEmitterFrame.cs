using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The outer emitter's prepared transform context. The host resolves
/// the declared binding slot/node against its actual system transform owner.
/// Shape sampling stays in its local frame; birth operations consume these
/// current/inverse matrices to perform the actual space conversion.</summary>
public sealed class G1EffectEmitterFrame
{
    private bool wroteInitialization;
    private readonly ushort transformMode;

    private G1EffectEmitterFrame(G1EffectEmitterDeclaration declaration)
    {
        ReadOnlySpan<byte> bytes = declaration.Record.Bytes.Span;
        if (bytes.Length < 0x190)
            throw new InvalidDataException("Emitter transform context is incomplete.");
        int type = BinaryPrimitives.ReadUInt16LittleEndian(bytes[0xA4..]);
        int mode = BinaryPrimitives.ReadUInt16LittleEndian(bytes[0xA6..]);
        if (type != 0 || mode is not (0x111 or 0x112))
            throw new NotSupportedException($"Emitter transform context {type}/{mode:X4} is not implemented.");
        transformMode = checked((ushort)mode);
        PrimaryBindingSlot = BinaryPrimitives.ReadInt32LittleEndian(bytes[0x180..]);
        PrimaryNode = BinaryPrimitives.ReadInt32LittleEndian(bytes[0x184..]);
        SecondaryBindingSlot = BinaryPrimitives.ReadInt32LittleEndian(bytes[0x188..]);
        SecondaryNode = BinaryPrimitives.ReadInt32LittleEndian(bytes[0x18C..]);
        CurrentTransform = ReadMatrix(bytes, 0xB0);
    }

    public static G1EffectEmitterFrame Read(G1EffectEmitterDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return new(declaration);
    }

    public int PrimaryBindingSlot { get; }
    public int PrimaryNode { get; }
    public int SecondaryBindingSlot { get; }
    public int SecondaryNode { get; }
    public bool RequiresSecondaryTransform => transformMode == 0x112;
    public bool IsPrepared { get; private set; }
    public Matrix4x4 CurrentTransform { get; private set; }
    public Matrix4x4 InverseTransform { get; private set; }
    public Matrix4x4 Velocity { get; private set; }
    public Vector3 InheritedScales => new(
        new Vector3(CurrentTransform.M11, CurrentTransform.M12, CurrentTransform.M13).Length(),
        new Vector3(CurrentTransform.M21, CurrentTransform.M22, CurrentTransform.M23).Length(),
        new Vector3(CurrentTransform.M31, CurrentTransform.M32, CurrentTransform.M33).Length());

    /// <summary>Mode 0111 takes all three transform components from the primary
    /// provider. Null means an actually absent provider, whose source transform
    /// is identity. A provider identity change refreshes at zero delta before
    /// the ordinary update and therefore clears velocity for that call.</summary>
    public void Update(Matrix4x4? primaryTransform, float deltaSeconds, bool bindingsChanged)
        => Update(primaryTransform, null, deltaSeconds, bindingsChanged);

    /// <summary>0112 takes rotation/translation from the primary provider and
    /// independent axis scales from the secondary. Only a negative declared
    /// binding is an absent owner; a required unresolved provider is rejected.</summary>
    public void Update(Matrix4x4? primaryTransform, Matrix4x4? secondaryTransform,
        float deltaSeconds, bool bindingsChanged)
    {
        Matrix4x4 next = primaryTransform ?? Matrix4x4.Identity;
        if (RequiresSecondaryTransform)
        {
            if (secondaryTransform is null && SecondaryBindingSlot >= 0)
                throw new NotSupportedException("Emitter secondary transform provider is unresolved.");
            Matrix4x4 secondary = secondaryTransform ?? Matrix4x4.Identity;
            next = Matrix(ScaleRow(Row(next, 0), Row(secondary, 0)),
                ScaleRow(Row(next, 1), Row(secondary, 1)),
                ScaleRow(Row(next, 2), Row(secondary, 2)), Row(next, 3));
        }
        if (!Matrix4x4.Invert(next, out var inverse))
            throw new NotSupportedException("Emitter provider transform is singular.");
        if (bindingsChanged || !IsPrepared || deltaSeconds <= 0) Velocity = default;
        else
        {
            Matrix4x4 relative = Multiply(CurrentTransform, inverse);
            relative.M11 -= 1; relative.M22 -= 1; relative.M33 -= 1; relative.M44 -= 1;
            Velocity = relative * (-1f / deltaSeconds);
        }
        CurrentTransform = next;
        InverseTransform = inverse;
        IsPrepared = true;
    }

    private static Vector4 ScaleRow(Vector4 primary, Vector4 secondary)
    {
        // A81200 -> 3FF980 normalizes XYZ with sqrtf and a distinct reciprocal;
        // no epsilon or zero-axis substitute exists. W is copied unscaled.
        float reciprocal = 1f / Length(primary);
        float x = NativeFloat.Multiply(primary.X, reciprocal);
        float y = NativeFloat.Multiply(primary.Y, reciprocal);
        float z = NativeFloat.Multiply(primary.Z, reciprocal);
        float scale = Length(secondary);
        return new(NativeFloat.Multiply(x, scale), NativeFloat.Multiply(y, scale),
            NativeFloat.Multiply(z, scale), primary.W);

        static float Length(Vector4 row)
        {
            float xy = NativeFloat.Add(NativeFloat.Multiply(row.X, row.X), NativeFloat.Multiply(row.Y, row.Y));
            return MathF.Sqrt(NativeFloat.Add(xy, NativeFloat.Multiply(row.Z, row.Z)));
        }
    }

    /// <summary>Write only the closed prepared emitter context and velocity
    /// fields into its actual mutable record. This does not reinterpret a raw
    /// declaration as an already prepared clone.</summary>
    public void WriteTo(Span<byte> emitter)
    {
        if (!IsPrepared) throw new InvalidOperationException("Emitter frame has no accepted provider update.");
        if (emitter.Length < 0x190) throw new InvalidDataException("Mutable emitter context is incomplete.");
        if (!wroteInitialization)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(emitter[0xA8..], 1);
            emitter.Slice(0x170, 16).Clear();
            wroteInitialization = true;
        }
        WriteMatrix(emitter, 0xB0, CurrentTransform);
        WriteMatrix(emitter, 0xF0, InverseTransform);
        WriteMatrix(emitter, 0x130, Velocity);
    }

    private static Matrix4x4 Multiply(Matrix4x4 a, Matrix4x4 b)
    {
        Vector4 R(Vector4 r) => (r.Z * Row(b, 2) + r.X * Row(b, 0)) + (r.W * Row(b, 3) + r.Y * Row(b, 1));
        return Matrix(R(Row(a, 0)), R(Row(a, 1)), R(Row(a, 2)), R(Row(a, 3)));
    }
    private static Vector4 Row(Matrix4x4 m, int row) => row switch
    {
        0 => new(m.M11, m.M12, m.M13, m.M14), 1 => new(m.M21, m.M22, m.M23, m.M24),
        2 => new(m.M31, m.M32, m.M33, m.M34), _ => new(m.M41, m.M42, m.M43, m.M44)
    };
    private static Matrix4x4 Matrix(Vector4 x, Vector4 y, Vector4 z, Vector4 w) => new(
        x.X, x.Y, x.Z, x.W, y.X, y.Y, y.Z, y.W, z.X, z.Y, z.Z, z.W, w.X, w.Y, w.Z, w.W);
    private static Matrix4x4 ReadMatrix(ReadOnlySpan<byte> bytes, int at)
    {
        Span<float> values = stackalloc float[16];
        for (int i = 0; i < 16; i++) values[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes[(at + i * 4)..]);
        return new(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7],
            values[8], values[9], values[10], values[11], values[12], values[13], values[14], values[15]);
    }
    private static void WriteMatrix(Span<byte> bytes, int at, Matrix4x4 matrix)
    {
        for (int row = 0; row < 4; row++)
        {
            Vector4 value = Row(matrix, row);
            BinaryPrimitives.WriteSingleLittleEndian(bytes[(at + row * 16)..], value.X);
            BinaryPrimitives.WriteSingleLittleEndian(bytes[(at + row * 16 + 4)..], value.Y);
            BinaryPrimitives.WriteSingleLittleEndian(bytes[(at + row * 16 + 8)..], value.Z);
            BinaryPrimitives.WriteSingleLittleEndian(bytes[(at + row * 16 + 12)..], value.W);
        }
    }
}
