using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual counted SRV setter whose current native pointer is known
/// null. No resource, pixel contents, extent, or RID is represented here.</summary>
public sealed class MapPostEffect3NullShaderResource
{
    private readonly MapPostEffect3TextureParameterState table;
    public int SourcePassIndex{get;}
    public uint SourceStageCode{get;}
    public MapPostEffect3Binding? Binding{get;}
    public long SlotRevision{get;}
    public long FrameNumber=>table.FrameNumber;
    public string ContextOwnerIdentity=>table.ContextOwnerIdentity;
    public string SourceSha256=>table.SourceSha256;
    public int? ParameterIndex=>Binding?.ParameterIndex;
    public uint SourceRegister{get;}
    private readonly Action? validateUnassignedRegister;
    public string SourceEvidence=>(Binding is null?";>-per-pass-reset;no-counted-or-Before-SRV-setter":";kind1-null-wrapper")+
        "-SRV0-keyFFFFFFFFFFFFFFFF;-one-null-stage-SRV;D3D11.3:7.18.17/22.4.6/22.4.14";
    private MapPostEffect3NullShaderResource(MapPostEffect3TextureParameterState table,
        int pass,uint stage,MapPostEffect3Binding binding)
    {this.table=table;SourcePassIndex=pass;SourceStageCode=stage;Binding=binding;
     SourceRegister=checked((uint)binding.RegisterIndex);
     SlotRevision=table.CaptureKnownNullRevision(binding.ParameterIndex);}

    internal MapPostEffect3NullShaderResource(MapPostEffect3TextureParameterState table,
        int pass,uint stage,uint register,Action validateUnassignedRegister)
    {this.table=table;SourcePassIndex=pass;SourceStageCode=stage;SourceRegister=register;
     this.validateUnassignedRegister=validateUnassignedRegister;validateUnassignedRegister();}

    public void ValidateCurrent()
    {
        if(Binding is{} binding)table.ValidateKnownNullRevision(binding.ParameterIndex,SlotRevision);
        else (validateUnassignedRegister??throw new InvalidDataException("Unassigned null SRV has no reset/source proof."))();
    }

    /// <summary>Preserves native ordered setters and proves only the final
    /// shader-visible source row at each register. Unknown pointers reject.</summary>
    public static IReadOnlyList<MapPostEffect3NullShaderResource> CapturePassReads(
        MapPostEffect3AllocationPlan actualAllocation,MapPostEffect3TextureParameterState table,
        int sourcePassIndex,uint sourceStageCode)
    {
        ArgumentNullException.ThrowIfNull(actualAllocation);ArgumentNullException.ThrowIfNull(table);
        MapPostEffect3ParameterState.ValidateSource(actualAllocation.Source);
        if(table.SourceSha256!=actualAllocation.Source.Sha256||table.ContextOwnerIdentity!=actualAllocation.ContextOwnerIdentity||
            (uint)sourcePassIndex>=actualAllocation.Passes.Count||sourceStageCode is not(0 or 5)||
            !actualAllocation.Passes[sourcePassIndex].Pass.ProgramReferences.Any(r=>r.StageCode==sourceStageCode&&r.StageIndex is not null))
            throw new InvalidDataException("Null SRV proof requires the actual same-context source pass and CS/PS stage.");
        byte mask=sourceStageCode==5?(byte)1:(byte)4;
        var final=actualAllocation.Passes[sourcePassIndex].ActiveBindings.Where(b=>
            b.Table==MapPostEffect3BindingTable.TextureParameters&&b.Role==MapPostEffect3BindingRole.ShaderResourceRead&&
            (b.StageMask&mask)!=0).GroupBy(b=>b.RegisterIndex).Select(g=>g.Last());
        return Array.AsReadOnly(final.Where(b=>table.ReadKnownNullablePointer(b.ParameterIndex) is null)
            .Select(b=>new MapPostEffect3NullShaderResource(table,sourcePassIndex,sourceStageCode,b)).ToArray());
    }
}

