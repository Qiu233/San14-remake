using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record SkyScenePreparedCamera(string SourceProvenance,
    Matrix4x4 Matrix70, Matrix4x4 MatrixB0, Matrix4x4 MatrixF0, Vector4 Position,
    float Scalar138, float Scalar13C, uint Flags148, int NativeClipProfile,
    int TargetWidth, int TargetHeight);

/// <summary>Actual Sky2 cache fields, supplied from a named source profile or
/// retained original state. Constructor defaults are not selected here.</summary>
public sealed record SkySceneSky2Parameters(string SourceProvenance,
    float Angle1430, float Angle1434, float Angle143C, float SkyYOffset1438,
    Vector4 AltitudeInfo, Vector4 StableDimensions,
    float SunSize1490, float SunIntensity1494, float MoonSize1498, float MoonIntensity149C,
    float MilkyScale14A0, float MilkyStrength1484, bool SuppressDirection1600,
    Vector4 ClampDelta1610, int NativeTargetFormat);

public sealed record SkySceneSky2Controller(string SourceProvenance,
    Matrix4x4 Matrix1B0, float Scalar38, Vector2 SkyInfoZW7C78, float Scalar1F0);

public sealed record SkySceneLights(string SourceProvenance,
    bool SunPresent, Vector4 RawSunPosition, Sky2RgbCache SunRgbCache1460,
    bool MoonPresent, Vector4 RawMoonPosition, Vector3 RawMoonRgb, float MoonMaximum147C,
    Vector4 PackageLightPosition)
{
    public Sky2SourceZeroMoon? SourceZeroMoon { get; init; }
    public Sky2RgbCache? SourceMoonRgbCache1470 { get; init; }
}

public sealed record SkySceneTexturePresence(bool Earth, bool Moon, bool Milky);

public sealed record SkySceneSharedAtmosphere(string SourceProvenance,
    SkyFogPacket Fog, IReadOnlyList<Vector4> Scattering);

public sealed record SkySceneSky2FaceCache(Sky2DrawInputs Draw, bool SunInFront)
{
    public bool? MoonInFront { get; init; }
}

public sealed record SkyScenePreparedFrame(
    IReadOnlyList<SkySceneSky2FaceCache> Faces, IReadOnlyList<float> SkyTransform,
    IReadOnlyList<string> SourceProvenance)
{
    public IReadOnlyList<Sky2DrawInputs> DrawPackets => Faces.Select(f => f.Draw).ToArray();
}

/// <summary>One source update with all owners and presence decisions already
/// resolved by the caller. The six cameras are prepared native cube cameras.</summary>
public sealed record SkySceneSourceFrame(
    IReadOnlyList<SkyScenePreparedCamera> SixCameras,
    SkySceneSky2Parameters Parameters,
    SkySceneSky2Controller? Controller,
    SkySceneLights Lights,
    SkySceneTexturePresence Textures,
    SkySceneSharedAtmosphere Atmosphere)
{
    public SkyDirectionalSourceSelectedAtmosphereReceipt? SourceSelectedAtmosphere { get; init; }
}

/// <summary>Composes the closed118DC30 preparation helpers from explicit
/// source state. It does not choose active owners, native variants, desired/
/// committed flags, clock/controller timing, or generation budget. Six camera
/// packets are caller-prepared original cube cameras, not a synthetic identity.
/// Null controller or absent light preserves the corresponding prior cache.</summary>
public static class SkySceneFramePreparation
{
    /// <summary>First frame with current camera, controller and light writers.
    /// The selected pixel declarations determine whether Moon geometry is needed.
    /// Constant-buffer backing and padding remain device-owner inputs.</summary>
    public static SkyScenePreparedFrame PrepareFreshFullyWritten(SkySceneSourceFrame currentSourceFrame,
        params DxbcResourceDefinitions[] pixelPrograms)
    {
        ArgumentNullException.ThrowIfNull(currentSourceFrame);
        if(currentSourceFrame.Controller is null||!currentSourceFrame.Lights.SunPresent||
            !currentSourceFrame.Lights.MoonPresent)
            throw new NotSupportedException("Fresh Sky requires a current controller and present Sun and Moon writers.");
        return PrepareCore(null,currentSourceFrame.SixCameras,currentSourceFrame.Parameters,currentSourceFrame.Controller,
            currentSourceFrame.Lights,currentSourceFrame.Textures,currentSourceFrame.Atmosphere,pixelPrograms);
    }

