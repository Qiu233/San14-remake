using System.Buffers.Binary;
using System.Numerics;
using San14.Data;

/// <summary>Current geometry/profile and actual D3 color setter fields. The
/// original native Sky/Cloud/Fog programs, textures and temporal caches remain
/// separate owners. Moon zero uses its actual zero-amplitude source branch.</summary>
public sealed class D3D11CurrentAtmosphereFrame
{
    // 64 SkyTransform,64 LocalToGlobal,16 projectedSun,16 rawMoon,16 altitude,
    // 6 floats(offset,sizeSun,sizeMoon,infoZ,infoW,tangent),32 currentD3RGB.
    // The final64 bytes are directly uploadable SunRGB/SunDir/MoonRGB/MoonDir.
    public const int ByteLength = 296;
    public ReadOnlyMemory<byte> NumericBytes { get; }
    public SkyScenePreparedController Controller { get; }
    public SkySceneSky2Parameters Parameters { get; }
    public Sky2PreparedLight Sun { get; }
    public Sky2PreparedLight Moon { get; }
    public Vector3 MoonRgb { get; }
    public float MoonMaximum { get; }
    /// <summary>Loaded selected Camera614 for the actual Main view0. This
    /// scalar is a native helper argument, not a made-up sparse Main word.</summary>
    public float ControllerGeometry40 { get; }
    private D3D11CurrentAtmosphereFrame(byte[] bytes,
        SkyScenePreparedController controller, SkySceneSky2Parameters parameters,
        Sky2PreparedLight sun, Sky2PreparedLight moon, Vector3 moonRgb, float moonMaximum,
        float controllerGeometry40)
    { NumericBytes = bytes; Controller = controller; Parameters = parameters;
        Sun = sun; Moon = moon; MoonRgb = moonRgb; MoonMaximum = moonMaximum;
        ControllerGeometry40 = controllerGeometry40; }

    internal static D3D11CurrentAtmosphereFrame Prepare(
        MapSourceCreatedSeasonLightingFrame present,
        LoadedNativeCameraTangentSource tangent,
        LoadedSkyDirectionalSetterColorNativeSource color,
        ReadOnlySpan<byte> selectedGroup80, float controllerGeometry40)
    {
        var main = present.PreparedCameraPrefix.Span;
        var parameters = present.CurrentSkyCamera.Parameters;
        var controller = SkySceneSourceDefaults.PrepareController(
            present.CurrentSkyCamera.Controller, parameters, true, true);
        var transform = Sky2GeometryPreparation.PrepareSkyTransform(
            parameters.Angle1430, parameters.Angle1434, parameters.Angle143C);
        byte[] baseline = new byte[32];
        selectedGroup80.Slice(0xFA0, 16).CopyTo(baseline);
        selectedGroup80.Slice(0xFB0, 16).CopyTo(baseline.AsSpan(16));
        byte[] rgb = SkyDirectionalSetterColorMath.Apply(baseline,
            main.Slice(0x29C, 16), BinaryPrimitives.ReadUInt32LittleEndian(main[0x21C..]), color);
        Vector3 sunRgb = ReadRgb(rgb), moonRgb = ReadRgb(rgb.AsSpan(16));
        var sun = Sky2CameraLightPreparation.PrepareSun(true,
            controller.SunPosition!.Value, Sky2CameraLightPreparation.PrepareRgbCache(sunRgb),
            transform, parameters.SunIntensity1494, parameters.SunSize1490,
            parameters.SuppressDirection1600, controller.Controller.Scalar38)!;
        float moonMaximum = MathF.Max(moonRgb.X, MathF.Max(moonRgb.Y, moonRgb.Z));
        var moon = Sky2CameraLightPreparation.PrepareCurrentMoonIncludingZeroAmplitude(
            controller.MoonPosition!.Value, moonRgb, moonMaximum, transform,
            parameters.MoonIntensity149C, parameters.MoonSize1498,
            parameters.SuppressDirection1600, controller.Controller.Scalar38);
        float fov = BinaryPrimitives.ReadSingleLittleEndian(main[0x148..]);
        float halfTangent = NativeCameraTangent.Tangent1020A90(fov * .5f,
            tangent, NativeCameraTangentDispatch.Sse);
        float[] values = [..transform, ..Words(controller.Controller.Matrix1B0),
            ..Words(sun.PreparedNegatedSunPosition!.Value), ..Words(controller.MoonPosition!.Value),
            ..Words(parameters.AltitudeInfo), parameters.SkyYOffset1438,
            parameters.SunSize1490, parameters.MoonSize1498,
            controller.Controller.SkyInfoZW7C78.X, controller.Controller.SkyInfoZW7C78.Y, halfTangent];
        byte[] bytes = new byte[ByteLength];
        for (int i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), values[i]);
        rgb.CopyTo(bytes.AsSpan(200));
        float[] shaderLights = [..Words(sun.Rgb), ..Words(sun.Direction), ..Words(moon.Rgb), ..Words(moon.Direction)];
        for (int i = 0; i < shaderLights.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(232 + i * 4), shaderLights[i]);
        return new(bytes, controller, parameters, sun, moon, moonRgb, moonMaximum, controllerGeometry40);
    }
    private static Vector3 ReadRgb(ReadOnlySpan<byte> bytes) => new(
        BinaryPrimitives.ReadSingleLittleEndian(bytes),
        BinaryPrimitives.ReadSingleLittleEndian(bytes[4..]),
        BinaryPrimitives.ReadSingleLittleEndian(bytes[8..]));
    private static float[] Words(Vector4 v) => [v.X,v.Y,v.Z,v.W];
    private static float[] Words(Matrix4x4 v) => [v.M11,v.M12,v.M13,v.M14,v.M21,v.M22,v.M23,v.M24,
        v.M31,v.M32,v.M33,v.M34,v.M41,v.M42,v.M43,v.M44];
}

