using Godot;
using System.Runtime.InteropServices;
using San14.Data;
using San14.Data.Formats;

/// <summary>Shares decoded GPU images within one view, including G1T subtextures.</summary>
internal sealed class UiAssets(GameResourceRepository repository) : IDisposable
{
    private readonly Dictionary<string, (LoadedSprite Source, AtlasTexture Texture)> _sprites = [];
    private readonly Dictionary<(string Name, LcskUvQuad Uv), AtlasTexture> _parts = [];
    private readonly Dictionary<string, ImageTexture> _textures = [];
    private readonly Dictionary<(string Sprite, string Mask), ImageTexture> _masked = [];
    private readonly List<AtlasTexture> _atlasTextures = [];
    private readonly Dictionary<string, AtlasTexture> _linearSprites = [];
    private readonly Dictionary<(string Name, LcskUvQuad Uv), AtlasTexture> _linearParts = [];
    private readonly List<ImageTexture> _linearTextures = [];
    private LocalizedUiSpriteSource? _declaredSprites;
    private readonly Dictionary<(uint File,string Name),LoadedSprite> _localSprites=[];
    private readonly Dictionary<(string Name,string? Pack),LoadedSprite> _commonSprites=[];
    private bool _disposed;

    public (LoadedSprite Source, AtlasTexture Texture) Sprite(string name, LoadedSprite? declaredSource = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string spriteKey=SpriteKey(name,declaredSource);
        if (_sprites.TryGetValue(spriteKey, out var existing)) return existing;
        LoadedSprite source = declaredSource ?? repository.LoadSprite("ScreenLayout", name);
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
        _sprites.Add(spriteKey, result);
        return result;
    }

    public AtlasTexture SpritePart(string name, LcskUvQuad uv, LoadedSprite? declaredSource = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var loaded = Sprite(name,declaredSource);
        const float tolerance = 0.00001f;
        if (Math.Abs(uv.TopLeft.V - uv.TopRight.V) > tolerance ||
            Math.Abs(uv.BottomLeft.V - uv.BottomRight.V) > tolerance ||
            Math.Abs(uv.TopLeft.U - uv.BottomLeft.U) > tolerance ||
            Math.Abs(uv.TopRight.U - uv.BottomRight.U) > tolerance ||
            uv.TopLeft.U < -tolerance || uv.TopLeft.V < -tolerance ||
            uv.BottomRight.U > 1 + tolerance || uv.BottomRight.V > 1 + tolerance ||
            uv.BottomRight.U <= uv.TopLeft.U || uv.BottomRight.V <= uv.TopLeft.V)
            throw new InvalidDataException($"Unsupported local sprite UV for {name}.");
        var key = (SpriteKey(name,declaredSource), uv);
        if (_parts.TryGetValue(key, out AtlasTexture? existing)) return existing;
        Rect2 region = loaded.Texture.Region;
        AtlasTexture part = CreateAtlas(loaded.Texture.Atlas,
            new Rect2(region.Position + region.Size * new Vector2(uv.TopLeft.U, uv.TopLeft.V),
                region.Size * new Vector2(uv.BottomRight.U - uv.TopLeft.U, uv.BottomRight.V - uv.TopLeft.V)));
        _parts.Add(key, part);
        return part;
    }

    public ImageTexture MaskedSprite(string name, string maskName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = (name, maskName);
        if (_masked.TryGetValue(key, out ImageTexture? existing)) return existing;
        using Image portrait = Sprite(name).Texture.GetImage();
        using Image mask = Sprite(maskName).Texture.GetImage();
        mask.Resize(portrait.GetWidth(), portrait.GetHeight(), Image.Interpolation.Bilinear);
        byte[] pixels = portrait.GetData(), maskPixels = mask.GetData();
        for (int i = 3; i < pixels.Length; i += 4)
            pixels[i] = (byte)((pixels[i] * maskPixels[i] + 127) / 255);
        using Image masked = Image.CreateFromData(portrait.GetWidth(), portrait.GetHeight(),
            false, Image.Format.Rgba8, pixels);
        ImageTexture texture = ImageTexture.CreateFromImage(masked);
        _masked.Add(key, texture);
        return texture;
    }

