using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The resource node1 Exposure declaration and project stock rectangle programs.</summary>
public sealed class LoadedMapExposureSource
{
    internal LoadedMapExposureSource(LoadedRenderGraph graph, RenderTextureDefinition texture,
        LoadedDeferredSamplerNativeSource native, byte[] vertex, byte[] pixel,
        G1TextureSamplerRecipe inputSampler, G1TextureSamplerRecipe outputSampler)
    { Graph=graph; Texture=texture; Native=native; this.vertex=vertex; this.pixel=pixel;
        InputSampler=inputSampler; OutputSampler=outputSampler; }
    private readonly byte[] vertex,pixel;
    public LoadedRenderGraph Graph { get; }
    public RenderTextureDefinition Texture { get; }
    public LoadedDeferredSamplerNativeSource Native { get; }
    public ReadOnlyMemory<byte> VertexProgram => vertex;
    public ReadOnlyMemory<byte> PixelProgram => pixel;
    public G1TextureSamplerRecipe InputSampler { get; }
    public G1TextureSamplerRecipe OutputSampler { get; }
    public string SourceResourceUri => Texture.Source.Source.ResourceUri+"#object/6C5630BB/exposure/node1";
}

/// <summary>Resolved native working surface after the node's proxy selection.
/// NativePixelFormat is the original +36 low seven bits, before DXGI conversion.
/// The selection provenance identifies its source owner and selected View.</summary>
public sealed record MapExposureSurface(byte? NativePixelFormat, bool? IntermediateLinear,
    string SelectionProvenance)
{
    /// <summary>For an existing working texture whose selected proxy format is
    /// not needed: cleared Aux bits22/23 force native mode0.5 for every original
    /// seven-bit format and either proxy branch. Missing working texture is a
    /// different native branch and cannot use this proof.</summary>
    public static MapExposureSurface ExistingViewModeInvariant(MapExposureViewAux aux,string workingOwnerProvenance)
    {
        ArgumentNullException.ThrowIfNull(aux);
        if((aux.Flags8&((1u<<22)|(1u<<23)))!=0||string.IsNullOrWhiteSpace(workingOwnerProvenance))
            throw new InvalidDataException("Exposure mode invariant requires an existing working-view owner and source Aux bits22/23 cleared.");
        return new(null,null,workingOwnerProvenance+";EAA63D-mode-invariant-Aux22=0-Aux23=0");
    }
}

/// <summary>Only fields consumed by Exposure from the typed ViewAux. Reading a
/// complete retained record keeps source construction separate from frame state.</summary>
public sealed record MapExposureViewAux(uint Flags8, int ToneType3A4,
    bool CustomPixelShaderAbsent, string SourceProvenance)
{
    /// <summary>Independent Apply route setting, from Common property31 or
    /// the complete prepared Aux. It is not inferred from ToneType or flags.</summary>
    public int? CommonMode3C0 { get; init; }
    /// <summary>Constructor plus the exact CommonViewSetting property loader.
    /// The caller explicitly selects this template for its remake View. This
    /// does not claim that the original process selected the same context entry.</summary>
    public static MapExposureViewAux ReadTemplate(LoadedObjects source,uint objectId)
    {
        uint Word(uint hash) => CommonRenderResources.ReadScalar(source, hash, 4, objectId);
        return new(Word(0x74C9D1B9),unchecked((int)Word(0x71564E51)),true,
            source.Source.ResourceUri+$"#object/{objectId:X8}@{source.Source.DecodedSha256};constructor=D02D40;loader=D230F0;selected-remake-template")
            {CommonMode3C0=unchecked((int)Word(0x668912BD))};
    }
    public static MapExposureViewAux ReadPrepared(ReadOnlySpan<byte> complete460,string provenance)
    {
        if(complete460.Length!=0x460||string.IsNullOrWhiteSpace(provenance))
            throw new InvalidDataException("Exposure requires the complete typed460 ViewAux and its source provenance.");
        return new(BinaryPrimitives.ReadUInt32LittleEndian(complete460.Slice(8,4)),
            BinaryPrimitives.ReadInt32LittleEndian(complete460.Slice(0x3A4,4)),
            BinaryPrimitives.ReadUInt64LittleEndian(complete460.Slice(0x108,8))==0,provenance)
            {CommonMode3C0=BinaryPrimitives.ReadInt32LittleEndian(complete460.Slice(0x3C0,4))};
    }
}

