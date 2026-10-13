using System.Numerics;

namespace San14.Data;

public sealed record Sky2VertexInputs(Vector4 Fog, Matrix4x4 CameraToLocal,
    float ProjectionFarZ, Matrix4x4 LocalToGlobal)
{
    public SkyDirectionalMode4FogReceipt? SourceMode4Fog { get; init; }
}

public sealed record Sky2PixelInputs(Vector4 Fog, IReadOnlyList<Vector4> Scattering,
    Vector4 ViewPosition, Vector4 SkyInfo0, Vector4 SkyInfo1, Vector4 SunRgb, Vector4 SunDirection,
    Vector4 MoonRgb, Vector4 MoonDirection, Vector4 SunScreenPosition, Vector4 AltitudeInfo,
    Vector4 StableDimensions, Vector4 ClampValue, Matrix4x4? MoonFace = null,
    Vector4? MoonScreenPosition = null)
{
    public Sky2SourceZeroMoon? SourceZeroMoon { get; init; }
    public SkyDirectionalMode4FogReceipt? SourceMode4Fog { get; init; }
}

public sealed record Sky2ConstantWrite(string Name, int Offset, int ElementCount, int Stride, int ElementBytes);
public sealed record Sky2ConstantPacket(byte[] Bytes, IReadOnlyList<Sky2ConstantWrite> Writes);
public sealed record Sky2DrawInputs(Sky2VertexInputs Vertex, Sky2PixelInputs Pixel);

/// <summary>Packs explicit named inputs against the verified original RDEF.
/// A complete caller-supplied template retains padding; this does not recover native globals.</summary>
public static class Sky2Constants
{

    /// <summary>Apply prepared camera/light outputs while retaining native
    /// cache values for absent lights, absent controller and skipped Sun token.
    /// Texture presence, SkyInfo and InFront/variant selection remain explicit.</summary>
    public static Sky2DrawInputs ApplyPreparedCameraAndLights(Sky2DrawInputs previous,
        Sky2CameraPacket camera,Sky2PreparedLight? sun,Sky2PreparedLight? moon,
        Sky2SunScreenPosition? sunScreen)
        =>ApplyPreparedCameraAndLights(previous,camera,sun,moon,sunScreen,null);

    /// <summary>The actual nonnull Moon continuation also writes Face and
    /// Screen. A skipped Moon setter retains both previous caches, independently
    /// of texture presence and the separate Sun projection.</summary>
    public static Sky2DrawInputs ApplyPreparedCameraAndLights(Sky2DrawInputs previous,
        Sky2CameraPacket camera,Sky2PreparedLight? sun,Sky2PreparedLight? moon,
        Sky2SunScreenPosition? sunScreen,Sky2MoonGeometryPacket? moonGeometry)
    {
        ArgumentNullException.ThrowIfNull(previous);ArgumentNullException.ThrowIfNull(camera);
        if(moon is null&&moonGeometry is not null)
            throw new InvalidDataException("Absent Moon setter cannot acquire current MoonFace/Screen writes.");
        return new(previous.Vertex with{CameraToLocal=camera.CameraToLocal,ProjectionFarZ=camera.ProjectionFarZ,
            LocalToGlobal=camera.LocalToGlobalOverride??previous.Vertex.LocalToGlobal},
            previous.Pixel with{SunRgb=sun?.Rgb??previous.Pixel.SunRgb,SunDirection=sun?.Direction??previous.Pixel.SunDirection,
                MoonRgb=moon?.Rgb??previous.Pixel.MoonRgb,MoonDirection=moon?.Direction??previous.Pixel.MoonDirection,
                SunScreenPosition=sunScreen?.Position??previous.Pixel.SunScreenPosition,
                MoonFace=moonGeometry?.MoonFace??previous.Pixel.MoonFace,
                MoonScreenPosition=moonGeometry?.Screen.Position??previous.Pixel.MoonScreenPosition});
    }

    /// <summary>Apply final shared fog to both stages, honoring the explicit
    /// mode7 neutral Scat override and the source mode4/profile1 inactive
    /// zeroFog branch. Otherwise retain the caller's existing Scat;
    /// this does not infer dirty state or unconditionally rerun the writer.</summary>
    public static Sky2DrawInputs ApplySharedFog(SkyFogPacket fog,
        Sky2VertexInputs vertex, Sky2PixelInputs pixel)
    {
        ArgumentNullException.ThrowIfNull(fog);ArgumentNullException.ThrowIfNull(vertex);ArgumentNullException.ThrowIfNull(pixel);
        bool sourceMode4=fog.RendererFogMode==4&&fog.Profile==1&&fog.SourceFogMode==0&&fog.FogEnabled&&
            !fog.RequiresMode7NeutralScattering&&fog.ScatteringOverride is null&&fog.Value==Vector4.Zero&&
            fog.Bytes.Length==16&&fog.Bytes.All(b=>b==0);
        if(fog.SourceMode4Proof is { } sourceMode4Proof)
        { sourceMode4Proof.Validate(fog); sourceMode4=true; }
        if((fog.RendererFogMode!=7&&!sourceMode4) || fog.RequiresMode7NeutralScattering!=(fog.ScatteringOverride is not null))
            throw new InvalidDataException("Unsupported or inconsistent original shared fog packet.");
        var scattering=fog.ScatteringOverride;
        if(scattering is not null && (scattering.RendererFogMode!=7 || scattering.Values.Count!=4 || scattering.Bytes.Length!=64))
            throw new InvalidDataException("Original neutral scattering override has an unsupported layout.");
        return new(vertex with{Fog=fog.Value,SourceMode4Fog=fog.SourceMode4Proof},
            pixel with{Fog=fog.Value,Scattering=scattering?.Values??pixel.Scattering,SourceMode4Fog=fog.SourceMode4Proof});
    }

}
