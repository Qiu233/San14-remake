using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Numerics;

namespace San14.Data;

public sealed record DeferredSelected383AdditionalInputs(DeferredOcclusionInputs Occlusion,
    DeferredRefractionInputs Refraction, DeferredShadowFadeInputs ShadowFade,
    DeferredHatchingInputs Hatching, DeferredTranslucencyViewportInputs TranslucencyViewport,
    DeferredProjectionInputs Projection, string CurrentProducerProvenance,
    DeferredSelected383HatchingIeeeProof? HatchingIeeeProof = null);

/// <summary>Successful owned common Hatching setter words in the actual PS383
/// layout. PS767's separately scoped proof never authorizes these offsets.</summary>
public sealed class DeferredSelected383HatchingIeeeProof
{
    private readonly uint[] words;
    private static readonly int[] offsets = [560,564,568,572,576,580,584,588,592,596,600,608,612,616];
    private DeferredSelected383HatchingIeeeProof(DeferredHatchingInputs source,
        long frame, string lightHash, string provenance)
    { words = Bits(source); FrameNumber = frame; EffectiveLightOwnerIdentity = lightHash; CurrentProducerProvenance = provenance; }
    public long FrameNumber { get; }
    public string EffectiveLightOwnerIdentity { get; }
    public string CurrentProducerProvenance { get; }
    internal static DeferredSelected383HatchingIeeeProof FromSuccessfulOwnedSource(
        DeferredHatchingInputs source, long frame, string lightHash, string provenance)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (frame <= 0 || string.IsNullOrWhiteSpace(lightHash))
            throw new InvalidDataException("PS383 Hatching authority requires the exact accepted frame and effective light generation.");
        return new(source, frame, lightHash, provenance);
    }
    internal void Validate(DeferredF103SelectedSource selected, DeferredHatchingInputs source)
    {
        if (selected.Combination.Index != 510 || selected.PixelProgramIndex != 383 ||
            selected.PixelProgramSha256 != DeferredSelected383Constants.PixelProgramSha256 ||
            selected.Current.FrameNumber != FrameNumber || selected.OwnedFeatureSource is not { } feature ||
            feature.EffectiveLightOwnerIdentity != EffectiveLightOwnerIdentity || !words.AsSpan().SequenceEqual(Bits(source)))
            throw new InvalidDataException("PS383 Hatching words or authority belong to another layout, frame or effective light generation.");
    }
    internal void Restore(Span<byte> bytes)
    { for (int i = 0; i < offsets.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offsets[i], 4), words[i]); }
    internal bool Authorizes(int offset, uint word)
    { int index = Array.IndexOf(offsets, offset); return index >= 0 && words[index] == word; }
    private static uint[] Bits(DeferredHatchingInputs input) => new[] {
        input.Ramp.X,input.Ramp.Y,input.Ramp.Z,input.Ramp.W,input.AmbientOcclusion.X,input.AmbientOcclusion.Y,
        input.AmbientOcclusion.Z,input.AmbientOcclusion.W,input.UvRow0.X,input.UvRow0.Y,input.UvRow0.Z,
        input.UvRow1.X,input.UvRow1.Y,input.UvRow1.Z }.Select(BitConverter.SingleToUInt32Bits).ToArray();
}

public sealed record DeferredSelected383ConstantPacket(
    IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> Constants, DeferredSceneConstantPacket NamedWrites,
    IReadOnlyList<int> ActiveScalarByteOffsets, int BackendNormalizedUnusedBytes, string CurrentProducerProvenance);

