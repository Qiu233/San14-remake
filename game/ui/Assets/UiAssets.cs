using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Shares decoded GPU images within one view, including G1T subtextures.</summary>
internal sealed class UiAssets(GameResourceRepository repository) : IDisposable
{
    private readonly Dictionary<string, (LoadedSprite Source, AtlasTexture Texture)> _sprites = [];
    private readonly Dictionary<(string Name, LcskUvQuad Uv), AtlasTexture> _parts = [];
    private readonly Dictionary<string, ImageTexture> _textures = [];
    private readonly List<AtlasTexture> _atlasTextures = [];
    private bool _disposed;

    public (LoadedSprite Source, AtlasTexture Texture) Sprite(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_sprites.TryGetValue(name, out var existing)) return existing;
        LoadedSprite source = repository.LoadSprite("ScreenLayout", name);
        string key = $"{source.Texture.DecodedSha256}/{source.Texture.TextureIndex}";
        if (!_textures.TryGetValue(key, out ImageTexture? texture))
        {
            RgbaTexture pixels = source.Texture.Texture;
            using Image image = Image.CreateFromData(pixels.Width, pixels.Height,
                false, Image.Format.Rgba8, pixels.Pixels);
            texture = ImageTexture.CreateFromImage(image);
            _textures.Add(key, texture);
        }
        SpriteRectangle region = source.Definition.GetRectangle(
            source.Texture.Texture.Width, source.Texture.Texture.Height);
        AtlasTexture atlas = CreateAtlas(texture,
            new Rect2(region.X, region.Y, region.Width, region.Height));
        var result = (source, atlas);
        _sprites.Add(name, result);
        return result;
    }

    public AtlasTexture SpritePart(string name, LcskUvQuad uv)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var loaded = Sprite(name);
        const float tolerance = 0.00001f;
        if (Math.Abs(uv.TopLeft.V - uv.TopRight.V) > tolerance ||
            Math.Abs(uv.BottomLeft.V - uv.BottomRight.V) > tolerance ||
            Math.Abs(uv.TopLeft.U - uv.BottomLeft.U) > tolerance ||
            Math.Abs(uv.TopRight.U - uv.BottomRight.U) > tolerance ||
            uv.TopLeft.U < -tolerance || uv.TopLeft.V < -tolerance ||
            uv.BottomRight.U > 1 + tolerance || uv.BottomRight.V > 1 + tolerance ||
            uv.BottomRight.U <= uv.TopLeft.U || uv.BottomRight.V <= uv.TopLeft.V)
            throw new InvalidDataException($"Unsupported local sprite UV for {name}.");
        var key = (name, uv);
        if (_parts.TryGetValue(key, out AtlasTexture? existing)) return existing;
        Rect2 region = loaded.Texture.Region;
        AtlasTexture part = CreateAtlas(loaded.Texture.Atlas,
            new Rect2(region.Position + region.Size * new Vector2(uv.TopLeft.U, uv.TopLeft.V),
                region.Size * new Vector2(uv.BottomRight.U - uv.TopLeft.U, uv.BottomRight.V - uv.TopLeft.V)));
        _parts.Add(key, part);
        return part;
    }

    private AtlasTexture CreateAtlas(Texture2D texture, Rect2 region)
    {
        var atlas = new AtlasTexture();
        // Track the resource before applying properties, so a later failed setup
        // is still covered by the owning view's cleanup.
        _atlasTextures.Add(atlas);
        atlas.Atlas = texture;
        atlas.Region = region;
        atlas.FilterClip = true;
        return atlas;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (AtlasTexture atlas in _atlasTextures) atlas.Dispose();
        _atlasTextures.Clear();
        _sprites.Clear();
        _parts.Clear();
        foreach (ImageTexture texture in _textures.Values) texture.Dispose();
        _textures.Clear();
    }
}
