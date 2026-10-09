using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>The original AddBloom and final Composite2 pixel dependencies.
/// Full selected-mip requests are conservative pixel footprints, not a demand
/// that unrelated physical mip levels be initialized. Literal exposure loads
/// retain native execution while their exact constant-selected output
/// dependencies may be absent.</summary>
public sealed class OriginalMapPostEffect3Composite2ReadFootprint
{
    private readonly OriginalMapPostEffect3ParameterComposite2Update update;
    private OriginalMapPostEffect3Composite2ReadFootprint(OriginalMapPostEffect3ParameterComposite2Update update,
        int pass,string pixelSha,IReadOnlyList<OriginalMapPostEffect3ShaderReadFootprint> reads)
    {
        this.update=update;SourcePassIndex=pass;PixelProgramSha256=pixelSha;ShaderReads=reads;
        RequiredConstantBufferBytesSha256=new ReadOnlyDictionary<int,string>(new Dictionary<int,string>
        {[22]=Convert.ToHexString(SHA256.HashData(update.ConstantBuffer.Bytes.Span))});
        SourceEvidence=update.SourceEvidence+";exact-original-PS48..51/53..54-resource/control/literal-load-token-footprints;"+
            "cb2[1].z-exposure-select;cb2[2].z-LensFlare-branch;cb2[1].y-LdrLut-branch;"+
            "full-selected-mip-is-conservative-pixel-domain-not-all-physical-mips;no-GPU-content-claim";
    }
    public long FrameNumber=>update.FrameNumber;
    public string ContextOwnerIdentity=>update.OwnerIdentity;
    public string SourceSha256=>update.ConstantBuffer.SourceSha256;
    public int SourcePassIndex{get;}
    public string PixelProgramSha256{get;}
    public IReadOnlyDictionary<int,string> RequiredConstantBufferBytesSha256{get;}
    public IReadOnlyList<OriginalMapPostEffect3ShaderReadFootprint> ShaderReads{get;}
    public bool EstablishesGpuContents=>false;
    public string SourceEvidence{get;}
    public void RequireCurrent()=>update.RequireCurrent();
    public void RequirePrepared(OriginalMapPostEffect3ParameterComposite2Update actual)
    {if(!ReferenceEquals(actual,update))throw new InvalidDataException("Composite2 footprint belongs to another exact prepared writer.");RequireCurrent();}
    public static OriginalMapPostEffect3Composite2ReadFootprint Prepare(
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3ParameterComposite2Update update,int sourcePassIndex)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(update);
        update.RequireCurrent();OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(update.ConstantBuffer.SourceSha256!=allocation.Source.Sha256||update.OwnerIdentity!=allocation.ContextOwnerIdentity||
            !update.PassSelection.OrderedFactory9Passes.Contains(sourcePassIndex))
            throw new InvalidDataException("Composite2 read footprint needs its current native selected pass/allocation.");
        if(!Profiles.TryGetValue(sourcePassIndex,out var profile))
            throw new NotSupportedException("This footprint closes original AddBloom and final Composite2; cross-filter/FXAA/Remake need separate producers.");
        var program=OriginalMapPostEffect3Functor1FootprintSource.RequireProgram(allocation,sourcePassIndex,0,profile.Pixel);
        var bindings=OriginalMapPostEffect3Functor1FootprintSource.ReadPixelTextures(allocation,sourcePassIndex);
        var cb=update.ConstantBuffer;
        if(cb.WriterRva!=checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(
            cb.Locale,"OriginalMapPostEffect3Composite2ReadFootprint.cs",0x115BC10))||cb.Declaration.Index!=22||cb.Bytes.Length!=208)
            throw new InvalidDataException("Composite2 footprint requires its complete actual original CB22 writer.");
        byte[] bytes=cb.Bytes.ToArray();
        float F(int at)=>BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(at));
        bool exposureDepends=F(24)<0;
        bool lens=F(40)>0;
        bool ldr=F(20)>0;
        var reads=new List<OriginalMapPostEffect3ShaderReadFootprint>();
        bool final=sourcePassIndex is 107 or 108;
        foreach(var binding in bindings)
        {
            int register=binding.RegisterIndex;
            if(register==2)
            {
                reads.Add(new(0,2,binding,OriginalMapPostEffect3ShaderReadFootprintKind.LiteralTexel,0,null,final?0:1,0,0,
                    true,exposureDepends,final?
                        "originalComposePS53/54-unconditional-ld-t2(0,0).x-to-r0.w-via-yzwx-resource-swizzle;movc-known-cb2[1].z-versus-load;exact-output-dependency":
                        "originalAddBloomPS48..51-unconditional-ld-t2(1,0).x;movc-known1-versus-load-for-cb2[1].z<0"));
            }
            else if(final&&register is 3 or 4)
            {
                bool executes=register==3||ldr;
                reads.Add(new(0,register,binding,OriginalMapPostEffect3ShaderReadFootprintKind.EntireSelectedVolumeMip,0,null,
                    0,0,0,executes,executes,register==3?
                        "originalComposePS53/54-unconditional-HDR-t3-texture3D-sample_l-literalLOD0-conservative-entire-volume":
                        "originalComposePS53/54-LDR-t4-texture3D-sample_l-literalLOD0-only-in-cb2[1].y>0-source-branch"));
            }
            else if(final&&register is 0 or 1)
            {
                bool executes=register==0||lens;
                reads.Add(new(0,register,binding,OriginalMapPostEffect3ShaderReadFootprintKind.EntireSelectedMip,0,null,
                    0,0,0,executes,executes,register==0?
                        "originalComposePS53/54-scene-t0-literalLOD0-conservative-full-selected-mip-with-chroma-samples":
                        "originalComposePS53/54-Lens-t1-literalLOD0-only-in-cb2[2].z>0-source-branch"));
            }
            else if(!final&&register==1)
                reads.Add(new(0,1,binding,OriginalMapPostEffect3ShaderReadFootprintKind.EntireSelectedMip,0,null,0,0,0,true,true,
                    "originalAddBloomPS48..51-Bloom-t1-five-or-nine-literalLOD0-samples;conservative-entire-selected-mip-only"));
            else throw new NotSupportedException("Composite2 exact original resource has an unclosed footprint.");
        }
        if(reads.Count!=(final?5:2))throw new InvalidDataException("Composite2 source original texture register set changed.");
        return new(update,sourcePassIndex,program.Sha256,reads.AsReadOnly());
    }
    private sealed record Profile(int Pixel);
    private static readonly IReadOnlyDictionary<int,Profile> Profiles=new Dictionary<int,Profile>
    {
        [98]=new(48),
        [99]=new(49),
        [100]=new(50),
        [101]=new(51),
        [107]=new(53),
        [108]=new(54)
    };
}
