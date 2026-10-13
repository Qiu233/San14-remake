using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Backend transport for source layouts with no second weight/index
/// attributes. bSkin2 must remain false; its inactive loads do not participate
/// in the original arithmetic. UInt8 selectors become positive signed32 values.</summary>
public enum ArmySkinVertexProfile { ThreeInfluenceSigned16, ThreeInfluenceUnsigned8, FourInfluenceUnsigned8 }
public sealed record ArmySkinVertexAdaptation(byte[] Bytes, string SourceSha256,
    string AdaptedSha256, int RemovedInputCount, int ReplacedInactiveLoads, int SelectorBitcasts);

public static class ArmySkinVertexAdapter
{
    private sealed record Instruction(ushort Code,uint[] Words);

    public static ArmySkinVertexProfile? Select(G1ModelGeometry geometry,int drawIndex)
    {
        var draw=G1ModelDrawReader.Read(geometry).Draws.Single(d=>d.Index==drawIndex);
        var layout=geometry.Layouts[draw.LayoutIndex];
        if(!layout.Attributes.Any(a=>a.SemanticCode is 1 or 2 or 0x0101 or 0x0102))return null;
        var additional=layout.Attributes.Where(a=>a.SemanticCode is 0x0101 or 0x0102).ToArray();
        if(additional.Length==2)return null;
        if(additional.Length!=0)throw new InvalidDataException("Army skin requires both additional source weight/index channels or neither.");
        var weight=layout.Attributes.Single(a=>a.SemanticCode==1);
        var index=layout.Attributes.Single(a=>a.SemanticCode==2);
        if(weight.TypeCode==2&&index.TypeCode==5)return ArmySkinVertexProfile.FourInfluenceUnsigned8;
        if(weight.TypeCode!=1)throw new InvalidDataException("Army inactive-input profile requires its actual supported stored source weights.");
        return index.TypeCode switch{7=>ArmySkinVertexProfile.ThreeInfluenceSigned16,
            5=>ArmySkinVertexProfile.ThreeInfluenceUnsigned8,
            _=>throw new InvalidDataException("Army inactive-input profile has an unsupported exact source selector encoding.")};
    }

    public static void ValidateGates(ReadOnlySpan<byte> completeVertex,ArmySkinVertexProfile profile)
    {
        if(!Enum.IsDefined(profile)||completeVertex.Length!=6256||BinaryPrimitives.ReadInt32LittleEndian(completeVertex.Slice(6240,4))!=0)
            throw new InvalidDataException("Army absent second weight/index inputs require the explicit original bSkin2=false gate.");
        if(profile==ArmySkinVertexProfile.FourInfluenceUnsigned8&&
            (BinaryPrimitives.ReadInt32LittleEndian(completeVertex.Slice(6208,4))!=1||
             BinaryPrimitives.ReadInt32LittleEndian(completeVertex.Slice(6224,4))!=1))
            throw new InvalidDataException("Army stored float3 flag weights require the exact source four-influence bSkin=[true,true,false] gates.");
    }

    /// <summary>The original one-record FOG/PSIZE/DEPTH stream retains its
    /// actual 48 bytes. A single actor draw broadcasts it through instance
    /// frequency, without extending or replacing the source vertex buffer.</summary>
    public static IReadOnlySet<int> SingleInstanceSourceStreams(G1ModelGeometry geometry,int drawIndex)
    {
        var draw=G1ModelDrawReader.Read(geometry).Draws.Single(d=>d.Index==drawIndex);
        var layout=geometry.Layouts[draw.LayoutIndex];
        var rows=layout.Attributes.Where(a=>a.SemanticCode is 4 or 11 or 12).ToArray();
        if(rows.Length==0)return new HashSet<int>();
        if(rows.Length!=3||rows.Select(a=>a.BufferReferenceSlot).Distinct().Count()!=1||
            rows.Any(a=>a.TypeCode!=3)||rows.Single(a=>a.SemanticCode==11).ByteOffset!=0||
            rows.Single(a=>a.SemanticCode==4).ByteOffset!=16||rows.Single(a=>a.SemanticCode==12).ByteOffset!=32)
            throw new InvalidDataException("Army single actor requires the exact source affine row stream profile.");
        int index=layout.BufferReferences[rows[0].BufferReferenceSlot];var buffer=geometry.VertexBuffers[index];
        if(buffer.VertexCount!=1||buffer.Stride!=48||buffer.Data.Length!=48||
            layout.Attributes.Any(a=>a.BufferReferenceSlot==rows[0].BufferReferenceSlot&&!rows.Contains(a)))
            throw new InvalidDataException("Army single actor requires one complete original 48-byte affine row record.");
        return new HashSet<int>{index};
    }

