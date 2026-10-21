using Godot;

/// <summary>Actual TL/TR/BL/BR screen corners. Integer source R is a separate
/// rounded TL/BR pair; sampling UV keeps the unrounded projected corners.</summary>
internal sealed record UiSourceType6Geometry(Vector2[] Corners, Vector2[] Uvs, Rect2I Region)
{
    internal static UiSourceType6Geometry FromScreenCorners(Vector2[] corners, Vector2I contextSize, bool flipUvRows)
    {
        if (corners.Length != 4 || contextSize.X <= 0 || contextSize.Y <= 0 ||
            corners.Any(p => !float.IsFinite(p.X) || !float.IsFinite(p.Y)))
            throw new ArgumentException("Type6 requires four finite projected corners and actual context dimensions.");
        Vector2[] positions = corners.ToArray();
        Vector2[] uv = positions.Select(p => p / (Vector2)contextSize).ToArray();
        if (flipUvRows) uv = [uv[2], uv[3], uv[0], uv[1]];
        Vector2I topLeft = new(Round(positions[0].X), Round(positions[0].Y));
        Vector2I bottomRight = new(Round(positions[3].X), Round(positions[3].Y));
        return new(positions, uv, new(topLeft, bottomRight - topLeft));
        static int Round(float value) => checked((int)(value < 0 ? value - .5f : value + .5f));
    }
}
