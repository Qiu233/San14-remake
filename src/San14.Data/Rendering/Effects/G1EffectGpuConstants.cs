using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record G1EffectGpuConstantRequest(uint StageCode, int ProgramIndex,
    DxbcResourceDefinitions Definitions, DxbcRdefBuffer Buffer, DxbcRdefVariable Variable);
public sealed record G1EffectGpuConstantMissing(uint StageCode, int ProgramIndex,
    string Buffer, string Variable, string Reason);
public sealed record G1EffectGpuConstantBuffer(uint StageCode, uint Slot, string Name, ReadOnlyMemory<byte> Bytes);
public sealed record G1EffectGpuConstantBuild(IReadOnlyList<G1EffectGpuConstantBuffer> Buffers,
    IReadOnlyList<G1EffectGpuConstantMissing> Missing)
{
    public bool IsComplete => Missing.Count == 0;
}

/// <summary>Writes every named variable of the currently selected stage's RDEF.
/// Fresh allocation clears padding only. Missing business values reject the
/// complete stage image instead of silently publishing a partially zero CB.</summary>
public static partial class G1EffectGpuConstants
{
    public static G1EffectGpuConstantBuild Build(G1EffectGpuStage stage, G1EffectGpuConstantInputs inputs) =>
        Build(stage.StageCode, stage.ProgramIndex, stage.ResourceDefinitions, inputs);

    public static G1EffectGpuConstantBuild Build(uint stageCode, int programIndex,
        DxbcResourceDefinitions definitions, G1EffectGpuConstantInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(definitions); ArgumentNullException.ThrowIfNull(inputs);
        var buffers = new List<G1EffectGpuConstantBuffer>();
        var missing = new List<G1EffectGpuConstantMissing>();
        foreach (DxbcRdefBuffer buffer in definitions.Buffers)
        {
            DxbcRdefBinding[] bindings = definitions.Bindings.Where(binding =>
                binding.InputType == 0 && binding.Name.Text == buffer.Name.Text).ToArray();
            if (bindings.Length != 1 || bindings[0].BindCount != 1)
            {
                missing.Add(new(stageCode, programIndex, buffer.Name.Text, "",
                    "RDEF constant buffer has no unique single-slot binding."));
                continue;
            }
            if (buffer.ByteSize < 0) throw new InvalidDataException("Negative effect constant buffer size.");
            byte[] storage = new byte[checked((buffer.ByteSize + 15) & ~15)];
            foreach (DxbcRdefVariable variable in buffer.Variables)
            {
                if (variable.StartOffset < 0 || variable.ByteSize < 0 || variable.StartOffset > buffer.ByteSize - variable.ByteSize)
                    throw new InvalidDataException("Effect constant variable escapes its declared buffer.");
                var request = new G1EffectGpuConstantRequest(stageCode, programIndex, definitions, buffer, variable);
                ReadOnlyMemory<byte>? value = Resolve(variable.Name.Text, inputs, out string reason);
                value ??= inputs.AdditionalProvider?.Invoke(request);
                if (value is null)
                {
                    missing.Add(new(stageCode, programIndex, buffer.Name.Text, variable.Name.Text, reason));
                    continue;
                }
                if (value.Value.Length != variable.ByteSize)
                {
                    missing.Add(new(stageCode, programIndex, buffer.Name.Text, variable.Name.Text,
                        $"Current producer returned {value.Value.Length} bytes; selected RDEF requires {variable.ByteSize}."));
                    continue;
                }
                value.Value.Span.CopyTo(storage.AsSpan(variable.StartOffset, variable.ByteSize));
            }
            buffers.Add(new(stageCode, bindings[0].BindPoint, buffer.Name.Text, storage));
        }
        return new(missing.Count == 0 ? buffers.AsReadOnly() : Array.Empty<G1EffectGpuConstantBuffer>(), missing.AsReadOnly());
    }