    public static DxbcProgramSignatures DeviceTransportSignatures(DxbcProgramSignatures signatures,ArmySkinVertexProfile profile)
    {
        if(profile is not (ArmySkinVertexProfile.ThreeInfluenceUnsigned8 or ArmySkinVertexProfile.FourInfluenceUnsigned8))return signatures;
        return signatures with{Parts=signatures.Parts.Select(p=>p.Kind!=DxbcSignatureKind.Input?p:p with{
            Parameters=p.Parameters.Select(v=>v.SemanticName.Text=="BLENDINDICES"&&v.SemanticIndex==0
                ?v with{ComponentType=1u}:v).ToArray()}).ToArray()};
    }

    /// <summary>Supplies zero for absent second skin channels and preserves the
    /// shader's signed selector arithmetic when the vertex layout uses unsigned bytes.
    /// Register roles come from the selected program's input semantics.</summary>
    public static ArmySkinVertexAdaptation Adapt(ShaderSpirv translation,
        DxbcProgramSignatures signatures, ArmySkinVertexProfile profile)
    {
        if (!Enum.IsDefined(profile) || translation.ExplicitDescriptorSet != 1)
            throw new InvalidDataException("Army skin transport requires a supported layout and an isolated vertex descriptor set.");
        var module = SpirvShaderInterfaceReader.Read(translation.Bytes);
        var inputs = signatures.Parts.Single(part => part.Kind == DxbcSignatureKind.Input).Parameters;
        uint Input(string semantic, uint index)
        {
            var matches = inputs.Where(input => input.SemanticName.Text == semantic && input.SemanticIndex == index).ToArray();
            if (matches.Length != 1)
                throw new InvalidDataException($"Army skin transport requires one {semantic}{index} input.");
            return matches[0].Register;
        }
        uint weight1 = Input("BLENDWEIGHT", 1), index1 = Input("BLENDINDICES", 1), index0 = Input("BLENDINDICES", 0);
        // The compiler maps source registers to input locations; debug names are optional.
        var removed=module.StageVariables.Where(v=>v.StorageClass==1&&(v.Location == weight1 || v.Location == index1)).ToArray();
        var selectors=module.StageVariables.Where(v=>v.StorageClass==1&&v.Location==index0).ToArray();
        if(removed.Length!=2||selectors.Length!=1||module.EntryPoints.Count!=1||
            module.EntryPoints[0].ExecutionModel!=0||selectors[0].Type is not {Kind:"vector",ComponentCount:4,ComponentType:{Kind:"int",Bits:32,Signed:true}})
            throw new InvalidDataException("Army skin source optional input/register inventory differs.");
        var selector=selectors[0];
        uint[] words=new uint[translation.Bytes.Length/4];
        for(int i=0;i<words.Length;i++)words[i]=BinaryPrimitives.ReadUInt32LittleEndian(translation.Bytes.AsSpan(i*4,4));
        var instructions=new List<Instruction>();
        for(int at=5;at<words.Length;){int n=(int)(words[at]>>16);instructions.Add(new((ushort)words[at],words.AsSpan(at,n).ToArray()));at+=n;}
        uint next=module.IdBound;
        uint Allocate()=>next++;
        uint[] Op(ushort code,params uint[] operands)=>new[]{((uint)(operands.Length+1)<<16)|code}.Concat(operands).ToArray();
        var removedIds=removed.Select(v=>v.VariableId).ToHashSet();
        // Derived pointers retain their value types when optional inputs become private.
        // Retain their complete pointer graph as initialized private constants.
        var pointerIds=removedIds.ToHashSet();
        var changedPointerTypes=new Dictionary<uint,uint>();
        var addedDeclarations=new List<uint[]>();
        uint Pointer(uint sourceType,uint storage)
        {
            var sourcePointer=instructions.Single(i=>i.Code==32&&i.Words[1]==sourceType);
            uint valueType=sourcePointer.Words[3];
            var existing=instructions.FirstOrDefault(i=>i.Code==32&&i.Words[2]==storage&&i.Words[3]==valueType);
            if(existing is not null)return existing.Words[1];
            if(changedPointerTypes.TryGetValue(sourceType,out uint prior))return prior;
            uint id=Allocate();changedPointerTypes.Add(sourceType,id);addedDeclarations.Add(Op(32,id,storage,valueType));return id;
        }
        var variables=removed.ToDictionary(v=>v.VariableId,v=>instructions.Single(i=>i.Code==59&&i.Words[2]==v.VariableId));
        var privatePointers=variables.ToDictionary(v=>v.Key,v=>Pointer(v.Value.Words[1],6));
        var zeroByType=new Dictionary<uint,uint>();
        var zeros=new Dictionary<uint,uint>();
        foreach(var variable in variables){uint type=instructions.Single(i=>i.Code==32&&i.Words[1]==variable.Value.Words[1]).Words[3];
            if(!zeroByType.TryGetValue(type,out uint zero)){zero=Allocate();zeroByType.Add(type,zero);addedDeclarations.Add(Op(46,type,zero));}zeros.Add(variable.Key,zero);}
        foreach(var i in instructions.Where(i=>i.Code is 65 or 66))if(pointerIds.Contains(i.Words[3]))
        {pointerIds.Add(i.Words[2]);_=Pointer(i.Words[1],6);}
        bool unsigned=profile is ArmySkinVertexProfile.ThreeInfluenceUnsigned8 or ArmySkinVertexProfile.FourInfluenceUnsigned8;
        uint unsignedScalar=0,unsignedVector=0,unsignedPointer=0,unsignedScalarPointer=0;
        if(unsigned){var scalar=instructions.FirstOrDefault(i=>i.Code==21&&i.Words[2]==32&&i.Words[3]==0);
            unsignedScalar=scalar?.Words[1]??Allocate();if(scalar is null)addedDeclarations.Add(Op(21,unsignedScalar,32,0));
            var vector=instructions.FirstOrDefault(i=>i.Code==23&&i.Words[2]==unsignedScalar&&i.Words[3]==4);
            unsignedVector=vector?.Words[1]??Allocate();if(vector is null)addedDeclarations.Add(Op(23,unsignedVector,unsignedScalar,4));
            var pointer=instructions.FirstOrDefault(i=>i.Code==32&&i.Words[2]==1&&i.Words[3]==unsignedVector);
            unsignedPointer=pointer?.Words[1]??Allocate();if(pointer is null)addedDeclarations.Add(Op(32,unsignedPointer,1,unsignedVector));
            var scalarPointer=instructions.FirstOrDefault(i=>i.Code==32&&i.Words[2]==1&&i.Words[3]==unsignedScalar);
            unsignedScalarPointer=scalarPointer?.Words[1]??Allocate();if(scalarPointer is null)addedDeclarations.Add(Op(32,unsignedScalarPointer,1,unsignedScalar));}
        var unsignedPointerIds=new HashSet<uint>();
        if(unsigned){unsignedPointerIds.Add(selector.VariableId);foreach(var i in instructions.Where(i=>i.Code is 65 or 66))
            if(unsignedPointerIds.Contains(i.Words[3])) {
                var pointer = instructions.Single(type => type.Code == 32 && type.Words[1] == i.Words[1]);
                var scalar = instructions.FirstOrDefault(type => type.Code == 21 && type.Words[1] == pointer.Words[3]);
                if (scalar is null || scalar.Words[2] != 32 || scalar.Words[3] != 1)
                    throw new InvalidDataException("Army selector access requires a signed 32-bit component.");
                unsignedPointerIds.Add(i.Words[2]);
            }}
        var output=new List<uint>(words.Take(5));bool emitted=false;int replaced=0,casts=0;
        var declared=new HashSet<uint>();var pending=addedDeclarations.ToList();
        void EmitReady()
        {
            bool changed;
            do{changed=false;foreach(var declaration in pending.ToArray()){
                ushort op=(ushort)declaration[0];uint dependency=op switch{21=>0,23=>declaration[2],32=>declaration[3],46=>declaration[1],_=>throw new InvalidDataException("Unexpected Army transport declaration.")};
                if(dependency!=0&&!declared.Contains(dependency))continue;
                output.AddRange(declaration);declared.Add(op==46?declaration[2]:declaration[1]);pending.Remove(declaration);changed=true;}}
            while(changed);
        }
        foreach(var instruction in instructions)
        {
            var w=instruction.Words;
            EmitReady();
            if(instruction.Code==54){if(pending.Count!=0)throw new InvalidDataException("Army transport declarations remain unresolved.");emitted=true;}
            if(instruction.Code==15){int start=3;while(start<w.Length){uint s=w[start++];if((s&255)==0||(s&0xFF00)==0||(s&0xFF0000)==0||(s&0xFF000000)==0)break;}
                var entry=w.Take(start).Concat(w.Skip(start).Where(id=>!removedIds.Contains(id))).ToArray();entry[0]=((uint)entry.Length<<16)|15u;output.AddRange(entry);continue;}
            if(instruction.Code is 5 or 71&&removedIds.Contains(w[1]))continue;
            if(instruction.Code==59&&removedIds.Contains(w[2])){output.AddRange(Op(59,privatePointers[w[2]],w[2],6,zeros[w[2]]));continue;}
            if(instruction.Code==59&&unsigned&&w[2]==selector.VariableId){output.AddRange(Op(59,unsignedPointer,w[2],1));continue;}
            if(instruction.Code is 65 or 66&&pointerIds.Contains(w[3])){var changed=w.ToArray();
                changed[1]=Pointer(w[1],6);output.AddRange(changed);continue;}
            if(instruction.Code is 65 or 66&&unsignedPointerIds.Contains(w[3])){var changed=w.ToArray();
                changed[1]=unsignedScalarPointer;output.AddRange(changed);continue;}
            if (instruction.Code == 61 && (pointerIds.Contains(w[3]) || unsignedPointerIds.Contains(w[3])) && w.Length != 4)
                throw new InvalidDataException("Army skin input load uses unsupported memory operands.");
            if(instruction.Code==61&&w.Length==4&&removedIds.Contains(w[3]))replaced++;
            if(instruction.Code==61&&unsigned&&w.Length==4&&unsignedPointerIds.Contains(w[3])){uint loaded=Allocate();
                uint loadType=w[3]==selector.VariableId?unsignedVector:unsignedScalar;
                output.AddRange(Op(61,loadType,loaded,w[3]));output.AddRange(Op(124,w[1],w[2],loaded));casts++;continue;}
            output.AddRange(w);
            if(instruction.Code is >=19 and <=39)declared.Add(w[1]);
            if(instruction.Code is >=41 and <=46)declared.Add(w[2]);
        }
        if (!emitted) throw new InvalidDataException("Army skin transport requires a vertex function.");
        output[3]=next;byte[] adapted=new byte[output.Count*4];
        for(int i=0;i<output.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(adapted.AsSpan(i*4,4),output[i]);
        return new(adapted,module.Sha256,Convert.ToHexString(SHA256.HashData(adapted)),2,replaced,casts);
    }
}
