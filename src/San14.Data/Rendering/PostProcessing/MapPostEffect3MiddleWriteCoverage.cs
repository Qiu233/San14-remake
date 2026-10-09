using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Exact original CS6..17 store domains. This proves only the
/// in-range mip0 write domain; successful execution and the physical output
/// revision must be joined by the device owner before granting read access.</summary>
public sealed class MapPostEffect3MiddleWriteCoverage
{
    private MapPostEffect3MiddleWriteCoverage(MapPostEffect3MiddleExecutionRecipe recipe,
        MapPostEffect3Program program,int output,uint width,uint height,
        MapPostEffect3DispatchExtent dispatch,MapPostEffect3DispatchExtent local,
        IReadOnlyDictionary<int,string> constants,IReadOnlyList<int> inputs,string evidence)
    {
        FrameNumber=recipe.FrameNumber;ContextOwnerIdentity=recipe.ContextOwnerIdentity;
        SourceSha256=recipe.SourceSha256;SourcePassIndex=recipe.SourcePassIndex;ProgramSha256=program.Sha256;
        ParameterIndex=output;TextureWidth=width;TextureHeight=height;ActualDispatch=dispatch;LocalSize=local;
        RequiredConstantBufferBytesSha256=constants;RequiredInputTextureParameters=inputs;
        SourceEvidence=recipe.SourceEvidence+";"+evidence;
    }
    public long FrameNumber{get;}
    public string ContextOwnerIdentity{get;}
    public string SourceSha256{get;}
    public int SourcePassIndex{get;}
    public string ProgramSha256{get;}
    public int ParameterIndex{get;}
    public uint TextureWidth{get;}
    public uint TextureHeight{get;}
    public int TextureMipLevel=>0;
    public MapPostEffect3DispatchExtent LocalSize{get;}
    public MapPostEffect3DispatchExtent ActualDispatch{get;}
    public IReadOnlyDictionary<int,string> RequiredConstantBufferBytesSha256{get;}
    public IReadOnlyList<int> RequiredInputTextureParameters{get;}
    public string SourceEvidence{get;}
    public bool EstablishesGpuCompletion=>false;

