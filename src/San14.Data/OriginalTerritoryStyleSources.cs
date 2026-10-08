using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Already selected native View+1E0/+1E4/+90 and Map+2F8B0/+2F480/+2F484.
/// InterpolationPercent retains its compatibility name but contains the raw
/// native +1E4 fraction0..9999, with denominator10000. Band indices are not the
/// four reference-image labels. Display controls remain explicit.</summary>
public sealed record OriginalTerritoryStyleSelection(int ZoomBand, int InterpolationPercent,
    double ZoomValue, int StyleSelector, int Width4Selector, int Width5Selector,
    bool HideAllocatedFill, uint ScreenMapMask, uint ProviderFlags)
{
    public int NativeFraction1E4 => InterpolationPercent;
}

public sealed record OriginalTerritoryStylePacket(Vector4[] CellColors, Vector4[] LineColors,
    Vector4 FrameColor, float[] LineWidths, float Curvature, uint LayerViewMask, uint[] Flags);

/// <summary>Finite source defaults and the original SC18CA50 / TC18CA70 writer.
/// Reads installed, hash-verified executable data. Zero-initialized PE storage is the
/// source startup value, not evidence of arbitrary later global parameter overrides.</summary>
public sealed class OriginalTerritoryStyleSources
{
    public const string ScreenMapObjectSha256 = "357F6A380CF19EB2A86B439AF24CF2BACDD38074AAF9BC3AE99F0EAD21EE9271";
    public const string ScWriterSha256 = "B4B66C631CEA7935B2F95EE0CE2BD0B46EE53C51C963CD9CDB5CF1FBC1819B67";
    public const string TcWriterSha256 = "341723D0960DDADBC79ABB1A54C45388A99A7002750906E3AE96C827C824E5EA";
    public const int WriterByteCount = 3578;
    public string Locale { get; }
    public string ExecutableSha256 { get; }
    private readonly IReadOnlyDictionary<string, int[]> _tables;
    private readonly Vector3 _frameRgb, _cell11Rgb, _line11Rgb, _line19Rgb;
    private readonly float _frameCutoff;
    private static readonly Vector4 InitializedField = CreateInitializedField();
    private OriginalTerritoryStyleSources(string locale, string executableSha256,
        IReadOnlyDictionary<string,int[]> tables,Vector3 frame,Vector3 cell11,Vector3 line11,Vector3 line19,float frameCutoff)
        => (Locale,ExecutableSha256,_tables,_frameRgb,_cell11Rgb,_line11Rgb,_line19Rgb,_frameCutoff)=
            (locale,executableSha256,tables,frame,cell11,line11,line19,frameCutoff);

