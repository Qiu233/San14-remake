using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>
/// A source LCSK nine-part panel shown at fixed screen size next to a map flag.
/// The sprite parts and UVs are original; placement in the playable map is a
/// screenshot-based client preview until the runtime layout selector is known.
/// </summary>
internal sealed class MapMarkerPanel : IDisposable
{
    private sealed record Part(int Group, Texture2D Texture, string SpriteName,
        string AtlasUri, string AtlasSha256);

    private readonly ImageTexture _texture;
    private readonly string _spriteName;
    private readonly string _atlasUri;
    private readonly string _atlasSha256;

    // Shared by the 3D preview and the native-map Canvas overlay. The panel
    // remains the owner; callers reuse its one composed source texture.
    internal Texture2D Texture => _texture;

    private MapMarkerPanel(ImageTexture texture, Part source)
        => (_texture, _spriteName, _atlasUri, _atlasSha256) =
            (texture, source.SpriteName, source.AtlasUri, source.AtlasSha256);

    public static MapMarkerPanel Resolve(LoadedLayout layout, UiAssets assets,
        string nodeName, string spriteName, Vector2 sourceSize, Vector2I targetSize)
    {
        if (layout.Source.ResourceUri != "san14://rdb/ScreenLayout/EDC35713")
            throw new InvalidDataException("Map marker panel received a different source layout.");
        LcskNodeRecord node = LcskNodeReader.Read(layout.Layout).Single(item => item.Name == nodeName);
        if (node.Type != 3) throw new InvalidDataException($"Map marker panel {nodeName} is not nine-part.");
        LcskSpriteNode source = LcskSpriteNodeReader.Read(layout.Layout, node);
        if (source.RawTintBytes.Any(value => value != 255))
            throw new InvalidDataException($"Map marker panel {nodeName} has an unknown source tint.");
        var parts = new List<Part>();
        foreach (LcskSpriteNodeGroup group in source.Groups)
        {
            if (group.ComponentCount == 0) continue;
            if (group.ComponentCount != 1 || group.Parts.Count != 1 ||
                group.Parts[0].SpriteName != spriteName)
                throw new InvalidDataException($"Map marker panel {nodeName} has an unknown component.");
            LcskSpriteNodePart part = group.Parts[0];
            AtlasTexture texture = assets.SpritePart(spriteName, part.Uv);
            LoadedSprite loaded = assets.Sprite(spriteName).Source;
            parts.Add(new(group.Index, texture, spriteName,
                loaded.AtlasSource.ResourceUri, loaded.AtlasSource.DecodedSha256));
        }
        if (parts.Count is not (3 or 9) || parts.Count != source.Groups.Count(group => group.ComponentCount != 0))
            throw new InvalidDataException($"Map marker panel {nodeName} has an incomplete source grid.");
        // Source LCSK grids are column-major. UV edges define fixed caps; only
        // their center span is stretched to the target panel dimensions.
        int leftGroup = parts.Count == 3 ? 1 : 0;
        int rightGroup = parts.Count == 3 ? 7 : 6;
        float left = sourceSize.X * source.Groups[leftGroup].Parts[0].Uv.TopRight.U;
        float right = sourceSize.X * (1 - source.Groups[rightGroup].Parts[0].Uv.TopLeft.U);
        float top = parts.Count == 3 ? 0 : sourceSize.Y * source.Groups[0].Parts[0].Uv.BottomLeft.V;
        float bottom = parts.Count == 3 ? 0 : sourceSize.Y *
            (1 - source.Groups[2].Parts[0].Uv.TopLeft.V);
        if (!(left > 0 && right > 0 && top >= 0 && bottom >= 0) ||
            left + right >= sourceSize.X || top + bottom >= sourceSize.Y)
            throw new InvalidDataException($"Map marker panel {nodeName} has invalid source cap dimensions.");
        if (targetSize.X <= left + right || targetSize.Y < top + bottom)
            throw new InvalidDataException($"Map marker panel {nodeName} target is smaller than its source caps.");
        int[] xs = [0, (int)Math.Round(left), targetSize.X - (int)Math.Round(right), targetSize.X];
        int[] ys = [0, (int)Math.Round(top), targetSize.Y - (int)Math.Round(bottom), targetSize.Y];
        using Image composite = Image.CreateEmpty(targetSize.X, targetSize.Y, false, Image.Format.Rgba8);
        foreach (Part part in parts)
        {
            int column = part.Group / 3, row = part.Group % 3;
            int width = xs[column + 1] - xs[column];
            int height = ys[row + 1] - ys[row];
            if (width <= 0 || height <= 0) throw new InvalidDataException($"Map marker panel {nodeName} has a zero-sized part.");
            using Image sourcePixels = part.Texture.GetImage();
            if (sourcePixels is null || sourcePixels.IsEmpty())
                throw new InvalidDataException($"Map marker panel {nodeName} source sprite is empty.");
            if (sourcePixels.GetWidth() != width || sourcePixels.GetHeight() != height)
                sourcePixels.Resize(width, height, Image.Interpolation.Bilinear);
            composite.BlitRect(sourcePixels, new Rect2I(0, 0, width, height),
                new Vector2I(xs[column], ys[row]));
        }
        ImageTexture compositeTexture = ImageTexture.CreateFromImage(composite)
            ?? throw new InvalidDataException($"Map marker panel {nodeName} could not be uploaded.");
        compositeTexture.ResourceName = $"SourceMarkerPanel_{spriteName}_{targetSize.X}x{targetSize.Y}";
        compositeTexture.SetMeta("source_layout_uri", layout.Source.ResourceUri);
        compositeTexture.SetMeta("source_layout_sha256", layout.Source.DecodedSha256);
        compositeTexture.SetMeta("source_atlas_uri", parts[0].AtlasUri);
        compositeTexture.SetMeta("source_atlas_decoded_sha256", parts[0].AtlasSha256);
        compositeTexture.SetMeta("source_nine_part_composed", true);
        return new(compositeTexture, parts[0]);
    }

    public Sprite3D AddTo(Node3D parent, string name, Vector2 center,
        float pixelSize, int renderPriority, Color tint)
    {
        var sprite = new Sprite3D
        {
            Name = name, Texture = _texture,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = pixelSize, FixedSize = true,
            DoubleSided = true, NoDepthTest = true,
            RenderPriority = renderPriority, Offset = center,
            Modulate = tint
        };
        parent.AddChild(sprite);
        sprite.SetMeta("source_sprite_name", _spriteName);
        sprite.SetMeta("source_atlas_uri", _atlasUri);
        sprite.SetMeta("source_atlas_decoded_sha256", _atlasSha256);
        sprite.SetMeta("source_nine_part_composed", true);
        sprite.SetMeta("original_map_panel_placement_verified", false);
        sprite.SetMeta("original_map_panel_tint_verified", false);
        return sprite;
    }

    public void Dispose() => _texture.Dispose();
}
