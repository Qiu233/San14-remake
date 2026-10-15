using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed record DeferredDirectionalLight(Vector4 Direction, Vector4 Color, float Occurrence);

/// <summary>Already prepared kind1 base lights, after the original trimming step.
/// Additional owner lights require a separate restored contract.</summary>
public sealed record DeferredLightingInputs(IReadOnlyList<DeferredDirectionalLight> Lights,
    Vector4 Ambient);
public sealed record DeferredCameraInputs(Matrix4x4 SourceWorldToView, Matrix4x4 SourceWorldToProjection,
    Vector4 SourceEye);
public sealed record DeferredFogInputs(Vector4 Color, Vector4 Coefficients, IReadOnlyList<Vector4> Scattering);
public sealed record DeferredAmbientInputs(float MapsDirectionalOcclusionRate, Vector4 DiffuseScale, Vector4 SpecularScale);
public sealed record DeferredPixelToTextureInputs(Vector4 Transform)
{
    /// <summary>Only for the original explicit no-currentColor branch.</summary>
    public static DeferredPixelToTextureInputs NoCurrentColorFallback() => new(new Vector4(1, 1, 0, 0));
}
public sealed record DeferredVolumeFogInputs(Vector4 Info);
public sealed record DeferredRefractionInputs(Vector3 CameraPosition, Vector3 CameraDirection, Vector3 CameraViewY,
    Vector2 DepthToZ, IReadOnlyList<Vector4>? WorldToTexture);
public sealed record DeferredProjectionInputs(Matrix4x4 ProjectionToView);
public sealed record DeferredLocalAmbientOcclusionInputs(Vector2 Value);
public sealed record DeferredTranslucencyViewportInputs(Vector4 Value);
public sealed record DeferredOcclusionInputs(Vector2 Rates)
{
    /// <summary>Only after successful original OccFader data allocation.</summary>
    public static DeferredOcclusionInputs SuccessfulDataDefaults() => new(new Vector2(1,1));
}
public sealed record DeferredHeightFogInputs(IReadOnlyList<int> Types,IReadOnlyList<Vector4> Colors,
    IReadOnlyList<Vector4> Parameters,IReadOnlyList<float> FluctuationParameters)
{
    /// <summary>Only after selecting the original three-layer module and its
    /// successful default callback, without subsequent setters.</summary>
    public static DeferredHeightFogInputs SuccessfulThreeLayerDefaults() => new(
        Array.AsReadOnly(new[] {-1,-1,-1}),Array.AsReadOnly(new Vector4[3]),Array.AsReadOnly(new Vector4[3]),Array.AsReadOnly(new float[60]));
}
public sealed record DeferredHatchingInputs(Vector4 Ramp, Vector4 AmbientOcclusion, Vector3 UvRow0, Vector3 UvRow1)
{
    /// <summary>Only after the exact plugin's successful default callback and
    /// while no later modifier has replaced its values.</summary>
    public static DeferredHatchingInputs SuccessfulPluginDefaults() =>
        new(new(0, -1, 0, 0), new(0, 0, 1, 0), new(1, 0, 0), new(0, 1, 0));
}

/// <summary>Optional, independently restored writer groups. Null retains all
/// corresponding caller bytes; it does not mean a disabled effect.</summary>
public sealed record DeferredSceneInputs(DeferredLightingInputs? Lighting = null,
    DeferredCameraInputs? Camera = null, DeferredFogInputs? Fog = null,
    DeferredAmbientInputs? Ambient = null, DeferredHatchingInputs? Hatching = null,
    DeferredPixelToTextureInputs? PixelToTexture = null, DeferredVolumeFogInputs? VolumeFog = null,
    DeferredRefractionInputs? Refraction = null, DeferredProjectionInputs? Projection = null,
    DeferredLocalAmbientOcclusionInputs? LocalAmbientOcclusion = null,
    DeferredTranslucencyViewportInputs? TranslucencyViewport = null,DeferredHeightFogInputs? HeightFog = null,
    DeferredOcclusionInputs? Occlusion = null,
    DeferredClusterInputs? Cluster = null,
    DeferredLocalAmbientInputs? LocalAmbient = null,
    DeferredShadowFadeInputs? ShadowFade = null,
    DeferredRimTableInputs? RimTable = null, DeferredRampInputs? Ramp = null);
