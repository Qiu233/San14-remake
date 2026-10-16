using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Already selected native View+1E0/+1E4/+90 and Map+2F8B0/+2F480/+2F484.
/// InterpolationPercent retains its compatibility name but contains the raw
/// native +1E4 fraction0..9999, with denominator10000. Band indices are not the
/// four reference-image labels. Display controls remain explicit.</summary>
public sealed record TerritoryStyleSelection(int ZoomBand, int InterpolationPercent,
    double ZoomValue, int StyleSelector, int Width4Selector, int Width5Selector,
    bool HideAllocatedFill, uint ScreenMapMask, uint ProviderFlags)
{
    public int NativeFraction1E4 => InterpolationPercent;
}

public sealed record TerritoryStylePacket(Vector4[] CellColors, Vector4[] LineColors,
    Vector4 FrameColor, float[] LineWidths, float Curvature, uint LayerViewMask, uint[] Flags);

internal sealed record TerritoryStyleResourceSettings(
    IReadOnlyDictionary<string,IReadOnlyList<int>> Tables,Vector3 FrameRgb,
    Vector3 Cell11Rgb,Vector3 Line11Rgb,Vector3 Line19Rgb,float FrameWidthCutoff);

/// <summary>Finite source defaults and the original SC18CA50 / TC18CA70 writer.
/// The project's startup tables and current writer are owned typed rules.
/// Later parameter overrides belong to the caller's current state.</summary>
public sealed class TerritoryStyleSources
{
    public string Locale { get; }
    public string RuleVersion { get; }
    private readonly IReadOnlyDictionary<string, int[]> _tables;
    private readonly Vector3 _frameRgb, _cell11Rgb, _line11Rgb, _line19Rgb;
    private readonly float _frameCutoff;
    private static readonly Vector4 InitializedField = CreateInitializedField();
    private TerritoryStyleSources(string locale, string ruleVersion,
        IReadOnlyDictionary<string,int[]> tables,Vector3 frame,Vector3 cell11,Vector3 line11,Vector3 line19,float frameCutoff)
        => (Locale,RuleVersion,_tables,_frameRgb,_cell11Rgb,_line11Rgb,_line19Rgb,_frameCutoff)=
            (locale,ruleVersion,tables,frame,cell11,line11,line19,frameCutoff);

    public static TerritoryStyleSources LoadCurrent(GameResourceRepository repository,
        LoadedTerritoryResources source,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(source);
        TerritoryResources.ValidateShader(source.Shader);
        if(source.ScreenMapObjects.Source.ResourceUri!="san14://rdb/system/DAB638B2" ||
            source.ScreenMapObject.ObjectId!=0xCA6B0C6B || source.ScreenMapObject.ClassId!=0x12EA4048 ||
            source.ScreenMapObject.Properties.Count!=0)
            throw new InvalidDataException("Territory style defaults require the exact original property-free ScreenMap object.");
        return Create(locale);
    }

    public static TerritoryStyleSources Create(string locale)
    {
        string version=RenderRuleIdentity.For("territory-styles",locale);
        var tables=new Dictionary<string,int[]> {
            ["FillAlpha"]=[0,11800,28000,23500,39210,62740,58820],
            ["Width0"]=[60,100,100,140,160,200,240],
            ["BorderAlpha"]=[62740,62740,62740,62740,62740,78430,94110],
            ["FrameWidth"]=[1000,1000,1000,1400,0,0,0],
            ["FrameAlpha"]=[10000,7000,7000,7000,0,0,0],
            ["Width6"]=[2000,2000,2000,3000,4000,6000,8000],
            ["LineAlpha19"]=[15700,15700,15700,15700,15700,15700,7000],
            ["CellAlpha11"]=[0,0,0,0,50000,100000,100000],
            ["Width1"]=[5000,5000,5000,10000,20000,10000,10000],
            ["LineAlpha11"]=[98000,98000,98000,98000,98000,100000,100000],
            ["Style1Width0"]=[120,120,200,280,320,1600,2400],
            ["Width45"]=[100,100,100,200,1000,2000,2000],
            ["SpecialWidth45"]=[200,200,200,200,500,500,500],
            ["Curvature"]=[0,2,4,7,10,10,10],
            ["ZoomValues"]=[5000,10000,17500,30000,55000,120000,200000],
        };
        return new(locale,version,tables,new Vector3(1),new Vector3(1/255f),new Vector3(1/255f),
            new Vector3(1),700f);
    }

    /// <summary>SC189E20 then186640 initializes all 128 base palettes to zero;
    /// SC18994A..189988 initializes all flags to zero except11/12=1.</summary>
    public static TerritoryStylePacket InitializedScreenMap() =>
        new(new Vector4[128],new Vector4[128],Vector4.Zero,new float[9],0,8,InitializedFlags());
    public static uint[] InitializedFlags(){var values=new uint[128];values[11]=1;values[12]=1;return values;}