    /// <summary>Fresh named caches from the actual selected Directional
    /// mode6 Camera writer. Finite Moon or its distinct PS10/4 zero capability
    /// is required; no mode2 constructor or synthetic previous face is used.</summary>
    public static SkyScenePreparedFrame PrepareFreshDirectionalFullyWritten(
        SkySceneSourceFrame currentSourceFrame, SkyDirectionalSelectedLightSource source,
        params DxbcResourceDefinitions[] pixelPrograms)
    {
        ArgumentNullException.ThrowIfNull(currentSourceFrame); ArgumentNullException.ThrowIfNull(source);
        source.ValidatePreparedLights(currentSourceFrame.Lights);
        currentSourceFrame.SourceSelectedAtmosphere?.ValidateSourceFrame(currentSourceFrame,source);
        if (currentSourceFrame.Controller is null || currentSourceFrame.Textures.Moon || currentSourceFrame.Textures.Milky)
            throw new NotSupportedException("Fresh Directional Sky requires a present controller and the explicit source-null Moon/Milky texture family.");
        return PrepareCore(null,currentSourceFrame.SixCameras,currentSourceFrame.Parameters,currentSourceFrame.Controller,
            currentSourceFrame.Lights,currentSourceFrame.Textures,currentSourceFrame.Atmosphere,pixelPrograms);
    }
    public static SkyScenePreparedFrame Prepare(
        IReadOnlyList<SkySceneSky2FaceCache> previousSixFaces,
        SkySceneSourceFrame explicitSourceFrame,
        params DxbcResourceDefinitions[] pixelPrograms)
    {
        ArgumentNullException.ThrowIfNull(previousSixFaces);
        ArgumentNullException.ThrowIfNull(explicitSourceFrame);
        return PrepareCore(previousSixFaces, explicitSourceFrame.SixCameras,
            explicitSourceFrame.Parameters, explicitSourceFrame.Controller,
            explicitSourceFrame.Lights, explicitSourceFrame.Textures,
            explicitSourceFrame.Atmosphere,pixelPrograms);
    }

