using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public sealed partial class G1EffectGenericRuntime
{
    private void ExecuteBirth(G1EffectParticleSlot slot, G1EffectParticleDefinition definition,
        G1EffectParticleBirthInputs inputs, ref bool parentReferenced)
    {
        foreach (var operation in Declaration.BirthOperations)
        {
            int subcode = operation.Subcode;
            if (operation.Opcode == 0 && subcode == 2) InitializeNestedPayload(slot.Bytes);
            else if (operation.Opcode == 0 && subcode is 0 or 1 or 6 or 9)
            {
                var raw = definition.Record.Bytes;
                int I(int at) => BinaryPrimitives.ReadInt32LittleEndian(raw.Span[at..]);
                G1EffectRenderBirthWriter.Write(subcode, Declaration, slot.Bytes, context.Random,
                    new(unchecked(inputs.MaterialBase + I(8)), unchecked((uint)I(0xC)),
                        unchecked((uint)I(0x18)), unchecked((uint)I(0x1C)),
                        channel => definition.EvaluateScalar(0x5C + channel * 4, inputs.SourceContext, context.Random),
                        () => definition.EvaluateScalar(0x4C, inputs.SourceContext, context.Random)));
            }
            else if (operation.Opcode == 1)
            {
                if (subcode is 0 or 1)
                {
                    PutVector(slot.Bytes,Field(0x38),Transform(inputs.Position,inputs.ParentWorld));
                    if (subcode == 1) PutVector(slot.Bytes,Field(0x44),inputs.Position);
                }
                else if (subcode is 8 or 9)
                {
                    Matrix4x4 local=Multiply(DefinitionRotation(definition,inputs),inputs.Orientation);
                    local.M41=inputs.Position.X;local.M42=inputs.Position.Y;
                    local.M43=inputs.Position.Z;local.M44=inputs.Position.W;
                    Matrix4x4 world=Multiply(local,inputs.ParentWorld);
                    if ((definition.Flags&0x80)!=0)
                    {
                        Vector4 position=Row(world,3);
                        world=Multiply(world,Basis(Inverse(inputs.ParentWorld)));
                        world.M41=position.X;world.M42=position.Y;world.M43=position.Z;world.M44=position.W;
                    }
                    PutMatrix(slot.Bytes,Field(0x34),world);
                    int localAt=Int(0x3C);
                    if(localAt>=0 && localAt!=Int(0x34)) PutMatrix(slot.Bytes,localAt,local);
                }
                else throw new NotSupportedException($"Particle transform birth variant {subcode} is not implemented.");
            }
            else if (operation.Opcode is 2 or 3)
            {
                if (subcode is not (0 or 1)) throw new NotSupportedException("Unsupported particle vector space.");
                Vector4 vector;
                int destination;
                if(operation.Opcode==2)
                {
                    vector=SampleDefinitionShape(definition,0x2C);
                    vector.W=0;
                    vector=Transform(vector,inputs.Orientation);
                    float scale=Float(definition.Record.Bytes,0x7C);
                    vector=new(vector.X*scale+inputs.InheritedVelocity.X,
                        vector.Y*scale+inputs.InheritedVelocity.Y,vector.Z*scale+inputs.InheritedVelocity.Z,0);
                    destination=Field(0x48);
                    if(subcode==0)
                        vector=Transform(vector,(definition.Flags&0x80)!=0 ? Basis(inputs.ParentWorld):inputs.ParentWorld);
                }
                else
                {
                    vector=SampleDefinitionShape(definition,0x38);
                    vector*=definition.EvaluateScalar(0x3C,inputs.SourceContext,context.Random);
                    float lane=vector.W;
                    vector=Transform(vector with { W=0 },inputs.Orientation);
                    if(subcode==0) vector=Transform(vector,Basis(inputs.ParentWorld));
                    vector.W=lane;
                    destination=Field(0x4C);
                }
                PutVector(slot.Bytes,destination,vector);
            }
            else if (operation.Opcode == 4)
            {
                // The managed parent is retained by the runtime, rather than
                // storing a native address in the particle's pointer field.
                if (Int(0x8C)>=0 && inputs.ParentSlot is { } parent)
                { Reference(parent, 1); parentReferenced = true; }
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
            else if (operation.Opcode is 6 or 7)
            {
                if(subcode is <0 or >3) throw new NotSupportedException("Unsupported particle size birth variant.");
                float scale=definition.EvaluateScalar(0x4C,inputs.SourceContext,context.Random);
                float actualScale=(Declaration.Flags&(1u<<17))!=0 ? 1f:scale;
                for(int axis=0;axis<subcode;axis++)
                {
                    int at=Field(0xA4+axis*4);
                    PutFloat(slot.Bytes,at,actualScale*TemplateFloat(at));
                }
                if(operation.Opcode==7)
                {
                    int at=Field(0x5C);
                    int templateAt=Int(0x150)<at ? at-Int(0x154):at;
                    Vector4 offset=Vector(Declaration.Layout,templateAt);
                    if((Declaration.Flags&(1u<<18))==0) {offset.X*=scale;offset.Y*=scale;offset.Z*=scale;}
                    PutVector(slot.Bytes,at,offset);
                    int transformAt=Field(0x34);
                    var matrix=Matrix(slot.Bytes,transformAt);
                    var translated=Transform(offset,matrix);
                    matrix.M41=translated.X;matrix.M42=translated.Y;matrix.M43=translated.Z;matrix.M44+=offset.W;
                    PutMatrix(slot.Bytes,transformAt,matrix);
                }
            }
            else if(operation.Opcode==10)
            {
                if((uint)subcode>=4) throw new NotSupportedException("Unsupported color baseline birth variant.");
                int color=Field(0x24),target=Field(0xB0+subcode*4);
                int channel=subcode switch {0=>2,1=>1,2=>0,_=>3};
                int value=(slot.Bytes.Span[color+channel]*4096+254)/255;
                PutFloat(slot.Bytes,target,value*0.000244140625f);
            }
            else if(operation.Opcode==18)
            {
                if(subcode is not (1 or 3)) throw new NotSupportedException("Unsupported size baseline birth variant.");
                for(int axis=0;axis<2;axis++)
                {
                    int target=Field(0x114+axis*4);
                    if(subcode==3) PutFloat(slot.Bytes,target,Float(slot.Bytes,Field(0xA4+axis*4)));
                    else
                    {
                        var row=Row(inputs.ParentWorld,axis);
                        float scale=MathF.Sqrt(row.X*row.X+row.Y*row.Y+row.Z*row.Z);
                        PutFloat(slot.Bytes,target,scale*Float(slot.Bytes,target));
                    }
                }
            }
            else if(operation.Opcode==19)
            {
                if(subcode is not (0 or 1)) throw new NotSupportedException("Unsupported inverse-basis birth variant.");
                var basis=Basis(inputs.Orientation);
                if(subcode==0) basis=Multiply(basis,Basis(inputs.ParentWorld));
                basis=Matrix4x4.Transpose(basis);
                PutMatrix(slot.Bytes,Field(0x68),basis);
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

    private void InitializeNestedPayload(Memory<byte> payload)
    {
        PutFloat(payload,0x140,0);
        int count=Word(Declaration.Layout,0x148);
        PutWord(payload,0x148,count);
        for(int index=0;index<count;index++)
        {
            int at=checked(0x14C+index*12);
            float initial=Float(Declaration.Layout,at);
            PutFloat(payload,at,initial);PutFloat(payload,at+4,initial);PutFloat(payload,at+8,0);
        }
        PutWord(payload,0x60,0xE0);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.Span[0x64..],1);
        BinaryPrimitives.WriteUInt64LittleEndian(payload.Span[0x68..],1);
        PutMatrix(payload,0x70,Matrix4x4.Identity);
        PutMatrix(payload,0xB0,Matrix4x4.Identity);
        payload.Span.Slice(0xF0,0x40).Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(payload.Span[0x130..],0);
    }
}
