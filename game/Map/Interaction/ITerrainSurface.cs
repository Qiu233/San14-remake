using Godot;
using San14.Data;

/// <summary>Surface sampling and its source scope, independent of the territory overlay.</summary>
public interface ITerrainSurface
{
    LoadedRdbAsset ProjectionSource { get; }
    ShaderMaterial Material { get; }
    string SamplingCode { get; }
    string VertexInitializationCode { get; }
    string FragmentInitializationCode { get; }
    void BindSamplingParameters(ShaderMaterial target);
}
