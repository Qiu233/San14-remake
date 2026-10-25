using System.Buffers.Binary;

namespace San14.Data;

public sealed partial class G1EffectGenericRuntime
{
    private void ExecuteBirth(G1EffectParticleSlot slot, G1EffectParticleDefinition definition,
        G1EffectParticleBirthInputs inputs, ref bool parentReferenced)
    {
        foreach (var operation in Declaration.BirthOperations)
        {
            int subcode = operation.Subcode;
            if (operation.Opcode == 0 && subcode is 0 or 1 or 6 or 9)
            {
                var raw = definition.Record.Bytes;
                int I(int at) => BinaryPrimitives.ReadInt32LittleEndian(raw.Span[at..]);
                G1EffectRenderBirthWriter.Write(subcode, Declaration, slot.Bytes, context.Random,
                    new(unchecked(inputs.MaterialBase + I(8)), unchecked((uint)I(0xC)),
                        unchecked((uint)I(0x18)), unchecked((uint)I(0x1C)),
                        channel => definition.EvaluateScalar(0x5C + channel * 4, inputs.SourceContext, context.Random),
                        () => definition.EvaluateScalar(0x4C, inputs.SourceContext, context.Random)));
            }
            else if (operation.Opcode == 4)
            {
                // The managed parent is retained by the runtime, rather than
                // storing a native address in the particle's pointer field.
                if (inputs.ParentSlot is { } parent) { Reference(parent, 1); parentReferenced = true; }
            }
            else if (operation.Opcode is 5 or 11)
            {
                int sourceField, targetIndex;
                if (operation.Opcode == 5)
                {
                    (sourceField, targetIndex) = subcode switch
                    { 0 => (0x44, 4), 1 => (0x40, 28), 2 => (0x48, 29),
                        3 => (0x50, 30), 4 => (0x34, 40), 5 => (-1, 41),
                        _ => throw new NotSupportedException($"Particle scalar birth variant {subcode} is not implemented.") };
                }
                else
                {
                    if ((uint)subcode >= 4) throw new NotSupportedException("Unsupported particle color expression birth variant.");
                    sourceField = subcode switch { 0 => 0x5C, 1 => 0x60, 2 => 0x64, _ => 0x68 };
                    targetIndex = 36 + subcode;
                }
                int target = Int(0x20 + targetIndex * 4);
                if (target < 0) throw new InvalidDataException("Particle birth targets an absent scalar field.");
                float value = sourceField < 0 ? 1f : definition.EvaluateScalar(sourceField, inputs.SourceContext, context.Random);
                BinaryPrimitives.WriteSingleLittleEndian(slot.Bytes.Span[target..], value);
            }
            else if (operation.Opcode is 14 or 15)
            {
                int sourceIndex = subcode switch { 24 => 15, >= 48 and <= 55 => subcode - 15,
                    _ => throw new NotSupportedException($"Particle copy birth variant {subcode} is not implemented.") };
                int source = Int(0x20 + sourceIndex * 4), target = Int(0x20 + subcode * 4);
                if (source < 0 || target < 0) throw new InvalidDataException("Particle copy references an absent field.");
                int size = operation.Opcode == 14 ? 16 : 4;
                slot.Bytes.Span.Slice(source, size).CopyTo(slot.Bytes.Span.Slice(target, size));
            }
            else throw new NotSupportedException($"Generic {Record.Type} birth opcode {operation.Opcode}/{subcode} is not implemented.");
        }
    }
}
