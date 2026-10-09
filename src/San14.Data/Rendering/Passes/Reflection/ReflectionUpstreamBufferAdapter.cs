using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

public sealed record ReflectionUpstreamAdaptedCompute(byte[] Bytes,string InputSpirvSha256,
    string AdaptedSha256,IReadOnlyList<uint> StorageBufferSourceRegisters,uint ReplacedTexelReads);

/// <summary>Godot Vulkan has no storage texel-buffer descriptor implementation.
/// For original Reflection CS10's read-only R32_UINT SH and R32_FLOAT sky
/// loads, retain every source DWORD index and replace each scalar texel read
/// with the same scalar storage-buffer load. All arithmetic, bitcasts, images,
/// constants and dispatches are unchanged. Caller supplies original bytes in
/// storage buffers. No texture/default packet or shader result is fabricated.</summary>
public static class ReflectionUpstreamBufferAdapter
{
    private sealed record Instruction(ushort Opcode,uint[] Args);
    private sealed record Buffer(uint Variable,uint Scalar,uint Vector,uint StructPointer,uint ScalarPointer,uint Zero,uint One);
    public static ReflectionUpstreamAdaptedCompute Adapt(ReadOnlySpan<byte> spirv,ReflectionComputeBindings original)
    {
        ArgumentNullException.ThrowIfNull(original);
        if(spirv.Length<20 || spirv.Length%4!=0 || Convert.ToHexString(SHA256.HashData(spirv))!=original.Interface.Sha256)
            throw new InvalidDataException("Reflection original compute module changed before adaptation.");
        var selected=original.Bindings.Where(b=>b.DescriptorKind=="ImageBuffer").ToArray();
        if(selected.Length==0)return new(spirv.ToArray(),original.Interface.Sha256,original.Interface.Sha256,Array.Empty<uint>(),0);
        if(!selected.Select(b=>b.Source.BindPoint).Order().SequenceEqual(new uint[]{2,4}) ||
            selected.Any(b=>b.Device.Type.Dimension!=5 || b.Device.Type.SampledCode!=2 || b.Device.Type.ImageFormat is not (3 or 33)))
            throw new InvalidDataException("Reflection adapter requires exactly original read-only SH u2 and sky u4 R32 buffers.");
        uint[] header=new uint[5];for(int i=0;i<5;i++)header[i]=BinaryPrimitives.ReadUInt32LittleEndian(spirv[(i*4)..]);
        var instructions=new List<Instruction>();
        for(int at=20;at<spirv.Length;)
        {
            uint first=BinaryPrimitives.ReadUInt32LittleEndian(spirv[at..]);int count=(int)(first>>16);
            if(count<1 || count>(spirv.Length-at)/4)throw new InvalidDataException("Truncated Reflection SPIR-V instruction.");
            var a=new uint[count-1];for(int i=0;i<a.Length;i++)a[i]=BinaryPrimitives.ReadUInt32LittleEndian(spirv[(at+4+i*4)..]);
            instructions.Add(new((ushort)first,a));at+=count*4;
        }
        uint bound=header[3];uint Id()=>bound++;
        var types=instructions.Where(i=>i.Opcode is 21 or 22 or 23 or 25 or 32).ToDictionary(i=>i.Args[0]);
        var annotations=new List<Instruction>();var declarations=new List<Instruction>();var buffers=new Dictionary<uint,Buffer>();
        uint unsigned=types.Single(t=>t.Value.Opcode==21 && t.Value.Args.AsSpan(1).SequenceEqual(new uint[]{32,0})).Key;
        uint indexZero=Id();declarations.Add(new(43,[unsigned,indexZero,0]));
        foreach(var b in selected)
        {
            var variable=instructions.Single(i=>i.Opcode==59 && i.Args[1]==b.Device.VariableId);
            var pointer=types[variable.Args[0]];
            var image=types[pointer.Args[2]];
            uint scalar=image.Args[1];
            uint vector=types.Single(t=>t.Value.Opcode==23 && t.Value.Args[1]==scalar && t.Value.Args[2]==4).Key;
            uint array=Id(),structure=Id(),structPointer=Id(),scalarPointer=Id(),zero=Id(),one=Id();
            annotations.Add(new(71,[array,6,4])); // ArrayStride=one source DWORD.
            annotations.Add(new(71,[structure,3])); // SPIR-V 1.0 BufferBlock.
            annotations.Add(new(72,[structure,0,35,0])); // Member Offset=0.
            annotations.Add(new(71,[b.Device.VariableId,24])); // NonWritable: source only reads these two buffers.
            declarations.Add(new(29,[array,scalar]));declarations.Add(new(30,[structure,array]));
            declarations.Add(new(32,[structPointer,2,structure]));declarations.Add(new(32,[scalarPointer,2,scalar]));
            declarations.Add(new(43,[scalar,zero,0]));
            declarations.Add(new(43,[scalar,one,b.Source.InputType==6?1u:0x3F800000u]));
            buffers.Add(b.Device.VariableId,new(b.Device.VariableId,scalar,vector,structPointer,scalarPointer,zero,one));
        }
        var handles=new Dictionary<uint,Buffer>();
        foreach(var i in instructions.Where(i=>i.Opcode==61 && i.Args.Length>=3 && buffers.ContainsKey(i.Args[2])))
            handles.Add(i.Args[1],buffers[i.Args[2]]);
        uint replaced=0;var output=new List<Instruction>();bool addedAnnotations=false,addedDeclarations=false;
        foreach(var i in instructions)
        {
            if(!addedAnnotations && i.Opcode is >=19 and <=39){output.AddRange(annotations);addedAnnotations=true;}
            if(!addedDeclarations && i.Opcode==54)
            {
                output.AddRange(declarations);
                foreach(var b in buffers.Values)output.Add(new(59,[b.StructPointer,b.Variable,2]));
                addedDeclarations=true;
            }
            if(i.Opcode==59 && buffers.TryGetValue(i.Args[1],out var buffer))
                continue;
            if(i.Opcode==61 && handles.ContainsKey(i.Args[1]))continue;
            if(i.Opcode==98 && handles.TryGetValue(i.Args[2],out buffer))
            {
                if(i.Args.Length!=4 || i.Args[0]!=buffer.Vector)throw new InvalidDataException("Unsupported Reflection texel-read operands.");
                uint pointer=Id(),scalar=Id();
                output.Add(new(65,[buffer.ScalarPointer,pointer,buffer.Variable,indexZero,i.Args[3]]));
                output.Add(new(61,[buffer.Scalar,scalar,pointer]));
                output.Add(new(80,[i.Args[0],i.Args[1],scalar,buffer.Zero,buffer.Zero,buffer.One]));
                replaced++;continue;
            }
            if(i.Opcode is >=87 and <=107 && i.Args.Any(a=>handles.ContainsKey(a)))
                throw new InvalidDataException("Reflection source texel buffer has a non-read image operation.");
            output.Add(i);
        }
        if(!addedAnnotations || !addedDeclarations || replaced==0 || handles.Count==0)
            throw new InvalidDataException("Reflection texel buffer adapter did not replace its complete source reads.");
        header[3]=bound;int wordCount=5+output.Sum(i=>i.Args.Length+1);var bytes=new byte[wordCount*4];int position=0;
        void Word(uint word){BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(position,4),word);position+=4;}
        foreach(var word in header)Word(word);
        foreach(var i in output){Word(((uint)i.Args.Length+1)<<16|i.Opcode);foreach(var a in i.Args)Word(a);}
        return new(bytes,original.Interface.Sha256,Convert.ToHexString(SHA256.HashData(bytes)),selected.Select(b=>b.Source.BindPoint).Order().ToArray(),replaced);
    }
}
