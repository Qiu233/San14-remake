using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Source-created Camera's color-correction sample branch is never
/// executed: intensity0 and LUT indices0. This does not assert a hardware NULL
/// binding; native113B5BF skips a NULL wrapper and retains prior backend state.</summary>
public sealed class MapFinalOutputDeadColorCorrection
{
    public LoadedMapCameraConstructionSource CameraSource { get; }
    public string SourceEvidence { get; }
    private MapFinalOutputDeadColorCorrection(LoadedMapCameraConstructionSource camera)
    {
        CameraSource=camera;SourceEvidence=camera.ConstructorOrigin+
            ";fresh-source-color-correction;CPU10EC/10F0/10F8=0;PS28:intensity>0 AND (Lut1!=0 OR Lut2!=0);no-samples;no-hardware-NULL-claim";
    }
    public static MapFinalOutputDeadColorCorrection FromSourceCreatedCamera(LoadedMapCameraConstructionSource camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var canonical=MapCameraConstructionSource.ReadPerspective(camera.Resource,camera.Camera.ObjectId);
        if(canonical.OpticsDefaults.ColorGrading.Lut1!=0||canonical.OpticsDefaults.ColorGrading.Lut2!=0||BitConverter.SingleToUInt32Bits(canonical.OpticsDefaults.ColorGrading.Intensity)!=0)
            throw new InvalidDataException("Final dead color correction requires the actual source-created zero intensity and both zero LUT indices.");
        return new(canonical);
    }
    public void ValidateFrame(ReadOnlySpan<byte> completePixelCb)
    {
        if(completePixelCb.Length!=240||BinaryPrimitives.ReadSingleLittleEndian(completePixelCb.Slice(92,4))!=0||
            BinaryPrimitives.ReadSingleLittleEndian(completePixelCb.Slice(216,4))!=0||
            BinaryPrimitives.ReadSingleLittleEndian(completePixelCb.Slice(220,4))!=0)
            throw new InvalidDataException("This final shader profile requires the source-created unexecuted color-correction branch in every accepted CB.");
    }
}

public sealed record MapFinalOutputDeadColorCorrectionSpirv(byte[] Bytes,string Sha256,
    string UnspecializedTranslationSha256,string SourceEvidence,int UnexecutedSamplesReplaced,int RetainedInstructions);

