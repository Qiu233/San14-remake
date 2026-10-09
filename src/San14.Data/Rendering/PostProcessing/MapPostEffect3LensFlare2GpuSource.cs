using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Source admitted LensFlare2 pass and complete output domain. Actual
/// GPU submission, input readiness, native raster and writer revisions must be
/// joined separately by the execution owner. No complete frame is claimed.</summary>
public sealed class MapPostEffect3LensFlare2GpuPassSource
{
    private readonly MapPostEffect3ParameterLensFlare2Update update;
    private readonly MapPostEffect3TextureParameterState table;
    private MapPostEffect3LensFlare2GpuPassSource(MapPostEffect3ParameterLensFlare2Update update,
        MapPostEffect3TextureParameterState table,int pass,int output,string program,string? vertex,
        MapPostEffect3DispatchExtent? dispatch,MapPostEffect3DispatchExtent? local,IReadOnlyList<int> reads)
    {
        this.update=update;this.table=table;SourcePassIndex=pass;OutputParameterIndex=output;
        ProgramSha256=program;VertexProgramSha256=vertex;ActualDispatch=dispatch;LocalSize=local;InputTextureParameters=reads;
        var allocation=table.Allocation.TextureAllocations.Single(t=>t.Parameter.Index==output);
        OutputWidth=allocation.Width;OutputHeight=allocation.Height;
        ConstantBufferBytesSha256=Convert.ToHexString(SHA256.HashData(update.ConstantBuffer.Bytes.Span));
    }
    public long FrameNumber=>update.FrameNumber;
    public string ContextOwnerIdentity=>update.OwnerIdentity;
    public string SourceSha256=>update.ConstantBuffer.SourceSha256;
    public int SourcePassIndex{get;}
    public int OutputParameterIndex{get;}
    public uint OutputWidth{get;}
    public uint OutputHeight{get;}
    public string ProgramSha256{get;}
    public string? VertexProgramSha256{get;}
    public string ConstantBufferBytesSha256{get;}
    public MapPostEffect3DispatchExtent? ActualDispatch{get;}
    public MapPostEffect3DispatchExtent? LocalSize{get;}
    public IReadOnlyList<int> InputTextureParameters{get;}
    public MapPostEffect3LensFlare2PassRecipe Recipe=>update.PreparePass(SourcePassIndex);
    public string SourceEvidence=>update.SourceEvidence+";original-LF2-PS4/CS41..44-complete1024-square-output;"+
        "CS41/42-FFT1024-per-lane-dual-store-before-barrier+ten-butterfly-barriers;CS43/44-one-unconditional-store-per-global-xy";
    public void RequireCurrent()=>update.RequireCurrentTextureTable(table);
    public void RequireCurrentTextureTable(MapPostEffect3TextureParameterState actualTable)
    {if(!ReferenceEquals(table,actualTable))throw new InvalidDataException("LensFlare2 GPU source uses another actual texture table.");RequireCurrent();}

