using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Positively identified original CS image-store domain. This is not
/// a GPU completion receipt. Publication additionally needs actual executor
/// bindings, initialized input views and completed physical output storage.</summary>
public sealed class OriginalMapPostEffect3LensTextureWriteCoverage
{
    private OriginalMapPostEffect3LensTextureWriteCoverage(OriginalMapPostEffect3LensExecutionRecipe recipe,
        string programSha,int parameter,int mip,uint width,uint height,OriginalMapPostEffect3DispatchExtent local,
        IReadOnlyDictionary<int,string> cbs,IReadOnlyList<OriginalMapPostEffect3Binding> textureReads,
        IReadOnlyList<OriginalMapPostEffect3Binding> bufferReads,IReadOnlyList<int> readWriteInputs,
        IReadOnlyList<OriginalMapPostEffect3NullShaderResource> nullReads,string evidence)
    {
        SourceSha256=recipe.SourceSha256;ContextOwnerIdentity=recipe.ContextOwnerIdentity;FrameNumber=recipe.FrameNumber;
        SourcePassIndex=recipe.SourcePassIndex;ProgramSha256=programSha;ParameterIndex=parameter;TextureMipLevel=mip;
        TextureWidth=width;TextureHeight=height;ActualDispatch=recipe.ActualDispatch;LocalSize=local;
        RequiredConstantBufferBytesSha256=cbs;RequiredInputTextureBindings=textureReads;RequiredInputBufferBindings=bufferReads;
        RequiredInputTextureParameters=Array.AsReadOnly(textureReads.Select(b=>b.ParameterIndex).Distinct().ToArray());
        RequiredInputBufferParameters=Array.AsReadOnly(bufferReads.Select(b=>b.ParameterIndex).Distinct().ToArray());
        RequiredPriorUavContentsParameters=readWriteInputs;SourceEvidence=evidence;
        RequiredNullSources=nullReads;RequiredNullRegisters=Array.AsReadOnly(nullReads.Select(n=>n.SourceRegister).Order().ToArray());
    }
    public string SourceSha256{get;}
    public string ContextOwnerIdentity{get;}
    public long FrameNumber{get;}
    public int SourcePassIndex{get;}
    public string ProgramSha256{get;}
    public int ParameterIndex{get;}
    public int TextureMipLevel{get;}
    public uint TextureWidth{get;}
    public uint TextureHeight{get;}
    public OriginalMapPostEffect3DispatchExtent ActualDispatch{get;}
    public OriginalMapPostEffect3DispatchExtent LocalSize{get;}
    public string SourceEvidence{get;}
    public IReadOnlyDictionary<int,string> RequiredConstantBufferBytesSha256{get;}
    public IReadOnlyList<int> RequiredInputTextureParameters{get;}
    public IReadOnlyList<int> RequiredInputBufferParameters{get;}
    public IReadOnlyList<OriginalMapPostEffect3Binding> RequiredInputTextureBindings{get;}
    public IReadOnlyList<OriginalMapPostEffect3Binding> RequiredInputBufferBindings{get;}
    /// <summary>CS38 samples the previous adaptation texel before replacing
    /// both output texels. Fresh undefined storage cannot satisfy this read.</summary>
    public IReadOnlyList<int> RequiredPriorUavContentsParameters{get;}
    public IReadOnlyList<uint> RequiredNullRegisters{get;}
    public IReadOnlyList<OriginalMapPostEffect3NullShaderResource> RequiredNullSources{get;}
    public bool EstablishesGpuCompletion=>false;