/// <summary>Finite PS28 descriptor adaptation for a proven unexecuted branch.
/// Only its12 unreachable LUT samples become unused float4 constants. No image
/// is allocated; all reachable pixel/color instructions remain byte-identical.
/// The owner must reject any accepted CB which enables this source branch.</summary>
public static class MapFinalOutputDeadColorCorrectionAdapter
{
    public const string PixelTranslationSha256="0C3C6378F5CB68BC3D9E08557C62B3151E85A8BDAA147214B99D2967707FB96E";
    public static MapFinalOutputDeadColorCorrectionSpirv Adapt(ShaderSpirv pixel,
        TranslatedShaderBindings source,MapFinalOutputDeadColorCorrection proof)
    {
        ArgumentNullException.ThrowIfNull(pixel);ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(proof);
        if(pixel.SourceProgramSha256!=MapFinalOutputConstants.PixelSha256||pixel.SpirvSha256!=PixelTranslationSha256||
            Convert.ToHexString(SHA256.HashData(pixel.Bytes))!=PixelTranslationSha256||source.Interface.Sha256!=PixelTranslationSha256||source.Bindings.Count!=27)
            throw new InvalidDataException("Dead color correction requires the exact original PS28 translation and source interface.");
        var image=source.Bindings.Single(b=>b.DescriptorKind=="Texture"&&b.Source.BindPoint==2);
        var sampler=source.Bindings.Single(b=>b.DescriptorKind=="Sampler"&&b.Source.BindPoint==2);
        if(image.Source.Name.Text!="smplColorCorrection_Tex"||sampler.Source.Name.Text!="smplColorCorrection"||image.Source.Dimension!=4||image.Source.ReturnType!=5)
            throw new InvalidDataException("Final LUT descriptor join differs from the pinned source.");
        uint[] words=new uint[pixel.Bytes.Length/4];for(int i=0;i<words.Length;i++)words[i]=BinaryPrimitives.ReadUInt32LittleEndian(pixel.Bytes.AsSpan(i*4,4));
        var instructions=new List<uint[]>();for(int at=5;at<words.Length;){int length=(int)(words[at]>>16);
            if(length<1||length>words.Length-at)throw new InvalidDataException("Truncated final source SPIR-V.");instructions.Add(words.AsSpan(at,length).ToArray());at+=length;}
        var resources=new HashSet<uint>{image.Device.VariableId,sampler.Device.VariableId};var removed=new HashSet<uint>(resources);
        var imageLoads=new HashSet<uint>();var sampledImages=new HashSet<uint>();var zeros=new Dictionary<uint,uint>();
        uint next=source.Interface.IdBound;var body=new List<uint[]>();int samples=0,retained=0;
        foreach(var w in instructions)
        {
            ushort op=(ushort)w[0];
            if(op==61&&resources.Contains(w[3])){removed.Add(w[2]);if(w[3]==image.Device.VariableId)imageLoads.Add(w[2]);continue;}
            if(op==86&&imageLoads.Contains(w[3]))
            {if(!removed.Contains(w[4]))throw new InvalidDataException("Source-dead LUT sample uses another sampler.");sampledImages.Add(w[2]);removed.Add(w[2]);continue;}
            if(op is 87 or 88&&sampledImages.Contains(w[3]))
            {
                var vector=instructions.Single(t=>(ushort)t[0]==23&&t[1]==w[1]);
                if(vector[3]!=4||!instructions.Any(t=>(ushort)t[0]==22&&t[1]==vector[2]&&t[2]==32))
                    throw new InvalidDataException("Source-dead LUT sample result differs from float4.");
                if(!zeros.TryGetValue(w[1],out var zero))zeros.Add(w[1],zero=next++);
                body.Add(Op(83,w[1],w[2],zero));samples++;continue;
            }
            body.Add(w);retained++;
        }
        if(samples!=12||sampledImages.Count!=12||imageLoads.Count!=12)
            throw new InvalidDataException("PS28 source-dead LUT sample/load schedule differs from the pinned source.");
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
        if(module.Descriptors.Count!=25||module.StageVariables.Count!=source.Interface.StageVariables.Count||module.Descriptors.Any(d=>resources.Contains(d.VariableId)))
            throw new InvalidDataException("Final source-dead adaptation changed a reachable descriptor or varying.");
        return new(bytes,module.Sha256,pixel.SpirvSha256,proof.SourceEvidence,samples,retained);
    }
    public static TranslatedShaderBindings ReadDeviceBindings(TranslatedShaderBindings original,
        MapFinalOutputDeadColorCorrectionSpirv adapted)
    {
        if(original.Interface.Sha256!=PixelTranslationSha256||adapted.UnspecializedTranslationSha256!=PixelTranslationSha256||
            Convert.ToHexString(SHA256.HashData(adapted.Bytes))!=adapted.Sha256||adapted.UnexecutedSamplesReplaced!=12)
            throw new InvalidDataException("Final source-dead device bindings require the verified adapted profile.");
        var subset=original.SourceDefinitions with{Bindings=Array.AsReadOnly(original.SourceDefinitions.Bindings.Where(b=>b.InputType is not(2 or 3)||b.BindPoint!=2).ToArray())};
        return TranslatedShaderBindingReader.Read(subset,adapted.Bytes) with{SourceDefinitions=original.SourceDefinitions};
    }
    private static uint[] Op(ushort opcode,params uint[] operands)=>new[]{(uint)(operands.Length+1)<<16|opcode}.Concat(operands).ToArray();
}