    /// <summary>Source00E940's exact native band anchor; no preview zoom factor conversion.</summary>
    public float ZoomAnchor(int nativeBand)=>nativeBand is >=0 and <=6?_tables["ZoomValues"][nativeBand]:
        throw new InvalidDataException("Original View has seven zoom anchors.");

    /// <summary>Immutable load-bound rules for a native ScreenMap resource
    /// generation. Current display selection and ownership are separate.</summary>
    internal TerritoryStyleResourceSettings CaptureResourceSettings()=>new(
        new System.Collections.ObjectModel.ReadOnlyDictionary<string,IReadOnlyList<int>>(
            _tables.ToDictionary(item=>item.Key,item=>(IReadOnlyList<int>)Array.AsReadOnly(item.Value))),
        _frameRgb,_cell11Rgb,_line11Rgb,_line19Rgb,_frameCutoff);

    /// <summary>Read the complete prefix containing native View+90/+1E0/+1E4.
    /// Type identity and readiness are established by the upstream View owner.</summary>
    public static TerritoryStyleSelection ReadViewPrefix(ReadOnlySpan<byte> completePrefix,
        TerritoryStyleSelection retainedMapControls)
    {
        ArgumentNullException.ThrowIfNull(retainedMapControls);
        if(completePrefix.Length!=0x1E8)throw new InvalidDataException("Territory View selection needs the complete original 0x1E8 prefix.");
        var selected=retainedMapControls with {
            ZoomBand=BinaryPrimitives.ReadInt32LittleEndian(completePrefix[0x1E0..]),
            InterpolationPercent=BinaryPrimitives.ReadInt32LittleEndian(completePrefix[0x1E4..]),
            ZoomValue=BinaryPrimitives.ReadDoubleLittleEndian(completePrefix[0x90..])};
        ValidateSelection(selected);return selected;
    }

    /// <summary>Actual RT2D/Array loaders replace constructor sampler fields from the
    /// source DOK properties; submit uses descriptor+38. Keep each register's own recipe.</summary>
    public static IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> ReadSamplers(
        LoadedTerritoryResources source,LoadedDeferredSamplerNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(native);
        TerritoryResources.ValidateShader(source.Shader);
        if(source.Gbuffers.Count!=5||!source.Gbuffers.Select(g=>g.Register).Order().SequenceEqual(new uint[]{1,2,3,4,5}))
            throw new InvalidDataException("Territory source requires its five exact native GBuffer registers.");
        return source.Gbuffers.ToDictionary(g=>g.Register,g=>(ReadOnlyMemory<byte>)
            RenderSamplerSourceAdapters.DecodeKey(native,RenderSamplerSourceAdapters.FinalRenderTextureKey(native,g.Texture)).EncodeD3D11Descriptor());
    }

    /// <summary>Writes the original style fields into complete retained CPU tables.
    /// Native allocated loop stops at the first color60 sentinel, even if a later slot is occupied.</summary>
    public TerritoryStylePacket Write(TerritoryStyleSelection selection,
        TerritoryOwnershipPacket ownership,TerritoryStylePacket retained)
    {
        ArgumentNullException.ThrowIfNull(selection);ArgumentNullException.ThrowIfNull(ownership);ArgumentNullException.ThrowIfNull(retained);
        ValidateSelection(selection);Validate(retained);
        if(ownership.CellColors is null||ownership.LineColors is null||ownership.AllocatedColorIndices is null||
            ownership.CellColors.Length!=128||ownership.LineColors.Length!=128)
            throw new InvalidDataException("Territory style requires both complete source ownership palettes.");
        Vector4[] cells=retained.CellColors.ToArray(),lines=retained.LineColors.ToArray();
        float V(string name)=>Interpolate(_tables[name],selection);
        float fill=selection.StyleSelector==3?1:V("FillAlpha")/100000f;
        float cell11=V("CellAlpha11")/100000f;
        if(fill<0.05f)fill=0;if(cell11<0.05f)cell11=0;
        float border=V("BorderAlpha")/100000f,line11=V("LineAlpha11")/100000f;
        for(int slot=25;slot<128;slot++)
        {
            if(!ownership.AllocatedColorIndices.TryGetValue(slot,out byte color)||color==60)break;
            if(color>59)throw new InvalidDataException("Territory style allocated source color is unresolved.");
            cells[slot]=ownership.CellColors[slot];
            cells[slot].W=selection.HideAllocatedFill?0:fill;
            lines[slot]=selection.HideAllocatedFill?new Vector4(.25f,.25f,.25f,border):new Vector4(new Vector3(cells[slot].X,cells[slot].Y,cells[slot].Z),border);
        }
        float[] widths=retained.LineWidths.ToArray();
        float frameWidth=V("FrameWidth");if(_frameCutoff>frameWidth)frameWidth=0;
        widths[0]=frameWidth/100f;
        widths[1]=V(selection.StyleSelector==1?"Style1Width0":"Width0");
        widths[2]=V("Width1")/100f;
        widths[5]=V(selection.Width4Selector==10?"SpecialWidth45":"Width45");
        widths[6]=V(selection.Width5Selector==10?"SpecialWidth45":"Width45");
        widths[7]=V("Width6")/100f;
        cells[11]=new(_cell11Rgb,cell11);lines[11]=new(_line11Rgb,line11);
        for(int i=19;i<24;i++)lines[i]=new(_line19Rgb,V("LineAlpha19")/100000f);
        cells[15]=new(1,1,1,selection.ZoomBand==0?0:5/255f);
        // Native packs a byte alpha after cvttss2si; retain this quantization.
        lines[15]=new(_line11Rgb,unchecked((byte)(int)Product(line11,255f))/255f);
        // Provider flag2 changes the consumer's separate stencil reference.
        // ScreenMap107C / shader layer selection is retained unchanged.
        uint mask=selection.ScreenMapMask;
        return new(cells,lines,new(_frameRgb,V("FrameAlpha")/100000f),widths,V("Curvature")/10f,mask,retained.Flags.ToArray());
    }

