using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data;
using San14.Data.Formats;

/// <summary>Exact generic state3 shadow input transport for source Army
/// layouts without the second skin weight/index channels. Original branch,
/// matrix and position math stays intact. Actual bSkin2=false is mandatory
/// at preparation and submission; unsigned8 selectors bitcast back before
/// the unchanged original signed uses.</summary>
public static class DeferredEarlyShadowArmyVertexTransport
{
    private sealed record Instruction(ushort Code,uint[] Words);
    private sealed record ProgramPin(string Container,string Vertex,string Spirv);
    private static readonly IReadOnlyDictionary<(string,int),ProgramPin> ProgramPins =
        new Dictionary<(string,int),ProgramPin>
    {
        [("san14://rdb/system/9DE72C62",5)]=new("6A4EE5D612C101F36A66518AC362664E19B1D169013D270BB909A8E832D54B41","DECFB37CAA471BB93CA9FC034AA9300F75248E8472C31D4752311293169E3435","1ACCBD8F36351C740F5768727530EEF6F8372B1B2C63C61FE83D52026CF7A4E8"),
        [("san14://rdb/system/9DE72C62",7)]=new("6A4EE5D612C101F36A66518AC362664E19B1D169013D270BB909A8E832D54B41","4C6A755FC406B8D6FCEF2311E43F08526AE9A074077D164F7A1F40AA98C62885","D366A4A94E9CB5324728B36C1330669466D5EE5B1A76A083ACFE9B83CA2C10E1"),
        [("san14://rdb/system/193FCD66",5)]=new("11BFCB4371B270DE4BC9B6A1E4ABADC5A24C29A6953E9D87A2F922F330C2FDDE","C8B92F41988B9E9BF93AF13AE3817992394368F26A9CDA37A220DD079D388DDA","EA9D6F001B57F5C7DB0EC1039ADE6F115C78AB2DDE549BD177FF629EC46F1D7F"),
        [("san14://rdb/system/193FCD66",7)]=new("11BFCB4371B270DE4BC9B6A1E4ABADC5A24C29A6953E9D87A2F922F330C2FDDE","CEA2C47CE1E3CDFB90333AEA6B98DCFACB9F222D2C7B039DA1D1F940BCD4407F","253275FAD65B01F53B433B4B68E95CD0AFD5B2D348E3C6BD0F3B09CFAD1CB2CC"),
        [("san14://rdb/system/7B385503",5)]=new("696396F19DA4DD9F35F04CB6F3814144793973D8556E7009FFDEDF13DFB7E9C1","E917050AD8BA4B26838829FD73880F49AF039F1D1DAFBA58D2AD131D4CC87B1F","7DAE0916D1A4D5B580D3045D954C1FB8026E941F924D70F411742C71998E44CD"),
        [("san14://rdb/system/7B385503",7)]=new("696396F19DA4DD9F35F04CB6F3814144793973D8556E7009FFDEDF13DFB7E9C1","8FDE6342B5BD87766CE78EE4F649712E0CF4E4264E563821D4E64BD8CE1496D1","AA636D6C33234EAE1A56DA77734062607CFEF3599D3C4777698076208E22A977"),
    };

    public static ModelShaderProgramPreparation Adapt(
        ModelShaderProgramPreparation prepared,ArmySkinVertexProfile profile)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        var transport=AdaptRaw(prepared,prepared.VertexTranslation,profile);
        var viewport=VertexShaderAdapter.AdaptPositionInput(transport.Bytes);
        var bindings=TranslatedShaderBindingReader.Read(
            prepared.Source.ReadResourceDefinitions(prepared.VertexProgramIndex),viewport.Bytes);
        return prepared with { VertexAdapter=viewport,VertexBindings=bindings,
            VertexDeviceTransportSignatures=ArmySkinVertexAdapter.DeviceTransportSignatures(prepared.VertexSignatures,profile) };
    }

    public static void RequireCurrentGates(TranslatedShaderBindings actualBindings,
        ArmySkinVertexProfile profile,ModelStageInputs actualVertex)
    {
        var buffer=actualBindings.SourceDefinitions.Buffers.Single();
        var skin=buffer.Variables.Single(v=>v.Name.Text=="bSkin");
        if(buffer.ByteSize is not (6256 or 14448)||skin.StartOffset!=6208||skin.ByteSize!=36||
            !actualVertex.Constants.TryGetValue(0,out var cb)||cb.Length!=buffer.ByteSize)
            throw new InvalidDataException("Early19 Army input transport needs the exact selected VS CB0 and actual skin fields.");
        ArmySkinVertexAdapter.ValidateGates(cb.Span[..6256],profile);
    }

    public static ArmySkinVertexAdaptation AdaptRaw(ModelShaderProgramPreparation prepared,ShaderSpirv translation,ArmySkinVertexProfile profile)
    {
        if(!Enum.IsDefined(profile)||translation.ExplicitDescriptorSet!=1||
            !ProgramPins.TryGetValue((prepared.Source.Source.ResourceUri,prepared.VertexProgramIndex),out var pin)||
            prepared.Source.Source.DecodedSha256!=pin.Container||translation.SourceProgramSha256!=pin.Vertex||
            translation.CompilerLibrarySha256!=ShaderCompilerRuntime.VerifiedLibrarySha256)
            throw new InvalidDataException("Early19 Army transport requires its pinned source state3 shader/VS and bundled compiler.");
        var module=SpirvShaderInterfaceReader.Read(translation.Bytes);
        if(module.Sha256!=pin.Spirv)
            throw new InvalidDataException("Early19 Army optional input transport requires its exact original translated module.");
        var inputs=prepared.VertexSignatures.Parts.Single(p=>p.Kind==DxbcSignatureKind.Input).Parameters;
        string Input(string semantic,uint index)=>"v"+inputs.Single(p=>p.SemanticName.Text==semantic&&p.SemanticIndex==index).Register;
        string weight1=Input("BLENDWEIGHT",1),index1=Input("BLENDINDICES",1),index0=Input("BLENDINDICES",0);
        var removed=module.StageVariables.Where(v=>v.StorageClass==1&&(v.Name==weight1||v.Name==index1)).ToArray();
        var selector=module.StageVariables.Single(v=>v.StorageClass==1&&v.Name==index0);
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
        int expectedLoads=prepared.Source.Source.FileId==0x7B385503?2:17;
        int expectedCasts=prepared.Source.Source.FileId==0x7B385503?3:20;
        if(!emitted||replaced!=expectedLoads||unsigned&&casts!=expectedCasts)
            throw new InvalidDataException($"Army optional shader inputs have uses outside the exact backend profile: {prepared.Source.Source.FileId:X8}/VS{prepared.VertexProgramIndex}, loads={replaced}, casts={casts}.");
        output[3]=next;byte[] adapted=new byte[output.Count*4];
        for(int i=0;i<output.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(adapted.AsSpan(i*4,4),output[i]);
        var check=SpirvShaderInterfaceReader.Read(adapted);
        if(check.Descriptors.Count!=module.Descriptors.Count||check.StageVariables.Count!=module.StageVariables.Count-2||
            check.StageVariables.Any(v=>v.Name==weight1||v.Name==index1))throw new InvalidDataException("Army backend adapter changed more than the explicit optional input transport.");
        return new(adapted,module.Sha256,Convert.ToHexString(SHA256.HashData(adapted)),2,replaced,casts);
    }
}
