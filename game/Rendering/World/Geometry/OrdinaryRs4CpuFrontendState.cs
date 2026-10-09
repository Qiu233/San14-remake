using San14.Data;

/// <summary>An unobserved cache lane stays null until an actual source
/// writer covers it. These values are not native constructor defaults.</summary>
public readonly record struct OrdinaryRs4DepthProjection(bool? Test,bool? Write,uint? Compare,bool? Stencil)
{
    public WorldGeometryLateDepth RequireActualDepth()=> Test is bool t&&Write is bool w&&
        Compare is uint c&&Stencil is bool s ? new(t,w,c,s) :
        throw new InvalidOperationException("Earlier RS4 has not written/observed the complete depth subset.");
    public static OrdinaryRs4DepthProjection FromActual(WorldGeometryLateDepth value)
        =>new(value.Test,value.Write,value.Compare,value.Stencil);
}
public sealed record OrdinaryRs4StencilProjection(bool? Enabled,uint? Compare,byte? Reference,
    byte? ReadMask,byte? WriteMask,uint? Fail,uint? DepthFail,uint? Pass);

/// <summary>Retained caller subset for earlier RS4. The normal finite entry
/// can begin with an unobserved projection, because D43577/D436DA and the
/// selected-record writers replace the fields they consume. Cull and other
/// opaque raster fields are never invented. The source-owned queue's type10
///23 is applied independently of its primitive's null renderer slot.</summary>
public sealed class OrdinaryRs4CpuFrontendState
{
    private readonly WorldGeometryLateColorState?[] colors=new WorldGeometryLateColorState?[8];
    private OrdinaryRs4CpuFrontendState() { }
    public static OrdinaryRs4CpuFrontendState UnobservedCallerProjection()=>new();
    public static OrdinaryRs4CpuFrontendState CaptureActual(WorldGeometryLateCpuState actual,
        uint? actualSourceCullCode=null,uint? actualSourceFillCode=null)
    {
        ArgumentNullException.ThrowIfNull(actual);
        var value=new OrdinaryRs4CpuFrontendState {Depth=OrdinaryRs4DepthProjection.FromActual(actual.Depth),
            AlphaEnabled=actual.ActualAlphaEnabled,ClipWord1F0=actual.ActualClipWord1F0,
            ActualSourceCullCode=actualSourceCullCode,ActualSourceFillCode=actualSourceFillCode,
            Stencil=new(actual.Stencil.Enabled,actual.Stencil.Compare,actual.Stencil.Reference,actual.Stencil.ReadMask,
                actual.Stencil.WriteMask,actual.Stencil.Fail,actual.Stencil.DepthFail,actual.Stencil.Pass)};
        for(int i=0;i<8;i++)value.colors[i]=actual.ColorProjection[i];
        return value;
    }
    public OrdinaryRs4DepthProjection Depth{get;internal set;}
    public OrdinaryRs4StencilProjection Stencil{get;internal set;}=new(null,null,null,null,null,null,null,null);
    public bool? AlphaEnabled{get;internal set;}
    public uint? ClipWord1F0{get;internal set;}
    public uint? ActualSourceCullCode{get;internal set;}
    public uint? ActualSourceFillCode{get;internal set;}
    public bool? DepthBiasEnabled{get;internal set;}
    public IReadOnlyList<WorldGeometryLateColorState?> ColorProjection=>Array.AsReadOnly(colors);
    internal void SetStencilEnabled(bool value)
    {Stencil=Stencil with{Enabled=value};Depth=Depth with{Stencil=value};}
    internal void SetActiveColors(ModelOrdinaryActiveColorState actualActive)
    {
        for(int i=0;i<actualActive.RgbaWriteMasks.Count;i++)
        {byte rgba=actualActive.RgbaWriteMasks[i];colors[i]=new(false,(byte)(rgba&7),(rgba&8)!=0);}
    }
}