    private static ReadOnlyMemory<byte>? Resolve(string name, G1EffectGpuConstantInputs input, out string reason)
    {
        reason = $"No current Effect2 producer is bound for {name}.";
        var camera = input.Camera; var effect = input.Effect; var light = input.Light;
        var scene = light?.Scene; var shadow = input.Shadow?.Current; var cluster = input.Cluster;
        if (name.StartsWith("nFieldApplyType", StringComparison.Ordinal) || name.StartsWith("vNoiseFreq", StringComparison.Ordinal))
        {
            bool type = name.StartsWith("nFieldApplyType", StringComparison.Ordinal);
            string suffix = name[(type ? 15 : 10)..];
            if (suffix.Length != 1 || suffix[0] is < '0' or > '3') return null;
            int index = suffix[0] - '0';
            if (effect?.Fields is not { } fields)
            { reason = "Current row field table is not bound."; return null; }
            // The callback explicitly clears rows beyond the actual table count.
            return type ? Int(index < fields.Count ? fields[index].ApplyType : 0) :
                Vector(index < fields.Count ? fields[index].Frequency : Vector4.Zero);
        }
        switch (name)
        {
            case "mW2P": return camera is null ? null : Matrix(camera.WorldToProjection);
            case "mP2V": return camera is null ? null : Matrix(camera.ProjectionToView);
            case "vEye": return camera is null ? null : Vector(camera.Eye);
            case "vDDot": return camera is null ? null : Vector(camera.PrepareDepthDot());
            case "bZReverse": return camera is null ? null : Int(camera.ReverseDepth ? 1 : 0);
            case "zbias": case "zoffset": return camera is null ? null : Float(camera.PrepareDepthBias());
            case "vDPort": return camera?.DestinationPort is Vector4 dp ? Vector(dp) : null;
            case "vSPort": return camera?.SourcePort is Vector4 sp ? Vector(sp) : null;
            case "vATest": return effect is null ? null : Float(effect.AlphaTestEnabled ? effect.AlphaReference * .003921569f : -float.MaxValue);
            case "vSatFade": return effect is null ? null : Pair(effect.SaturationFade);
            case "bSatFade": return effect is null ? null : Int(effect.SaturationFadeEnabled ? 1 : 0);
            case "vFadeDist": return effect is null ? null : Pair(effect.FadeDistance);
            case "nDebugParticleID": return effect is null ? null : Int(effect.DebugParticleId);
            case "fMotionVectorScale": return effect?.MotionVectorScale is float motion ? Float(motion) : null;
            case "vColorScale": return light is null ? null : Vector(light.FlexColor);
            case "fog": return scene?.Fog is { } fog ? Vector(fog.Coefficients) : null;
            case "cFog": return scene?.Fog is { } fogColor ? Vector(fogColor.Color) : null;
            case "Scat": return scene?.Fog is { } scattering ? Vectors(scattering.Scattering) : null;
            case "ambLit": return scene?.Lighting is { } ambient ? Vector(ambient.Ambient) : null;
            case "nDirLit": return scene?.Lighting is { } count ? Int(count.Lights.Count - 1) : null;
            case "litDir": return scene?.Lighting is { } directions ? LightVectors(directions, false) : null;
            case "litCol": return scene?.Lighting is { } colors ? LightVectors(colors, true) : null;
            case "litOcc": return scene?.Lighting is { } occurrence ? Occurrence(occurrence) : null;
            case "mW2Pt": return input.Pattern is { } pattern ? Matrix(pattern.WorldToPattern) : shadow is not null ? Matrix(shadow.WorldToPattern) : null;
            case "fPtn": return input.Pattern is { } patternValue ? Float(patternValue.Pattern) : shadow is not null ? Float(shadow.Pattern) : null;
            case "fPtnPlSub": return input.Pattern?.PatternPlaneSub is { } planes ? Vectors(planes) : shadow is not null ? Vectors(shadow.PatternPlaneSub) : null;
            case "mW2S": return shadow is null ? null : Matrices(shadow.WorldToShadow);
            case "vSMOf": return shadow is null ? null : Vectors(shadow.ShadowOffsets);
            case "vS2VZ": return shadow is null ? null : Vector(shadow.ViewZScale);
            case "vSMDF": return shadow is null ? null : Vector(shadow.FadeDistances);
            case "scSM": return shadow is null ? null : Vector(shadow.ShadowScale);
            case "nSSLoop": return shadow is null ? null : Int(shadow.ShadowLoopCount);
            case "vLPlane": return shadow is null ? null : Vectors(shadow.LightPlanes);
            case "sdplane": return shadow is null ? null : Vector(shadow.SidePlane);
            case "scSM2": return input.Shadow?.SecondaryScale is Vector4 second ? Vector(second) : null;
            case "bSM": return input.Shadow?.ShadowModes is { } modes ? ScalarArray(modes.Select(value => value ? 1 : 0).ToArray()) : null;
            case "vClstZPrm": return cluster is null ? null : Vector(cluster.DepthParameters);
            case "nClstPtch": return cluster is null ? null : ScalarArray([cluster.HorizontalPatchCount, cluster.PatchCount]);
            case "fClstScl": return cluster?.Scaling is float scale ? Float(scale) : null;
            default: return null;
        }
    }

