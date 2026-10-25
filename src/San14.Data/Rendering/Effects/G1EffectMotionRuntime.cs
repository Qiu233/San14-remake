using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual motion integration, module fields and parent-space conversion.
/// The parent resolver binds the source transform owner through managed state,
/// rather than writing native pointers into particle bytes.</summary>
public sealed partial class G1EffectMotionRuntime
{
    private readonly G1EffectContext context;
    private readonly G1EffectAuxiliaryRuntime auxiliaries;
    private readonly HashSet<int> prepared = [];
    public Func<G1EffectRecord, Memory<byte>, ReadOnlyMemory<byte>>? ParentStateResolver { get; set; }

    public G1EffectMotionRuntime(G1EffectModule module, IReadOnlyList<G1EffectRecord> auxiliaries,
        G1EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(module); ArgumentNullException.ThrowIfNull(context);
        this.context = context;
        this.auxiliaries = new(auxiliaries, context);
    }

    public void Advance(float seconds) => auxiliaries.Advance(seconds);

    public IEnumerable<G1EffectObjectDataPatch> EnumeratePreparedData() => auxiliaries.EnumeratePreparedData();
    public IEnumerable<G1EffectObjectDataPatch> EnumerateLiveData() => auxiliaries.EnumerateLiveData();

    public void Apply(G1EffectRecord generic, Memory<byte> particle, float seconds)
    {
        ArgumentNullException.ThrowIfNull(generic);
        int field = generic.Type == 3 ? 0x200 : 0x204;
        int offset = I(generic.Bytes, field);
        if (offset == 0 || (H(particle, 6) & 16) != 0) return;
        int at = checked(field + offset);
        Memory<byte> declaration;
        // The runtime supplies a mutable generic clone. MemoryMarshal retains
        // that owned storage so standalone descriptor preparation persists.
        if (!System.Runtime.InteropServices.MemoryMarshal.TryGetArray(generic.Bytes, out ArraySegment<byte> array) || array.Array is null)
            throw new ArgumentException("Particle motion requires the runtime's mutable generic clone.", nameof(generic));
        declaration = array.Array.AsMemory(checked(array.Offset + at), generic.Bytes.Length - at);
        if (!prepared.Contains(generic.FileOffset))
        {
            PrepareMotion(declaration);
            prepared.Add(generic.FileOffset);
        }
        ApplyMotion(declaration, checked(generic.FileOffset + at), generic, particle, seconds);
    }

    private static void PrepareMotion(Memory<byte> descriptor)
    {
        switch (I(descriptor, 4))
        {
            case 0: case 3: return;
            case 6: PrepareMotion(descriptor[0x10..]); return;
            case 9:
                PrepareMotion(descriptor[0x20..]);
                G1EffectPostTransform.Initialize(descriptor[checked(0x10 + I(descriptor, 0x10))..]);
                return;
            default: throw new NotSupportedException($"Effect motion descriptor {I(descriptor, 4)} is not implemented.");
        }
    }

    private void ApplyMotion(ReadOnlyMemory<byte> descriptor, int fileOffset, G1EffectRecord generic,
        Memory<byte> particle, float dt)
    {
        switch (I(descriptor, 4))
        {
            case 0:
            {
                if (dt <= 1.1920929e-05f) return;
                Vector4 force = default;
                int count = I(descriptor, 0x10);
                if (count < 0) throw new InvalidDataException("Negative particle force count.");
                for (int index = 0; index < count; index++)
                {
                    int field = checked(0x14 + index * 4), at = checked(field + I(descriptor, field));
                    force = Force(descriptor[at..], checked(fileOffset + at), generic, particle, dt, force);
                }
                int velocityAt = I(generic.Bytes, 0x48), positionAt = I(generic.Bytes, 0x44);
                Vector4 velocity = V(particle, velocityAt);
                float factor = dt / F(particle, I(generic.Bytes, 0x90));
                velocity = new(force.X * factor + velocity.X, force.Y * factor + velocity.Y,
                    force.Z * factor + velocity.Z, force.W * factor + velocity.W);
                PutV(particle, velocityAt, velocity);
                Vector4 position = V(particle, positionAt);
                PutV(particle, positionAt, new(dt * velocity.X + position.X, dt * velocity.Y + position.Y,
                    dt * velocity.Z + position.Z, dt * velocity.W + position.W));
                return;
            }
            case 3: return; // The installed motion operation itself is empty.
            case 6:
            {
                ApplyMotion(descriptor[0x10..], checked(fileOffset + 0x10), generic, particle, dt);
                ReadOnlyMemory<byte> parent = Parent(generic, particle);
                Vector4 position = V(particle, I(generic.Bytes, 0x44));
                PutV(particle, I(generic.Bytes, 0x38), Transform(position, parent, 0x10));
                if ((generic.Bytes.Span[10] & 1) != 0)
                {
                    InheritScale(generic, particle, parent, 0x114, 0xA4, 0x10);
                    InheritScale(generic, particle, parent, 0x118, 0xA8, 0x20);
                }
                return;
            }
            case 9:
            {
                ApplyMotion(descriptor[0x20..], checked(fileOffset + 0x20), generic, particle, dt);
                int post = checked(0x10 + I(descriptor, 0x10));
                // Some post transforms use no parent. The precise consumer will
                // require one only for the branches which actually dereference it.
                ReadOnlyMemory<byte> parent = ParentStateResolver?.Invoke(generic, particle) ?? default;
                G1EffectPostTransform.Apply(descriptor[post..], generic, particle, dt, parent);
                return;
            }
            default: throw new NotSupportedException($"Effect motion descriptor {I(descriptor, 4)} is not implemented.");
        }
    }