    public static IReadOnlyList<OriginalMapPostEffect3LensTextureWriteCoverage> Prepare(
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3LensExecutionRecipe recipe,
        IReadOnlyList<OriginalMapPostEffect3NullShaderResource>? actualSourceNulls=null)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(recipe);
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(!recipe.ExecutesPass||recipe.SourceSha256!=allocation.Source.Sha256||recipe.ContextOwnerIdentity!=allocation.ContextOwnerIdentity||
            recipe.FrameNumber<=0||recipe.SourcePassIndex<20||recipe.SourcePassIndex>43)
            throw new InvalidDataException("Lens image coverage requires its actual active owned execution recipe.");
        int pass=recipe.SourcePassIndex;
        var suppliedNulls=(actualSourceNulls??Array.Empty<OriginalMapPostEffect3NullShaderResource>()).ToArray();
        foreach(var sourceNull in suppliedNulls)
        {
            sourceNull.ValidateCurrent();
            if(sourceNull.SourcePassIndex!=pass||sourceNull.SourceStageCode!=5||sourceNull.FrameNumber!=recipe.FrameNumber||
                sourceNull.ContextOwnerIdentity!=recipe.ContextOwnerIdentity||sourceNull.SourceSha256!=recipe.SourceSha256||
                sourceNull.SourceRegister!=2||sourceNull.Binding is not null||sourceNull.ParameterIndex is not null||pass is not(35 or 39))
                throw new InvalidDataException("Lens complete image coverage requires exact original35/39 reset-null t2 receipts, with no fake parameter.");
        }
        if(suppliedNulls.Select(n=>n.SourceRegister).Distinct().Count()!=suppliedNulls.Length)
            throw new InvalidDataException("Lens source-null register receipt is duplicated.");
        if(pass==41)
            throw new NotSupportedException("Original ToneMapInfo texture44 is4x1, but CS38 only writes texels0/1 and reads prior texel1; no complete-mip or fresh preserved-input producer exists.");
        if(!Profiles.TryGetValue(pass,out var profile))
            throw new NotSupportedException("This Lens pass has no recovered complete image-store domain; buffer and raster passes use separate producers.");
        var refs=allocation.Source.Passes[pass].ProgramReferences.Where(p=>p.StageIndex is not null).ToArray();
        if(refs.Length!=1||refs[0].StageCode!=5||refs[0].StageIndex!=profile.Program)
            throw new InvalidDataException("Lens image coverage selected program differs from its actual source pass.");
        var program=allocation.Source.GetProgram(5,profile.Program);
        if(program.Sha256!=profile.Sha||Convert.ToHexString(SHA256.HashData(program.Bytes.Span))!=profile.Sha)
            throw new InvalidDataException("Lens image kernel full bytecode differs from its recovered control/store domain.");
        CheckKernel(program,profile,pass);
        if(recipe.ActualDispatch.X==0||recipe.ActualDispatch.Y==0||recipe.ActualDispatch.Z!=1)
            throw new NotSupportedException("Lens complete image writes require a positive exact 2D dispatch.");
        var cbs=new Dictionary<int,string>();
        foreach(var req in OriginalMapPostEffect3ParameterBindingRequirements.Read(allocation,pass)
            .Where(r=>r.StageCode==5&&r.ShaderDeclaredByteCount is not null).DistinctBy(r=>r.ParameterIndex))
        {
            var cb=recipe.Initialization.ConstantBuffers.Single(c=>c.Declaration.Index==req.ParameterIndex);
            if(cb.FrameNumber!=recipe.FrameNumber||cb.OwnerIdentity!=recipe.ContextOwnerIdentity||cb.SourceSha256!=recipe.SourceSha256||
                cb.Locale!=recipe.Initialization.Locale||cb.Writer!=OriginalMapPostEffect3ConstantBufferWriter.LensLegacy||cb.Bytes.Length!=req.NativeDeclaration.ByteCount)
                throw new InvalidDataException("Lens coverage CB producer/frame/extent differs from the actual initialization.");
            cbs.Add(req.ParameterIndex,Convert.ToHexString(SHA256.HashData(cb.Bytes.Span)));
        }
        // Only resource registers declared by the exact shader are required;
        // counted duplicated native rows retain their last effective binding.
        var rdef=DxbcResourceDefinitionReader.Read(program.Bytes.Span);
        var texReads=new List<OriginalMapPostEffect3Binding>();var bufReads=new List<OriginalMapPostEffect3Binding>();
        var requiredNulls=new List<OriginalMapPostEffect3NullShaderResource>();
        foreach(var input in rdef.Bindings.Where(b=>b.InputType is 2 or 5 or 7))
        {
            if(input.BindCount!=1)throw new NotSupportedException("Lens image coverage has an unclosed resource-register array.");
            var table=input.InputType==2?OriginalMapPostEffect3BindingTable.TextureParameters:OriginalMapPostEffect3BindingTable.BufferParameters;
            if(table==OriginalMapPostEffect3BindingTable.TextureParameters&&
                !recipe.OrderedEffectiveBindings.Any(b=>b.Table==table&&b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&
                    b.RegisterIndex==input.BindPoint&&(b.StageMask&1)!=0))
            {
                var sourceNull=suppliedNulls.SingleOrDefault(n=>n.SourceRegister==input.BindPoint);
                if(sourceNull is null)throw new InvalidDataException($"Lens pass{pass} CS{profile.Program} t{input.BindPoint} needs its actual source register-reset/null receipt.");
                requiredNulls.Add(sourceNull);continue;
            }
            var binding=Effective(table,OriginalMapPostEffect3BindingRole.ShaderResourceRead,checked((int)input.BindPoint));
            (table==OriginalMapPostEffect3BindingTable.TextureParameters?texReads:bufReads).Add(binding);
        }
        if(requiredNulls.Count!=suppliedNulls.Length)throw new InvalidDataException("Lens source-null receipt has no actual original shader read.");
        if(pass==42)
        {
            var scale=recipe.Initialization.ConstantBuffers.Single(c=>c.Declaration.Index==
                OriginalMapPostEffect3ParameterBindingRequirements.Read(allocation,pass).First(r=>r.RegisterIndex==3&&r.StageCode==5).ParameterIndex);
            float n=BinaryPrimitives.ReadSingleLittleEndian(scale.Bytes.Span);
            // Exact CS39 converts width to unsigned, shifts right once and
            // uses an unsigned counter with uge break then iadd +1.
            if(!float.IsFinite(n)||n<0||n>=2147483648f)
                throw new NotSupportedException("Anamorphic image coverage requires its finite source width and nonwrapping bounded loop.");
        }
        if(pass==41)
        {
            var main=recipe.Initialization.ConstantBuffers.Single(c=>c.Declaration.Index==6);
            if(recipe.ActualDispatch!=new OriginalMapPostEffect3DispatchExtent(1,1,1)||
                BinaryPrimitives.ReadSingleLittleEndian(main.Bytes.Span)!=(float)allocation.ContextWidth||
                BinaryPrimitives.ReadSingleLittleEndian(main.Bytes.Span[4..])!=(float)allocation.ContextHeight)
                throw new NotSupportedException("ToneMapInfo requires exactly one native16-thread group and actual context-size loop bounds.");
        }
        var result=new List<OriginalMapPostEffect3LensTextureWriteCoverage>();
        foreach(var output in rdef.Bindings.Where(b=>b.InputType==4).OrderBy(b=>b.BindPoint))
        {
            if(output.BindCount!=1)throw new NotSupportedException("Lens image-store UAV array is not recovered.");
            int register=checked((int)output.BindPoint);
            var binding=Effective(OriginalMapPostEffect3BindingTable.TextureParameters,
                OriginalMapPostEffect3BindingRole.UnorderedAccessReadWrite,register);
            var shape=OriginalMapPostEffect3AllocationShapeReader.Read(allocation.TextureAllocations.Single(a=>a.Parameter.Index==binding.ParameterIndex));
            int mip=binding.MipSelector;
            if(mip==-1&&shape.MipLevels==1)mip=0;
            if(shape.Dimension!=OriginalMapPostEffect3TextureDimension.Texture2D||!shape.AllowsUnorderedAccess||mip<0||mip>=shape.MipLevels)
                throw new NotSupportedException("Lens coverage needs a recovered actual typed 2D UAV mip.");
            uint width=Math.Max(1u,shape.Width>>mip),height=Math.Max(1u,shape.Height>>mip);
            ulong coveredX=(ulong)recipe.ActualDispatch.X*profile.Local.X,coveredY=(ulong)recipe.ActualDispatch.Y*profile.Local.Y;
            string domain="global-thread-id.xy;unconditional typed float4 store after all value calculations";
            if(pass==24)
            {
                uint side=register switch{0=>8u,1=>4u,2=>2u,3=>1u,_=>throw new InvalidDataException("Bloom multi-mip store register changed.")};
                coveredX=(ulong)recipe.ActualDispatch.X*side;coveredY=(ulong)recipe.ActualDispatch.Y*side;
                domain=$"local8x8;u{register}: lanes x/y<{side}, coordinate group.xy*{side}+lane.xy;conditions depend only on lane IDs, never sampled values";
            }
            else if(pass is 26 or 27)
            {
                if(recipe.ActualDispatch.X!=1||recipe.ActualDispatch.Y!=1024||width!=1024||height!=1024)
                    throw new NotSupportedException("FFT complete transpose requires original1024x1024 extent and one1024-thread X group per row.");
                coveredX=recipe.ActualDispatch.Y;coveredY=1024;
                domain="local1024x1;10 fixed FFT rounds with unconditional barriers;after loop unconditional store globalThread.yx;bijective1024x1024 transpose";
            }
            else if(pass==41)
            {
                if(width!=2||height!=1||register!=0)throw new NotSupportedException("ToneMapInfo source writes exactly the two texels of the original2x1 target.");
                coveredX=2;coveredY=1;domain="one16-thread group;bounded reductions;thread0 writes literal(1,0) and(0,0), after reading prior u0(1,0)";
            }
            if(coveredX<width||coveredY<height)
                throw new NotSupportedException("Lens actual dispatch/store-coordinate domain does not cover every target mip texel.");
            IReadOnlyList<int> prior=pass==41?Array.AsReadOnly(new[]{binding.ParameterIndex}):Array.Empty<int>();
            result.Add(new(recipe,profile.Sha,binding.ParameterIndex,mip,width,height,profile.Local,
                new ReadOnlyDictionary<int,string>(new Dictionary<int,string>(cbs)),texReads.AsReadOnly(),bufReads.AsReadOnly(),prior,requiredNulls.AsReadOnly(),
                $"source-full-DXBC/SHEX-SHA={profile.Sha};CS{profile.Program};pass{pass};{domain};"+
                "source-store-domain-only;native duplicate binding order plus actual Before last-write;input/CB/physical-GPU-completion joins required"));
        }
        if(result.Count!=profile.OutputCount)throw new InvalidDataException("Lens complete source image-store count differs from its exact shader.");
        return result.AsReadOnly();

