using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Actual selected Lens shader's source read mip set. Native SRVs
/// retain their original full extent. The bank must authorize only this
/// proved usage set, and the owner must join actual shader/CB/sampler bindings.</summary>
public sealed class OriginalMapPostEffect3LensTextureReadFootprint
{
    private OriginalMapPostEffect3LensTextureReadFootprint(OriginalMapPostEffect3LensExecutionRecipe recipe,
        string sha,int programIndex,OriginalMapPostEffect3Binding input,int[] mips,Dictionary<int,string> cbs,bool sampled,string domain)
    {
        SourceSha256=recipe.SourceSha256;ContextOwnerIdentity=recipe.ContextOwnerIdentity;FrameNumber=recipe.FrameNumber;
        SourcePassIndex=recipe.SourcePassIndex;ProgramSha256=sha;NativeBinding=input;
        RequiredMipLevels=Array.AsReadOnly(mips);RequiredConstantBufferBytesSha256=new ReadOnlyDictionary<int,string>(cbs);
        UsesExplicitSampleLevel=sampled;
        SourceEvidence=$"actual source pass{recipe.SourcePassIndex}/CS{programIndex};fullDXBC-SHA={sha};{domain};"+
            (sampled?"native s7 linear/MIP_POINT bias0/min0/maxFLTMAX owner required;":"integer Load;no sampler instruction;")+
            "native whole-SRV retained;only proved initialized mip set required;noGPUcompletion or sampler binding claim";
    }
    public string SourceSha256{get;}
    public string ContextOwnerIdentity{get;}
    public long FrameNumber{get;}
    public int SourcePassIndex{get;}
    public string ProgramSha256{get;}
    public int ParameterIndex=>NativeBinding.ParameterIndex;
    public uint SourceRegister=>checked((uint)NativeBinding.RegisterIndex);
    public OriginalMapPostEffect3Binding NativeBinding{get;}
    public IReadOnlyList<int> RequiredMipLevels{get;}
    public IReadOnlyDictionary<int,string> RequiredConstantBufferBytesSha256{get;}
    public bool UsesExplicitSampleLevel{get;}
    public bool RequiresActualSamplerProducer=>UsesExplicitSampleLevel;
    public uint? RequiredSamplerRegister=>UsesExplicitSampleLevel?7u:null;
    public uint? RequiredSamplerFilter=>UsesExplicitSampleLevel?0x14u:null;
    public uint? RequiredSamplerMipLodBiasBits=>UsesExplicitSampleLevel?0u:null;
    public uint? RequiredSamplerMinimumLodBits=>UsesExplicitSampleLevel?0u:null;
    public uint? RequiredSamplerMaximumLodBits=>UsesExplicitSampleLevel?0x7F7FFFFFu:null;
    public bool RetainsOriginalFullShaderResourceView=>true;
    public bool EstablishesGpuCompletion=>false;
    public string SourceEvidence{get;}