    public static SkyScenePreparedFrame Prepare(
        IReadOnlyList<SkySceneSky2FaceCache> previousSixFaces,
        IReadOnlyList<SkyScenePreparedCamera> explicitSixCameras,
        SkySceneSky2Parameters explicitParameters,
        SkySceneSky2Controller? explicitController,
        SkySceneLights explicitLights,
        SkySceneTexturePresence explicitTexturePresence,
        SkySceneSharedAtmosphere explicitAtmosphere,params DxbcResourceDefinitions[] pixelPrograms)
    {
        ArgumentNullException.ThrowIfNull(previousSixFaces);
        return PrepareCore(previousSixFaces,explicitSixCameras,explicitParameters,explicitController,
            explicitLights,explicitTexturePresence,explicitAtmosphere,pixelPrograms);
    }
    private static SkyScenePreparedFrame PrepareCore(
        IReadOnlyList<SkySceneSky2FaceCache>? previousSixFaces,
        IReadOnlyList<SkyScenePreparedCamera> explicitSixCameras,
        SkySceneSky2Parameters explicitParameters,SkySceneSky2Controller? explicitController,
        SkySceneLights explicitLights,SkySceneTexturePresence explicitTexturePresence,
        SkySceneSharedAtmosphere explicitAtmosphere,params DxbcResourceDefinitions[] pixelPrograms)
    {
        ArgumentNullException.ThrowIfNull(pixelPrograms);
        if (pixelPrograms.Length == 0)
            throw new ArgumentException("Sky frame preparation requires its actual pixel program definitions.", nameof(pixelPrograms));
        // One prepared frame can feed atmosphere and contribution programs.
        // Only their actual live Moon fields require the complete geometry.
        bool needsMoonGeometry = pixelPrograms.SelectMany(program => program.Buffers)
            .SelectMany(buffer => buffer.Variables).Any(variable =>
                (variable.Flags & 2) != 0 && (variable.Name.Text is "g_mMoonFace" or "g_vMoonScreenPos"));
        ArgumentNullException.ThrowIfNull(explicitSixCameras);
        ArgumentNullException.ThrowIfNull(explicitParameters);
        ArgumentNullException.ThrowIfNull(explicitLights);
        ArgumentNullException.ThrowIfNull(explicitTexturePresence);
        ArgumentNullException.ThrowIfNull(explicitAtmosphere);
        if ((previousSixFaces is not null&&(previousSixFaces.Count != 6||previousSixFaces.Any(f=>f is null||f.Draw is null))) || explicitSixCameras.Count != 6 ||
            explicitSixCameras.Any(c => c is null || c.TargetWidth <= 0 || c.TargetHeight <= 0) ||
            explicitAtmosphere.Scattering is null || explicitAtmosphere.Scattering.Count != 4)
            throw new InvalidDataException("Sky frame requires six retained caches, six prepared cameras and complete shared Scat.");
        var provenance = explicitSixCameras.Select(c => c.SourceProvenance)
            .Concat([explicitParameters.SourceProvenance, explicitLights.SourceProvenance,
                explicitAtmosphere.SourceProvenance])
            .Concat(explicitController is null ? Array.Empty<string>() : [explicitController.SourceProvenance]).ToArray();
        if (provenance.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Every sky frame source needs explicit retained provenance.");
        var p = explicitParameters; var l = explicitLights; var controller = explicitController;
        float[] transform = Sky2GeometryPreparation.PrepareSkyTransform(p.Angle1430, p.Angle1434, p.Angle143C);
        var sun = Sky2CameraLightPreparation.PrepareSun(l.SunPresent, l.RawSunPosition,
            l.SunRgbCache1460, transform, p.SunIntensity1494, p.SunSize1490,
            p.SuppressDirection1600, controller?.Scalar38);
        if (l.SourceZeroMoon is not null && (!l.MoonPresent || l.SourceMoonRgbCache1470 is null))
            throw new InvalidDataException("Source Moon zero branch requires its nonnull setter and native retained cache.");
        if (l.SourceZeroMoon is { } cacheProof)
        {
            var nativeCache = Sky2CameraLightPreparation.PrepareSourceZeroMoonCache(l.RawMoonRgb, cacheProof);
            var cache = l.SourceMoonRgbCache1470!;
            if (BitConverter.SingleToUInt32Bits(cache.Maximum) != 0 ||
                new[] { cache.NormalizedRgb.X, cache.NormalizedRgb.Y, cache.NormalizedRgb.Z }
                    .Any(v => BitConverter.SingleToUInt32Bits(v) != BitConverter.SingleToUInt32Bits(nativeCache.NormalizedRgb.X)))
                throw new InvalidDataException("Source Moon setter cache must preserve the exact native invalid-product bits.");
        }
        var moon = l.SourceZeroMoon is { } zeroMoon
            ? Sky2CameraLightPreparation.PrepareSourceZeroMoon(l.RawMoonPosition, l.RawMoonRgb,
                l.MoonMaximum147C, transform, p.MoonIntensity149C, p.MoonSize1498,
                p.SuppressDirection1600, controller?.Scalar38, zeroMoon)
            : Sky2CameraLightPreparation.PrepareMoon(l.MoonPresent, l.RawMoonPosition,
            l.RawMoonRgb, l.MoonMaximum147C, transform, p.MoonIntensity149C, p.MoonSize1498,
            p.SuppressDirection1600, controller?.Scalar38);
        Vector4 skyInfo1 = Sky2ParameterPreparation.PrepareSkyInfo1(explicitTexturePresence.Earth,
            explicitTexturePresence.Moon, explicitTexturePresence.Milky, p.MilkyScale14A0,
            p.MilkyStrength1484, l.PackageLightPosition, l.RawMoonPosition);
        Vector4 clamp = Sky2ParameterPreparation.PrepareClampValue(p.NativeTargetFormat,
            p.ClampDelta1610, controller?.Scalar1F0);
        Vector4[] scat = explicitAtmosphere.Scattering.ToArray();
        var result = new List<SkySceneSky2FaceCache>(6);
        for (int face = 0; face < 6; face++)
        {
            var c = explicitSixCameras[face]; var old = previousSixFaces?[face];
            var camera = Sky2CameraLightPreparation.PrepareCamera(c.Matrix70, c.MatrixB0,
                c.Position, transform, c.Flags148, c.NativeClipProfile, controller?.Matrix1B0);
            var screen = sun?.PreparedNegatedSunPosition is Vector4 preparedSun
                ? Sky2GeometryPreparation.PrepareSunScreenPosition(true, preparedSun,
                    Words(c.MatrixF0), c.Scalar138, c.Scalar13C, c.TargetWidth, c.TargetHeight, p.SunSize1490)
                : null;
            var moonGeometry=needsMoonGeometry&&moon is not null
                ? Sky2MoonGeometryPreparation.Prepare(l.MoonPresent,
                    new Vector3(moon.Direction.X,moon.Direction.Y,moon.Direction.Z),l.RawMoonPosition,
                    Words(c.MatrixF0),c.Scalar138,c.Scalar13C,c.TargetWidth,c.TargetHeight,p.MoonSize1498)
                : null;
            Sky2DrawInputs draw;
            if(old is not null)draw=Sky2Constants.ApplyPreparedCameraAndLights(old.Draw,camera,sun,moon,screen,moonGeometry);
            else
            {
                // These are actual current writers, not constructor guesses.
                // Their absence leaves a native cache read and is rejected.
                if(camera.LocalToGlobalOverride is not Matrix4x4 localToGlobal||sun is null||moon is null||screen is null)
                    throw new InvalidDataException("Fresh Sky named readset lacks a current camera/controller/Sun/Moon/screen writer.");
                draw=new(new(explicitAtmosphere.Fog.Value,camera.CameraToLocal,camera.ProjectionFarZ,localToGlobal),
                    new(explicitAtmosphere.Fog.Value,scat,
                        Sky2ParameterPreparation.PrepareViewPosition(c.Position,p.AltitudeInfo,p.SkyYOffset1438,transform),
                        Sky2ParameterPreparation.PrepareSkyInfo0(c.Scalar138,c.TargetHeight,l.SunPresent,p.SunSize1490,l.MoonPresent,p.MoonSize1498,controller?.SkyInfoZW7C78),
                        skyInfo1,sun.Rgb,sun.Direction,moon.Rgb,moon.Direction,screen.Position,p.AltitudeInfo,p.StableDimensions,clamp,
                        moonGeometry?.MoonFace,moonGeometry?.Screen.Position)
                    {SourceZeroMoon=l.SourceZeroMoon});
            }
            draw = draw with { Pixel = draw.Pixel with {
                ViewPosition = Sky2ParameterPreparation.PrepareViewPosition(c.Position,
                    p.AltitudeInfo, p.SkyYOffset1438, transform),
                SkyInfo0 = Sky2ParameterPreparation.PrepareSkyInfo0(c.Scalar138,
                    c.TargetHeight, l.SunPresent, p.SunSize1490, l.MoonPresent, p.MoonSize1498,
                    controller?.SkyInfoZW7C78),
                SkyInfo1 = skyInfo1, AltitudeInfo = p.AltitudeInfo,
                StableDimensions = p.StableDimensions, ClampValue = clamp, Scattering = scat.ToArray(),
                SourceZeroMoon = l.SourceZeroMoon } };
            draw = Sky2Constants.ApplySharedFog(explicitAtmosphere.Fog, draw.Vertex, draw.Pixel);
            draw = draw with { Pixel = draw.Pixel with { Scattering = draw.Pixel.Scattering.ToArray() } };
            // Complete selected RDEF packing remains the GPU program's gate;
            // this layer also refuses nonfinite newly prepared named values.
            Finite(draw.Vertex.CameraToLocal); Finite(draw.Vertex.LocalToGlobal);
            if(explicitAtmosphere.Fog.SourceMode4Proof is { } fogProof)
            {fogProof.ValidateValue(draw.Vertex.Fog);fogProof.ValidateValue(draw.Pixel.Fog);}
            else {Finite(draw.Vertex.Fog);Finite(draw.Pixel.Fog);}
            Finite(draw.Vertex.ProjectionFarZ);
            foreach (var value in new[] { draw.Pixel.ViewPosition, draw.Pixel.SkyInfo0,
                draw.Pixel.SkyInfo1, draw.Pixel.SunRgb, draw.Pixel.SunDirection,
                draw.Pixel.MoonDirection, draw.Pixel.SunScreenPosition, draw.Pixel.AltitudeInfo,
                draw.Pixel.StableDimensions, draw.Pixel.ClampValue }) Finite(value);
            if (l.SourceZeroMoon is { } sourceZero) sourceZero.ValidatePreparedRgb(draw.Pixel.MoonRgb);
            else Finite(draw.Pixel.MoonRgb);
            foreach (var value in draw.Pixel.Scattering) Finite(value);
            result.Add(new(draw, screen?.InFront ?? old!.SunInFront)
                {MoonInFront=moonGeometry?.Screen.InFront??old?.MoonInFront});
        }
        return new(result.AsReadOnly(), Array.AsReadOnly(transform), Array.AsReadOnly(provenance));
    }

    private static float[] Words(Matrix4x4 m) => [m.M11,m.M12,m.M13,m.M14,
        m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
    private static void Finite(Matrix4x4 m) { foreach (var value in Words(m)) Finite(value); }
    private static void Finite(Vector4 v) { Finite(v.X);Finite(v.Y);Finite(v.Z);Finite(v.W); }
    private static void Finite(float v)
    {
        if (!float.IsFinite(v)) throw new InvalidDataException("Prepared sky frame contains a nonfinite source value.");
    }
}