    public static TerritoryInputs Inputs(DeferredCameraInputs camera,TerritoryStylePacket style)
    {
        ArgumentNullException.ThrowIfNull(camera);Validate(style);
        var viewToWorld=NativeMatrixMath.Invert(camera.SourceWorldToView,out _);
        var projectionToWorld=NativeMatrixMath.Invert(camera.SourceWorldToProjection,out _);
        return new(Words(viewToWorld),Words(projectionToWorld),Words(camera.SourceWorldToView),
            InitializedField,style.CellColors.ToArray(),style.LineColors.ToArray(),
            style.FrameColor,style.LineWidths.ToArray(),new(220,220,468,7),new(.5f,1,1,0),new(0,0,0,1),
            style.Curvature,style.LayerViewMask,style.Flags.ToArray());
    }
    public static int CacheZoomValue(TerritoryStyleSelection selection)
    {ValidateSelection(selection);return (int)(float)selection.ZoomValue;}
    public static uint LayerMask(TerritoryStyleSelection selection)
        =>selection.ScreenMapMask;
    private static Vector4 CreateInitializedField()
    {
        // CE0011D0 initializes the game Hex origin; CE1895D0 separately
        // adds another half-row before CE8730 publishes the ScreenMap field.
        // The game/world Hex origin remains13335.2998; it is not this origin.
        float halfRow=Product(Product(MathF.Sqrt(3f),.5f),468f);
        float worldOrigin=Sum(halfRow,12930f);
        return new(37594f,Sum(halfRow,worldOrigin),-100000f,0f);
    }
    private static void ValidateSelection(TerritoryStyleSelection s)
    {
        if(s.ZoomBand is <0 or >6||s.NativeFraction1E4 is <0 or >9999||!double.IsFinite(s.ZoomValue)||
            !float.IsFinite((float)s.ZoomValue)||(float)s.ZoomValue<int.MinValue||(float)s.ZoomValue>=2147483648f)
            throw new InvalidDataException("Territory style requires a finite native seven-band View selection and raw fraction0..9999.");
    }
    private static void Validate(TerritoryStylePacket s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if(s.CellColors is null||s.LineColors is null||s.Flags is null||s.LineWidths is null||
            s.CellColors.Length!=128||s.LineColors.Length!=128||s.Flags.Length!=128||s.LineWidths.Length!=9||
            s.LineWidths.Any(v=>!float.IsFinite(v))||!float.IsFinite(s.Curvature)||
            s.CellColors.Concat(s.LineColors).Append(s.FrameColor).Any(v=>
                !float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z)||!float.IsFinite(v.W)))
            throw new InvalidDataException("Incomplete or nonfinite retained territory CPU style.");
    }
    private static float Interpolate(int[] table,TerritoryStyleSelection s)
    {
        // SC18CB2B/TC18CB4B reads the primary Main controller's raw1E4;
        // CVTDQ2PS then DIVSS uses source float10000, without percent rounding.
        if(s.ZoomBand==6)return table[6];float f=s.NativeFraction1E4/10000f;
        return Sum(Product(table[s.ZoomBand],1f-f),Product(table[s.ZoomBand+1],f));
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Product(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Sum(float a,float b)=>a+b;
    private static float[] Words(Matrix4x4 m)=>[m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];

}
