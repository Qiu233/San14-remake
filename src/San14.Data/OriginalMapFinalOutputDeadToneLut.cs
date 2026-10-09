using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One scoped current source selection: Aux tone2 becomes exact .2,
/// and the selected Camera1CC is nonzero, so EC3360 does not replace it with .3.
/// The actual embedded PS only samples t2 inside fToneCurveType&lt;0.</summary>
public sealed class OriginalMapFinalOutputDeadToneLut
{
    private OriginalMapFinalOutputDeadToneLut(string evidence)=>SourceEvidence=evidence;
    public string SourceEvidence{get;}
    public uint RequiredCurveBits=>0x3E4CCCCD;
    public static OriginalMapFinalOutputDeadToneLut FromSelectedCurrentAux(OriginalMapExposureViewAux actualAux,
        uint actualSelectedCamera1CC,string currentSelectionEvidence)
    {
        ArgumentNullException.ThrowIfNull(actualAux);ArgumentException.ThrowIfNullOrWhiteSpace(currentSelectionEvidence);
        var curve=OriginalMapPostEffect3ApplyFallbackToneCurve.ResolveCurrentAux(actualAux);
        if(BitConverter.SingleToUInt32Bits(curve.ActualFifthFloatArgument)!=0x3E4CCCCD||actualSelectedCamera1CC==0)
            throw new InvalidDataException("Dead tone LUT scope requires actual Aux tone2/.2 and the selected Camera's nonzero override field.");
        return new(curve.SourceEvidence+";"+currentSelectionEvidence+";selectedCamera1CC="+actualSelectedCamera1CC.ToString("X8")+
            ";actualTonePS-1DFBAF0D;CB20=3E4CCCCD;curveEqual-.1-or-.2-polynomial;only-t2-sample-if-curve-less-than-zero;no-placeholder-LUT");
    }
    /// <summary>Required on every accepted pixel-CB update, after the original
    /// writer. Byte20 is float .2, never the integer Aux tone type2.</summary>
    public void ValidateFrame(ReadOnlySpan<byte> completePixelCb)
    {
        if(completePixelCb.Length!=32||BinaryPrimitives.ReadUInt32LittleEndian(completePixelCb.Slice(20,4))!=RequiredCurveBits)
            throw new InvalidDataException("The adapted original tone shader requires its source curve .2 in every complete accepted 32B pixel CB.");
    }
}

public sealed record OriginalMapFinalOutputDeadToneLutSpirv(byte[] Bytes,string Sha256,
    string OriginalTranslationSha256,string SourceEvidence,int UnexecutedSamplesReplaced,int RetainedInstructions);

