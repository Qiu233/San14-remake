using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One actual outer emitter. The scheduler, local shape, prepared
/// transform context and definition state share the host's effect random stream.
/// Births go to the referenced generic's real allocator and ordered birth program.</summary>
public sealed class G1EffectEmitterRuntime
{
    private sealed record Target(G1EffectParticleDefinition Definition, byte[] Bytes,
        G1EffectGenericRuntime Generic);

    private readonly G1EffectContext context;
    private readonly G1EffectEmitterDeclaration declaration;
    private readonly List<Target> targets = [];
    private readonly byte[] current;
    private bool framePrepared;

    public G1EffectEmitterRuntime(G1EffectModule module, G1EffectEmitterDeclaration declaration,
        G1EffectContext context, Func<int, G1EffectGenericRuntime> genericLookup)
    {
        ArgumentNullException.ThrowIfNull(module); ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(genericLookup);
        this.context = context; this.declaration = declaration;
        current = declaration.Record.Bytes.ToArray();
        Scheduler = new(declaration);
        Frame = G1EffectEmitterFrame.Read(declaration);
        Shape = G1EffectShape.Read(declaration);
        foreach (var link in declaration.Definitions)
        {
            var definition = G1EffectParticleDefinition.Read(module, link.Declaration);
            byte[] mutable = definition.Record.Bytes.ToArray();
            definition = definition with { Record = definition.Record with { Bytes = mutable } };
            targets.Add(new(definition, mutable, genericLookup(link.Particle.FileOffset)));
        }
        Reset();
    }

    public G1EffectEmitter Scheduler { get; }
    public G1EffectEmitterFrame Frame { get; private set; }
    public G1EffectShape Shape { get; private set; }
    public ReadOnlyMemory<byte> LiveSourceContext => current;
    public bool CanEmit => Scheduler.CanEmit(record =>
    {
        var generic = targets.First(target => target.Generic.Record.FileOffset == record.FileOffset).Generic;
        return generic.Declaration.UsesDirectBirth ? generic.HasDirectStorage : generic.HasAllocatedPool;
    });
    public bool QueuedDirectEmission { get; private set; }

    public void SetEmissionEnabled(bool enabled)
    {
        Scheduler.SetEmissionEnabled(enabled);
        uint flags = U(4);
        W(4, enabled ? flags | 0x10u : flags & ~0x10u);
    }

    /// <summary>Reset retains the actual enable bit, restores all twelve
    /// authored source parameters and resets elapsed/residue. The system owner
    /// reapplies its explicitly set user parameters after this reset.</summary>
    public void Reset()
    {
        Scheduler.Reset();
        declaration.Record.Bytes.Span.Slice(0x3C, 48).CopyTo(current.AsSpan(0xC, 48));
        W(4, (declaration.Flags | 8u) & ~0x10u | (Scheduler.EmissionEnabled ? 0x10u : 0));
        W(8, 0f); W(0x6C, declaration.InitialRate); W(0x74, 0f);
        Frame = G1EffectEmitterFrame.Read(declaration);
        Shape = G1EffectShape.Read(declaration);
        QueuedDirectEmission = false; framePrepared = false;
    }