public static class D3D11CurrentLightingFields
{
    public static IReadOnlyDictionary<string, byte[]> Create(D3D11CurrentSeasonFrame frame,
        uint outerMask, uint innerMask, bool arg18, float ambientMapsRate, bool fogOwnerEnabled)
    {
        var outer = frame.Lights.FirstOrDefault(light => (light.GroupMask & outerMask) != 0)
            ?? throw new InvalidDataException("Current HDR has no actual matching outer light.");
        var inner = frame.Lights.FirstOrDefault(light => (light.GroupMask & innerMask) != 0) ?? outer;
        byte[] effective = DeferredF103Kind1Modifier.ApplyWithModifier(
            outer.CompleteData.Span, inner.CompleteData.Span, arg18).Bytes;
        ReadOnlyMemory<byte> selectedModifier = DeferredF103Kind1Modifier.HasBuiltinModifier(inner.CompleteData.Span)
            ? inner.CompleteData : effective;
        int count = 4;
        float F(int at) => BinaryPrimitives.ReadSingleLittleEndian(effective.AsSpan(at));
        while (count > 1)
        { int at = (count - 1) * 32 + 16; if (F(at) > 0 || F(at + 4) > 0 || F(at + 8) > 0) break; count--; }
        var lighting = DeferredSceneConstants.ReadPreparedBaseLighting(effective.AsSpan(0, 0x1B0), count);
        var fog = DeferredSceneConstants.ReadSharedFog(effective.AsSpan(0, 0x1B0), fogOwnerEnabled);
        var ambient = DeferredSceneConstants.ReadAmbientModifier(selectedModifier.Span.Slice(0x1B0, 0x178), ambientMapsRate);
        var result = new Dictionary<string, byte[]>();
        void Add(string name, params float[] values)
        { byte[] bytes = new byte[values.Length * 4]; for (int i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), values[i]); result.Add(name, bytes); }
        var dirs = Enumerable.Range(0, 4).Select(i => i < count ? lighting.Lights[i].Direction : new Vector4(0, 1, 0, 0));
        var cols = Enumerable.Range(0, 4).Select(i => i < count ? lighting.Lights[i].Color : Vector4.Zero);
        Add("litDir", dirs.SelectMany(Words).ToArray()); Add("litCol", cols.SelectMany(Words).ToArray());
        Add("litOcc", Enumerable.Range(0, 4).Select(i => i < count ? lighting.Lights[i].Occurrence : 1).ToArray());
        Add("ambLit", Words(lighting.Ambient));
        byte[] dirCount = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(dirCount, count - 1); result.Add("nDirLit", dirCount);
        Add("fog", Words(fog.Coefficients)); Add("cFog", Words(fog.Color)); Add("Scat", fog.Scattering.SelectMany(Words).ToArray());
        Add("fAmbMapsDORate", ambient.MapsDirectionalOcclusionRate);
        Add("vAmbDifScl", Words(ambient.DiffuseScale)); Add("vAmbSpcScl", Words(ambient.SpecularScale));
        return result;
    }
    private static float[] Words(Vector4 v) => [v.X,v.Y,v.Z,v.W];
}
