using Godot;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using San14.Data;
using San14.Data.Formats;

/// <summary>Shares decoded GPU images within one view, including G1T subtextures.</summary>
internal sealed partial class UiAssets(GameResourceRepository repository, UiAssets.PortraitImages? sharedPortraits = null) : IDisposable
{
    internal PortraitImages Portraits {get;}=(sharedPortraits??new PortraitImages()).Retain();
    private readonly bool useSharedNativeSprites=sharedPortraits is not null;
    private readonly Dictionary<SpriteCacheKey, LoadedSprite> _sources = [];
    private readonly Dictionary<SpriteCacheKey, (LoadedSprite Source, AtlasTexture Texture)> _sprites = [];
    private readonly Dictionary<(SpriteCacheKey Name, LcskUvQuad Uv), AtlasTexture> _parts = [];
    private readonly Dictionary<LoadedTexture, ImageTexture> _textures = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(string Sprite, string Mask), ImageTexture> _masked = [];
    private readonly List<AtlasTexture> _atlasTextures = [];
    private readonly Dictionary<SpriteCacheKey, AtlasTexture> _linearSprites = [];
    private readonly Dictionary<SpriteCacheKey, AtlasTexture> _addressedSprites = [];
    private readonly Dictionary<(SpriteCacheKey Name, LcskUvQuad Uv), AtlasTexture> _linearParts = [];
    private readonly List<ImageTexture> _linearTextures = [];
    private LocalizedUiSpriteSource? _declaredSprites;
    private readonly Dictionary<(uint File,string Name),LoadedSprite> _localSprites=[];
    private readonly Dictionary<(string Name,string? Pack),LoadedSprite> _commonSprites=[];
    private readonly Dictionary<(string Hash, int Index), SourceSampling> _sampling = [];
    private bool _disposed;
    private readonly Dictionary<uint, LoadedLayout> _layouts = [];
    private readonly Dictionary<uint, UiSourceLayout> _sourceLayouts = [];
    private readonly Dictionary<(uint File, string Names), IReadOnlyDictionary<string, LcskAnimationClip>> _animationClips = [];
    private readonly Dictionary<string, uint> _namedLayouts = new(StringComparer.Ordinal);
    private readonly Dictionary<Vector2, UiSourceLayout> _commandLayouts = [];
    private readonly Dictionary<LcskDocument,Dictionary<string,UiSourceButtonStateProfile>> _buttonStateProfiles = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(string Locale, string Caption), LoadedSprite> _captions = [];
    private sealed record PreparedLinearPixels(int Width, int Height, byte[] Bytes);
    private readonly Dictionary<SpriteCacheKey, PreparedLinearPixels> _preparedLinearPixels = [];
    private readonly Dictionary<SpriteCacheKey, (string Name, LoadedSprite? Declaration)> _preparedEncodedSprites = [];

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

