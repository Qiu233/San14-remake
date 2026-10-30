using Godot;
using System.Runtime.InteropServices;
using San14.Data;
using San14.Data.Formats;

/// <summary>Shares decoded GPU images within one view, including G1T subtextures.</summary>
internal sealed class UiAssets(GameResourceRepository repository) : IDisposable
{
    private readonly Dictionary<string, LoadedSprite> _sources = [];
    private readonly Dictionary<string, (LoadedSprite Source, AtlasTexture Texture)> _sprites = [];
    private readonly Dictionary<(string Name, LcskUvQuad Uv), AtlasTexture> _parts = [];
    private readonly Dictionary<string, ImageTexture> _textures = [];
    private readonly Dictionary<(string Sprite, string Mask), ImageTexture> _masked = [];
    private readonly List<AtlasTexture> _atlasTextures = [];
    private readonly Dictionary<string, AtlasTexture> _linearSprites = [];
    private readonly Dictionary<string, AtlasTexture> _addressedSprites = [];
    private readonly Dictionary<(string Name, LcskUvQuad Uv), AtlasTexture> _linearParts = [];
    private readonly List<ImageTexture> _linearTextures = [];
    private LocalizedUiSpriteSource? _declaredSprites;
    private readonly Dictionary<(uint File,string Name),LoadedSprite> _localSprites=[];
    private readonly Dictionary<(string Name,string? Pack),LoadedSprite> _commonSprites=[];
    private readonly Dictionary<(string Hash, int Index), SourceSampling> _sampling = [];
    private bool _disposed;
    private readonly Dictionary<uint, LoadedLayout> _layouts = [];
    private readonly Dictionary<uint, UiSourceLayout> _sourceLayouts = [];
    private readonly Dictionary<Vector2, UiSourceLayout> _commandLayouts = [];
    private readonly Dictionary<(string Locale, string Caption), LoadedSprite> _captions = [];
    private sealed record PreparedLinearPixels(int Width, int Height, byte[] Bytes);
    private readonly Dictionary<string, PreparedLinearPixels> _preparedLinearPixels = [];

