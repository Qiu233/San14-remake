using System.Numerics;

namespace San14.Data;

public readonly record struct SkyCloudPatternDirections(Vector4 Direction0, Vector4 Direction1,
    Vector4 Direction2, Vector4 Direction3);

public sealed record SkyCloudPatternVertexMaterial(Vector4 ClipPlane, Matrix4x4 ProjectionToWorld,
    SkyCloudPatternDirections CameraDirections, Vector2 ProjectionFarNear);

public sealed record SkyCloudPatternPixelMaterial(Vector4 Eye,
    Vector3 BaseTextureOffset, Vector3 BaseTextureScale, Vector3 DetailTextureOffset, Vector3 DetailTextureScale,
    Vector2 DistortTextureOffset, Vector2 DistortTextureScale, Vector4 RaySampleCount, Vector4 EarthRadius,
    Vector4 CloudParameters0, Vector4 CloudParameters1, Vector4 DistortionScale, Vector2 ErosionScale);

/// <summary>Named values consumed by the cloud-mask operation. This material
/// contains no device buffer, selected option, animation clock or source template.</summary>
public sealed record SkyCloudPatternMaterial(SkyCloudPatternVertexMaterial Vertex, SkyCloudPatternPixelMaterial Pixel)
{
    internal bool ValidateWork(uint maximumRaySamples, bool usesIntegration)
    {
        if (maximumRaySamples is < 1 or > 4096)
            throw new InvalidDataException("Cloud Pattern requires an explicit finite shader work budget.");
        bool enabled = usesIntegration && Pixel.CloudParameters1.X < 1 && Pixel.CloudParameters1.W > 0;
        if (enabled)
        {
            var count = Pixel.RaySampleCount;
            if (count.Y <= 0 || count.W <= 0 || (double)count.Y + Math.Min(0d, count.X) <= 0 ||
                Math.Ceiling((double)count.Y) > maximumRaySamples)
                throw new InvalidDataException("Cloud Pattern integration exceeds its work limit or supported finite sampling domain.");
        }
        return enabled;
    }
}
