using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Numerics;

namespace San14.Data;

/// <summary>The six additional current owners read by source PS767. Lighting,
/// Camera and ambient modifier values belong to the accepted lighting packet.
/// None of these live groups has an implicit zero/default/retained-template path.</summary>
public sealed record DeferredSelected767AdditionalInputs(
    DeferredOcclusionInputs Occlusion,
    DeferredRefractionInputs Refraction,
    DeferredShadowFadeInputs ShadowFade,
    DeferredHatchingInputs Hatching,
    DeferredTranslucencyViewportInputs TranslucencyViewport,
    DeferredProjectionInputs Projection,
    string CurrentProducerProvenance,
    DeferredSelected767HatchingIeeeProof? HatchingIeeeProof=null);

/// <summary>Exact IEEE words emitted by a successful current source Hatching
/// producer. Only Data-owned recovered setter factories can create authority.
/// Other fields and edited Hatching values never inherit this authorization.</summary>
public sealed class DeferredSelected767HatchingIeeeProof
{
    private readonly uint[] words;
    private readonly int[] offsets=[464,468,472,476,480,484,488,492,496,500,504,512,516,520];
    private DeferredSelected767HatchingIeeeProof(DeferredHatchingInputs source,string provenance,
        long frameNumber,string effectiveLightOwnerIdentity)
    { words=Bits(source);CurrentProducerProvenance=provenance;FrameNumber=frameNumber;EffectiveLightOwnerIdentity=effectiveLightOwnerIdentity; }
    public string CurrentProducerProvenance { get; }
    public long FrameNumber { get; }
    public string EffectiveLightOwnerIdentity { get; }
    internal static DeferredSelected767HatchingIeeeProof FromSuccessfulOwnedSource(
        DeferredHatchingInputs source,string provenance,long frameNumber,string effectiveLightOwnerIdentity)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentException.ThrowIfNullOrWhiteSpace(effectiveLightOwnerIdentity);
        if(frameNumber<1)
            throw new InvalidDataException("Hatching IEEE authority requires its exact accepted source frame and effective light generation.");
        return new(source,provenance,frameNumber,effectiveLightOwnerIdentity);
    }
    internal void ValidateCurrent(DeferredF103SelectedSource selected)
    {
        if(selected.Current.FrameNumber!=FrameNumber||selected.OwnedFeatureSource is not { } feature||
            feature.FrameNumber!=FrameNumber||feature.EffectiveLightOwnerIdentity!=EffectiveLightOwnerIdentity)
            throw new InvalidDataException("Hatching IEEE authority belongs to another source frame or effective light generation.");
    }
    internal void Validate(DeferredHatchingInputs? input)
    {
        if(input is null||!words.AsSpan().SequenceEqual(Bits(input)))
            throw new InvalidDataException("Scoped Hatching IEEE values differ from their exact successful current source setter.");
    }
    internal bool Authorizes(DeferredHatchingInputs? input,int selectedOffset,uint word)
    {
        Validate(input);int index=Array.IndexOf(offsets,selectedOffset);
        return index>=0&&words[index]==word;
    }
    internal void RestoreSelectedWords(Span<byte> destination,DeferredHatchingInputs? input)
    {
        Validate(input);
        for(int i=0;i<offsets.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offsets[i],4),words[i]);
    }
    private static uint[] Bits(DeferredHatchingInputs input)
        =>new[]{input.Ramp.X,input.Ramp.Y,input.Ramp.Z,input.Ramp.W,
            input.AmbientOcclusion.X,input.AmbientOcclusion.Y,input.AmbientOcclusion.Z,input.AmbientOcclusion.W,
            input.UvRow0.X,input.UvRow0.Y,input.UvRow0.Z,input.UvRow1.X,input.UvRow1.Y,input.UvRow1.Z}
            .Select(BitConverter.SingleToUInt32Bits).ToArray();
}

