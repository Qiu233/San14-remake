using San14.Data;
using San14.Data.Formats;

internal sealed partial class UiAssets
{
    private readonly Dictionary<(SpriteCacheKey Name, LcskUvQuad Uv),
        (string Name, LoadedSprite? Declaration)> _preparedLinearParts = [];

    internal void PrepareSrgbLayoutSpritePart(LoadedLayout layout, string name, LcskUvQuad uv)
        => PrepareSpritePart(name, uv, LayoutLocalSprite(layout, name));

    internal void PrepareSrgbTexturePackSpritePart(string pack, string name, LcskUvQuad uv)
    {
        PrepareCommonSprite(name, pack);
        PrepareSpritePart(name, uv, _commonSprites[(name, pack)]);
    }

    private void PrepareSpritePart(string name, LcskUvQuad uv, LoadedSprite? declaration)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateUv(name, uv);
        PrepareLinearSprite(name, declaration);
        var key = (SpriteKey(name, declaration), uv);
        if (!_linearParts.ContainsKey(key)) _preparedLinearParts.TryAdd(key, (name, declaration));
    }

    // Metadata and source pixels are prepared by the sole startup worker.
    // AtlasTexture views are created by the existing UI-thread materializer.
    private void MaterializePreparedSpriteParts()
    {
        foreach (var entry in _preparedLinearParts)
            _ = SrgbSpritePart(entry.Value.Name, entry.Key.Uv, entry.Value.Declaration);
        _preparedLinearParts.Clear();
    }
}
