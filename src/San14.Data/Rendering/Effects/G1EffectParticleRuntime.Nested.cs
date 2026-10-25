using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class G1EffectGenericRuntime
{
    private sealed record NestedDefinition(G1EffectParticleDefinition Definition,
        G1EffectGenericRuntime Target, byte[] MutableBytes);
    private sealed record NestedEmitter(uint Flags, int StateIndex, G1EffectShape Shape,
        bool HasModifier, IReadOnlyList<NestedDefinition> Definitions);
    private IReadOnlyList<NestedEmitter>? nestedEmitters;

    private IReadOnlyList<NestedEmitter> PreparedNestedEmitters()
    {
        if (nestedEmitters is not null) return nestedEmitters;
        var result = new List<NestedEmitter>();
        foreach (var record in Record.Children.Where(record => record.Table == G1EffectTable.Controller))
        {
            int at = checked(record.FileOffset - Record.FileOffset);
            int size = Int(at);
            if (at < 0 || size < 0x28 || at > CurrentGenericBytes.Length - size)
                throw new InvalidDataException("Nested emitter escapes its owning generic.");
            uint flags = unchecked((uint)Int(at + 4)) | 8;
            int stateIndex = Int(at + 0xC);
            int shapeAt = checked(at + 0x10 + Int(at + 0x10));
            int shapeLength = Int(shapeAt);
            if (shapeAt < at || shapeLength < 0x50 || shapeAt > at + size - shapeLength)
                throw new InvalidDataException("Nested emitter shape escapes its declaration.");
            var shape = G1EffectShape.Read(unchecked((uint)Int(shapeAt + 4)),
                CurrentGenericBytes.AsMemory(shapeAt, shapeLength));
            int count = Int(at + 8), table = checked(at + 0x14 + Int(at + 0x14));
            var definitions = new List<NestedDefinition>(count);
            for (int index = 0; index < count; index++)
            {
                int entry = checked(table + index * 4), target = checked(entry + Int(entry));
                int fileOffset = checked(Record.FileOffset + target);
                var declaration = Record.Children.FirstOrDefault(child =>
                    child.Table == G1EffectTable.NestedParticleDefinition && child.FileOffset == fileOffset)
                    ?? throw new InvalidDataException("Nested emitter references an absent definition.");
                byte[] mutable = declaration.Bytes.ToArray();
                uint definitionFlags = BinaryPrimitives.ReadUInt32LittleEndian(mutable.AsSpan(4));
                definitionFlags = (flags & 0x400) != 0 || (definitionFlags & 0x40) != 0
                    ? definitionFlags | 0x80U : definitionFlags & ~0x80U;
                BinaryPrimitives.WriteUInt32LittleEndian(mutable.AsSpan(4), definitionFlags);
                var current = declaration with { Bytes = mutable, Flags = definitionFlags };
                var definition = G1EffectParticleDefinition.Read(module, current);
                var childRuntime = Children.FirstOrDefault(child =>
                    child.Record.FileOffset == definition.Particle.Record.FileOffset)
                    ?? throw new InvalidDataException("Nested definition has no owned child generic.");
                definitions.Add(new(definition, childRuntime, mutable));
            }
            result.Add(new(flags, stateIndex, shape, Int(at + 0x18) != 0, definitions.AsReadOnly()));
        }
        nestedEmitters = result.AsReadOnly();
        return nestedEmitters;
    }

    /// <summary>Run after parent motion and the ordinary child-generic update;
    /// the initial birth path calls this separately after its initial update.</summary>
    private void EmitNested(G1EffectParticleSlot parent, float dt)
    {
        if (Record.Type != 3 || !parent.IsAllocated || (parent.Bytes.Span[6] & 1) == 0) return;
        foreach (var emitter in PreparedNestedEmitters())
        {
            if ((emitter.Flags & 0x200) != 0)
                throw new NotSupportedException("Nested direct-parent emission state is not implemented.");
            int state = checked(0x14C + emitter.StateIndex * 12);
            float amount = Float(parent.Bytes, state + 4);
            uint mode = emitter.Flags & 7;
            if (mode == 1) amount *= dt;
            else if (mode == 2) amount = emitter.Shape.Measure * amount * dt;
            if ((emitter.Flags & 0x20) != 0)
                amount *= ModuleEmissionScale ??
                    throw new InvalidOperationException("Nested emitter has no current module emission scale.");
            if (emitter.HasModifier)
                throw new NotSupportedException("Nested emitter scalar modifier is not implemented.");
            float accumulated = amount + Float(parent.Bytes, state + 8);
            int batches = (int)accumulated;
            float residue = accumulated - batches;
            PutFloat(parent.Bytes, state + 8, residue);
            if (batches < 1) continue;

            Matrix4x4 frame = G1EffectBirthTransform.EmissionFrame(emitter.Flags, emitter.Shape.Transform);
            Matrix4x4 parentWorld = Matrix(parent.Bytes, 0x70);
            Matrix4x4 parentVelocity = Matrix(parent.Bytes, 0xF0);
            bool spread = (emitter.Flags & 0x40) != 0 && dt > 0;
            float spacing = spread ? dt / amount : 0;
            float initialStep = (emitter.Flags & 0x100) != 0 ? dt : 0;
            for (int remaining = batches - 1; remaining >= 0; remaining--)
            {
                float offset = spread ? remaining * spacing + residue * spacing : 0;
                Vector4 sample = emitter.Shape.Sample(context.Random);
                foreach (var linked in emitter.Definitions)
                {
                    var definition = linked.Definition;
                    bool worldSpace = (definition.Particle.Flags & 2) != 0;
                    Vector4 position = G1EffectBirthTransform.CompensateMovement(sample, parentVelocity, offset, worldSpace);
                    var orientation = G1EffectBirthTransform.Orientation(definition.Flags, frame, sample, Row(emitter.Shape.Transform, 3));
                    Vector4 velocity = G1EffectBirthTransform.InheritedVelocity(definition.Flags, sample, parentVelocity,
                        Float(definition.Record.Bytes, 0x70), worldSpace);
                    int multiplierField = Int(0xC4);
                    float multiplier = multiplierField < 0 ? 1f : Float(parent.Bytes, multiplierField);
                    PutFloat(linked.MutableBytes, 0x7C, multiplier);
                    try
                    {
                        var inputs = new G1EffectParticleBirthInputs(orientation, position,
                            parentWorld, velocity, parent.Bytes, ParentSlot: parent);
                        int count = definition.SampleCount(context.Random);
                        for (int index = 0; index < count; index++)
                        {
                            var distributed = G1EffectDistribution.Apply(definition, inputs, index, count, context.Random);
                            _ = linked.Target.Birth(definition, distributed, offset + initialStep);
                        }
                    }
                    finally { PutFloat(linked.MutableBytes, 0x7C, 1f); }
                }
            }
        }
    }

    /// <summary>The type-three early-retirement emission test. The caller also
    /// tests its actual parent reference count before retiring the slot.</summary>
    private bool NestedCanContinue(G1EffectParticleSlot parent)
    {
        int count = Int(0x218);
        for (int index = 0; index < count; index++)
            if (Float(parent.Bytes, checked(0x150 + index * 12)) > 0.0011920928955078125f) return true;
        int end = checked(0x14C + Word(parent.Bytes, 0x148) * 12);
        foreach (var controller in Controllers(0x204))
        {
            int target = Word(controller, 8);
            if (target > 0x14B && target < end && G1EffectParticleControllers.CanContinue(controller, parent.Bytes))
                return true;
        }
        return false;
    }
}