/// <summary>Actual combo510/PS383 cb0: 736 bytes, all26 named fields active,
/// 150 literal-index scalar lanes (600 bytes). Only136 unread bytes may be
/// normalized. Its mode7 fog and modifier H use their own current owners.</summary>
public static class DeferredSelected383Constants
{
    public const int ByteSize = 736;
    public const int ActiveByteCount = 600;
    public const string PixelProgramSha256 = "91C2A580A1D05CA6144FD7CDE4F0AC24CD7ED425F2CFB4BCAAA4E9A4F62A3AF1";
    private static readonly byte[] masks = [7,7,7,7,7,7,7,7,15,7,1,7,3,7,7,7,3,15,15,15,
        15,7,15,15,15,15,7,7,7,0,15,15,15,15,1,7,7,7,15,15,15,15,15,15,15,15];
    private static readonly (string Name, int Offset, int Bytes)[] fields = [
        ("litDir",0,64),("litCol",64,64),("litOcc",128,16),("ambLit",144,16),("nDirLit",160,4),
        ("vEye",176,16),("vOccRate",192,8),("vCameraPos",208,12),("vCameraDir",224,12),
        ("vCameraViewY",240,12),("vD2Z",256,8),("mRrW2T",272,48),("fog",320,16),("cFog",336,16),("Scat",352,64),
        ("mV2W",416,64),("mP2W",480,64),("fSdmFade",544,4),("hsRampPrm",560,16),("hsAOPrm",576,16),
        ("hsUVMat",592,28),("fAmbMapsDORate",620,4),("vAmbDifScl",624,16),("vAmbSpcScl",640,16),
        ("vTsbViewPort",656,16),("mP2V",672,64)];
    public static IReadOnlyList<int> ActiveScalarByteOffsets { get; } = Array.AsReadOnly(masks.SelectMany((mask, index) =>
        Enumerable.Range(0, 4).Where(component => (mask & (1 << component)) != 0).Select(component => index * 16 + component * 4)).ToArray());

    public static DeferredSelected383ConstantPacket Create(DeferredF103SelectedSource selected,
        MapSourceCreatedLightingPacket lighting, DeferredSelected383AdditionalInputs additional)
    {
        ArgumentNullException.ThrowIfNull(selected); ArgumentNullException.ThrowIfNull(lighting); ArgumentNullException.ThrowIfNull(additional);
        ArgumentException.ThrowIfNullOrWhiteSpace(additional.CurrentProducerProvenance);
        var feature = selected.OwnedFeatureSource ?? throw new InvalidDataException("PS383 constants require the actual owned kind1 source feature.");
        feature.ValidateFrame(selected.Camera, lighting);
        if (feature.Scratch.Mode50 != 7 || lighting.AcceptedInputs.SelectedSharedFogOwnerEnabled is not bool fogEnabled ||
            lighting.AcceptedInputs.SelectedBuiltinAmbientCameraRate is not float mapOcclusionRate || !float.IsFinite(mapOcclusionRate) ||
            lighting.FrameNumber != selected.Current.FrameNumber || lighting.SceneInputs.Camera is null ||
            lighting.SceneInputs.Camera != PreparedCameraSceneSource.ReadCompletePrefix(selected.Camera.Prefix.Span))
            throw new InvalidDataException("PS383 requires its same-frame mode7 effective light, actual accepted Camera and known fog/Camera map-occlusion owners.");
        var scene = new DeferredSceneInputs(
            Lighting: DeferredSceneConstants.ReadPreparedBaseLighting(feature.EffectiveLocalLightData.Span[..0x1B0], checked((int)feature.Scratch.DirectionalCount)),
            Camera: lighting.SceneInputs.Camera,
            Fog: DeferredSceneConstants.ReadSharedFog(feature.EffectiveLocalLightData.Span[..0x1B0], fogEnabled),
            Ambient: DeferredSceneConstants.ReadAmbientModifier(feature.SelectedModifierLightData.Span.Slice(0x1B0, 0x178), mapOcclusionRate),
            Occlusion: additional.Occlusion, Refraction: additional.Refraction, ShadowFade: additional.ShadowFade,
            Hatching: additional.Hatching, TranslucencyViewport: additional.TranslucencyViewport, Projection: additional.Projection);
        return CreateFromExplicitCurrentScene(selected, scene, lighting.SourceProvenance + ";" + additional.CurrentProducerProvenance,
            additional.HatchingIeeeProof);
    }

