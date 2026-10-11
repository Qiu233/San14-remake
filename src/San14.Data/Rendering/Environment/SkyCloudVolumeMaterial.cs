using System.Numerics;

namespace San14.Data;

/// <summary>The cloud volume after one weather and animation update. Cameras,
/// atmosphere radius and lighting belong to the consuming draw operation.</summary>
public sealed record SkyCloudVolumeMaterial(
    Vector3 BaseTextureOffset, Vector3 BaseTextureScale,
    Vector3 DetailTextureOffset, Vector3 DetailTextureScale,
    Vector2 DistortTextureOffset, Vector2 DistortTextureScale,
    Vector2 RaySampleSlopeAndMaximum, Vector4 LayerGeometry, Vector4 DensityParameters,
    Vector4 DistortionScale, Vector2 ErosionScale, float CirrusAltitude)
{
    public SkyCloudPatternPixelMaterial PreparePixel(Vector4 eye, float verticalFieldOfView, float planetRadius)
    {
        float bottomRadius = NativeFloat.Add(LayerGeometry.X, planetRadius);
        var earthRadius = new Vector4(planetRadius, bottomRadius,
            NativeFloat.Add(bottomRadius, LayerGeometry.Y), NativeFloat.Add(CirrusAltitude, planetRadius));
        return new(eye, BaseTextureOffset, BaseTextureScale, DetailTextureOffset, DetailTextureScale,
            DistortTextureOffset, DistortTextureScale,
            new(RaySampleSlopeAndMaximum, 0, verticalFieldOfView), earthRadius,
            LayerGeometry, DensityParameters, DistortionScale, ErosionScale);
    }
}