        OriginalMapPostEffect3Binding Effective(OriginalMapPostEffect3BindingTable table,OriginalMapPostEffect3BindingRole role,int register)
            =>recipe.OrderedEffectiveBindings.LastOrDefault(b=>b.Table==table&&b.Role==role&&b.RegisterIndex==register&&(b.StageMask&1)!=0)
                ??throw new InvalidDataException("Lens actual shader resource has no effective native binding.");
    }

    private sealed record Profile(int Program,string Sha,OriginalMapPostEffect3DispatchExtent Local,int OutputCount);
    private static readonly IReadOnlyDictionary<int,Profile> Profiles=new Dictionary<int,Profile>
    {
        [20]=new(18,"582167FBF37357F1BA3D78E7BCF8E5C291FE2577804FAF6A27B9074C6D5941F0",new(16,16,1),1),
        [21]=new(19,"582167FBF37357F1BA3D78E7BCF8E5C291FE2577804FAF6A27B9074C6D5941F0",new(16,16,1),1),
        [22]=new(20,"582167FBF37357F1BA3D78E7BCF8E5C291FE2577804FAF6A27B9074C6D5941F0",new(16,16,1),1),
        [23]=new(21,"0501748E162819EB7F3715BAE82F05F2469F221A50AD5CE64BAEB3C97CFD4E8E",new(16,16,1),2),
        [24]=new(22,"5E24F6D276510C0714A6494AAEC2568D1A936AD61AFE7DFDB71A6AA85F634F48",new(8,8,1),4),
        [25]=new(23,"BCF54024FE5BD904561BFB4F881995C4B904F11EFE9981AE8D49662613B80D30",new(8,8,1),1),
        [26]=new(24,"DC6D9814316F3FD42E08254C80B9698408F254753DE73612ECF11D119C7944BA",new(1024,1,1),1),
        [27]=new(25,"DC6D9814316F3FD42E08254C80B9698408F254753DE73612ECF11D119C7944BA",new(1024,1,1),1),
        [28]=new(26,"53C94951B8C9708A43139AEF209C66DF896CAB92C409D2D46106BA0EAF5AD8FC",new(8,8,1),1),
        [29]=new(27,"28F0AB02A911ED0ACDB4557BA05E32B6E0A809A2C81672A4A296F2620D48D5ED",new(8,8,1),1),
        [33]=new(31,"17EBDE78D6B9B1067FBEB2A07C66CF8A4DCD7BD9E7B810C992D85D370ECB0082",new(16,16,1),1),
        [35]=new(32,"DB9FE7FFDC11813E9513991299128253C8679841F43DB00F9A12D54A6626AF4B",new(16,16,1),1),
        [36]=new(33,"CE5EA4EEE87A35A7B124929EAD9C3741EAC5E275EAFEC849DB68DCBEFB6F4416",new(16,16,1),1),
        [37]=new(34,"DB9FE7FFDC11813E9513991299128253C8679841F43DB00F9A12D54A6626AF4B",new(16,16,1),1),
        [38]=new(35,"CE5EA4EEE87A35A7B124929EAD9C3741EAC5E275EAFEC849DB68DCBEFB6F4416",new(16,16,1),1),
        [39]=new(36,"DB9FE7FFDC11813E9513991299128253C8679841F43DB00F9A12D54A6626AF4B",new(16,16,1),1),
        [40]=new(37,"CE5EA4EEE87A35A7B124929EAD9C3741EAC5E275EAFEC849DB68DCBEFB6F4416",new(16,16,1),1),
        [41]=new(38,"5F84FD6A28934662775F9D2D13DD52E1B9A3ED4A3FB7775676C1D851C89939C5",new(16,1,1),1),
        [42]=new(39,"39DE4325CE13921F2FF2874D93E7479B52BD41D2C5AF3A567F47AF374E4752FE",new(16,16,1),1),
        [43]=new(40,"6BE02E4F34CDCD712B20EBEBB26E979A9B8D18907E13C84132495587DC844B4A",new(16,16,1),1),
    };
    private static void CheckKernel(OriginalMapPostEffect3Program program,Profile profile,int pass)
    {
        var part=program.Container.Parts.Single(p=>p.TokenHeader is not null);var source=part.PayloadRange.Slice(program.Bytes.Span);
        int count=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source[4..]));
        if(source.Length!=count*4)throw new InvalidDataException("Lens original SHEX token extent changed.");
        int groupCount=0,storeCount=0,retCount=0;
        for(int word=2;word<count;)
        {
            uint token=BinaryPrimitives.ReadUInt32LittleEndian(source[(word*4)..]);int opcode=(int)(token&2047);
            int n=opcode==53?checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source[((word+1)*4)..])):(int)((token>>24)&127);
            if(n<=0||word+n>count)throw new InvalidDataException("Lens token instruction extent differs.");
            if(opcode==155)
            {if(n!=4||Word(source,word+1)!=profile.Local.X||Word(source,word+2)!=profile.Local.Y||Word(source,word+3)!=profile.Local.Z)throw new InvalidDataException("Lens exact numthreads changed.");groupCount++;}
            if(opcode==164)storeCount++;
            if(opcode==62)retCount++;
            // All these positively frozen programs are straight-line up to
            // their typed stores. The separately proved domains have source
            // branch/finite-loop semantics described above.
            if(pass is not (24 or 26 or 27 or 33 or 41 or 42)&&(opcode is 2 or 3 or 4 or 7 or 8 or 18 or 21 or 22 or 31 or 48 or 63))
                throw new InvalidDataException("Lens unconditional image kernel unexpectedly contains control flow.");
            word+=n;
        }
        int expectedStores=pass==41?2:profile.OutputCount;
        if(groupCount!=1||storeCount!=expectedStores||retCount!=1)
            throw new InvalidDataException("Lens original local-size/store/return control domain changed.");
    }
    private static uint Word(ReadOnlySpan<byte> bytes,int word)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[(word*4)..]);
}