public sealed record MapPostEffect3NullShaderResourceSpirv(byte[] Bytes,string Sha256,
    string UnspecializedTranslationSha256,IReadOnlyList<uint> RemovedTextureRegisters,
    int ReplacedReads,int ReplacedDimensionQueries);

/// <summary>Finite source-null D3D11 SRV adaptation. Samples/loads are typed
/// zero; GetDimensions returns zero dimensions and zero mip count. Original
/// arithmetic (including reciprocal zero), samplers, CBs and geometry remain.
/// No dummy1x1 image is allocated. Unsupported image operations reject.</summary>
public static class MapPostEffect3NullShaderResourceAdapter
{
    public static MapPostEffect3NullShaderResourceSpirv Adapt(
        MapPostEffect3Program source,ShaderSpirv device,
        TranslatedShaderBindings bindings,IReadOnlyList<MapPostEffect3NullShaderResource> nulls)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(bindings);ArgumentNullException.ThrowIfNull(nulls);
        if(nulls.Count==0||source.StageCode is not(0 or 5)||
            Convert.ToHexString(SHA256.HashData(source.Bytes.Span))!=source.Sha256||
            device.SourceProgramSha256!=source.Sha256||Convert.ToHexString(SHA256.HashData(device.Bytes))!=device.SpirvSha256||
            bindings.Interface.Sha256!=device.SpirvSha256||nulls.Any(n=>n.SourceStageCode!=source.StageCode||
                n.SourceSha256!=MapFinalOutputPostEffect3Source.PayloadSha256||n.FrameNumber!=nulls[0].FrameNumber||
                n.ContextOwnerIdentity!=nulls[0].ContextOwnerIdentity||n.SourcePassIndex!=nulls[0].SourcePassIndex))
            throw new InvalidDataException("Null SRV adapter needs the exact original source program/device translation and known-null source setters.");
        foreach(var n in nulls)n.ValidateCurrent();
        uint[] registers=nulls.Select(n=>n.SourceRegister).Order().ToArray();
        if(registers.Distinct().Count()!=registers.Length)throw new InvalidDataException("Duplicate null shader register.");
        var variables=new HashSet<uint>();
        foreach(uint register in registers)
        {
            var matches=bindings.Bindings.Where(b=>b.DescriptorKind=="Texture"&&b.Source.BindPoint==register).ToArray();
            if(matches.Length==0)continue; // Counted setter can have no shader read.
            var b=matches.Single();
            uint expectedDimension=b.Source.Dimension switch{4=>1u,8=>2u,_=>uint.MaxValue};
            if(b.Source.InputType!=2||expectedDimension==uint.MaxValue||b.Source.BindCount!=1||b.Device.DescriptorSet!=0||
                b.Device.Type is not{Kind:"image",Arrayed:false,Multisampled:false,SampledCode:1} image||image.Dimension!=expectedDimension||
                b.Source.ReturnType is not(3 or 4 or 5))
                throw new NotSupportedException("Null SRV adapter needs the exact typed single Texture2D or Texture3D source image.");
            variables.Add(b.Device.VariableId);
        }
        var words=new uint[device.Bytes.Length/4];
        for(int i=0;i<words.Length;i++)words[i]=BinaryPrimitives.ReadUInt32LittleEndian(device.Bytes.AsSpan(i*4));
        var instructions=new List<uint[]>();
        for(int at=5;at<words.Length;)
        {int count=(int)(words[at]>>16);if(count==0||count>words.Length-at)throw new InvalidDataException("Truncated original null SRV SPIR-V.");
         instructions.Add(words.AsSpan(at,count).ToArray());at+=count;}
        var removed=new HashSet<uint>(variables);var images=new HashSet<uint>();var sampled=new HashSet<uint>();
        var zeros=new Dictionary<uint,uint>();uint next=words[3];var body=new List<uint[]>();int reads=0,queries=0;
        bool Scalar(uint id)=>instructions.Any(w=>((ushort)w[0]==21||(ushort)w[0]==22)&&w[1]==id&&w[2]==32);
        void VerifyZeroType(uint id)
        {
            if(Scalar(id))return;
            var vector=instructions.SingleOrDefault(w=>(ushort)w[0]==23&&w[1]==id);
            if(vector is null||vector[3] is <2 or >4||!Scalar(vector[2]))
                throw new NotSupportedException("Original null image operation has an unsupported result type.");
        }
        foreach(var w in instructions)
        {
            ushort op=(ushort)w[0];
            if(op==61&&w.Length==4&&variables.Contains(w[3]))
            {images.Add(w[2]);removed.Add(w[2]);continue;}
            if(op==86&&images.Contains(w[3]))
            {sampled.Add(w[2]);removed.Add(w[2]);continue;}
            if(op==100&&sampled.Contains(w[3]))
            {images.Add(w[2]);removed.Add(w[2]);continue;}
            bool replaceRead=op is 87 or 88&&sampled.Contains(w[3])||op==95&&images.Contains(w[3]);
            bool replaceQuery=op is 103 or 104 or 106&&images.Contains(w[3]);
            if(replaceRead||replaceQuery)
            {
                // Only ordinary implicit and explicit Lod samples. No Grad,
                // bias, multisample, compare/gather or feedback assumptions.
                bool KnownOperands(int operandAt)
                {
                    if(w.Length==operandAt)return true;
                    uint mask=w[operandAt];
                    return mask switch{2=>w.Length==operandAt+2,8=>w.Length==operandAt+2,
                        10=>w.Length==operandAt+3,_=>false};
                }
                if(op==87&&!KnownOperands(5)||op==88&&(w.Length<=5||(w[5]&2)==0||!KnownOperands(5))||
                    op==95&&!KnownOperands(5))
                    throw new NotSupportedException("Unknown source null sample/fetch image operands.");
                VerifyZeroType(w[1]);if(!zeros.TryGetValue(w[1],out uint zero))zeros.Add(w[1],zero=next++);
                body.Add(Op(83,w[1],w[2],zero));if(replaceRead)reads++;else queries++;continue;
            }
            // Every use of an eliminated image must be handled explicitly.
            // Only image operations use these values in supported translations.
            if(op is >=87 and <=107&&w.Length>3&&(images.Contains(w[3])||sampled.Contains(w[3])))
                throw new NotSupportedException($"Original null SRV operation{op} is unsupported.");
            body.Add(w);
        }
        if(variables.Count>0&&reads+queries==0)throw new InvalidDataException("Declared source null image had no recognized read.");
        var final=words.Take(5).ToList();final[3]=next;bool inserted=false;
        foreach(var w in body)
        {
            ushort op=(ushort)w[0];
            if(op is 5 or 71&&removed.Contains(w[1])||op==59&&variables.Contains(w[2]))continue;
            if(op==54&&!inserted)
            {foreach(var zero in zeros)final.AddRange(Op(46,zero.Key,zero.Value));inserted=true;}
            final.AddRange(w);
        }
        byte[] bytes=new byte[final.Count*4];for(int i=0;i<final.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4),final[i]);
        var result=SpirvShaderInterfaceReader.Read(bytes);
        if(result.Descriptors.Count!=bindings.Interface.Descriptors.Count-variables.Count||
            result.StageVariables.Count!=bindings.Interface.StageVariables.Count||result.Descriptors.Any(d=>variables.Contains(d.VariableId)))
            throw new InvalidDataException("Null SRV adaptation changed a remaining original shader interface.");
        return new(bytes,result.Sha256,device.SpirvSha256,Array.AsReadOnly(registers),reads,queries);
    }
    private static uint[] Op(ushort opcode,params uint[] operands)=>new[]{((uint)(operands.Length+1)<<16)|opcode}.Concat(operands).ToArray();
}
