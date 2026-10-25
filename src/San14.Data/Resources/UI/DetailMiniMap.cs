namespace San14.Data;

public enum DetailMiniMapIconKind { City, Gate, Area, Force, Person }

public sealed record DetailMiniMapMarker(DetailMiniMapIconKind Kind, int Identity,
    int HexId, int X, int Y, byte ColorIndex);

public sealed record DetailMiniMapValues(IReadOnlyList<DetailMiniMapMarker> Bases,
    DetailMiniMapMarker? Selection);

/// <summary>The original small map's integer canvas coordinates. The world
/// endpoints and operation order are shared with its selected-marker writer.</summary>
public static class DetailMiniMapProjection
{
    public static DetailMiniMapMarker Project(DetailMiniMapIconKind kind, int identity, int hexId, byte colorIndex) =>
        Project(kind, identity, hexId, colorIndex, CeHexWorldCoordinates.Project(hexId));

    /// <summary>1888D0 projects the raw current ushort, including the known
    /// 48400 invalid-object sentinel. The ordinary map's Hex guard stays intact.</summary>
    public static DetailMiniMapMarker ProjectForce(int identity, ushort hexId, byte colorIndex)
    {
        int column = hexId % CeHexWorldCoordinates.Width, row = hexId / CeHexWorldCoordinates.Width;
        float x = column * (float)CeHexWorldCoordinates.ColumnStep;
        x += (float)CeHexWorldCoordinates.OriginX;
        float stride = MathF.Sqrt(3f) * 468f;
        float evenOffset = column % 2 == 0 ? stride * .5f : 0f;
        float z = stride * row;
        z += evenOffset;
        z += (float)CeHexWorldCoordinates.InitializedOriginZ;
        return Project(DetailMiniMapIconKind.Force, identity, hexId, colorIndex, new(hexId, x, z));
    }

    private static DetailMiniMapMarker Project(DetailMiniMapIconKind kind, int identity, int hexId,
        byte colorIndex, ProjectedMapPoint point)
    {
        var start = CeHexWorldCoordinates.Project(0);
        var end = CeHexWorldCoordinates.Project(CeHexWorldCoordinates.Width * CeHexWorldCoordinates.Height - 1);
        // The source bounds are (5,3)..(221,252). Multiply, divide and add
        // remain float32, then CVTTSS2SI truncates toward zero.
        float x = 216f * ((float)point.X - (float)start.X);
        x /= (float)end.X - (float)start.X;
        x += 5f;
        float y = 249f * ((float)point.Z - (float)start.Z);
        y /= (float)end.Z - (float)start.Z;
        y += 3f;
        return new(kind, identity, hexId, (int)x, (int)y, colorIndex);
    }
}