public sealed record DeferredSelected767ConstantPacket(
    IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> Constants,
    DeferredSceneConstantPacket NamedWrites,
    IReadOnlyList<int> ActiveScalarByteOffsets,
    int BackendNormalizedUnusedBytes,
    string CurrentProducerProvenance);

/// <summary>Complete cb0 factory for actual F103 row1/combo1022/PS767. Exact
/// source instructions use literal cb0 indices, reading 127 scalar lanes.
/// All 23 named fields are active. Only the remaining 132 bytes may be
/// normalized by this backend, including 80 unread declared components.
/// The factory never substitutes unknown active fields with zero.</summary>
public static class DeferredSelected767Constants
{
    public const int ByteSize=640;
    public const int ActiveByteCount=508;
    public const string PixelProgramSha256="62CEB4427347D0D7E37CA096138B2F39CAEB9342134356177CFE83027E879A4C";
    private static readonly byte[] masks=[
        7,7,7,7,7,7,7,7,15,7,1,7,3,7,7,7,3,15,15,15,
        7,7,7,0,15,15,15,15,1,7,7,7,15,15,15,15,15,15,15,15];
    private static readonly (string Name,int Offset,int Bytes)[] fields=[
        ("litDir",0,64),("litCol",64,64),("litOcc",128,16),("ambLit",144,16),("nDirLit",160,4),
        ("vEye",176,16),("vOccRate",192,8),("vCameraPos",208,12),("vCameraDir",224,12),
        ("vCameraViewY",240,12),("vD2Z",256,8),("mRrW2T",272,48),("mV2W",320,64),("mP2W",384,64),
        ("fSdmFade",448,4),("hsRampPrm",464,16),("hsAOPrm",480,16),("hsUVMat",496,28),
        ("fAmbMapsDORate",524,4),("vAmbDifScl",528,16),("vAmbSpcScl",544,16),
        ("vTsbViewPort",560,16),("mP2V",576,64)];
    public static IReadOnlyList<int> ActiveScalarByteOffsets { get; }=Array.AsReadOnly(
        masks.SelectMany((mask,index)=>Enumerable.Range(0,4).Where(c=>(mask&(1<<c))!=0)
            .Select(c=>index*16+c*4)).ToArray());

    /// <summary>Build from the same source-owned current feature/light/Camera
    /// generation used by terrain and models. Additional inputs are actual
    /// source owner values; their provenance cannot replace their values.</summary>
    public static DeferredSelected767ConstantPacket Create(
        DeferredF103SelectedSource selected,MapSourceCreatedLightingPacket lighting,
        DeferredSelected767AdditionalInputs additional)
    {
        ArgumentNullException.ThrowIfNull(selected);ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(additional);
        ArgumentException.ThrowIfNullOrWhiteSpace(additional.CurrentProducerProvenance);
        var feature=selected.OwnedFeatureSource??
            throw new InvalidDataException("PS767 constants require the actual owned kind1 feature generation.");
        feature.ValidateFrame(selected.Camera,lighting);
        if(lighting.FrameNumber!=selected.Current.FrameNumber||lighting.SceneInputs.Camera is null||
            lighting.SceneInputs.Camera!=selected.Camera.SceneInputs||
            lighting.SceneInputs.Ambient is null)
            throw new InvalidDataException("PS767 constants require the accepted Camera and source ambient modifier from the same lighting frame.");
        var scene=new DeferredSceneInputs(
            Lighting:DeferredSceneConstants.ReadPreparedBaseLighting(feature.EffectiveLocalLightData.Span[..0x1B0],
                checked((int)feature.Scratch.DirectionalCount)),
            Camera:lighting.SceneInputs.Camera,Ambient:lighting.SceneInputs.Ambient,
            Occlusion:additional.Occlusion,Refraction:additional.Refraction,ShadowFade:additional.ShadowFade,
            Hatching:additional.Hatching,TranslucencyViewport:additional.TranslucencyViewport,Projection:additional.Projection);
        return CreateFromExplicitCurrentScene(selected,scene,
            lighting.SourceProvenance+";"+additional.CurrentProducerProvenance,additional.HatchingIeeeProof);
    }