public sealed record DeferredSceneWrite(string Name, int Offset, int ElementCount, int Stride, int ElementBytes);
public sealed record DeferredSceneConstantPacket(byte[] Bytes, IReadOnlyList<DeferredSceneWrite> Writes,
    IReadOnlyList<string> RetainedVariables);

/// <summary>Named writes for the verified original VS0/PS1 $Globals layout.
/// Preserves a complete caller template, including unknown fields and array
/// padding. It does not select a live View, LightGroup, shader row or producer.</summary>
public static class DeferredSceneConstants
{
    public const int ByteSize = 2704;

    /// <summary>Original9969F0 using currentColor dimensions and viewport X/Y/W/H.
    /// Each operation remains binary32; these are not source allocation sizes.</summary>
    public static DeferredPixelToTextureInputs PreparePixelToTexture(int currentColorWidth, int currentColorHeight,
        int viewportX, int viewportY, int viewportWidth, int viewportHeight)
    {
        if (currentColorWidth <= 0 || currentColorHeight <= 0)
            throw new InvalidDataException("Pixel-to-texture requires positive currentColor dimensions and an explicit int32 viewport.");
        float sx = Multiply(Divide(viewportWidth, currentColorWidth), .5f);
        float sy = Multiply(Divide(viewportHeight, currentColorHeight), -.5f);
        float tx = Add(Divide(viewportX, currentColorWidth), sx);
        float ty = Add(Divide(viewportY, currentColorHeight), Multiply(Divide(viewportHeight, currentColorHeight), .5f));
        return new(new Vector4(sx, sy, tx, ty));
    }

    /// <summary>Prepared Camera C prefix, not the raw ViewCPU camera fields.
    /// Eye's fourth word is copied as-is, never replaced by a positional default.</summary>
    public static DeferredCameraInputs ReadPreparedCamera(ReadOnlySpan<byte> preparedCameraPrefix)
    {
        if (preparedCameraPrefix.Length != 0x130)
            throw new InvalidDataException("Prepared camera requires the explicit 0x130 prefix.");
        return new(ReadMatrix(preparedCameraPrefix, 0x70), ReadMatrix(preparedCameraPrefix, 0xF0), ReadVector(preparedCameraPrefix, 0));
    }

    /// <summary>Share one prepared camera between terrain and final shading.
    /// Explicit terrain/material parameters are retained; matrix and Eye values
    /// are derived in their respective original shader layouts.</summary>
    public static TerrainGbufferDrawInputs PrepareTerrainCamera(DeferredCameraInputs camera,
        TerrainGbufferDrawInputs explicitTerrainParameters)
    {
        ArgumentNullException.ThrowIfNull(camera); ArgumentNullException.ThrowIfNull(explicitTerrainParameters);
        return explicitTerrainParameters with
        {
            WorldToProjectionRows = Words(camera.SourceWorldToProjection).Select(BitConverter.UInt32BitsToSingle).ToArray(),
            WorldToViewRows = Words(Matrix4x4.Transpose(camera.SourceWorldToView))[..12].Select(BitConverter.UInt32BitsToSingle).ToArray(),
            Eye = camera.SourceEye
        };
    }

    /// <summary>Original9BC9A0. Inputs are C+130/+134, C+148 bit1 and plugin
    /// P+28/+2C. Caller retains the actual provider; no log/volume default is inferred.</summary>
    public static DeferredVolumeFogInputs PrepareVolumeFog(float camera130, float camera134,
        bool camera148Bit1, float plugin28, float plugin2C)
    {
        if (new[] { camera130, camera134, plugin28, plugin2C }.Any(v => !float.IsFinite(v)))
            throw new InvalidDataException("Volumetric fog writer requires explicit finite source fields.");
        float a = camera148Bit1 ? camera134 : camera130, b = camera148Bit1 ? camera130 : camera134;
        return new(new Vector4(Divide(Divide(Multiply(b, a), Subtract(b, a)), camera130),
            Divide(b, Subtract(a, b)), Divide(Multiply(camera134, plugin28), camera130), plugin2C));
    }