    public static IReadOnlyList<MapPostEffect3MiddleWriteCoverage> Prepare(
        MapPostEffect3AllocationPlan allocation,MapPostEffect3MiddleExecutionRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(recipe);recipe.RequireCurrent();
        if(!recipe.ExecutesPass||recipe.SourceSha256!=allocation.Source.Sha256||
            recipe.ContextOwnerIdentity!=allocation.ContextOwnerIdentity)
            throw new InvalidDataException("Middle write coverage requires the actual enabled source recipe.");
        int pass=recipe.SourcePassIndex;
        var links=allocation.Passes[pass];
        var stage=links.Pass.ProgramReferences.Single(r=>r.StageCode==5&&r.StageIndex is not null);
        var program=allocation.Source.GetProgram(5,stage.StageIndex!.Value);
        if(stage.StageIndex!=pass-2||program.Sha256!=Hashes[pass-8]||
            Convert.ToHexString(SHA256.HashData(program.Bytes.Span))!=Hashes[pass-8])
            throw new InvalidDataException("Middle shader differs from its exact complete store/control-flow source.");
        var dispatch=links.DispatchExtent??throw new InvalidDataException("Original middle dispatch is absent.");
        uint n=pass switch{8=>7,9=>32,10=>8,_=>16};
        var local=new MapPostEffect3DispatchExtent(n,n,1);
        var outputs=links.ActiveBindings.Where(b=>b.Table==MapPostEffect3BindingTable.TextureParameters&&
            b.Role==MapPostEffect3BindingRole.UnorderedAccessReadWrite&&(b.StageMask&1)!=0)
            .DistinctBy(b=>b.ParameterIndex).OrderBy(b=>b.RegisterIndex).ToArray();
        if(!outputs.Select(b=>b.ParameterIndex).SequenceEqual(OutputParameters[pass-8])||
            !outputs.Select(b=>(int)b.RegisterIndex).SequenceEqual(Enumerable.Range(0,outputs.Length)))
            throw new InvalidDataException("Middle UAV stores are bound to another original counted output mapping.");
        var constants=new System.Collections.ObjectModel.ReadOnlyDictionary<int,string>(
            recipe.ConstantBufferBindings.Where(b=>b.ShaderDeclaresRegister).Select(b=>b.ConstantBuffer)
                .DistinctBy(cb=>cb.Declaration.Index).ToDictionary(cb=>cb.Declaration.Index,
                    cb=>Convert.ToHexString(SHA256.HashData(cb.Bytes.Span))));
        var inputs=Array.AsReadOnly(links.ActiveBindings.Where(b=>b.Table==MapPostEffect3BindingTable.TextureParameters&&
            b.Role==MapPostEffect3BindingRole.ShaderResourceRead&&(b.StageMask&1)!=0)
            .Select(b=>b.ParameterIndex).Distinct().Order().ToArray());
        var result=new List<MapPostEffect3MiddleWriteCoverage>();
        foreach(var output in outputs)
        {
            var shape=MapPostEffect3AllocationShapeReader.Read(
                allocation.TextureAllocations.Single(a=>a.Parameter.Index==output.ParameterIndex));
            uint perGroup=pass==9&&output.ParameterIndex==17?2u:pass==16?1u:n;
            if(output.MipSelector!=-1||shape.Dimension!=MapPostEffect3TextureDimension.Texture2D||
                shape.Depth!=1||shape.MipLevels!=1||dispatch.Z!=1||
                (ulong)dispatch.X*perGroup<shape.Width||(ulong)dispatch.Y*perGroup<shape.Height)
                throw new NotSupportedException("Actual source middle dispatch does not cover every coordinate of this original single mip.");
            string domain=pass==9&&output.ParameterIndex==17?
                "CS7 reduction: group-local xy<2;u3[2*group.xy+local.xy];four writes per32x32 group":
                pass==16?"CS14 reduction: local.xy<1;u0[group.xy];one write per16x16 group":
                pass is 18 or 19?"CS16/17: early branch writes global.xy before ret;remaining branch unconditionally writes global.xy":
                "unconditional source store_uav_typed at global-thread-id.xy;all control flow rejoins before final stores";
            result.Add(new(recipe,program,output.ParameterIndex,shape.Width,shape.Height,dispatch,local,constants,inputs,
                "exact-DXBC-SHA="+program.Sha256+";"+domain+";in-range target mip0 only"));
        }
        return result.AsReadOnly();
    }
    private static readonly int[][] OutputParameters=[
        [12],[14,15,16,17],[18],[19,20,21],[15,16,22],[19,20],[23],[24,25],[26],[27],[29],[30]];
    private static readonly string[] Hashes=[
        "05BEF273CAC020EB57DFDF2C229579C69FE11D946D0DDE7630FF7DD1B9917557",
        "25D6CDDBF137D3AB660ECA3D2407A8AC887F0A9D38E73A5DD6BBD87A6222C676",
        "06462E26A589708656D48E259E676044ADE82165A0278F71572247D74FF7439A",
        "A618739C4A0811800A2E3232A63F8CEEAFAF82D829B2C0B0A8106632BE779857",
        "A161C12A1B2EF5E4396D24131CBC63DECB14B5B228DDC26FB5F77EE483ACF36E",
        "69C4700917158C938BB50F87C3CDBFFC836AEAB227AD61511E005AB897326628",
        "8587B8FE08E83A7ABF1D71574E4E0CACD8C4893EF89766C08A9F0AD6692D07E4",
        "BA29B4DBC9C6E6482896E42EC8CBDFF11F23492B3BE91AFC81F04291FA535119",
        "FCE770CE0F3BE699BA7AA285E05CDF7037010B176CA5EBF79680575F80B9CD86",
        "3A9C1507BC639C3A86907CB46D65192D2F08EBF3DD8A7A5CA22AC5781E76E62B",
        "4798D4F58C5983E7425FE4C28021519E1FE4049FF18DBA25574ECC2B3DE62C3A",
        "4798D4F58C5983E7425FE4C28021519E1FE4049FF18DBA25574ECC2B3DE62C3A"];
}