    public static MapPostEffect3LensFlare2GpuPassSource Prepare(MapPostEffect3AllocationPlan allocation,
        MapPostEffect3ParameterLensFlare2Update update,MapPostEffect3TextureParameterState table,int pass)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(update);ArgumentNullException.ThrowIfNull(table);
        update.RequireCurrentTextureTable(table);MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(!ReferenceEquals(allocation,table.Allocation)||allocation.Source.Sha256!=update.ConstantBuffer.SourceSha256||
            allocation.ContextOwnerIdentity!=update.OwnerIdentity)
            throw new InvalidDataException("LensFlare2 GPU source is not this allocation/module owner.");
        _=update.PreparePass(pass);
        (int output,int shader,string hash,int? input)=pass switch{
            45=>(50,4,"9F70DDD072A148FB1F3EAE3820C90A18EDDB5A32710FC919477EEA41E9802168",(int?)null),
            46=>(52,41,"1753C6CA03CE3F0CEC8629621D34CC734050C8AB031DCA0EFD24FCA83BDA8C8D",51),
            47=>(53,42,"1753C6CA03CE3F0CEC8629621D34CC734050C8AB031DCA0EFD24FCA83BDA8C8D",52),
            48=>(54,43,"93C32926A47FCBF037EE716FD48C35A9A9082EA04BB347CD87F460A51FC13588",53),
            49=>(55,44,"8E486A05862B8459B578E8BEE74BEBB9EE609DF6F09D6EA7CB362EA0C3E27FC8",54),
            _=>throw new NotSupportedException("LensFlare2 active halo/ghost draws need their source-specific input and primitive owners.")};
        uint stage=pass==45?0u:5u;var program=allocation.Source.GetProgram(stage,shader);
        if(program.Sha256!=hash||Convert.ToHexString(SHA256.HashData(program.Bytes.Span))!=hash||
            !allocation.Source.Passes[pass].ProgramReferences.Any(r=>r.StageCode==stage&&r.StageIndex==shader))
            throw new InvalidDataException("LensFlare2 selected program differs from its exact original output control flow.");
        string? vertex=null;
        if(pass==45)
        {
            vertex="34C87F23A9643C887C1B6AC951CB6438C368C106E238CDDAE7CFA9095D09C1C3";
            if(allocation.Source.GetProgram(1,1).Sha256!=vertex||
                !allocation.Source.Passes[pass].ProgramReferences.Any(r=>r.StageCode==1&&r.StageIndex==1))
                throw new InvalidDataException("LensFlare2 diaphragm requires its exact original fullscreen VS1.");
            float depth=BinaryPrimitives.ReadSingleLittleEndian(update.ConstantBuffer.Bytes.Span[72..]);
            if(!float.IsFinite(depth)||depth<0||depth>1)
                throw new NotSupportedException("LensFlare2 current native context clip depth can reject the complete target domain.");
        }
        var target=table.Require(output);
        if(target.Reference.PhysicalAllocationParameterIndex!=output||target.Reference.HasSourceSelectedMipView)
            throw new NotSupportedException("LensFlare2 initialization output requires its actual owned full wrapper.");
        var shape=MapPostEffect3AllocationShapeReader.Read(allocation.TextureAllocations.Single(t=>t.Parameter.Index==output));
        if(shape.Width!=1024||shape.Height!=1024||shape.Depth!=1||(pass!=45&&(!shape.AllowsUnorderedAccess||shape.MipLevels!=(pass==49?8:1))))
            throw new NotSupportedException("LensFlare2 pinned kernels require their exact 1024-square source allocations.");
        var dispatch=pass==45?null:allocation.Passes[pass].DispatchExtent;
        MapPostEffect3DispatchExtent? local=pass==45?null:pass is 46 or 47?new(1024,1,1):new(16,16,1);
        if(pass!=45&&dispatch!=(pass is 46 or 47?new(1u,1024u,1u):new MapPostEffect3DispatchExtent(64,64,1)))
            throw new InvalidDataException("LensFlare2 native FFT/spectrum dispatch changed.");
        var bindings=allocation.Passes[pass].ActiveBindings.ToArray();
        if(!bindings.Where(b=>b.Role is MapPostEffect3BindingRole.RenderTargetWrite or MapPostEffect3BindingRole.UnorderedAccessReadWrite)
            .All(b=>b.ParameterIndex==output)||bindings.Where(b=>b.Role==MapPostEffect3BindingRole.ShaderResourceRead)
            .Any(b=>b.ParameterIndex!=input||b.RegisterIndex!=0))
            throw new InvalidDataException("LensFlare2 pinned shader bindings no longer match their source producer chain.");
        return new(update,table,pass,output,hash,vertex,dispatch,local,input is{}i?Array.AsReadOnly(new[]{i}):[]);
    }

    /// <summary>Exact original FFT local storage, never an external resource
    /// waiver. The caller must additionally join this source's program/hash.
    /// </summary>
    public IReadOnlySet<uint> RequireFftWorkgroupRoots(ReadOnlySpan<byte> spirv)
    {
        RequireCurrent();if(SourcePassIndex is not(46 or 47))throw new InvalidDataException("Selected LensFlare2 source is not an FFT kernel.");
        if(spirv.Length<20||spirv.Length%4!=0||BinaryPrimitives.ReadUInt32LittleEndian(spirv)!=0x07230203)
            throw new InvalidDataException("LensFlare2 FFT SPIR-V header is invalid.");
        byte[] code=spirv.ToArray();
        var pointers=new Dictionary<uint,(uint Storage,uint Element)>();var arrays=new Dictionary<uint,(uint Element,uint Length)>();
        var uints=new HashSet<uint>();var constants=new Dictionary<uint,(uint Type,uint Value)>();var roots=new List<(uint Type,uint Id)>();
        var barriers=new List<(uint Execution,uint Memory,uint Semantics)>();bool local=false;
        for(int at=20;at<spirv.Length;)
        {
            uint header=BinaryPrimitives.ReadUInt32LittleEndian(spirv[at..]);int n=checked((int)(header>>16));uint op=header&0xFFFF;
            if(n==0||at+n*4>spirv.Length)throw new InvalidDataException("LensFlare2 FFT SPIR-V extent is invalid.");
            uint W(int word)=>BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(at+word*4,4));
            if(op==21&&n==4&&W(2)==32&&W(3)==0)uints.Add(W(1));
            else if(op==28&&n==4)arrays.Add(W(1),(W(2),W(3)));
            else if(op==32&&n==4)pointers.Add(W(1),(W(2),W(3)));
            else if(op==43&&n==4)constants.Add(W(2),(W(1),W(3)));
            else if(op==59&&n==4&&W(3)==4)roots.Add((W(1),W(2)));
            else if(op==224&&n==4)barriers.Add((W(1),W(2),W(3)));
            else if(op==16&&n==6&&W(2)==17)local=W(3)==1024&&W(4)==1&&W(5)==1;
            at+=n*4;
        }
        bool Is(uint id,uint value)=>constants.TryGetValue(id,out var c)&&uints.Contains(c.Type)&&c.Value==value;
        if(!local||roots.Count!=1||barriers.Count!=2||barriers.Any(b=>!Is(b.Execution,2)||!Is(b.Memory,2)||!Is(b.Semantics,264))||
            !pointers.TryGetValue(roots[0].Type,out var p)||p.Storage!=4||!arrays.TryGetValue(p.Element,out var a)||
            !uints.Contains(a.Element)||!Is(a.Length,2048))
            throw new NotSupportedException("LensFlare2 FFT requires its original1024-lane uint32[2048] initialization and two static barrier sites.");
        return new HashSet<uint>{roots[0].Id};
    }
}
