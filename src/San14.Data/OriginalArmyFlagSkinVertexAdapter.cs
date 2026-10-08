using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Finite backend transport for the actual ColorVariation flag
/// A72FC040 VS1. Its absent second weight/index inputs are v2/v4 and its
/// UInt8 selector is v3. Original source arithmetic remains unchanged.</summary>
public static class OriginalArmyFlagSkinVertexAdapter
{
    public const string ShaderSha256 = OriginalArmyFlagMaterialVariations.GbufferShaderSha256;
    public const string VertexProgramSha256 = OriginalArmyFlagMaterialVariations.VertexProgramSha256;
    public const string TranslatedProgramSha256 = "4107A6A2D8DD3359FB39210CC3816E6CDF948466D71625330EAE0D8EED819905";
    private sealed record Instruction(ushort Code, uint[] Words);
    public static OriginalArmySkinVertexAdaptation Adapt(OriginalShaderSpirv translation,OriginalArmySkinVertexProfile profile)
    {
        if(profile!=OriginalArmySkinVertexProfile.FourInfluenceUnsigned8||translation.SourceProgramSha256!=VertexProgramSha256||translation.ExplicitDescriptorSet!=1)
            throw new InvalidDataException("Army skin backend adaptation requires exact source VS1 and explicit descriptor set1.");
        var module=SpirvShaderInterfaceReader.Read(translation.Bytes);
        if(module.Sha256!=TranslatedProgramSha256)
            throw new InvalidDataException("Army flag optional transport requires the exact source A72 VS1 translation bytes.");
        var removed=module.StageVariables.Where(v=>v.StorageClass==1&&v.Name is "v2" or "v4").ToArray();
        var selector=module.StageVariables.Single(v=>v.StorageClass==1&&v.Name=="v3");
        if(removed.Length!=2||removed.Any(v=>v.Location is null)||module.EntryPoints.Count!=1||
            module.EntryPoints[0].ExecutionModel!=0||selector.Type is not {Kind:"vector",ComponentCount:4,ComponentType:{Kind:"int",Bits:32,Signed:true}})
            throw new InvalidDataException("Army skin source optional input/register inventory differs.");
        uint[] words=new uint[translation.Bytes.Length/4];
        for(int i=0;i<words.Length;i++)words[i]=BinaryPrimitives.ReadUInt32LittleEndian(translation.Bytes.AsSpan(i*4,4));
        var instructions=new List<Instruction>();
        for(int at=5;at<words.Length;){int n=(int)(words[at]>>16);instructions.Add(new((ushort)words[at],words.AsSpan(at,n).ToArray()));at+=n;}
        uint next=module.IdBound;
        uint Allocate()=>next++;
        uint[] Op(ushort code,params uint[] operands)=>new[]{((uint)(operands.Length+1)<<16)|code}.Concat(operands).ToArray();
        var removedIds=removed.Select(v=>v.VariableId).ToHashSet();
        // Optional scalar selector loads use AccessChain in this exact module.
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
        bool unsigned=profile is OriginalArmySkinVertexProfile.ThreeInfluenceUnsigned8 or OriginalArmySkinVertexProfile.FourInfluenceUnsigned8;
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
            if(unsignedPointerIds.Contains(i.Words[3]))unsignedPointerIds.Add(i.Words[2]);}
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
            if(instruction.Code==61&&w.Length==4&&removedIds.Contains(w[3]))replaced++;
            if(instruction.Code==61&&unsigned&&w.Length==4&&unsignedPointerIds.Contains(w[3])){uint loaded=Allocate();
                uint loadType=w[3]==selector.VariableId?unsignedVector:unsignedScalar;
                output.AddRange(Op(61,loadType,loaded,w[3]));output.AddRange(Op(124,w[1],w[2],loaded));casts++;continue;}
            output.AddRange(w);
            if(instruction.Code is >=19 and <=39)declared.Add(w[1]);
            if(instruction.Code is >=41 and <=46)declared.Add(w[2]);
        }
        if(!emitted||replaced!=27||unsigned&&casts!=20)throw new InvalidDataException($"Army optional shader inputs have uses outside the exact backend profile: loads={replaced}, casts={casts}.");
        output[3]=next;byte[] adapted=new byte[output.Count*4];
        for(int i=0;i<output.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(adapted.AsSpan(i*4,4),output[i]);
        var check=SpirvShaderInterfaceReader.Read(adapted);
        if(check.Descriptors.Count!=module.Descriptors.Count||check.StageVariables.Count!=module.StageVariables.Count-2||
            check.StageVariables.Any(v=>v.Name is "v2" or "v4"))throw new InvalidDataException("Army backend adapter changed more than the explicit optional input transport.");
        return new(adapted,module.Sha256,Convert.ToHexString(SHA256.HashData(adapted)),2,replaced,casts);
    }
}
