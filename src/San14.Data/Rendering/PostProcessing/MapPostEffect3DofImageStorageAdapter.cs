using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record MapPostEffect3DofImageStorageAdaptation(byte[] Bytes,string SourceProgramSha256,
    string DefaultSpirvSha256,string AdaptedSpirvSha256,
    IReadOnlyList<MapPostEffect3DofSpriteStorageDescriptorProof> DescriptorProofs,
    int RetainedInstructions,int RemovedImageLoads,int AddedInstructions,string SourceEvidence)
{
    public bool ProvesExecutionAdmission=>false;
    public bool ProvesGpuContents=>false;
    public bool ProvesSpritePrefixPublication=>false;
}

/// <summary>Exact original CS47/48/49 shader creation interface. Retains the
/// default official translation's complete float4 UAV stores, converts native
/// B6/B7 RGBA16F texels into two uint32 words using D3D11 RTZ, and represents
/// original B5 raw atomic addresses as uint32 SSBO pointers. Every other
/// instruction, including Texture2D loads, loops, workgroup memory and barriers,
/// is byte-identical. This supplies shader creation only; the existing selected
/// draw/dispatch gates continue rejecting these unsupported execution paths.</summary>
public static class MapPostEffect3DofImageStorageAdapter
{
    public static string SourceSha256(int index)=>index switch{
        47=>"93389903ECB685317EEBC5B7E1C749BF6A9D11939E103FC4B85FC607244547D7",
        48=>"C5024496FF482FF914C501ACB1B1DEADA432AD847711958B1FD39E1AF7E46AA2",
        49=>"2721200CC1073DC8066E99B5E1C98534AE7CA173B40BDE93D1CF045068E63885",
        _=>throw new NotSupportedException("DOF image program profile requires exact source CS47..49.")};
    public static string DefaultTranslationSha256(int index)=>index switch{
        47=>"8EFCDDB80336FF7DCE8A50D7871884E4876532E29441C1957B3A76AA3105B6C9",
        48=>"8A4FEAD97406AB03EA27DF7C5C45BF8B12C29F97814C99D191583CCD680C6E73",
        49=>"51154EC614DA0DF446E4125D59158B3CB02388D240811D2540BAA6963F930DA5",
        _=>throw new NotSupportedException("DOF image program profile requires exact source CS47..49.")};
    public static MapPostEffect3DofImageStorageAdaptation Adapt(MapPostEffect3Container actualModule,
        MapPostEffect3Program source,ShaderSpirv defaultTranslation)
    {
        ArgumentNullException.ThrowIfNull(actualModule);ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(defaultTranslation);
        string sourceSha=SourceSha256(source.StageIndex),defaultSha=DefaultTranslationSha256(source.StageIndex);
        if(actualModule.Sha256!=MapFinalOutputPostEffect3Source.PayloadSha256||Hash(actualModule.Bytes.Span)!=actualModule.Sha256||
            source.StageCode!=5||source.Sha256!=sourceSha||Hash(source.Bytes.Span)!=sourceSha||
            actualModule.GetProgram(5,source.StageIndex).Sha256!=sourceSha||defaultTranslation.SourceProgramSha256!=sourceSha||
            defaultTranslation.SpirvSha256!=defaultSha||Hash(defaultTranslation.Bytes)!=defaultSha||
            defaultTranslation.BufferUavsAsStorageBuffers||defaultTranslation.ExplicitDescriptorSet is not null)
            throw new InvalidDataException("DOF image shader creation requires exact original module/CS47..49/default official translation; changed source or compiler output must be audited.");
        var allocation=MapPostEffect3AllocationReader.Read(actualModule,640,360,"source-program-interface-only");
        foreach(int parameter in new[]{5,6,7})
        {
            var native=allocation.BufferAllocations.Single(b=>b.Parameter.Index==parameter);
            var shape=MapPostEffect3AllocationBufferShapeReader.Read(native);
            if(parameter==5?shape.ByteCount!=32||shape.ViewElementCount!=8||shape.ViewFormat!=39||
                shape.Dimension!=MapPostEffect3BufferDimension.Raw:
                shape.ByteCount!=2073600||shape.ViewElementCount!=259200||shape.ViewFormat!=10||
                shape.Dimension!=MapPostEffect3BufferDimension.Typed||shape.StructureByteStride!=0)
                throw new InvalidDataException("DOF exact raw B5 or typed RGBA16F B6/B7 physical byte/view profile differs.");
        }
        var rdef=DxbcResourceDefinitionReader.Read(source.Bytes.Span);var before=SpirvShaderInterfaceReader.Read(defaultTranslation.Bytes);
        if(rdef.Bindings.Count!=6||before.Descriptors.Count!=6||rdef.Bindings.Any(b=>b.InputType is 9 or 10 or 11)||
            before.EntryPoints is not{Count:1}||before.EntryPoints[0].ExecutionModel!=5||
            !before.ExecutionModes.Single(m=>m.Mode==17).Operands.SequenceEqual(new uint[]{16,16,1}))
            throw new InvalidDataException("Exact DOF image source roster/entry/local group differs; append/counter resources are not admitted.");
        foreach(uint register in new uint[]{0,1})
        {
            var original=rdef.Bindings.Single(b=>b.BindPoint==register&&b.InputType==2);
            var d=before.Descriptors.Single(d=>d.Name==$"t{register}");
            if(original.BindCount!=1||original.ReturnType!=5||original.Dimension!=4||d.DescriptorSet!=0||d.Binding!=register+1||
                d.StorageClass!=0||d.Type is not{Kind:"image",Dimension:1,Arrayed:false,Multisampled:false,SampledCode:1,
                    ImageFormat:0,ComponentType:{Kind:"float",Bits:32}})
                throw new InvalidDataException("Exact DOF image readonly float Texture2D interface changed.");
        }
        var cb=rdef.Bindings.Single(b=>b.InputType==0);var cbDevice=before.Descriptors.Single(d=>d.Name=="cb2_2");
        if(cb.BindPoint!=2||cb.BindCount!=1||rdef.Buffers is not[{ByteSize:144}]||cbDevice.DescriptorSet!=0||cbDevice.Binding!=0||
            cbDevice.RequiredBufferBytes!=(source.StageIndex==47?96u:144u))
            throw new InvalidDataException("Exact DOF image CB18 active-prefix interface changed.");
        var instructions=Read(defaultTranslation.Bytes);
        uint uintType=instructions.Single(i=>i.Op==21&&i.A.Skip(1).SequenceEqual(new uint[]{32,0})).A[0];
        uint floatType=instructions.Single(i=>i.Op==22&&i.A[1]==32).A[0];
        uint float4=instructions.Single(i=>i.Op==23&&i.A[1]==floatType&&i.A[2]==4).A[0];
        uint boolType=instructions.Single(i=>i.Op==20).A[0];
        uint zero=Constant(instructions,uintType,0),one=Constant(instructions,uintType,1),two=Constant(instructions,uintType,2);
        var descriptors=new Dictionary<uint,SpirvInterfaceVariable>();
        foreach(uint register in new uint[]{0,1,2})
        {
            var original=rdef.Bindings.Single(b=>b.BindPoint==register&&b.InputType==(register==0?8u:4u));
            var d=before.Descriptors.Single(d=>d.Name==$"u{register}");
            if(original.BindCount!=1||original.Dimension!=1||original.ReturnType!=(register==0?6u:5u)||
                d.StorageClass!=0||d.DescriptorSet!=0||d.Binding!=register+3||
                d.Type is not{Kind:"image",Dimension:5,Arrayed:false,Multisampled:false,SampledCode:2}||
                (register==0?d.Type.ImageFormat!=33||d.Type.ComponentType is not{Kind:"int",Bits:32,Signed:false}:
                    d.Type.ImageFormat!=0||d.Type.ComponentType is not{Kind:"float",Bits:32}))
                throw new InvalidDataException("Exact DOF image UAV is not its raw uint u0 or complete float4 typed u1/u2.");
            descriptors.Add(register,d);
        }
        var variables=descriptors.ToDictionary(x=>x.Value.VariableId,x=>x.Key);
        var loads=instructions.Where(i=>i.Op==61&&variables.ContainsKey(i.A[2])).ToDictionary(i=>i.A[1],i=>variables[i.A[2]]);
        if(loads.Count!=4||instructions.Where(i=>i.Op==61&&loads.ContainsKey(i.A[1])).Any(i=>i.A.Length!=3))
            throw new InvalidDataException("Exact DOF image output load origins changed.");
        var stores=instructions.Where(i=>i.Op==99).ToArray();var pointers=instructions.Where(i=>i.Op==60).ToArray();
        if(stores.Length!=4||stores.Any(i=>i.A.Length!=3||!loads.TryGetValue(i.A[0],out uint r)||r is not(1 or 2)||ResultType(instructions,i.A[2])!=float4)||
            stores.GroupBy(i=>loads[i.A[0]]).Any(g=>g.Count()!=2)||pointers.Length!=2||
            pointers.Any(i=>i.A.Length!=5||!variables.TryGetValue(i.A[2],out uint r)||r!=0||i.A[4]!=zero))
            throw new InvalidDataException("Exact DOF image source four float4 stores/two raw atomic pointers changed.");
        var atomics=instructions.Where(i=>i.Op==234).ToArray();
        if(atomics.Length!=2||atomics.Any(i=>i.A.Length!=6||i.A[0]!=uintType||!pointers.Any(p=>p.A[1]==i.A[2])||
            i.A[3]!=one||i.A[4]!=zero||i.A[5]!=one))
            throw new InvalidDataException("Exact DOF image AtomicIAdd operands/scope/semantics/increment changed.");
        uint next=BinaryPrimitives.ReadUInt32LittleEndian(defaultTranslation.Bytes.AsSpan(12));uint Next()=>next++;
        uint array=Next(),structure=Next(),storagePointer=Next(),wordPointer=Next();
        Instruction[] declarations=[Make(29,array,uintType),Make(30,structure,array),Make(32,storagePointer,2,structure),Make(32,wordPointer,2,uintType)];
        Instruction[] decorations=[Make(71,array,6,4),Make(71,structure,3),Make(72,structure,0,35,0)];
        var constants=new List<Instruction>();var existing=instructions.Where(i=>i.Op==43&&i.A.Length==3&&i.A[0]==uintType).ToDictionary(i=>i.A[2],i=>i.A[1]);
        uint C(uint value){if(existing.TryGetValue(value,out uint id))return id;id=Next();existing.Add(value,id);constants.Add(Make(43,uintType,id,value));return id;}
        foreach(uint value in new uint[]{0x8000,0x7FFFFFFF,0x7FFFFF,0x800000,0x1C000,103,113,126,143,255,0x7E00,0x7C00,0x7BFF,13,16,23,31})_=C(value);
        var output=new List<Instruction>();
        uint Binary(ushort op,uint type,uint lhs,uint rhs){uint id=Next();output.Add(Make(op,type,id,lhs,rhs));return id;}
        uint Select(uint condition,uint whenTrue,uint whenFalse){uint id=Next();output.Add(Make(169,uintType,id,condition,whenTrue,whenFalse));return id;}
        uint HalfBits(uint vector,uint channel)
        {
            // Exact D3D11 typed-store RTZ: integer bits preserve representable
            // denorms and signedzero, saturate finite overflow, retain Inf/NaN.
            // Every evaluated shift has an explicitly bounded count<=31.
            uint scalar=Next(),bits=Next();output.Add(Make(81,floatType,scalar,vector,channel));output.Add(Make(124,uintType,bits,scalar));
            uint sign=Binary(199,uintType,Binary(194,uintType,bits,C(16)),C(0x8000));
            uint magnitude=Binary(199,uintType,bits,C(0x7FFFFFFF)),exponent=Binary(194,uintType,magnitude,C(23));
            uint mantissa=Binary(199,uintType,magnitude,C(0x7FFFFF));
            uint normal=Binary(130,uintType,Binary(194,uintType,magnitude,C(13)),C(0x1C000));
            uint subnormalShift=Binary(199,uintType,Binary(130,uintType,C(126),exponent),C(31));
            uint subnormal=Binary(194,uintType,Binary(197,uintType,mantissa,C(0x800000)),subnormalShift);
            uint tiny=Binary(176,boolType,exponent,C(103)),sub=Binary(176,boolType,exponent,C(113));
            uint overflow=Binary(174,boolType,exponent,C(143)),special=Binary(170,boolType,exponent,C(255));
            uint nan=Binary(171,boolType,mantissa,zero),nanBits=Binary(197,uintType,Binary(194,uintType,mantissa,C(13)),C(0x7E00));
            uint specialBits=Select(nan,nanBits,C(0x7C00)),finite=Select(overflow,C(0x7BFF),normal);
            uint low=Select(sub,subnormal,finite),nonSpecial=Select(tiny,zero,low);
            return Binary(197,uintType,sign,Select(special,specialBits,nonSpecial));
        }
        bool declared=false,decorated=false;int retained=0,added=0;
        foreach(var i in instructions)
        {
            if(!decorated&&i.Op is >=19 and <=33){output.AddRange(decorations);decorated=true;added+=decorations.Length;}
            if(i.Op==54){output.AddRange(constants);added+=constants.Count;}
            if(i.Op==59&&variables.ContainsKey(i.A[1]))
            {
                if(i.A.Length!=3||i.A[2]!=0)throw new InvalidDataException("DOF output variable initialization/storage differs.");
                if(!declared){output.AddRange(declarations);declared=true;added+=declarations.Length;}
                output.Add(Make(59,storagePointer,i.A[1],2));added++;
            }
            else if(i.Op==61&&loads.ContainsKey(i.A[1])){}
            else if(i.Op==60)
            {output.Add(Make(65,wordPointer,i.A[1],i.A[2],zero,i.A[3]));added++;}
            else if(i.Op==99)
            {
                uint r=loads[i.A[0]],variable=descriptors[r].VariableId,coordinate=i.A[1],vector=i.A[2];
                if(instructions.Single(t=>t.Op==21&&t.A[0]==ResultType(instructions,coordinate)) is not{A:[_,32,1]})
                    throw new InvalidDataException("Original float4 typed-store texel coordinate is not signed32.");
                int beforeStore=output.Count;uint unsigned=Next(),word0=Next(),word1=Next();
                output.Add(Make(124,uintType,unsigned,coordinate));output.Add(Make(132,uintType,word0,unsigned,two));output.Add(Make(128,uintType,word1,word0,one));
                uint x=HalfBits(vector,0),y=HalfBits(vector,1),z=HalfBits(vector,2),w=HalfBits(vector,3);
                uint xy=Binary(197,uintType,x,Binary(196,uintType,y,C(16))),zw=Binary(197,uintType,z,Binary(196,uintType,w,C(16)));
                uint p0=Next(),p1=Next();output.Add(Make(65,wordPointer,p0,variable,zero,word0));output.Add(Make(65,wordPointer,p1,variable,zero,word1));
                output.Add(Make(62,p0,xy));output.Add(Make(62,p1,zw));added+=output.Count-beforeStore;
            }
            else{output.Add(i);retained++;}
        }
        if(!declared||!decorated)throw new InvalidDataException("DOF output storage declaration insertion failed.");
        byte[] bytes=Serialize(defaultTranslation.Bytes.AsSpan(0,20),next,output);var after=SpirvShaderInterfaceReader.Read(bytes);
        if(before.Descriptors.Count!=after.Descriptors.Count||
            System.Text.Json.JsonSerializer.Serialize(before.StageVariables)!=System.Text.Json.JsonSerializer.Serialize(after.StageVariables)||
            !before.EntryPoints[0].InterfaceIds.SequenceEqual(after.EntryPoints[0].InterfaceIds))
            throw new InvalidDataException("DOF image adapter altered source stage IO or descriptor count.");
        foreach(var d in after.Descriptors)
        {
            var previous=before.Descriptors.Single(b=>b.VariableId==d.VariableId);
            if(d.Name!=previous.Name||d.DescriptorSet!=previous.DescriptorSet||d.Binding!=previous.Binding)
                throw new InvalidDataException("DOF image adapter changed an original descriptor ID/name/set/binding.");
            if(!variables.ContainsKey(d.VariableId))
            {if(d.StorageClass!=previous.StorageClass||d.RequiredBufferBytes!=previous.RequiredBufferBytes||d.Type.Kind!=previous.Type.Kind)
                throw new InvalidDataException("DOF image adapter changed readonly/constant descriptor.");}
            else if(d.StorageClass!=2||d.Type is not{Kind:"struct",BufferBlock:true,Block:false,Members:{Count:1}}||
                d.Type.Members[0] is not{Offset:0,Type:{Kind:"runtime-array",ArrayStride:4,ElementType:{Kind:"int",Bits:32,Signed:false}}})
                throw new InvalidDataException("DOF image adapted output is not exact uint32/stride4 storage.");
        }
        var proofs=descriptors.Select(p=>new MapPostEffect3DofSpriteStorageDescriptorProof(source.StageIndex,p.Key,p.Key==0?8u:4u,
            5+(int)p.Key,p.Key==0?39u:10u,p.Value.VariableId,p.Value.DescriptorSet!.Value,p.Value.Binding!.Value,2,4,
            p.Key==0?4u:8u,p.Key==0?1u:2u,0,p.Key==0?2:0,p.Key==0?0:2,
            p.Key==0?"original raw-u0/B5 32B;byte-address>>2 operand and two AtomicIAdd retained":
                "original full-float4 u1/u2;B6/B7 nativeDXGI10/RGBA16F;RTZ/finite-saturation/Inf/NaN/denorm/signedzero;two uint32words/eight bytes per texel;all four source components;creation only")).OrderBy(p=>p.SourceRegister).ToArray();
        return new(bytes,sourceSha,defaultSha,Hash(bytes),Array.AsReadOnly(proofs),retained,loads.Count,added,
            $"exact original Functor1 CS{source.StageIndex};default official vkd3d module pinned;sourceDXBC unchanged;all non-UAV instructions byte-identical;raw atomics and fullRGBA16F typed stores;no append/counter admission;shader creation interface only;selected execution still unsupported");
    }
    private sealed record Instruction(ushort Op,uint[] A);
    private static Instruction Make(ushort op,params uint[] a)=>new(op,a);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static uint Constant(List<Instruction> instructions,uint type,uint value)=>
        instructions.Single(i=>i.Op==43&&i.A.SequenceEqual(new uint[]{type,i.A[1],value})).A[1];
    private static uint ResultType(List<Instruction> instructions,uint id)=>
        instructions.Single(i=>i.Op is 43 or 44 or 61 or 79 or 81 or 124 or 128 or 132 or 194&&i.A.Length>=2&&i.A[1]==id).A[0];
    private static List<Instruction> Read(ReadOnlySpan<byte> bytes)
    {var result=new List<Instruction>();for(int at=20;at<bytes.Length;){uint h=BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);int count=checked((int)(h>>16));
        if(count==0||count>(bytes.Length-at)/4)throw new InvalidDataException("Truncated original DOF image SPIR-V instruction.");
        uint[] a=new uint[count-1];for(int i=0;i<a.Length;i++)a[i]=BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+4+i*4)..]);
        result.Add(new((ushort)h,a));at+=count*4;}return result;}
    private static byte[] Serialize(ReadOnlySpan<byte> header,uint bound,List<Instruction> instructions)
    {byte[] bytes=new byte[20+instructions.Sum(i=>4+i.A.Length*4)];header.CopyTo(bytes);BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12),bound);int at=20;
        foreach(var i in instructions){BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at),checked((uint)(i.A.Length+1)<<16)|i.Op);at+=4;
            foreach(uint a in i.A){BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at),a);at+=4;}}return bytes;}
}
