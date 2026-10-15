using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>
/// Supplies verified G1T mip-major BC3 layers to Godot from the installed source.
/// Godot's high-level array may decode them; this does not establish GPU storage
/// format or the original shader binding.
/// </summary>
public sealed class TerrainArrayTexture : IDisposable
{
    public Texture2DArray Texture { get; }
    public TerrainTextureBinding Binding { get; }
    private bool _disposed;

    private TerrainArrayTexture(Texture2DArray texture, TerrainTextureBinding binding)
        => (Texture, Binding) = (texture, binding);

    public static TerrainArrayTexture Build(LoadedTerrainTextureGroup group, uint slot)
    {
        ArgumentNullException.ThrowIfNull(group);
        TerrainTextureBinding binding = TerrainTexturePreparation.Select(group, slot);
        LoadedRdbAsset source = binding.Resource.Source;
        G1TextureImage image = binding.Image;
        string context = $"{source.ResourceUri} image {binding.TextureIndex} slot {slot}";
        if (image.SubresourceLayout != G1TextureSubresourceLayout.MipMajorArray ||
            image.PixelFormat != 0x5B || image.LayerCount < 1 || image.Width < 1 || image.Height < 1)
            throw new InvalidDataException($"{context}: no verified BC3 array upload profile.");
        var prepared = TerrainTexturePreparation.PrepareCompatibility(binding);

        Texture2DArray? texture = null;
        var layers = new Godot.Collections.Array<Image>();
        try
        {
            for (int layer = 0; layer < image.LayerCount; layer++)
            {
                byte[] encoded = prepared.Layers[layer].ToArray();
                Image sourceLayer = Image.CreateFromData(image.Width, image.Height, false,
                    Image.Format.Dxt5, encoded);
                if (sourceLayer is null || sourceLayer.IsEmpty())
                    throw new InvalidDataException($"{context}: native source layer {layer} is empty.");
                layers.Add(sourceLayer);
            }
            texture = new Texture2DArray
            {
                ResourceName = $"TerrainSourceArray_{source.FileId:X8}_Image{binding.TextureIndex}"
            };
            Error result = texture.CreateFromImages(layers);
            if (result != Error.Ok || texture.GetLayers() != image.LayerCount ||
                texture.GetWidth() != image.Width || texture.GetHeight() != image.Height)
                throw new InvalidDataException($"{context}: native BC3 array upload failed ({result}).");
            texture.SetMeta("table_resource_uri", group.TableSource.ResourceUri);
            texture.SetMeta("table_decoded_sha256", group.TableSource.DecodedSha256);
            texture.SetMeta("slot", slot);
            texture.SetMeta("proxy_object_id", binding.ProxyObject.ObjectId);
            texture.SetMeta("source_resource_uri", source.ResourceUri);
            texture.SetMeta("source_decoded_sha256", source.DecodedSha256);
            texture.SetMeta("source_is_override", source.OverrideSource is not null ||
                group.TableSource.OverrideSource is not null);
            texture.SetMeta("image_index", binding.TextureIndex);
            texture.SetMeta("source_mip_count", image.MipCount);
            texture.SetMeta("uploaded_mip_count", 1);
            texture.SetMeta("upload_input_format", (int)Image.Format.Dxt5);
            texture.SetMeta("native_storage_format_verified", false);
            texture.SetMeta("original_shader_binding_verified", false);
            return new(texture, binding);
        }
        catch
        {
            texture?.Dispose();
            throw;
        }
        finally
        {
            foreach (Image layer in layers) layer.Dispose();
            layers.Clear();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Texture.Dispose();
        _disposed = true;
    }
}
