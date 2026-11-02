using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed partial class UiAssets
{
    private sealed record PreparedNativeSprite(LoadedUiSpriteSource Image,SpriteRectangle Rectangle);
    private readonly Dictionary<SpriteCacheKey,PreparedNativeSprite> _preparedNativeSprites=[];

    private AtlasTexture MaterializeNativeSprite(SpriteCacheKey key,PreparedNativeSprite prepared)
    {
        if(_linearSprites.TryGetValue(key,out var existing))return existing;
        var rectangle=prepared.Rectangle;
        var result=CreateAtlas(Portraits.RequireTexture(prepared.Image),
            new(rectangle.X,rectangle.Y,rectangle.Width,rectangle.Height));
        _linearSprites.Add(key,result);_preparedNativeSprites.Remove(key);
        return result;
    }

    internal sealed partial class PortraitImages
    {
        // This is the existing application image generation. Read-only sprite
        // consumers add original images to its upload/lifetime, not a second
        // registry or a page-owned GPU cache. Named portrait counters retain
        // their existing meaning; these logical sources can share their image.
        private sealed record NativeSpriteSource(LoadedTexture Canonical,LoadedUiSpriteSource Image);
        private readonly Dictionary<(string Hash,int Index),NativeSpriteSource> nativeSpriteImages=[];
        private readonly HashSet<(string Hash,int Index)> unsupportedNativeSprites=[];
        private readonly Dictionary<(string Hash,int Index),LoadedUiSpriteSource> pendingUploads=[];
        internal int NativeSpriteImageCount=>nativeSpriteImages.Count;
        internal long NativeSpriteEncodedSourceBytes {get;private set;}
        internal int PendingUploadCount=>pendingUploads.Count;

        internal bool TryPrepareNativeSprite(GameResourceRepository repository,LoadedSprite sprite,
            out LoadedUiSpriteSource? original)
        {
            lock(sourceGate)
            {
                ObjectDisposedException.ThrowIf(disposed,this);
                var key=(sprite.Texture.DecodedSha256,sprite.Texture.TextureIndex);
                if(nativeSpriteImages.TryGetValue(key,out var retained))
                {
                    original=ReferenceEquals(retained.Canonical,sprite.Texture)?retained.Image:null;
                    return original is not null;
                }
                original=null;
                if(unsupportedNativeSprites.Contains(key)){original=null;return false;}
                if(sprite.Definition.Value64!=(uint)sprite.Texture.TextureIndex)return false;

                // Synthetic/editable CPU images and external portrait files
                // keep their existing pixel contract. This path admits only a
                // real resident ScreenLayout G1T, including normal overrides.
                var reader=repository.CreateReadScope(RecordDependency);
                if(!reader.ContainsRdbAsset("ScreenLayout",sprite.Texture.FileId))return false;
                var source=reader.LoadTextureContainer("ScreenLayout",sprite.Texture.FileId);
                if(source.Source.DecodedSha256!=sprite.Texture.DecodedSha256)return false;
                if(!ReferenceEquals(reader.LoadTexture("ScreenLayout",sprite.Texture.FileId,sprite.Texture.TextureIndex),sprite.Texture))
                    return false;
                if((uint)sprite.Texture.TextureIndex>=(uint)source.Container.Textures.Count)
                    throw new InvalidDataException("UI native sprite image index differs from its source container.");
                var image=source.Container.Textures[sprite.Texture.TextureIndex];
                var sampler=G1TextureSamplerReader.ReadRetainedSource(image);
                if(image.Width!=sprite.Texture.Texture.Width||image.Height!=sprite.Texture.Texture.Height||
                    image.PixelFormat is not(0x00 or 0x01 or 0x59 or 0x5B)||image.TextureSystem!=0||
                    image.MipCount!=1||image.LayerCount!=1||image.Depth!=1||
                    image.RawHeader.Length<20||image.RawHeader[19]==0||sampler.Filter!=0x14||
                    sampler.AddressU!=3||sampler.AddressV!=3||sampler.MinimumLodBits!=0||sampler.MaximumLodBits!=0)
                    return Unsupported();
                // SrgbSprite requests decoded-linear sampling. The original
                // typed sRGB view provides that decode before hardware linear
                // filtering. Repeating/local domains, point and border modes
                // are deliberately retained by their existing implementations.
                _=sprite.Definition.GetRectangle(image.Width,image.Height);
                original=new(sprite.Definition,source,sprite.AtlasSource);
                nativeSpriteImages.Add(key,new(sprite.Texture,original));
                NativeSpriteEncodedSourceBytes=checked(NativeSpriteEncodedSourceBytes+image.EncodedDataRange.Length);
                if(!images.ContainsKey(key))pendingUploads.Add(key,original);
                return true;

                bool Unsupported()
                {
                    unsupportedNativeSprites.Add(key);
                    return false;
                }
            }
        }
    }
}