    /// <summary>Reads a bounded effective LightData prefix. The explicit count
    /// must already reflect the native trailing-light trimming; no publisher
    /// identity or frame coherence is established by this byte projection.</summary>
    public static DeferredLightingInputs ReadPreparedBaseLighting(ReadOnlySpan<byte> effectiveLightData,
        int explicitPreparedDirectionalCount)
    {
        if (effectiveLightData.Length != 0x1B0 || explicitPreparedDirectionalCount is < 1 or > 4)
            throw new InvalidDataException("Prepared kind1 lighting requires a complete 0x1B0 prefix and explicit count1..4.");
        var lights = new DeferredDirectionalLight[explicitPreparedDirectionalCount];
        for (int i = 0; i < lights.Length; i++)
            lights[i] = new(ReadVector(effectiveLightData, i * 0x20), ReadVector(effectiveLightData, i * 0x20 + 0x10),
                ReadFloat(effectiveLightData, 0x80 + i * 4));
        return new(Array.AsReadOnly(lights), ReadVector(effectiveLightData, 0xE0));
    }

    /// <summary>Generate the shared mode7/profile2 fog from the SAME effective
    /// LightData record. Inactive fog honors the original neutral Scat override.</summary>
    public static DeferredFogInputs ReadSharedFog(ReadOnlySpan<byte> effectiveLightData, bool explicitOwnerFogEnabled)
    {
        if (effectiveLightData.Length != 0x1B0)
            throw new InvalidDataException("Shared fog requires the complete effective LightData 0x1B0 prefix.");
        return ReadSharedFog(effectiveLightData, explicitOwnerFogEnabled,
            new(ReadFloat(effectiveLightData, 0x10), ReadFloat(effectiveLightData, 0x14), ReadFloat(effectiveLightData, 0x18)));
    }

    /// <summary>Fog belongs to the outer light; scattering uses its current
    /// modified directional color, independently of the selected modifier.</summary>
    public static DeferredFogInputs ReadSharedFog(ReadOnlySpan<byte> effectiveLightData,
        bool explicitOwnerFogEnabled, Vector3 directionalRgb)
    {
        if (effectiveLightData.Length != 0x1B0)
            throw new InvalidDataException("Shared fog requires the complete effective LightData 0x1B0 prefix.");
        ReadOnlySpan<byte> record = effectiveLightData.Slice(0x150, 96);
        SkyFogPacket fog = SkyFogMath.Generate(7, 2, explicitOwnerFogEnabled, record);
        SkyScatteringPacket scatter = fog.ScatteringOverride ?? SkyScatteringMath.Generate(7, record, directionalRgb);
        return new(ReadVector(record, 0), fog.Value, Array.AsReadOnly(scatter.Values.ToArray()));
    }

    /// <summary>Prepare shared fog directly from the current light's named
    /// values and mode, using its current modified directional color.</summary>
    public static DeferredFogInputs PrepareSharedFog(DirectionalLightFogState currentFog,
        bool explicitOwnerFogEnabled, Vector3 directionalRgb)
    {
        ArgumentNullException.ThrowIfNull(currentFog);
        ArgumentNullException.ThrowIfNull(currentFog.Values);
        SkyWeatherFogValues values = currentFog.Values;
        SkyFogPacket fog = SkyFogMath.GenerateWeather(currentFog.Mode, values, 2, explicitOwnerFogEnabled);
        SkyScatteringPacket scatter = fog.ScatteringOverride ?? SkyScatteringMath.GenerateWeather(values, directionalRgb);
        return new(values.Background, fog.Value, Array.AsReadOnly(scatter.Values.ToArray()));
    }

