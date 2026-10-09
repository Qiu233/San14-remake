using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>
/// Static pose of the original three-part portrait. This adapter uses the inspected
/// centered chara_B branch; animation and the separate masked branch are still pending.
/// Its pivot interpretation is restricted to this portrait, not general LCSK rendering.
/// </summary>
public partial class PortraitView : Control
{
    internal void Configure(GameResourceRepository repository, UiAssets assets,
        int appearanceCode, Rect2 rectangle)
    {
        Position = rectangle.Position;
        Size = rectangle.Size;
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
        using var file = Godot.FileAccess.Open("res://ui/portrait.json", Godot.FileAccess.ModeFlags.Read)
            ?? throw new InvalidDataException("Original portrait profile is missing.");
        using JsonDocument profile = JsonDocument.Parse(file.GetAsText());
        JsonElement settings = profile.RootElement;
        uint layoutId = uint.Parse(settings.GetProperty("layoutId").GetString()!,
            NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        LoadedLayout source = repository.LoadLayout("ScreenLayout", layoutId);
        IReadOnlyList<LcskNodeRecord> nodes = LcskNodeReader.Read(source.Layout);
        LcskNodeRecord parent = nodes.Single(node => node.Name == settings.GetProperty("rootNode").GetString());
        RequirePlanar(parent);
        Vector2 sourceSize = new(parent.Value44, parent.Value46);
        if (parent.Type != 0 || parent.Value48 != 0x1111 || sourceSize.X <= 0 || sourceSize.Y <= 0 ||
            parent.HeaderValues[7] != 1 || parent.HeaderValues[8] != 1 ||
            Math.Abs(parent.HeaderValues[0] - sourceSize.X / 2) > 0.001f ||
            Math.Abs(parent.HeaderValues[1] + sourceSize.Y / 2) > 0.001f)
            throw new InvalidDataException("Original portrait branch is no longer centered on its canvas.");
        JsonElement[] layers = settings.GetProperty("layers").EnumerateArray().ToArray();
        LcskNodeRecord[] children = nodes.Where(node => node.ParentIndex == parent.Index).ToArray();
        if (children.Length != layers.Length)
            throw new InvalidDataException("Original portrait layer count differs from the inspected branch.");
        Vector2 scale = rectangle.Size / sourceSize;
        for (int index = 0; index < layers.Length; index++)
        {
            LcskNodeRecord node = children[index];
            if (node.Name != layers[index].GetProperty("node").GetString() || node.Type != 1)
                throw new InvalidDataException("Original portrait layer identity or order differs.");
            RequirePlanar(node);
            LcskSpriteNode sprite = LcskSpriteNodeReader.Read(source.Layout, node);
            if (sprite.RawTintBytes.Any(value => value != 255) || sprite.Groups.Count != 1 ||
                sprite.Groups[0].ComponentCount != 1 ||
                BinaryPrimitives.ReadSingleLittleEndian(sprite.Groups[0].RawScaleBytes.AsSpan(0, 4)) != 1 ||
                BinaryPrimitives.ReadSingleLittleEndian(sprite.Groups[0].RawScaleBytes.AsSpan(4, 4)) != 1 ||
                BinaryPrimitives.ReadUInt32LittleEndian(node.RawBytes.AsSpan(84, 4)) != 0x00058400 ||
                BinaryPrimitives.ReadSingleLittleEndian(node.RawBytes.AsSpan(88, 4)) != 1)
                throw new InvalidDataException($"Original portrait has an unsupported composition: {node.Name}.");
            Vector2 layerSize = new Vector2(node.Value44, node.Value46) *
                new Vector2(node.HeaderValues[7], node.HeaderValues[8]);
            // Both portrait anchors are relative to the centered branch origin.
            // b: parent bottom + upward 450, child center; a: bottom to bottom.
            // They produce the same 636 x 900 canvas, while c keeps its source scale.
            Vector2 anchor = (node.Value48 >> 8) switch
            {
                0x11 => Vector2.Zero,
                0x12 => new Vector2(0, sourceSize.Y / 2),
                _ => throw new InvalidDataException($"Unsupported portrait parent anchor: {node.Value48:X4}.")
            };
            Vector2 pivot = (node.Value48 & 0xFF) switch
            {
                0x11 => layerSize / 2,
                0x12 => new Vector2(layerSize.X / 2, layerSize.Y),
                _ => throw new InvalidDataException($"Unsupported portrait layer pivot: {node.Value48:X4}.")
            };
            Vector2 position = sourceSize / 2 + anchor +
                new Vector2(node.HeaderValues[0], -node.HeaderValues[1]) - pivot;
            string suffix = layers[index].GetProperty("suffix").GetString()!;
            if (suffix is not ("a" or "b" or "c"))
                throw new InvalidDataException("Unknown original portrait part suffix.");
            string name = $"face_{appearanceCode:0000}_{suffix}";
            var image = new TextureRect
            {
                Name = name,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                Texture = assets.SpritePart(name, sprite.Groups[0].Parts[0].Uv),
                Position = position * scale, Size = layerSize * scale,
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(image);
        }
        GD.Print($"Three-part portrait {appearanceCode}: {source.Source.ResourceUri}, " +
            $"layers {string.Join(",", layers.Select(layer => layer.GetProperty("suffix").GetString()))}; static pose.");
    }

    private static void RequirePlanar(LcskNodeRecord node)
    {
        IReadOnlyList<float> values = node.HeaderValues;
        if (values[2] != 0 || values[3] != 0 || values[4] != 0 || values[5] != 0 ||
            values[6] != 1 || values[7] <= 0 || values[8] <= 0)
            throw new InvalidDataException($"Unsupported portrait transform: {node.Name}.");
    }
}