    // CPU-only page resources. A worker may own this instance exclusively
    // until it transfers the entire instance to the UI thread.
    internal LoadedLayout Layout(uint file)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_layouts.TryGetValue(file, out var result))
            _layouts.Add(file, result = repository.LoadLayout("ScreenLayout", file));
        return result;
    }

    internal UiSourceLayout SourceLayout(uint file)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_sourceLayouts.TryGetValue(file, out var result))
            _sourceLayouts.Add(file, result = new UiSourceLayout(Layout(file)));
        return result;
    }

    internal UiSourceLayout CommandButtonLayout(Vector2 size)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_commandLayouts.TryGetValue(size, out var result))
            _commandLayouts.Add(size, result = SourceLayout(0x16D9E461).ResizedSubtree(0, size, true));
        return result;
    }

    internal void PrepareLayoutArt(LoadedLayout layout, IEnumerable<int> indices, bool commonImports = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var nodes = SourceLayout(layout.Source.FileId).Nodes;
        foreach (int index in indices)
        {
            var art = LcskSpriteNodeReader.Read(layout.Layout, nodes[index]);
            foreach (var group in art.Groups)
            {
                if (group.ComponentCount == 0) continue;
                if (group.ComponentCount != 1)
                    throw new InvalidDataException("UI sprite composition requires its own renderer.");
                var name = group.Parts.Single().SpriteName;
                PrepareLinearSprite(name, LayoutLocalSprite(layout, name, commonImports));
            }
        }
    }

    internal LoadedSprite PrepareCommonCaption(string locale, string caption)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_captions.TryGetValue((locale, caption), out var result))
        {
            result = DeclaredSprites.LoadCommonCaption(locale, caption);
            PrepareLinearSprite(result.Definition.Name, result);
            _captions.Add((locale, caption), result);
        }
        return result;
    }

    private PreparedLinearPixels PrepareLinearSprite(string name, LoadedSprite? declaredSource)
    {
        string key = SpriteKey(name, declaredSource);
        if (_preparedLinearPixels.TryGetValue(key, out var result)) return result;
        var sprite = SourceSprite(name, declaredSource);
        RgbaTexture source = sprite.Texture.Texture;
        SpriteRectangle region = sprite.Definition.GetRectangle(source.Width, source.Height);
        int width = (int)region.Width, height = (int)region.Height;
        SourceSampling? sampler = sprite.Texture.SourceImage is not null ? Sampling(sprite.Texture) : null;
        float[] pixels = UiSrgbPixels.Bordered(source, (int)region.X, (int)region.Y, width, height,
            sampler?.AddressU == 1, sampler?.AddressV == 1);
        result = new(width, height, MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray());
        _preparedLinearPixels.Add(key, result);
        return result;
    }

    // All common/local artwork in a page shares its existing declaration owner.
    // Retire it with the page, just like its decoded sprite and texture caches.
    internal LocalizedUiSpriteSource DeclaredSprites
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _declaredSprites ??= new(repository);
        }
    }

    internal readonly record struct SourceSampling(byte PixelFormat, int MipCount,
        int HeaderLength, bool Srgb, uint Filter, uint AddressU, uint AddressV,
        uint MinimumLodBits, uint MaximumLodBits);

    // Sampler/image metadata is immutable within the same decoded texture
    // generation. Cache scalars here, alongside that view's existing sprites.
    internal SourceSampling Sampling(LoadedTexture texture)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = (texture.DecodedSha256, texture.TextureIndex);
        if (_sampling.TryGetValue(key, out var existing)) return existing;
        var image = texture.SourceImage ?? repository.LoadTextureContainer("ScreenLayout", texture.FileId)
            .Container.Textures[texture.TextureIndex];
        var sampler = G1TextureSamplerReader.ReadRetainedSource(image);
        var result = new SourceSampling(image.PixelFormat, image.MipCount, image.RawHeader.Length,
            image.RawHeader.Length > 19 && image.RawHeader[19] != 0, sampler.Filter,
            sampler.AddressU, sampler.AddressV, sampler.MinimumLodBits, sampler.MaximumLodBits);
        _sampling.Add(key, result);
        return result;
    }

    // CPU source ownership is separate from GPU image creation. An sRGB-only
    // consumer never needs the additional full encoded atlas uploaded by Sprite.
    internal LoadedSprite SourceSprite(string name, LoadedSprite? declaredSource = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string key = SpriteKey(name, declaredSource);
        if (!_sources.TryGetValue(key, out var source))
        {
            source = declaredSource ?? repository.LoadSprite("ScreenLayout", name);
            _ = source.Definition.GetRectangle(source.Texture.Texture.Width, source.Texture.Texture.Height);
            _sources.Add(key, source);
        }
        return source;
    }

    public (LoadedSprite Source, AtlasTexture Texture) Sprite(string name, LoadedSprite? declaredSource = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string spriteKey=SpriteKey(name,declaredSource);
        if (_sprites.TryGetValue(spriteKey, out var existing)) return existing;
        LoadedSprite source = SourceSprite(name, declaredSource);
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
        ValidateUv(name, uv);
        var loaded = Sprite(name,declaredSource);
        var key = (SpriteKey(name,declaredSource), uv);
        if (_parts.TryGetValue(key, out AtlasTexture? existing)) return existing;
        Rect2 region = loaded.Texture.Region;
        AtlasTexture part = CreateAtlas(loaded.Texture.Atlas,
            new Rect2(region.Position + region.Size * new Vector2(uv.TopLeft.U, uv.TopLeft.V),
                region.Size * new Vector2(uv.BottomRight.U - uv.TopLeft.U, uv.BottomRight.V - uv.TopLeft.V)));
        _parts.Add(key, part);
        return part;
    }

    private static void ValidateUv(string name, LcskUvQuad uv)
    {
        const float tolerance = 0.00001f;
        if (Math.Abs(uv.TopLeft.V - uv.TopRight.V) > tolerance ||
            Math.Abs(uv.BottomLeft.V - uv.BottomRight.V) > tolerance ||
            Math.Abs(uv.TopLeft.U - uv.BottomLeft.U) > tolerance ||
            Math.Abs(uv.TopRight.U - uv.BottomRight.U) > tolerance ||
            uv.TopLeft.U < -tolerance || uv.TopLeft.V < -tolerance ||
            uv.BottomRight.U > 1 + tolerance || uv.BottomRight.V > 1 + tolerance ||
            uv.BottomRight.U <= uv.TopLeft.U || uv.BottomRight.V <= uv.TopLeft.V)
            throw new InvalidDataException($"Unsupported local sprite UV for {name}.");
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
        var prepared = PrepareLinearSprite(name, declaredSource);
        int width = prepared.Width, height = prepared.Height;
        using Image image = Image.CreateFromData(width + 2, height + 2, false, Image.Format.Rgbaf, prepared.Bytes);
        ImageTexture texture = ImageTexture.CreateFromImage(image);
        _linearTextures.Add(texture);
        _preparedLinearPixels.Remove(spriteKey);
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
        // Validate the same source UV rectangle without creating an unused
        // encoded GPU atlas, then apply it to the bordered linear atlas.
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateUv(name, uv);
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
        => SrgbSpritePart(name,uv,LayoutLocalSprite(layout,name));

    internal LoadedSprite SourceLayoutSprite(LoadedLayout layout, string name)
        => SourceSprite(name, LayoutLocalSprite(layout, name));

    private LoadedSprite? LayoutLocalSprite(LoadedLayout layout, string name, bool commonImports = false)
    {
        if (_localSprites.TryGetValue((layout.Source.FileId, name), out var declared)) return declared;
        uint reference = layout.Layout.SpriteReferences.Single(r => r.Name == name).Value4;
        if (reference == 0) declared = DeclaredSprites.LoadLayoutLocalSprite(layout, name);
        else if (commonImports && reference == 1) declared = DeclaredSprites.LoadCommonSprite(name);
        else return null;
        _localSprites.Add((layout.Source.FileId, name), declared);
        return declared;
    }

    // Local UV repetition is sampling, not an AtlasTexture crop. Admit it only
    // from an explicit local declaration and its actual mip-zero sampler.
    internal bool TrySrgbLayoutAddressedSprite(LoadedLayout layout,string name,LcskUvQuad uv,
        out AtlasTexture? texture,out SourceSampling sampler)
    {
        texture=null;sampler=default;
        if(layout.Layout.SpriteReferences.Single(r=>r.Name==name).Value4!=0)return false;
        if(!float.IsFinite(uv.TopLeft.U)||!float.IsFinite(uv.TopLeft.V)||
            !float.IsFinite(uv.BottomRight.U)||!float.IsFinite(uv.BottomRight.V)||
            uv.TopLeft.V!=uv.TopRight.V||uv.BottomLeft.V!=uv.BottomRight.V||
            uv.TopLeft.U!=uv.BottomLeft.U||uv.TopRight.U!=uv.BottomRight.U||
            uv.BottomRight.U<=uv.TopLeft.U||uv.BottomRight.V<=uv.TopLeft.V)return false;
        bool outsideU=uv.TopLeft.U<0||uv.BottomRight.U>1;
        bool outsideV=uv.TopLeft.V<0||uv.BottomRight.V>1;
        if(!outsideU&&!outsideV)return false;
        var source=SourceSprite(name,LayoutLocalSprite(layout,name));
        sampler=Sampling(source.Texture);
        if(sampler.Filter!=0x14||sampler.MinimumLodBits!=0||sampler.MaximumLodBits!=0||
            sampler.AddressU is not (1 or 2 or 3)||sampler.AddressV is not (1 or 2 or 3)||
            outsideU&&sampler.AddressU==3||outsideV&&sampler.AddressV==3)return false;
        string key=SpriteKey(name,source);
        if(_addressedSprites.TryGetValue(key,out texture))return true;
        var input=source.Texture.Texture;
        var region=source.Definition.GetRectangle(input.Width,input.Height);
        if(region.X!=(int)region.X||region.Y!=(int)region.Y||
            region.Width!=(int)region.Width||region.Height!=(int)region.Height)
            throw new InvalidDataException($"Addressed local sprite has a fractional texel rectangle: {name}.");
        int left=(int)region.X,top=(int)region.Y,width=(int)region.Width,height=(int)region.Height;
        // Give the declared sprite its own periodic border. An atlas neighbor
        // must never become part of this local repeat/mirror sampling domain.
        byte[] crop=new byte[checked(width*height*4)];
        for(int y=0;y<height;y++)
            input.Pixels.AsSpan(((top+y)*input.Width+left)*4,width*4).CopyTo(crop.AsSpan(y*width*4));
        float[] pixels=UiSrgbPixels.Bordered(new RgbaTexture(width,height,crop),0,0,width,height,
            sampler.AddressU==1,sampler.AddressV==1);
        using Image image=Image.CreateFromData(width+2,height+2,false,Image.Format.Rgbaf,
            MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray());
        var uploaded=ImageTexture.CreateFromImage(image);_linearTextures.Add(uploaded);
        texture=CreateAtlas(uploaded,new(1,1,width,height));_addressedSprites.Add(key,texture);
        return true;
    }

    public AtlasTexture SrgbCommonSpritePart(string name,LcskUvQuad uv,bool expansion=false)
        => DeclaredCommonSpritePart(name,uv,expansion?"texture_pk_common":null);

    public AtlasTexture SrgbTexturePackSpritePart(string texturePack,string name,LcskUvQuad uv)
        => DeclaredCommonSpritePart(name,uv,texturePack);

    private AtlasTexture DeclaredCommonSpritePart(string name,LcskUvQuad uv,string? texturePack)
    {
        if(!_commonSprites.TryGetValue((name,texturePack),out var declared))
        {
            declared=texturePack is null?DeclaredSprites.LoadCommonSprite(name):
                DeclaredSprites.LoadTexturePackSprite(texturePack,name);
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
        _sources.Clear();
        _layouts.Clear(); _sourceLayouts.Clear(); _commandLayouts.Clear(); _captions.Clear();
        _preparedLinearPixels.Clear();
        _sampling.Clear();
        _parts.Clear();
        _linearSprites.Clear();
        _addressedSprites.Clear();
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