    /// <summary>Complete narrow current-owner path. Also supports independent
    /// numerical verification without a complete unknown 640-byte template.
    /// All nine writer groups must be explicit, including three refraction rows.</summary>
    public static DeferredSelected767ConstantPacket CreateFromExplicitCurrentScene(
        DeferredF103SelectedSource selected,DeferredSceneInputs scene,string currentProducerProvenance)
        =>CreateFromExplicitCurrentScene(selected,scene,currentProducerProvenance,null);

    public static DeferredSelected767ConstantPacket CreateFromExplicitCurrentScene(
        DeferredF103SelectedSource selected,DeferredSceneInputs scene,string currentProducerProvenance,
        DeferredSelected767HatchingIeeeProof? hatchingIeeeProof)
    {
        ArgumentNullException.ThrowIfNull(selected);ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentProducerProvenance);
        selected.Validate();
        if(selected.Combination.Index!=1022||selected.PixelProgramIndex!=767||
            selected.PixelProgramSha256!=PixelProgramSha256)
            throw new NotSupportedException("The no-retained-template constant factory is scoped to actual combo1022/PS767.");
        var globals=selected.PixelDefinitions.Buffers.Single(b=>b.Name.Text=="$Globals");
        if(globals.ByteSize!=ByteSize||globals.Variables.Count!=fields.Length||
            selected.PixelDefinitions.Bindings.Count(b=>b.InputType==0)!=1||
            fields.Any(f=>!globals.Variables.Any(v=>v.Name.Text==f.Name&&v.StartOffset==f.Offset&&v.ByteSize==f.Bytes)))
            throw new InvalidDataException("PS767 constant layout differs from its source read inventory.");
        if(scene.Lighting is null||scene.Camera is null||scene.Occlusion is null||scene.Refraction is null||
            scene.Refraction.WorldToTexture is not {Count:3}||scene.ShadowFade is null||scene.Hatching is null||
            scene.Ambient is null||scene.TranslucencyViewport is null||scene.Projection is null)
            throw new InvalidDataException("PS767 reads all 23 named fields: all nine source writer groups and refraction rows are required.");
        if(scene.Camera!=selected.Camera.SceneInputs)
            throw new InvalidDataException("PS767 constant Camera differs from the same current source-selected Camera.");
        hatchingIeeeProof?.ValidateCurrent(selected);hatchingIeeeProof?.Validate(scene.Hatching);
        var prepared=DeferredSelectedSceneConstants.Apply(selected,new byte[ByteSize],scene,hatchingIeeeProof);
        if(prepared.RetainedVariables.Count!=0||prepared.Writes.Count!=fields.Length||
            !prepared.Writes.Select(w=>w.Name).Order().SequenceEqual(fields.Select(f=>f.Name).Order()))
            throw new InvalidDataException("An active PS767 named source writer is missing; no unknown active field can be normalized.");
        byte[] result=new byte[ByteSize];
        foreach(int at in ActiveScalarByteOffsets)
        {
            uint word=BinaryPrimitives.ReadUInt32LittleEndian(prepared.Bytes.AsSpan(at,4));
            if(at!=160&&!float.IsFinite(BitConverter.UInt32BitsToSingle(word))&&
                hatchingIeeeProof?.Authorizes(scene.Hatching,at,word)!=true)
                throw new InvalidDataException("PS767 current source writer produced a nonfinite active scalar at byte"+at+".");
            prepared.Bytes.AsSpan(at,4).CopyTo(result.AsSpan(at,4));
        }
        var packet=new DeferredSceneConstantPacket(result,prepared.Writes,Array.AsReadOnly(Array.Empty<string>()));
        var constants=new ReadOnlyDictionary<uint,ReadOnlyMemory<byte>>(
            new Dictionary<uint,ReadOnlyMemory<byte>>{{0,result.ToArray()}});
        return new(constants,packet,ActiveScalarByteOffsets,ByteSize-ActiveByteCount,currentProducerProvenance);
    }
}
