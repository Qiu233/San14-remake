using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Draw source three/nine-part panels directly from their atlas UVs.
/// Changing a label's width stretches only the center span, without generating
/// another image or widening the source end caps. Atlas ownership is external.</summary>
internal sealed class OriginalMapMarkerSlices
{
    private readonly record struct Part(int Group, Texture2D Texture);
    private readonly Part[] _parts;
    private readonly Vector4 _caps;

    private OriginalMapMarkerSlices(Part[] parts, Vector4 caps) => (_parts, _caps) = (parts, caps);

    internal static OriginalMapMarkerSlices Load(OriginalMapMarkerLayout layout, int nodeIndex, OriginalUiAssets assets)
    {
        var source = LcskSpriteNodeReader.Read(layout.Layout, layout.Nodes[nodeIndex]);
        var groups = source.Groups.Where(group => group.ComponentCount != 0).ToArray();
        if (source.NodeType != 3 || groups.Length is not (3 or 9) ||
            groups.Any(group => group.Parts.Count != 1) || source.RawTintBytes.Any(value => value != 255))
            throw new InvalidDataException($"Unsupported original marker panel parts/tint in {layout.Name}/{nodeIndex}.");
        Part[] parts = groups.Select(group => new Part(group.Index,
            assets.SpritePart(group.Parts[0].SpriteName, group.Parts[0].Uv))).ToArray();
        Vector2 size = assets.Sprite(groups[0].Parts[0].SpriteName).Texture.Region.Size;
        bool strip = groups.Length == 3;
        float left = size.X * source.Groups[strip ? 1 : 0].Parts[0].Uv.TopRight.U;
        float right = size.X * (1 - source.Groups[strip ? 7 : 6].Parts[0].Uv.TopLeft.U);
        float top = strip ? 0 : size.Y * source.Groups[0].Parts[0].Uv.BottomLeft.V;
        float bottom = strip ? 0 : size.Y * (1 - source.Groups[2].Parts[0].Uv.TopLeft.V);
        return new(parts, new(left, top, right, bottom));
    }

    internal void Draw(CanvasItem canvas, Rect2 rectangle, Color tint, float scaleX = 1, float scaleY = 1)
    {
        Span<float> xs = stackalloc float[4] { 0, _caps.X * scaleX, rectangle.Size.X - _caps.Z * scaleX, rectangle.Size.X };
        Span<float> ys = stackalloc float[4] { 0, _caps.Y * scaleY, rectangle.Size.Y - _caps.W * scaleY, rectangle.Size.Y };
        foreach (Part part in _parts)
        {
            int column = part.Group / 3, row = part.Group % 3;
            canvas.DrawTextureRect(part.Texture, new(rectangle.Position + new Vector2(xs[column], ys[row]),
                new Vector2(xs[column + 1] - xs[column], ys[row + 1] - ys[row])), false, tint);
        }
    }
}