    public static OriginalTerritoryStyleSources LoadCurrent(OriginalGameRepository repository,
        LoadedOriginalTerritoryResources source,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(source);
        OriginalTerritoryResources.ValidateShader(source.Shader);
        if(source.ScreenMapObjects.Source.ResourceUri!="san14://rdb/system/DAB638B2" ||
            source.ScreenMapObjects.Source.DecodedSha256!=ScreenMapObjectSha256 ||
            source.ScreenMapObject.ObjectId!=0xCA6B0C6B || source.ScreenMapObject.ClassId!=0x12EA4048 ||
            source.ScreenMapObject.Properties.Count!=0)
            throw new InvalidDataException("Territory style defaults require the exact original property-free ScreenMap object.");
        string hash=OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(repository.SourceDirectory,locale);
        byte[] executable=File.ReadAllBytes(Path.Combine(repository.SourceDirectory,$"SAN14CE_{locale}.exe"));
        if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(executable))!=hash)
            throw new InvalidDataException("Territory executable changed while reading its source style data.");
        int Rva(int sc)=>locale switch {"SC"=>sc,"TC"=>sc-(sc>=0x27D0000?0x1060:0x1030),_=>throw new NotSupportedException("Only current SC/TC styles are verified.")};
        int I(int sc)=>BinaryPrimitives.ReadInt32LittleEndian(ReadImage(executable,Rva(sc),4));
        int C(int sc)=>BinaryPrimitives.ReadInt32LittleEndian(ReadImage(executable,sc-(locale=="TC"?0x1000:0),4));
        int[] T(params int[] rvas)=>rvas.Select(I).ToArray();
        var tables=new Dictionary<string,int[]> {
            ["FillAlpha"]=T(0x27D47F0,0x1A4B608,0x1A4B614,0x1A4B620,0x1A4B62C,0x1A4B638,0x1A4B644),
            ["Width0"]=T(0x1A4B668,0x1A4B674,0x1A4B680,0x1A4B68C,0x1A4B698,0x1A4B6A4,0x1A4B6B0),
            ["BorderAlpha"]=T(0x1A4B6BC,0x1A4B6C8,0x1A4B6D4,0x1A4B6E0,0x1A4B6EC,0x1A4B6F8,0x1A4B704),
            ["FrameWidth"]=T(0x1A4B4C8,0x1A4B4D0,0x1A4B4D8,0x1A4B4E0,0x27D47D4,0x27D47D8,0x27D47DC),
            ["FrameAlpha"]=T(0x1A4B49C,0x1A4B4A4,0x1A4B4AC,0x1A4B4B4,0x27D47C8,0x27D47CC,0x27D47D0),
            ["Width6"]=T(0x1A4B560,0x1A4B56C,0x1A4B578,0x1A4B584,0x1A4B590,0x1A4B59C,0x1A4B5A8),
            ["LineAlpha19"]=T(0x1A4B510,0x1A4B51C,0x1A4B528,0x1A4B534,0x1A4B540,0x1A4B548,0x1A4B554),
            ["CellAlpha11"]=T(0x27D47F4,0x27D47F8,0x27D4800,0x27D4808,0x1A4B74C,0x1A4B758,0x1A4B764),
            ["Width1"]=T(0x1A4B7E8,0x1A4B7F4,0x1A4B800,0x1A4B80C,0x1A4B818,0x1A4B824,0x1A4B830),
            ["LineAlpha11"]=T(0x1A4B794,0x1A4B7A0,0x1A4B7AC,0x1A4B7B8,0x1A4B7C4,0x1A4B7D0,0x1A4B7DC),
            ["Style1Width0"]=T(0x1A4B888,0x1A4B894,0x1A4B8A0,0x1A4B8AC,0x1A4B8B8,0x1A4B8C0,0x1A4B8CC),
            ["Width45"]=T(0x1A4B5B4,0x1A4B5C0,0x1A4B5CC,0x1A4B5D8,0x1A4B5E4,0x1A4B5EC,0x1A4B5F4),
            ["SpecialWidth45"]=T(0x1A4B83C,0x1A4B844,0x1A4B850,0x1A4B858,0x1A4B864,0x1A4B870,0x1A4B87C),
            ["Curvature"]=[C(0x13979D0),C(0x13979D4),C(0x13979D8),C(0x13979DC),10,10,10],
            ["ZoomValues"]=T(0x1A4B950,0x1A4B95C,0x1A4B968,0x1A4B974,0x1A4B980,0x1A4B98C,0x1A4B998)
        };
        Vector3 Color(int x,int y,int z)=>new(I(x)/255f,I(y)/255f,I(z)/255f);
        return new(locale,hash,tables,Color(0x1A4B484,0x1A4B48C,0x1A4B494),
            Color(0x1A4B710,0x1A4B71C,0x1A4B728),Color(0x1A4B770,0x1A4B77C,0x1A4B788),
            Color(0x1A4B4F4,0x1A4B4FC,0x1A4B504),I(0x1A4B4E0)*0.5f);
    }

    /// <summary>SC189E20 then186640 initializes all 128 base palettes to zero;
    /// SC18994A..189988 initializes all flags to zero except11/12=1.</summary>
    public static OriginalTerritoryStylePacket InitializedScreenMap() =>
        new(new Vector4[128],new Vector4[128],Vector4.Zero,new float[9],0,8,InitializedFlags());
    public static uint[] InitializedFlags(){var values=new uint[128];values[11]=1;values[12]=1;return values;}

    /// <summary>Source00E940's exact native band anchor; no preview zoom factor conversion.</summary>
    public float ZoomAnchor(int nativeBand)=>nativeBand is >=0 and <=6?_tables["ZoomValues"][nativeBand]:
        throw new InvalidDataException("Original View has seven zoom anchors.");

    /// <summary>Read the complete prefix containing native View+90/+1E0/+1E4.
    /// Type identity and readiness are established by the upstream View owner.</summary>
    public static OriginalTerritoryStyleSelection ReadViewPrefix(ReadOnlySpan<byte> completePrefix,
        OriginalTerritoryStyleSelection retainedMapControls)
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
        LoadedOriginalTerritoryResources source,LoadedOriginalDeferredSamplerNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(native);
        OriginalTerritoryResources.ValidateShader(source.Shader);
        if(source.Gbuffers.Count!=5||!source.Gbuffers.Select(g=>g.Register).Order().SequenceEqual(new uint[]{1,2,3,4,5}))
            throw new InvalidDataException("Territory source requires its five exact native GBuffer registers.");
        return source.Gbuffers.ToDictionary(g=>g.Register,g=>(ReadOnlyMemory<byte>)
            OriginalDeferredSamplerSources.DecodeKey(native,OriginalDeferredSamplerSources.FinalRenderTextureKey(native,g.Texture)).EncodeD3D11Descriptor());
    }

    /// <summary>Writes the original style fields into complete retained CPU tables.
    /// Native allocated loop stops at the first color60 sentinel, even if a later slot is occupied.</summary>
    public OriginalTerritoryStylePacket Write(OriginalTerritoryStyleSelection selection,
        OriginalTerritoryOwnershipPacket ownership,OriginalTerritoryStylePacket retained)
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

    public static OriginalTerritoryInputs Inputs(OriginalDeferredCameraInputs camera,OriginalTerritoryStylePacket style)
    {
        ArgumentNullException.ThrowIfNull(camera);Validate(style);
        var viewToWorld=OriginalNativeMatrixMath.Inverse3F6EB0(camera.SourceWorldToView,out _);
        var projectionToWorld=OriginalNativeMatrixMath.Inverse3F6EB0(camera.SourceWorldToProjection,out _);
        return new(Words(viewToWorld),Words(projectionToWorld),Words(camera.SourceWorldToView),
            InitializedField,style.CellColors.ToArray(),style.LineColors.ToArray(),
            style.FrameColor,style.LineWidths.ToArray(),new(220,220,468,7),new(.5f,1,1,0),new(0,0,0,1),
            style.Curvature,style.LayerViewMask,style.Flags.ToArray());
    }
    public static int CacheZoomValue(OriginalTerritoryStyleSelection selection)
    {ValidateSelection(selection);return (int)(float)selection.ZoomValue;}
    public static uint LayerMask(OriginalTerritoryStyleSelection selection)
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
    private static void ValidateSelection(OriginalTerritoryStyleSelection s)
    {
        if(s.ZoomBand is <0 or >6||s.NativeFraction1E4 is <0 or >9999||!double.IsFinite(s.ZoomValue)||
            !float.IsFinite((float)s.ZoomValue)||(float)s.ZoomValue<int.MinValue||(float)s.ZoomValue>=2147483648f)
            throw new InvalidDataException("Territory style requires a finite native seven-band View selection and raw fraction0..9999.");
    }
    private static void Validate(OriginalTerritoryStylePacket s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if(s.CellColors is null||s.LineColors is null||s.Flags is null||s.LineWidths is null||
            s.CellColors.Length!=128||s.LineColors.Length!=128||s.Flags.Length!=128||s.LineWidths.Length!=9||
            s.LineWidths.Any(v=>!float.IsFinite(v))||!float.IsFinite(s.Curvature)||
            s.CellColors.Concat(s.LineColors).Append(s.FrameColor).Any(v=>
                !float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z)||!float.IsFinite(v.W)))
            throw new InvalidDataException("Incomplete or nonfinite retained territory CPU style.");
    }
    private static float Interpolate(int[] table,OriginalTerritoryStyleSelection s)
    {
        // SC18CB2B/TC18CB4B reads the primary Main controller's raw1E4;
        // CVTDQ2PS then DIVSS uses source float10000, without percent rounding.
        if(s.ZoomBand==6)return table[6];float f=s.NativeFraction1E4/10000f;
        return Sum(Product(table[s.ZoomBand],1f-f),Product(table[s.ZoomBand+1],f));
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Product(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Sum(float a,float b)=>a+b;
    private static float[] Words(Matrix4x4 m)=>[m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];

    private static byte[] ReadImage(byte[] exe,int rva,int count)
    {
        int pe=BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(0x3c,4));int sections=BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(pe+6,2));
        int table=pe+24+BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(pe+20,2));
        for(int i=0;i<sections;i++)
        {
            int at=table+i*40,va=BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(at+12,4)),
                virtualSize=BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(at+8,4)),rawSize=BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(at+16,4)),
                raw=BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(at+20,4));
            if(rva<va||rva+count>va+Math.Max(virtualSize,rawSize))continue;
            int delta=rva-va;
            if(delta>=rawSize&&delta+count<=virtualSize)return new byte[count];
            if(delta+count<=rawSize)return exe.AsSpan(raw+delta,count).ToArray();
            throw new InvalidDataException("Territory style data crosses a PE raw/virtual boundary.");
        }
        throw new InvalidDataException("Territory style data lies outside a source PE section.");
    }
}
