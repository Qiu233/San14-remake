using San14.Data.Formats;
namespace San14.Data;

/// <summary>The actual declared sprite and original G1 image, without a decoded
/// RGBA allocation. The repository owns its immutable encoded source bytes.</summary>
public sealed record LoadedUiSpriteSource(SpriteDefinition Definition,LoadedTextureContainer Resource,LoadedRdbAsset AtlasSource)
{
    public int ImageIndex=>checked((int)Definition.Value64);
    public G1TextureImage Image=>Resource.Container.Textures[ImageIndex];
}