    /// <summary>Modifier H is distinct from effective LightData L. The separate
    /// map-occlusion rate comes from CameraScratch+3CC, not the preceding zero argument.</summary>
    public static DeferredAmbientInputs ReadAmbientModifier(ReadOnlySpan<byte> modifierPrefix,
        float explicitCameraMapOcclusionRate)
    {
        if (modifierPrefix.Length != 0x178)
            throw new InvalidDataException("Ambient modifier requires an explicit 0x178 prefix.");
        float diffuse = ReadFloat(modifierPrefix, 0x168), specular = ReadFloat(modifierPrefix, 0x158);
        return new(explicitCameraMapOcclusionRate,
            new(Multiply(diffuse, ReadFloat(modifierPrefix, 0x16C)), Multiply(diffuse, ReadFloat(modifierPrefix, 0x170)),
                Multiply(diffuse, ReadFloat(modifierPrefix, 0x174)), ReadFloat(modifierPrefix, 0x150)),
            new(Multiply(specular, ReadFloat(modifierPrefix, 0x15C)), Multiply(specular, ReadFloat(modifierPrefix, 0x160)),
                Multiply(specular, ReadFloat(modifierPrefix, 0x164)), ReadFloat(modifierPrefix, 0x154)));
    }

    public static DeferredSceneConstantPacket Apply(DxbcResourceDefinitions definitions,
        ReadOnlySpan<byte> explicitTemplate, DeferredSceneInputs input)
    {
        ArgumentNullException.ThrowIfNull(definitions); ArgumentNullException.ThrowIfNull(input);
        var layout=DeferredSceneConstantLayout.Canonical(definitions);
        layout.RequireTemplate(explicitTemplate.Length);
        return Apply(layout,explicitTemplate,input);
    }