/// <summary>Finite actual embedded TonePS adaptation. Removes only t2/s2 and
/// their sole proven-unexecuted sample; all reachable instructions are retained
/// verbatim. The frame owner must continuously enforce proof.ValidateFrame.</summary>
public static class OriginalMapFinalOutputDeadToneLutAdapter
{
    public static OriginalMapFinalOutputDeadToneLutSpirv Adapt(OriginalShaderSpirv pixel,
        OriginalTranslatedShaderBindings source,OriginalMapFinalOutputDeadToneLut proof,
        OriginalMapFinalOutputProgramSource actualProgram)
    {
        ArgumentNullException.ThrowIfNull(pixel);ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(proof);
        if(actualProgram.Kind!=OriginalMapFinalOutputProgramKind.EmbeddedTone||source.Bindings.Count!=7)
            throw new InvalidDataException("Dead tone LUT requires the exact embedded original PS and default compiler translation.");
        var image=source.Bindings.Single(b=>b.DescriptorKind=="Texture"&&b.Source.BindPoint==2);
        var sampler=source.Bindings.Single(b=>b.DescriptorKind=="Sampler"&&b.Source.BindPoint==2);
        if(image.Source.Name.Text!="t2"||sampler.Source.Name.Text!="s2"||image.Source.Dimension!=8||image.Source.ReturnType!=5)
            throw new InvalidDataException("Tone LUT descriptors do not join the actual original 3D texture and sampler.");
        uint[] words=new uint[pixel.Bytes.Length/4];for(int i=0;i<words.Length;i++)words[i]=BinaryPrimitives.ReadUInt32LittleEndian(pixel.Bytes.AsSpan(i*4,4));
        var instructions=new List<uint[]>();for(int at=5;at<words.Length;){int size=(int)(words[at]>>16);
            if(size<1||size>words.Length-at)throw new InvalidDataException("Truncated original tone SPIR-V.");instructions.Add(words.AsSpan(at,size).ToArray());at+=size;}
        var resources=new HashSet<uint>{image.Device.VariableId,sampler.Device.VariableId};var removed=new HashSet<uint>(resources);
        var imageLoads=new HashSet<uint>();var sampledImages=new HashSet<uint>();var zeros=new Dictionary<uint,uint>();
        uint next=source.Interface.IdBound;var body=new List<uint[]>();int samples=0,retained=0;
        foreach(var w in instructions)
        {
            ushort op=(ushort)w[0];
            if(op==61&&resources.Contains(w[3])){removed.Add(w[2]);if(w[3]==image.Device.VariableId)imageLoads.Add(w[2]);continue;}
            if(op==86&&imageLoads.Contains(w[3]))
            {if(!removed.Contains(w[4]))throw new InvalidDataException("Source-dead tone sample uses another sampler.");sampledImages.Add(w[2]);removed.Add(w[2]);continue;}
            if(op==88&&sampledImages.Contains(w[3]))
            {
                var vector=instructions.Single(t=>(ushort)t[0]==23&&t[1]==w[1]);
                if(vector[3]!=4||!instructions.Any(t=>(ushort)t[0]==22&&t[1]==vector[2]&&t[2]==32))
                    throw new InvalidDataException("Source-dead tone sample result differs from float4.");
                if(!zeros.TryGetValue(w[1],out uint zero))zeros.Add(w[1],zero=next++);
                body.Add(Op(83,w[1],w[2],zero));samples++;continue;
            }
            body.Add(w);retained++;
        }
        if(samples!=1||sampledImages.Count!=1||imageLoads.Count!=1)
            throw new InvalidDataException("Tone source LUT sample/load schedule differs from the pinned translation.");
        var result=new List<uint>();result.AddRange(words.AsSpan(0,5).ToArray());result[3]=next;bool inserted=false;
        foreach(var w in body)
        {
            ushort op=(ushort)w[0];
            if(op is 5 or 71&&removed.Contains(w[1])){retained--;continue;}
            if(op==59&&resources.Contains(w[2])){retained--;continue;}
            if(op==54&&!inserted){foreach(var zero in zeros)result.AddRange(Op(46,zero.Key,zero.Value));inserted=true;}
            result.AddRange(w);
        }
        byte[] bytes=new byte[result.Count*4];for(int i=0;i<result.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4,4),result[i]);
        var module=SpirvShaderInterfaceReader.Read(bytes);
        if(module.Descriptors.Count!=5||module.StageVariables.Count!=source.Interface.StageVariables.Count||module.Descriptors.Any(d=>resources.Contains(d.VariableId)))
            throw new InvalidDataException("Dead tone adaptation changed a reachable descriptor or varying.");
        return new(bytes,module.Sha256,pixel.SpirvSha256,proof.SourceEvidence,samples,retained);
    }
    public static OriginalTranslatedShaderBindings ReadDeviceBindings(OriginalTranslatedShaderBindings original,
        OriginalMapFinalOutputDeadToneLutSpirv adapted)
    {
        if(original.Interface.Sha256!=adapted.OriginalTranslationSha256||adapted.UnexecutedSamplesReplaced!=1)
            throw new InvalidDataException("Tone bindings require the exact source-dead adapted profile.");
        var subset=original.OriginalDefinitions with{Bindings=Array.AsReadOnly(original.OriginalDefinitions.Bindings.Where(b=>b.InputType is not(2 or 3)||b.BindPoint!=2).ToArray())};
        return OriginalTranslatedShaderBindingReader.Read(subset,adapted.Bytes) with{OriginalDefinitions=original.OriginalDefinitions};
    }
    private static uint[] Op(ushort opcode,params uint[] operands)=>new[]{(uint)(operands.Length+1)<<16|opcode}.Concat(operands).ToArray();
}