    // For source sprites whose actual SRV is sRGB. Decode each texel before
    // filtering, as the original sampler does. A one-texel atlas border keeps
    // mip-zero bilinear samples at the sprite edges identical to its atlas.
    public AtlasTexture SrgbSprite(string name, LoadedSprite? declaredSource = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string spriteKey=SpriteKey(name,declaredSource);
        if (_linearSprites.TryGetValue(spriteKey, out AtlasTexture? existing)) return existing;
        var sprite = Sprite(name, declaredSource);
        RgbaTexture source = sprite.Source.Texture.Texture;
        Rect2 region = sprite.Texture.Region;
        int width = (int)region.Size.X, height = (int)region.Size.Y;
        int left = (int)region.Position.X, top = (int)region.Position.Y;
        float[] pixels = new float[(width + 2) * (height + 2) * 4];
        for (int y = 0; y < height + 2; y++)
            for (int x = 0; x < width + 2; x++)
            {
                int input = (Math.Clamp(top + y - 1, 0, source.Height - 1) * source.Width +
                    Math.Clamp(left + x - 1, 0, source.Width - 1)) * 4;
                int output = (y * (width + 2) + x) * 4;
                for (int channel = 0; channel < 3; channel++)
                {
                    float value = source.Pixels[input + channel] / 255f;
                    pixels[output + channel] = value <= .04045f ? value / 12.92f :
                        MathF.Pow((value + .055f) / 1.055f, 2.4f);
                }
                pixels[output + 3] = source.Pixels[input + 3] / 255f;
            }
        using Image image = Image.CreateFromData(width + 2, height + 2, false, Image.Format.Rgbaf,
            MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray());
        ImageTexture texture = ImageTexture.CreateFromImage(image);
        _linearTextures.Add(texture);
        AtlasTexture result = CreateAtlas(texture, new(1, 1, width, height));
        _linearSprites.Add(spriteKey, result);
        return result;
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

    public AtlasTexture SrgbSpritePart(string name, LcskUvQuad uv, LoadedSprite? declaredSource = null)
    {
        // Reuse the validated source UV rectangle, then apply it to the
        // decoded linear atlas, preserving its one-texel filtering border.
        SpritePart(name, uv,declaredSource);
        string spriteKey=SpriteKey(name,declaredSource);
        if (_linearParts.TryGetValue((spriteKey, uv), out var cached)) return cached;
        var sprite = SrgbSprite(name,declaredSource);
        Rect2 region = sprite.Region;
        var result = CreateAtlas(sprite.Atlas, new Rect2(
            region.Position + region.Size * new Vector2(uv.TopLeft.U, uv.TopLeft.V),
            region.Size * new Vector2(uv.BottomRight.U - uv.TopLeft.U, uv.BottomRight.V - uv.TopLeft.V)));
        _linearParts.Add((spriteKey, uv), result);
        return result;
    }

    public AtlasTexture SrgbLayoutSpritePart(LoadedLayout layout,string name,LcskUvQuad uv)
    {
        LoadedSprite? declared=null;
        if(layout.Layout.SpriteReferences.Single(r=>r.Name==name).Value4==0)
        {
            if(!_localSprites.TryGetValue((layout.Source.FileId,name),out declared))
            {
                _declaredSprites??=new(repository);
                declared=_declaredSprites.LoadLayoutLocalSprite(layout,name);
                _localSprites.Add((layout.Source.FileId,name),declared);
            }
        }
        return SrgbSpritePart(name,uv,declared);
    }

    public AtlasTexture SrgbCommonSpritePart(string name,LcskUvQuad uv,bool expansion=false)
        => DeclaredCommonSpritePart(name,uv,expansion?"texture_pk_common":null);

    public AtlasTexture SrgbTexturePackSpritePart(string texturePack,string name,LcskUvQuad uv)
        => DeclaredCommonSpritePart(name,uv,texturePack);

    private AtlasTexture DeclaredCommonSpritePart(string name,LcskUvQuad uv,string? texturePack)
    {
        if(!_commonSprites.TryGetValue((name,texturePack),out var declared))
        {
            _declaredSprites??=new(repository);
            declared=texturePack is null?_declaredSprites.LoadCommonSprite(name):
                _declaredSprites.LoadTexturePackSprite(texturePack,name);
            _commonSprites.Add((name,texturePack),declared);
        }
        return SrgbSpritePart(name,uv,declared);
    }

    private static string SpriteKey(string name,LoadedSprite? source)=>source is null?name:
        $"{source.Texture.DecodedSha256}/{source.Texture.TextureIndex}/{name}";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (AtlasTexture atlas in _atlasTextures) atlas.Dispose();
        _atlasTextures.Clear();
        _sprites.Clear();
        _parts.Clear();
        _linearSprites.Clear();
        _linearParts.Clear();
        _localSprites.Clear();_commonSprites.Clear();_declaredSprites=null;
        foreach (ImageTexture texture in _linearTextures) texture.Dispose();
        _linearTextures.Clear();
        foreach (ImageTexture texture in _masked.Values) texture.Dispose();
        _masked.Clear();
        foreach (ImageTexture texture in _textures.Values) texture.Dispose();
        _textures.Clear();
    }
}