    public void SetParameter(int slot, float value)
    {
        int offset = slot switch
        {
            0 => 0x10, 1 => 0x18, 2 => 0xC, 3 => 0x14, 4 => 0x1C, 5 => 0x20,
            6 => 0x24, 7 => 0x28, 8 => 0x2C, 9 => 0x30, 10 => 0x34, 11 => 0x38,
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
        W(offset, value);
    }

    /// <summary>The module calls this for all emitters before generic update.
    /// Advance can also prepare an emitter when used on its own. A slot/node
    /// outside the actual system root requires its real transform provider.</summary>
    public void PrepareFrame(float deltaSeconds, Matrix4x4 actualWorldTransform)
    {
        if (Frame.PrimaryBindingSlot > 0 || Frame.PrimaryBindingSlot == 0 && Frame.PrimaryNode != 0)
            throw new NotSupportedException("Outer emitter requires a non-root transform provider.");
        Frame.Update(Frame.PrimaryBindingSlot < 0 ? null : actualWorldTransform, deltaSeconds, false);
        Frame.WriteTo(current);
        framePrepared = true;
    }

    public G1EffectEmitterStep Advance(float deltaSeconds, float moduleStrength, Matrix4x4 actualWorldTransform)
    {
        if (!framePrepared) PrepareFrame(deltaSeconds, actualWorldTransform);
        try
        {
            QueuedDirectEmission = false;
            var step = Scheduler.Advance(deltaSeconds, moduleStrength, (_, value) => Emit(value));
            W(4, step.Flags); W(8, step.ElapsedAfter); W(0x6C, step.Rate); W(0x74, step.Residue);
            return step;
        }
        finally { framePrepared = false; }
    }

    private void Emit(G1EffectEmitterStep step)
    {
        // Controllers and residue have already written their current values
        // when the source enters the shape/definition birth loops.
        W(4, step.Flags); W(0x6C, step.Rate); W(0x74, step.Residue);
        if (declaration.ShapeType == 11)
            throw new NotSupportedException("Outer shape eleven requires its actual fractional direct-birth provider.");
        if (step.Count < 1) return;
        bool hasOrdinary = false;
        foreach (var target in targets)
        {
            if (!target.Generic.Declaration.UsesDirectBirth) { hasOrdinary = true; continue; }
            if (!target.Generic.HasDirectStorage) continue;
            target.Generic.EmitDirect(target.Definition, current, step.Count);
            QueuedDirectEmission = true;
        }
        if (!hasOrdinary) return;
        Matrix4x4 emissionFrame = G1EffectBirthTransform.EmissionFrame(step.Flags, Shape.Transform);
        Vector4 shapeCenter = new(Shape.Transform.M41, Shape.Transform.M42, Shape.Transform.M43, Shape.Transform.M44);
        for (int remaining = step.Count - 1; remaining >= 0; remaining--)
        {
            // One sample is shared across all non-direct definitions in this
            // scheduled batch, including definitions whose count becomes zero.
            Vector4 sample = Shape.Sample(context.Random);
            float offset = step.BirthOffsetSeconds(remaining);
            float initialAge = step.InitialParticleAgeSeconds(remaining);
            foreach (var target in targets)
            {
                if (target.Generic.Declaration.UsesDirectBirth) continue;
                uint flags = target.Definition.Flags;
                flags = (flags & ~0x80u) | (((step.Flags & 0x200) != 0 || (flags & 0x40) != 0) ? 0x80u : 0);
                BinaryPrimitives.WriteUInt32LittleEndian(target.Bytes.AsSpan(4), flags);
                BinaryPrimitives.WriteSingleLittleEndian(target.Bytes.AsSpan(0x7C), F(0x20));
                var definition = target.Definition with { Flags = flags,
                    Record = target.Definition.Record with { Flags = flags } };
                bool worldSpace = (target.Generic.Declaration.Flags & 2) != 0;
                Vector4 position = G1EffectBirthTransform.CompensateMovement(sample, Frame.Velocity, offset, worldSpace);
                Matrix4x4 orientation = G1EffectBirthTransform.Orientation(flags, emissionFrame, sample, shapeCenter);
                float inheritanceScale = BinaryPrimitives.ReadSingleLittleEndian(target.Bytes.AsSpan(0x70));
                Vector4 inherited = G1EffectBirthTransform.InheritedVelocity(flags, sample, Frame.Velocity, inheritanceScale, worldSpace);
                BinaryPrimitives.WriteSingleLittleEndian(target.Bytes.AsSpan(0x80), inherited.X);
                BinaryPrimitives.WriteSingleLittleEndian(target.Bytes.AsSpan(0x84), inherited.Y);
                BinaryPrimitives.WriteSingleLittleEndian(target.Bytes.AsSpan(0x88), inherited.Z);
                BinaryPrimitives.WriteSingleLittleEndian(target.Bytes.AsSpan(0x8C), inherited.W);
                var inputs = new G1EffectParticleBirthInputs(orientation, position, Frame.CurrentTransform,
                    inherited, current);
                int count = definition.SampleCount(context.Random);
                for (int index = 0; index < count; index++)
                {
                    var birth = G1EffectDistribution.Apply(definition, inputs, index, count, context.Random);
                    _ = target.Generic.Birth(definition, birth, initialAge);
                }
            }
        }
    }

    private uint U(int at) => BinaryPrimitives.ReadUInt32LittleEndian(current.AsSpan(at));
    private float F(int at) => BinaryPrimitives.ReadSingleLittleEndian(current.AsSpan(at));
    private void W(int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(current.AsSpan(at), value);
    private void W(int at, float value) => BinaryPrimitives.WriteSingleLittleEndian(current.AsSpan(at), value);
}