    private Vector4 Force(ReadOnlyMemory<byte> descriptor, int fileOffset, G1EffectRecord generic,
        Memory<byte> particle, float dt, Vector4 sum)
    {
        switch (I(descriptor, 4))
        {
            case 0:
                return AddScaled(sum, V(descriptor, 0x10), F(particle, I(generic.Bytes, 0x90)));
            case 2:
            {
                Vector4 velocity = V(particle, I(generic.Bytes, 0x48));
                float damping = F(particle, I(generic.Bytes, 0x94));
                return new(sum.X - damping * velocity.X, sum.Y - damping * velocity.Y,
                    sum.Z - damping * velocity.Z, sum.W - damping * velocity.W);
            }
            case 3:
            {
                Vector4 target = SampleField(descriptor[0x10..], checked(fileOffset + 0x10), generic, particle);
                Vector4 velocity = V(particle, I(generic.Bytes, 0x48));
                float factor = F(particle, I(generic.Bytes, 0x94));
                return new((target.X - velocity.X) * factor + sum.X, (target.Y - velocity.Y) * factor + sum.Y,
                    (target.Z - velocity.Z) * factor + sum.Z, (target.W - velocity.W) * factor + sum.W);
            }
            case 15:
            {
                int cursor = 0x1C;
                Vector4 value = ExpressionVector(descriptor, ref cursor, generic, particle, particle);
                return new(value.X + sum.X, value.Y + sum.Y, value.Z + sum.Z, value.W + sum.W);
            }
            default: throw new NotSupportedException($"Particle force operation {I(descriptor, 4)} is not implemented.");
        }
    }

    private Vector4 SampleField(ReadOnlyMemory<byte> descriptor, int fileOffset,
        G1EffectRecord generic, Memory<byte> particle)
    {
        int type = H(descriptor, 4);
        if (type != 0) throw new NotSupportedException($"Particle field sampler {type} is not implemented.");
        int target = checked(fileOffset + 0x10 + I(descriptor, 0x10));
        return auxiliaries.Sample(target, V(particle, I(generic.Bytes, 0x44)));
    }

    private ReadOnlyMemory<byte> Parent(G1EffectRecord generic, Memory<byte> particle)
    {
        ReadOnlyMemory<byte> parent = ParentStateResolver?.Invoke(generic, particle) ?? default;
        if (parent.IsEmpty) throw new InvalidOperationException("Particle motion has no bound live parent transform owner.");
        return parent;
    }
    private static void InheritScale(G1EffectRecord generic, Memory<byte> particle,
        ReadOnlyMemory<byte> parent, int originalField, int destinationField, int row)
    {
        int original = I(generic.Bytes, originalField);
        if (original < 0) return;
        Vector4 axis = V(parent, row);
        float scale = MathF.Sqrt(axis.Y * axis.Y + axis.X * axis.X + axis.Z * axis.Z);
        PutF(particle, I(generic.Bytes, destinationField), scale * F(particle, original));
    }
    private static Vector4 Transform(Vector4 value, ReadOnlyMemory<byte> matrix, int at) => new(
        (value.Z * F(matrix, at + 32) + value.X * F(matrix, at)) + (value.W * F(matrix, at + 48) + value.Y * F(matrix, at + 16)),
        (value.Z * F(matrix, at + 36) + value.X * F(matrix, at + 4)) + (value.W * F(matrix, at + 52) + value.Y * F(matrix, at + 20)),
        (value.Z * F(matrix, at + 40) + value.X * F(matrix, at + 8)) + (value.W * F(matrix, at + 56) + value.Y * F(matrix, at + 24)),
        (value.Z * F(matrix, at + 44) + value.X * F(matrix, at + 12)) + (value.W * F(matrix, at + 60) + value.Y * F(matrix, at + 28)));
    private static Vector4 AddScaled(Vector4 sum, Vector4 value, float scale) =>
        new(value.X * scale + sum.X, value.Y * scale + sum.Y, value.Z * scale + sum.Z, value.W * scale + sum.W);
    private static void Require(int size, int at, int length)
    {
        if (at < 0 || length < 0 || at > size - length)
            throw new InvalidDataException("Particle motion escapes its declaration or live state.");
    }
    private static int I(ReadOnlyMemory<byte> bytes, int at)
    { Require(bytes.Length, at, 4); return BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[at..]); }
    private static ushort H(ReadOnlyMemory<byte> bytes, int at)
    { Require(bytes.Length, at, 2); return BinaryPrimitives.ReadUInt16LittleEndian(bytes.Span[at..]); }
    private static float F(ReadOnlyMemory<byte> bytes, int at)
    { Require(bytes.Length, at, 4); return BinaryPrimitives.ReadSingleLittleEndian(bytes.Span[at..]); }
    private static Vector4 V(ReadOnlyMemory<byte> bytes, int at) =>
        new(F(bytes, at), F(bytes, checked(at + 4)), F(bytes, checked(at + 8)), F(bytes, checked(at + 12)));
    private static void PutF(Memory<byte> bytes, int at, float value)
    { Require(bytes.Length, at, 4); BinaryPrimitives.WriteSingleLittleEndian(bytes.Span[at..], value); }
    private static void PutV(Memory<byte> bytes, int at, Vector4 value)
    { PutF(bytes, at, value.X); PutF(bytes, at + 4, value.Y); PutF(bytes, at + 8, value.Z); PutF(bytes, at + 12, value.W); }
}