    // The numerical writer is shared by canonical, early and selected layouts.
    // Only a validated PS767 source proof may authorize its three IEEE fields.
    internal static DeferredSceneConstantPacket Apply(DeferredSceneConstantLayout layout,
        ReadOnlySpan<byte> explicitTemplate,DeferredSceneInputs input,bool allowHatchingIeee=false)
    {
        byte[] bytes=explicitTemplate.ToArray();var writes=new List<DeferredSceneWrite>();
        void Write(string name,uint[] words)=>layout.Write(name,words,bytes,writes,allowHatchingIeee);
        void Vector(string name,Vector4 value)=>Write(name,Words(value));
        if (input.Lighting is { } lighting)
        {
            if (lighting.Lights is null || lighting.Lights.Count is < 1 or > 4 || lighting.Lights.Any(l => l is null))
                throw new InvalidDataException("Scene lighting requires explicit prepared kind1 base lights1..4.");
            var dirs = new Vector4[4]; var colors = new Vector4[4]; var occurrence = new float[4];
            for (int i = 0; i < 4; i++)
            {
                var light = i < lighting.Lights.Count ? lighting.Lights[i] : null;
                dirs[i] = light?.Direction ?? new Vector4(0, 1, 0, 0);
                colors[i] = light?.Color ?? Vector4.Zero; occurrence[i] = light?.Occurrence ?? 1;
            }
            Write("litDir", dirs.SelectMany(Words).ToArray());
            Write("litCol", colors.SelectMany(Words).ToArray());
            // RDEF retains an array of ONE float4 (Elements=1), although the
            // Microsoft textual disassembly prints it as plain float4.
            Write("litOcc", occurrence.Select(BitConverter.SingleToUInt32Bits).ToArray());
            Vector("ambLit", lighting.Ambient);
            Write("nDirLit", [(uint)(lighting.Lights.Count - 1)]);
        }
        if (input.Camera is { } camera)
        {
            Vector("vEye", camera.SourceEye);
            Write("mW2P", Words(camera.SourceWorldToProjection));
            uint[] transposed = Words(Matrix4x4.Transpose(camera.SourceWorldToView));
            Write("mWV", transposed[..12]);
            Write("mV2W", Words(NativeMatrixMath.Invert(camera.SourceWorldToView, out _)));
            Write("mP2W", Words(NativeMatrixMath.Invert(camera.SourceWorldToProjection, out _)));
        }
        if (input.Fog is { } fog)
        {
            if (fog.Scattering is null || fog.Scattering.Count != 4)
                throw new InvalidDataException("Scene fog requires four explicit Scat rows.");
            Vector("fog", fog.Coefficients); Vector("cFog", fog.Color);
            Write("Scat", fog.Scattering.SelectMany(Words).ToArray());
        }
        if (input.Ambient is { } ambient)
        {
            Write("fAmbMapsDORate", [BitConverter.SingleToUInt32Bits(ambient.MapsDirectionalOcclusionRate)]);
            Vector("vAmbDifScl", ambient.DiffuseScale); Vector("vAmbSpcScl", ambient.SpecularScale);
        }
        if (input.Hatching is { } hatching)
        {
            Vector("hsRampPrm", hatching.Ramp); Vector("hsAOPrm", hatching.AmbientOcclusion);
            Write("hsUVMat", [BitConverter.SingleToUInt32Bits(hatching.UvRow0.X),
                BitConverter.SingleToUInt32Bits(hatching.UvRow0.Y), BitConverter.SingleToUInt32Bits(hatching.UvRow0.Z),
                BitConverter.SingleToUInt32Bits(hatching.UvRow1.X), BitConverter.SingleToUInt32Bits(hatching.UvRow1.Y),
                BitConverter.SingleToUInt32Bits(hatching.UvRow1.Z)]);
        }
        if (input.PixelToTexture is { } pixelToTexture) Vector("vP2T", pixelToTexture.Transform);
        if (input.VolumeFog is { } volumeFog) Vector("gVfogInfo", volumeFog.Info);
        if (input.Refraction is { } refraction)
        {
            Write("vCameraPos", Words(refraction.CameraPosition));
            Write("vCameraDir", Words(refraction.CameraDirection));
            Write("vCameraViewY", Words(refraction.CameraViewY));
            Write("vD2Z", Words(refraction.DepthToZ));
            if (refraction.WorldToTexture is { } rows)
            {
                if (rows.Count != 3) throw new InvalidDataException("Refraction projection requires three explicit float4 rows.");
                Write("mRrW2T", rows.SelectMany(Words).ToArray());
            }
        }
        if (input.Projection is { } projection) Write("mP2V", Words(projection.ProjectionToView));
        if (input.LocalAmbientOcclusion is { } localAmbient) Write("vAmbSpcLclOcc", Words(localAmbient.Value));
        if (input.TranslucencyViewport is { } translucency) Vector("vTsbViewPort", translucency.Value);
        if (input.HeightFog is { } heightFog)
        {
            if(heightFog.Types is null||heightFog.Types.Count!=3||heightFog.Colors is null||heightFog.Colors.Count!=3||
                heightFog.Parameters is null||heightFog.Parameters.Count!=3||heightFog.FluctuationParameters is null||heightFog.FluctuationParameters.Count!=60)
                throw new InvalidDataException("PS1 height fog requires explicit arrays3/3/3/60 from the selected module.");
            Write("hFogType",heightFog.Types.Select(v=>unchecked((uint)v)).ToArray());
            Write("cHFog",heightFog.Colors.SelectMany(Words).ToArray());
            Write("hFogParam",heightFog.Parameters.SelectMany(Words).ToArray());
            Write("hFogFlucPrms",heightFog.FluctuationParameters.Select(BitConverter.SingleToUInt32Bits).ToArray());
        }
        if(input.Occlusion is { } occlusion)Write("vOccRate",Words(occlusion.Rates));
        if(input.Cluster is { } cluster)
        {
            Vector("vClstZPrm",cluster.DepthParameters);
            Write("nClstPtch",
                [unchecked((uint)cluster.HorizontalPatchCount),unchecked((uint)cluster.PatchCount)]);
            if(cluster.Scaling is { } scale)
                Write("fClstScl",[BitConverter.SingleToUInt32Bits(scale)]);
            if(cluster.FadeDistance is { } distance)Write("vFadeDist",Words(distance));
        }
        if(input.LocalAmbient is { } localMaps)
        {
            Write("vLclAmbFogDecayRange",Words(localMaps.FogDecayRange));
            Write("vLclAmbDecay",Words(localMaps.Decay));
        }
        if(input.ShadowFade is { } shadowFade)
            Write("fSdmFade",[BitConverter.SingleToUInt32Bits(shadowFade.Value)]);
        if(input.RimTable is { } rim)
        {
            if(rim.Colors is null||rim.Colors.Count!=16||rim.Extents is null||rim.Extents.Count!=16)
                throw new InvalidDataException("PS1 rim tables require sixteen explicit color and int4 extent rows.");
            Write("cRimTbl",rim.Colors.SelectMany(Words).ToArray());
            Write("nRimExt",rim.Extents.SelectMany(v=>new[]
                {unchecked((uint)v.X),unchecked((uint)v.Y),unchecked((uint)v.Z),unchecked((uint)v.W)}).ToArray());
        }
        if(input.Ramp is { } ramp)
        {
            if(ramp.Parameters is null||ramp.Parameters.Count!=3||ramp.RimParameters is null||ramp.RimParameters.Count!=2||
                ramp.SecondaryRimParameters is null||ramp.SecondaryRimParameters.Count!=2)
                throw new InvalidDataException("PS1 ramp parameters require explicit prepared arrays3/2/2.");
            Write("rampType",[unchecked((uint)ramp.Type)]);
            Write("rampTexHeight",[unchecked((uint)ramp.TextureHeight)]);
            Write("rampPkPrms",ramp.Parameters.SelectMany(Words).ToArray());
            Write("rampRimPkPrms",ramp.RimParameters.SelectMany(Words).ToArray());
            Write("rampSecondaryRimPkPrms",ramp.SecondaryRimParameters.SelectMany(Words).ToArray());
            Write("rampRimThres",[BitConverter.SingleToUInt32Bits(ramp.RimThreshold)]);
        }
        return new(bytes,writes.AsReadOnly(),layout.RetainedVariables(writes));
    }