    public static OriginalMapPostEffect3LensTextureReadFootprint Prepare(
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3LensExecutionRecipe recipe,int register=0)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(recipe);
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        int pass=recipe.SourcePassIndex;
        bool down=pass is >=20 and <=24,up=pass is >=35 and <=40;
        int maxRegister=pass==24?1:up&&(pass&1)!=0?pass==37?2:1:0;
        if(!recipe.ExecutesPass||recipe.FrameNumber<=0||recipe.SourceSha256!=allocation.Source.Sha256||
            recipe.ContextOwnerIdentity!=allocation.ContextOwnerIdentity||(!down&&!up)||register<0||register>maxRegister)
            throw new NotSupportedException("Lens source mip footprint is closed only for active original20..24/35..40 bound texture registers;reset-null reads have no texture footprint.");
        string sha=up?((pass&1)!=0?"DB9FE7FFDC11813E9513991299128253C8679841F43DB00F9A12D54A6626AF4B":
            "CE5EA4EEE87A35A7B124929EAD9C3741EAC5E275EAFEC849DB68DCBEFB6F4416"):
            pass<=22?"582167FBF37357F1BA3D78E7BCF8E5C291FE2577804FAF6A27B9074C6D5941F0":pass==23?
            "0501748E162819EB7F3715BAE82F05F2469F221A50AD5CE64BAEB3C97CFD4E8E":
            "5E24F6D276510C0714A6494AAEC2568D1A936AD61AFE7DFDB71A6AA85F634F48";
        int index=up?pass-3:pass-2;var program=allocation.Source.GetProgram(5,index);
        if(program.Sha256!=sha||Convert.ToHexString(SHA256.HashData(program.Bytes.Span))!=sha||
            allocation.Source.Passes[pass].ProgramReferences.Single(p=>p.StageCode==5).StageIndex!=index)
            throw new InvalidDataException("Lens exact LOD/read instruction shader changed.");
        var input=recipe.OrderedEffectiveBindings.Last(b=>b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&
            b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&b.RegisterIndex==register&&(b.StageMask&1)!=0);
        int expectedInput=pass switch{20=>31,21=>32,22=>33,23=>32,24=>register==0?32:33,
            35=>35,37 or 39=>register switch{0=>35,1=>recipe.Initialization.AnamorphicEnabled16?Named("AnamorphicBloom1"):43,2=>41,_=>throw new InvalidDataException()},
            36 or 38 or 40=>recipe.Initialization.AnamorphicEnabled16?Named("AnamorphicBloom0"):42,_=>throw new InvalidDataException()};
        if(input.ParameterIndex!=expectedInput||input.MipSelector!=-1)
            throw new InvalidDataException("Lens exact original whole-SRV logical binding differs.");
        var cbs=new Dictionary<int,string>();
        foreach(var req in OriginalMapPostEffect3ParameterBindingRequirements.Read(allocation,pass)
            .Where(r=>r.StageCode==5&&r.ShaderDeclaredByteCount is not null).DistinctBy(r=>r.ParameterIndex))
        {
            var cb=recipe.Initialization.ConstantBuffers.Single(c=>c.Declaration.Index==req.ParameterIndex);
            if(cb.FrameNumber!=recipe.FrameNumber||cb.OwnerIdentity!=recipe.ContextOwnerIdentity||cb.SourceSha256!=recipe.SourceSha256||
                cb.Locale!=recipe.Initialization.Locale||cb.Writer!=OriginalMapPostEffect3ConstantBufferWriter.LensLegacy||cb.Bytes.Length!=req.NativeDeclaration.ByteCount)
                throw new InvalidDataException("Lens source LOD footprint current CB producer differs.");
            cbs.Add(req.ParameterIndex,Convert.ToHexString(SHA256.HashData(cb.Bytes.Span)));
        }
        int[] mips;string domain;bool sampled=down&&pass!=24||up&&(pass&1)!=0;
        if(pass==24){mips=register==0?[0,2]:[1,3];domain="MakeBloomBase exact local-ID branches:Load t0 mips0/2,t1 mips1/3";}
        else if(pass==37&&register==2){mips=[0];domain="UpScaleAdd t2 SampleLevel literal0;actual Ghost41 register binding";}
        else
        {
            int scaleIndex=OriginalMapPostEffect3ParameterBindingRequirements.Read(allocation,pass)
                .First(r=>r.StageCode==5&&r.RegisterIndex==3&&r.ShaderDeclaredByteCount is not null).ParameterIndex;
            var scale=recipe.Initialization.ConstantBuffers.Single(c=>c.Declaration.Index==scaleIndex);
            float level=BinaryPrimitives.ReadSingleLittleEndian(scale.Bytes.Span[8..]);
            int expected=up?(40-pass)/2:pass<22?0:pass-21;
            if(!float.IsFinite(level)||level!=expected)
                throw new NotSupportedException("Lens exact native scaling CB integerLOD differs; fractional/modified LOD needs a separate footprint.");
            int readLevel=up&&(pass&1)!=0&&register==1?expected+1:expected;
            mips=[readLevel];
            domain=up?(sampled?"UpScaleAdd t0 actualCB3.z,t1 actualCB3.z+1;integer SampleLevel":"GaussBlur9 integer Load withftou(actualCB3.z)"):
                "DownScale four sample_l_aoffimmi t0 actualCB3.z integerLOD";
        }
        if(pass!=20)
        {
            var shape=OriginalMapPostEffect3AllocationShapeReader.Read(allocation.TextureAllocations.Single(a=>a.Parameter.Index==input.ParameterIndex));
            if(shape.Dimension!=OriginalMapPostEffect3TextureDimension.Texture2D||mips.Any(m=>m<0||m>=shape.MipLevels))
                throw new InvalidDataException("Lens source read mip set exceeds its actual 2D source declaration.");
        }
        return new(recipe,sha,index,input,mips,cbs,sampled,domain);
        int Named(string name)=>allocation.TextureParameters.Single(p=>p.NameHash==OriginalMapPostEffect3ParameterState.PrimaryNameHash(name)).Index;
    }
}