    private static byte[] LightVectors(DeferredLightingInputs input, bool colors)
    {
        if (input.Lights.Count is < 1 or > 4) throw new InvalidDataException("Effect base light count must be one through four.");
        var values = new Vector4[4];
        for (int index = 0; index < values.Length; index++)
            values[index] = index < input.Lights.Count ? colors ? input.Lights[index].Color : input.Lights[index].Direction :
                colors ? Vector4.Zero : new(0, 1, 0, 0);
        return Vectors(values);
    }
    private static byte[] Occurrence(DeferredLightingInputs input)
    {
        if (input.Lights.Count is < 1 or > 4) throw new InvalidDataException("Effect base light count must be one through four.");
        float Value(int index) => index < input.Lights.Count ? input.Lights[index].Occurrence : 1;
        return Vector(new(Value(0), Value(1), Value(2), Value(3)));
    }
    private static byte[] ScalarArray(IReadOnlyList<int> values)
    {
        if (values.Count == 0) return [];
        byte[] bytes = new byte[checked((values.Count - 1) * 16 + 4)];
        for (int index = 0; index < values.Count; index++) BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(index * 16), values[index]);
        return bytes;
    }
    private static byte[] Int(int value)
    { byte[] bytes = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(bytes, value); return bytes; }
    private static byte[] Float(float value) => Int(BitConverter.SingleToInt32Bits(value));
    private static byte[] Pair(Vector2 value)
    { byte[] bytes = new byte[8]; Put(bytes, 0, value.X); Put(bytes, 4, value.Y); return bytes; }
    private static byte[] Vector(Vector4 value)
    { byte[] bytes = new byte[16]; Put(bytes, 0, value.X); Put(bytes, 4, value.Y); Put(bytes, 8, value.Z); Put(bytes, 12, value.W); return bytes; }
    private static byte[] Vectors(IReadOnlyList<Vector4> values)
    {
        byte[] bytes = new byte[checked(values.Count * 16)];
        for (int index = 0; index < values.Count; index++) Vector(values[index]).CopyTo(bytes, index * 16);
        return bytes;
    }
    private static byte[] Matrix(Matrix4x4 value) => Vectors([
        new(value.M11,value.M12,value.M13,value.M14),new(value.M21,value.M22,value.M23,value.M24),
        new(value.M31,value.M32,value.M33,value.M34),new(value.M41,value.M42,value.M43,value.M44)]);
    private static byte[] Matrices(IReadOnlyList<Matrix4x4> values)
    {
        byte[] bytes = new byte[checked(values.Count * 64)];
        for (int index = 0; index < values.Count; index++) Matrix(values[index]).CopyTo(bytes, index * 64);
        return bytes;
    }
    private static void Put(Span<byte> bytes, int at, float value) => BinaryPrimitives.WriteSingleLittleEndian(bytes[at..], value);
}