    // The same prepared source curves serve every instance of a map button.
    // This cache follows the existing resource owner and its generation.
    internal IReadOnlyDictionary<string, LcskAnimationClip> SourceAnimationClips(UiSourceLayout source,
        IReadOnlySet<string> names)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = (source.Source.Source.FileId, string.Join("\0", names.Order(StringComparer.Ordinal)));
        if (!_animationClips.TryGetValue(key, out var clips))
            _animationClips.Add(key, clips = LcskAnimationReader.Read(source.Source.Layout, names)
                .ToDictionary(clip => clip.Name, StringComparer.Ordinal));
        return clips;
    }

    internal UiSourceLayout SourceLayout(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_namedLayouts.TryGetValue(name, out uint file))
        {
            var layout = MapMarkerLayout.LoadNamed(repository, name);
            file = layout.Source.FileId; _namedLayouts.Add(name, file);
            _layouts.TryAdd(file, layout);
        }
        return SourceLayout(file);
    }

    internal void PrepareCommonSprite(string name, string? texturePack = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_commonSprites.TryGetValue((name, texturePack), out var source))
        {
            source = texturePack is null ? DeclaredSprites.LoadCommonSprite(name) :
                DeclaredSprites.LoadTexturePackSprite(texturePack, name);
            _commonSprites.Add((name, texturePack), source);
        }
        PrepareLinearSprite(name, source);
    }

    internal UiSourceButtonStateProfile ButtonStateProfile(LoadedLayout layout,IReadOnlyList<string>? actionNames=null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(!_buttonStateProfiles.TryGetValue(layout.Layout,out var profiles))
            _buttonStateProfiles.Add(layout.Layout,profiles=new(StringComparer.Ordinal));
        string key=actionNames is null?"":string.Join('\0',actionNames);
        if(!profiles.TryGetValue(key,out var profile))
            profiles.Add(key,profile=new UiSourceButtonStateProfile(layout,actionNames));
        return profile;
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

    // CPU-only preparation follows the actual component SRV. Encoded portrait
    // components borrow the decoded source; linear components retain their
    // converted pixels. Main creates both image kinds before title input.
    internal void PrepareSprite(LoadedSprite source, bool srgb)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string name = source.Definition.Name;
        if (srgb) PrepareLinearSprite(name, source);
        else
        {
            _ = SourceSprite(name, source);
            _preparedEncodedSprites.TryAdd(SpriteKey(name, source), (name, source));
        }
    }

    // This overload follows the same unqualified lookup as Sprite(name) and
    // SrgbSprite(name). Keep its identity separate from layout/pack-qualified
    // declarations; equal names in different banks are not interchangeable.
    internal void PrepareSprite(string name, bool srgb)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (srgb) PrepareLinearSprite(name, null);
        else
        {
            _ = SourceSprite(name);
            _preparedEncodedSprites.TryAdd(SpriteKey(name, null), (name, null));
        }
    }

    private void PrepareLinearSprite(string name, LoadedSprite? declaredSource)
    {
        var key = SpriteKey(name, declaredSource);
        if (_linearSprites.ContainsKey(key) || _preparedLinearPixels.ContainsKey(key) || _preparedNativeSprites.ContainsKey(key)) return;
        var sprite = SourceSprite(name, declaredSource);
        if (useSharedNativeSprites && Portraits.TryPrepareNativeSprite(repository, sprite, out var native))
        {
            _preparedNativeSprites.Add(key, new(native!, sprite.Definition.GetRectangle(native!.Image.Width, native.Image.Height)));
            return;
        }
        RgbaTexture source = sprite.Texture.Texture;
        SpriteRectangle region = sprite.Definition.GetRectangle(source.Width, source.Height);
        int width = (int)region.Width, height = (int)region.Height;
        SourceSampling? sampler = sprite.Texture.SourceImage is not null ? Sampling(sprite.Texture) : null;
        float[] pixels = UiSrgbPixels.Bordered(source, (int)region.X, (int)region.Y, width, height,
            sampler?.AddressU == 1, sampler?.AddressV == 1);
        _preparedLinearPixels.Add(key, new(width, height, MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray()));
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
        var key = SpriteKey(name, declaredSource);
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
        var spriteKey=SpriteKey(name,declaredSource);
        if (_sprites.TryGetValue(spriteKey, out var existing)) return existing;
        LoadedSprite source = SourceSprite(name, declaredSource);
        var key = source.Texture;
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
        var spriteKey=SpriteKey(name,declaredSource);
        if (_linearSprites.TryGetValue(spriteKey, out AtlasTexture? existing)) return existing;
        PrepareLinearSprite(name, declaredSource);
        if (_preparedNativeSprites.TryGetValue(spriteKey, out var native))
        {
            Portraits.MaterializeAsync().GetAwaiter().GetResult();
            return MaterializeNativeSprite(spriteKey, native);
        }
        return MaterializeLinearSprite(spriteKey, _preparedLinearPixels[spriteKey]);
    }

    // Called on Main after the CPU preparation has transferred this owner.
    // Preserve the exact qualified keys selected by the ordinary declaration
    // resolver; no extra atlas lookup or fabricated scene is required.
    internal void MaterializePreparedTextures()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if(Portraits.IsPrepared)Portraits.MaterializeAsync().GetAwaiter().GetResult();
        foreach (var prepared in _preparedNativeSprites.ToArray())
            _ = MaterializeNativeSprite(prepared.Key, prepared.Value);
        foreach (var prepared in _preparedLinearPixels.ToArray())
            _ = MaterializeLinearSprite(prepared.Key, prepared.Value);
        foreach (var prepared in _preparedEncodedSprites.Values)
            _ = Sprite(prepared.Name, prepared.Declaration);
        _preparedEncodedSprites.Clear();
        MaterializePreparedSpriteParts();
    }

    private AtlasTexture MaterializeLinearSprite(SpriteCacheKey spriteKey, PreparedLinearPixels prepared)
    {
        if (_linearSprites.TryGetValue(spriteKey, out var existing)) return existing;
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
        var spriteKey=SpriteKey(name,declaredSource);
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

    // Multi-component gauges consume one real layout sprite through the same
    // nullable declaration path. Keep its key identical to the later draw.
    internal void PrepareSrgbLayoutSprite(LoadedLayout layout, string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PrepareLinearSprite(name, LayoutLocalSprite(layout, name));
    }

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
        var key=SpriteKey(name,source);
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

    internal AtlasTexture PortraitPart(LoadedUiSpriteSource source,LcskUvQuad uv)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);ValidateUv(source.Definition.Name,uv);
        var key=SpriteKey("portrait:"+source.Resource.Source.DecodedSha256+"/"+source.ImageIndex+"/"+source.Definition.Name,null);
        if(_parts.TryGetValue((key,uv),out var cached))return cached;
        var image=Portraits.RequireTexture(source);var rectangle=source.Definition.GetRectangle(source.Image.Width,source.Image.Height);
        var region=new Rect2(rectangle.X,rectangle.Y,rectangle.Width,rectangle.Height);
        var atlas=CreateAtlas(image,new(region.Position+region.Size*new Vector2(uv.TopLeft.U,uv.TopLeft.V),
            region.Size*new Vector2(uv.BottomRight.U-uv.TopLeft.U,uv.BottomRight.V-uv.TopLeft.V)));
        _parts.Add((key,uv),atlas);return atlas;
    }

    // A qualified sprite includes its actual CPU source object, not only its
    // content label. Editable clones can retain the original metadata hash.
    // Unqualified lookup remains a separate key with no declared source.
    private readonly struct SpriteCacheKey : IEquatable<SpriteCacheKey>
    {
        private readonly string name;
        private readonly LoadedTexture? texture;
        internal SpriteCacheKey(string name,LoadedTexture? texture)=>(this.name,this.texture)=(name,texture);
        public bool Equals(SpriteCacheKey other)=>name==other.name&&ReferenceEquals(texture,other.texture);
        public override bool Equals(object? value)=>value is SpriteCacheKey other&&Equals(other);
        public override int GetHashCode()=>HashCode.Combine(name,texture is null?0:RuntimeHelpers.GetHashCode(texture));
    }
    private static SpriteCacheKey SpriteKey(string name,LoadedSprite? source)=>new(name,source?.Texture);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (AtlasTexture atlas in _atlasTextures) atlas.Dispose();
        _atlasTextures.Clear();
        _sprites.Clear();
        _sources.Clear();
        _layouts.Clear(); _sourceLayouts.Clear(); _animationClips.Clear(); _namedLayouts.Clear(); _commandLayouts.Clear(); _captions.Clear();
        _buttonStateProfiles.Clear();
        _preparedLinearPixels.Clear();
        _preparedNativeSprites.Clear();
        _preparedEncodedSprites.Clear();
        _preparedLinearParts.Clear();
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
        Portraits.Release();
    }
}