    private static Vector4 ReadVector(ReadOnlySpan<byte> bytes, int offset) =>
        new(ReadFloat(bytes, offset), ReadFloat(bytes, offset + 4), ReadFloat(bytes, offset + 8), ReadFloat(bytes, offset + 12));
    private static Matrix4x4 ReadMatrix(ReadOnlySpan<byte> b, int at) => new(
        ReadFloat(b,at),ReadFloat(b,at+4),ReadFloat(b,at+8),ReadFloat(b,at+12),
        ReadFloat(b,at+16),ReadFloat(b,at+20),ReadFloat(b,at+24),ReadFloat(b,at+28),
        ReadFloat(b,at+32),ReadFloat(b,at+36),ReadFloat(b,at+40),ReadFloat(b,at+44),
        ReadFloat(b,at+48),ReadFloat(b,at+52),ReadFloat(b,at+56),ReadFloat(b,at+60));
    private static float ReadFloat(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset, 4));
    private static uint[] Words(Vector4 v) => [BitConverter.SingleToUInt32Bits(v.X), BitConverter.SingleToUInt32Bits(v.Y), BitConverter.SingleToUInt32Bits(v.Z), BitConverter.SingleToUInt32Bits(v.W)];
    private static uint[] Words(Vector3 v) => [BitConverter.SingleToUInt32Bits(v.X), BitConverter.SingleToUInt32Bits(v.Y), BitConverter.SingleToUInt32Bits(v.Z)];
    private static uint[] Words(Vector2 v) => [BitConverter.SingleToUInt32Bits(v.X), BitConverter.SingleToUInt32Bits(v.Y)];
    private static uint[] Words(Matrix4x4 m) => new[] { m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44 }.Select(BitConverter.SingleToUInt32Bits).ToArray();
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float left, float right) => left * right;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float left, float right) => left / right;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float left, float right) => left + right;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Subtract(float left, float right) => left - right;
}