    public static DeferredSelected383ConstantPacket CreateFromExplicitCurrentScene(DeferredF103SelectedSource selected,
        DeferredSceneInputs scene, string currentProducerProvenance, DeferredSelected383HatchingIeeeProof? hatchingIeeeProof = null)
    {
        ArgumentNullException.ThrowIfNull(selected); ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentProducerProvenance); selected.Validate();
        if (selected.Combination.Index != 510 || selected.PixelProgramIndex != 383 || selected.PixelProgramSha256 != PixelProgramSha256)
            throw new NotSupportedException("The PS383 factory requires its exact original combo510 shader.");
        var globals = selected.PixelDefinitions.Buffers.Single(buffer => buffer.Name.Text == "$Globals");
        var bindings = selected.PixelDefinitions.Bindings.Where(binding => binding.InputType == 0).ToArray();
        if (globals.ByteSize != ByteSize || globals.Variables.Count != fields.Length || bindings.Length != 1 ||
            bindings[0].BindPoint != 0 || bindings[0].BindCount != 1 ||
            fields.Any(field => !globals.Variables.Any(variable => variable.Name.Text == field.Name && variable.StartOffset == field.Offset && variable.ByteSize == field.Bytes)))
            throw new InvalidDataException("PS383 cb0 differs from its original RDEF and literal-index read inventory.");
        if (scene.Lighting is null || scene.Camera is null || scene.Fog is null || scene.Fog.Scattering.Count != 4 || scene.Ambient is null ||
            scene.Occlusion is null || scene.Refraction is null || scene.Refraction.WorldToTexture is not { Count: 3 } ||
            scene.ShadowFade is null || scene.Hatching is null || scene.TranslucencyViewport is null || scene.Projection is null ||
            scene.Camera != PreparedCameraSceneSource.ReadCompletePrefix(selected.Camera.Prefix.Span))
            throw new InvalidDataException("PS383 reads all26 fields: every current writer group, four mode7 scattering rows and the accepted Camera must be explicit.");
        hatchingIeeeProof?.Validate(selected, scene.Hatching);
        var evaluation = scene;
        if (hatchingIeeeProof is not null)
        {
            float Finite(float value) => float.IsFinite(value) ? value : 0;
            Vector4 V4(Vector4 value) => new(Finite(value.X), Finite(value.Y), Finite(value.Z), Finite(value.W));
            Vector3 V3(Vector3 value) => new(Finite(value.X), Finite(value.Y), Finite(value.Z));
            evaluation = scene with { Hatching = new(V4(scene.Hatching.Ramp), V4(scene.Hatching.AmbientOcclusion), V3(scene.Hatching.UvRow0), V3(scene.Hatching.UvRow1)) };
        }
        var written = DeferredSelectedSceneConstants.Apply(selected, new byte[ByteSize], evaluation);
        hatchingIeeeProof?.Restore(written.Bytes);
        if (written.RetainedVariables.Count != 0 || written.Writes.Count != fields.Length ||
            !written.Writes.Select(write => write.Name).Order(StringComparer.Ordinal).SequenceEqual(fields.Select(field => field.Name).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("An active PS383 field lacks its current source writer.");
        byte[] result = new byte[ByteSize];
        foreach (int offset in ActiveScalarByteOffsets)
        {
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(written.Bytes.AsSpan(offset, 4));
            if (offset != 160 && !float.IsFinite(BitConverter.UInt32BitsToSingle(word)) && hatchingIeeeProof?.Authorizes(offset, word) != true)
                throw new InvalidDataException("PS383 current source writer produced an unauthorized nonfinite active scalar at byte" + offset + ".");
            written.Bytes.AsSpan(offset, 4).CopyTo(result.AsSpan(offset, 4));
        }
        var packet = new DeferredSceneConstantPacket(result, written.Writes, Array.AsReadOnly(Array.Empty<string>()));
        var constants = new ReadOnlyDictionary<uint, ReadOnlyMemory<byte>>(new Dictionary<uint, ReadOnlyMemory<byte>> { [0] = result.ToArray() });
        return new(constants, packet, ActiveScalarByteOffsets, ByteSize - ActiveByteCount, currentProducerProvenance);
    }
}