/// <summary>Same-frame prepared Camera and resolved source View state. The
/// optional luminance is already selected by EAA79A's external-light branch;
/// null means that branch is absent and Camera+17C is retained. Special flag
/// and view mask are explicit tail fields outside the prepared3C0 prefix.</summary>
public sealed record MapExposureInputs(long FrameNumber, ReadOnlyMemory<byte> PreparedCameraPrefix,
    string CameraSourceProvenance, MapExposureViewAux ViewAux, MapExposureSurface Surface,
    bool CameraSpecialRectangleFlag1510, int? CameraViewMask19B8, float? SelectedExternalLuminance=null)
{
    public CameraExposureSettings? OpticalExposure { get; init; }
    public DeferredCameraInputs? CameraInputs { get; init; }
}

public sealed class MapExposurePacket
{
    private readonly byte[] bytes;
    internal MapExposurePacket(MapExposureInputs input, byte[] data,float mode,float luminance,bool half)
    {
        FrameNumber=input.FrameNumber;bytes=data;Mode=mode;Luminance=luminance;HalfRectangle=half;
        ViewFlags8=input.ViewAux.Flags8;ToneType3A4=input.ViewAux.ToneType3A4;
        // The existing130-byte reader is based at native Camera+10:
        // Eye+0, View+70 and Projection+F0 map to native+10/+80/+100.
        Camera=input.CameraInputs ?? DeferredSceneConstants.ReadPreparedCamera(input.PreparedCameraPrefix.Span.Slice(0x10,0x130));
        SourceProvenance=input.CameraSourceProvenance+";ViewAux="+input.ViewAux.SourceProvenance+
            ";Surface="+input.Surface.SelectionProvenance+$";frame={FrameNumber};native=EAA964..EAAA9E";
    }
    public long FrameNumber { get; }
    public ReadOnlyMemory<byte> Bytes => bytes;
    public DeferredCameraInputs Camera { get; }
    public float Mode { get; }
    public float Luminance { get; }
    public bool HalfRectangle { get; }
    public uint ViewFlags8 { get; }
    public int ToneType3A4 { get; }
    // EAAA D6 disables a previously enabled alpha test before screen begin;
    // the native disabled writer95FA2C publishes -FLT_MAX to default3 vATest.
    public float AlphaTestThreshold => -float.MaxValue;
    public string SourceProvenance { get; }
    private MapExposurePacket(MapExposurePacket source)
    {FrameNumber=source.FrameNumber;bytes=source.bytes.ToArray();Camera=source.Camera;Mode=source.Mode;Luminance=source.Luminance;
        HalfRectangle=source.HalfRectangle;ViewFlags8=source.ViewFlags8;ToneType3A4=source.ToneType3A4;SourceProvenance=source.SourceProvenance;}
    public MapExposurePacket CopyAcceptedSnapshot()=>new(this);
}

public static class MapExposureSource
{
    public const uint TextureObjectId=0x6C5630BB;

    public static LoadedMapExposureSource Load(GameResourceRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var graph=repository.LoadRenderGraph("system",0x024496EE);
        if(graph.Document.Nodes.Count!=211||graph.Document.Nodes[1].Call.FactoryClassId!=0x5F0A8446)
            throw new InvalidDataException("Exposure requires the exact system024496EE node1 factory.");
        var node=graph.Document.Nodes[1];
        byte[] Arg(uint hash,int count)
        {
            var rows=node.Call.Arguments.Where(a=>a.NameHash==hash).ToArray();
            return rows.Length==1&&rows[0].RawValue.Length==count?rows[0].RawValue:
                throw new InvalidDataException("Exposure node1 source argument is missing or ambiguous.");
        }
        if(Arg(0x9075EDA0,1)[0]!=0||BinaryPrimitives.ReadUInt16LittleEndian(Arg(0x05D92ACC,2))!=3||
            BinaryPrimitives.ReadUInt32LittleEndian(Arg(0x5C1A1454,4))!=TextureObjectId)
            throw new InvalidDataException("Exposure node1 View, Flag or texture declaration differs.");
        var texture=RenderTextureResources.Read(repository.LoadObjects("system",0x93FB3280),TextureObjectId);
        if(texture.Kind!=RenderTextureKind.Texture2D||texture.Width!=2||texture.Height!=1||
            texture.PixelFormat!=4||texture.LevelNum!=1||texture.MultiSample!=0||texture.ColorSpace!=0||
            texture.OutputColorSpace!=2||texture.NoiseParameterObjectNameHash!=0)
            throw new InvalidDataException("Exposure requires its original2x1 FloatRGBA128 declaration.");
        var native=RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale);
        byte[] vertex=ProjectStockShaders.Vertex.Dxbc,pixel=ProjectStockShaders.Default3.Dxbc;
        var sampler=RenderSamplerSourceAdapters.DecodeKey(native,RenderSamplerSourceAdapters.RenderTextureKey(native,1));
        var outputSampler=RenderSamplerSourceAdapters.DecodeKey(native,
            RenderSamplerSourceAdapters.FinalRenderTextureKey(native,texture));
        return new(graph,texture,native,vertex,pixel,sampler,outputSampler);
    }

    public static MapExposurePacket Prepare(MapExposureInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(input.ViewAux);
        ArgumentNullException.ThrowIfNull(input.Surface);
        if(input.FrameNumber<1||input.PreparedCameraPrefix.Length!=0x3C0||
            new[]{input.CameraSourceProvenance,input.ViewAux.SourceProvenance,input.Surface.SelectionProvenance}.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Exposure requires a positive accepted frame, complete3C0 Camera and explicit source identities.");
        if(!input.ViewAux.CustomPixelShaderAbsent)
            throw new NotSupportedException("Exposure custom pixel mode5 requires its selected original program.");
        CameraExposureSettings optical=input.OpticalExposure ?? CameraOpticsEncoding.DecodeExposure(input.PreparedCameraPrefix.Span);
        float exposure=optical.UserScale;
        float luminance=input.SelectedExternalLuminance??optical.Luminance;
        int cameraMode=BinaryPrimitives.ReadInt32LittleEndian(input.PreparedCameraPrefix.Span.Slice(0x1CC,4));
        if(!float.IsFinite(exposure)||!float.IsFinite(luminance))
            throw new InvalidDataException("Prepared source Exposure and selected luminance must be finite.");
        float mode;
        if(input.Surface.NativePixelFormat is byte pixel&&input.Surface.IntermediateLinear is bool linear)
            mode=SelectMode(pixel,linear,cameraMode,input.ViewAux.Flags8,input.ViewAux.ToneType3A4);
        else if(input.Surface.NativePixelFormat is null&&input.Surface.IntermediateLinear is null&&
            (input.ViewAux.Flags8&((1u<<22)|(1u<<23)))==0)mode=.5f;
        else throw new InvalidDataException("Exposure working-view selection is incomplete and has no native mode invariant.");
        float reciprocal=ReciprocalTone(luminance,mode,input.ViewAux.Flags8,input.ViewAux.ToneType3A4);
        byte[] bytes=new byte[32];
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(0,4),exposure);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(16,4),mode);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(20,4),reciprocal);
        bool special=(input.ViewAux.Flags8&(1u<<25))!=0||input.CameraSpecialRectangleFlag1510;
        if(special&&input.CameraViewMask19B8 is null)
            throw new InvalidDataException("Special Exposure rectangle requires the selected Camera's explicit runtime ViewMask19B8.");
        bool half=special&&input.CameraViewMask19B8!=1;
        return new(input,bytes,mode,luminance,half);
    }

    /// <summary>TC EA9775..EA97DE selected external-light branch. Apply-reference
    /// presence and the selected source LightData are resolved by the same View
    /// owner. Empty data means the original selected-light lookup returned null.
    /// A null result retains the prepared Camera+17C. This is not light selection.</summary>
    public static float? ResolveSelectedLuminance(bool exposureControllerPresent,ReadOnlySpan<byte> selectedPreparedLightData)
    {
        if(!exposureControllerPresent||selectedPreparedLightData.IsEmpty)return null;
        if(selectedPreparedLightData.Length!=0x10C0)
            throw new InvalidDataException("Exposure selected light requires its complete prepared10C0 source data.");
        byte type=selectedPreparedLightData[0x10B0];
        if(type is not(3 or 4 or 5)&&(selectedPreparedLightData[0xFDC]&1)==0)return null;
        float r=BinaryPrimitives.ReadSingleLittleEndian(selectedPreparedLightData.Slice(0xFA0,4)),
            g=BinaryPrimitives.ReadSingleLittleEndian(selectedPreparedLightData.Slice(0xFA4,4)),
            b=BinaryPrimitives.ReadSingleLittleEndian(selectedPreparedLightData.Slice(0xFA8,4));
        if(!float.IsFinite(r)||!float.IsFinite(g)||!float.IsFinite(b))
            throw new InvalidDataException("Exposure selected baseline light RGB is not finite.");
        float green=NativeFloat.Multiply(g,F(0x3F1645A2));
        float red=NativeFloat.Multiply(r,F(0x3E991687));
        float blue=NativeFloat.Multiply(b,F(0x3DE978D5));
        return NativeFloat.Add(NativeFloat.Add(green,red),blue);
    }

    // EAA63D..EAA889, entered with a resolved working CPU texture. This is
    // the native low-seven-bit PixelFormat mask, never a DXGI-format mask.
    public static float SelectMode(byte pixel,bool linear,int cameraMode,uint flags,int toneType)
    {
        if(pixel>0x7F)throw new InvalidDataException("Exposure native PixelFormat exceeds seven bits.");
        bool fixedHalf=pixel<64?((0x18000781CUL>>pixel)&1)!=0:
            ((0x1C03E204000F000UL>>(pixel-64))&1)!=0;
        if(fixedHalf)return .5f;
        if((flags&(1u<<22))==0||cameraMode>=0)
            return linear?.5f:(flags&(1u<<23))!=0?1f:.5f;
        int selected=(flags&(1u<<23))!=0?Math.Clamp(toneType,0,3):toneType;
        return selected==1?.1f:0f;
    }

    /// <summary>EAA964..EAAA2E, preserving each native scalar binary32
    /// product/add/divide. The two parameter branches read verified rdata.</summary>
    public static float ReciprocalTone(float luminance,float mode,uint flags,int toneType)
    {
        if(!float.IsFinite(luminance)||!float.IsFinite(mode))throw new InvalidDataException("Exposure tone input is not finite.");
        if(mode!=.1f&&mode!=.5f)return 0;
        int selected=(flags&(1u<<23))!=0?Math.Clamp(toneType,0,3):toneType;
        bool first=selected==1;
        float a=first?F(0x3E6147AE):F(0x3E19999A),b=first?F(0x3E99999A):.5f,
            c=first?F(0x3CF5C28F):F(0x3D4CCCCD),d=first?F(0x3B03126F):F(0x3B83126F),
            e=first?F(0x3D086595):F(0x3D89374C);
        float product=NativeFloat.Multiply(a,luminance);
        float denominator=NativeFloat.Add(NativeFloat.Multiply(NativeFloat.Add(product,b),luminance),F(0x3D75C28F));
        float numerator=NativeFloat.Add(NativeFloat.Multiply(NativeFloat.Add(product,c),luminance),d);
        float ratio=NativeFloat.Subtract(D(numerator,denominator),e);
        return ratio>0?D(1,ratio):0;
    }
    private static float F(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a,float b)=>a/b;
}
