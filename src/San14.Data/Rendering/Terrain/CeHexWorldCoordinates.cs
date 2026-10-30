using San14.Core;
using System.Numerics;
using F = San14.Data.UnitWorldGroundHeightFloat;

namespace San14.Data;

/// <summary>
/// CE Hex index to terrain X/Z calculation.
/// Constants and float32 operation order are part of this project?s rules;
/// Scene selection and height sampling belong to their respective owners.
/// </summary>
public static class CeHexWorldCoordinates
{
    public const string FormulaId = "ce-20260928-hex-world";
    public const int Width = 220;
    public const int Height = 220;
    public const double OriginX = 37594.0;
    public const double ColumnStep = 702.0;
    public const double InitializedOriginZ = 13335.2998046875;
    public const double RowStep = 810.5997924804688;
    public const double EvenColumnRowOffset = 0.5;

    public static string GetRuleVersion(string locale) => SimulationRuleIdentity.ForLocale(locale);

    public static ProjectedMapPoint Project(int hexId)
    {
        if ((uint)hexId >= Width * Height)
            throw new ArgumentOutOfRangeException(nameof(hexId));
        return ProjectIndex(hexId);
    }

    /// <summary>1888D0 also projects a camera caller's raw word index outside
    /// the map. In particular, 48400 is row 220, not a missing camera request.
    /// This does not make that index a valid world Hex.</summary>
    public static ProjectedMapPoint ProjectEventCamera(ushort rawHexIndex) => ProjectIndex(rawHexIndex);

    private static ProjectedMapPoint ProjectIndex(int hexId)
    {
        int column = hexId % Width, row = hexId / Width;
        float x = column * 702f;
        x += 37594f;

        float root3 = MathF.Sqrt(3f);
        // Startup initializer: square root, two products, then addition.
        float initialHalf = root3 * 0.5f;
        float initialOffset = initialHalf * 468f;
        float zOrigin = initialOffset + 12930f;
        // Even columns add a separate half-row product.
        float evenOffset = column % 2 == 0 ? (root3 * 468f) * 0.5f : 0f;
        float zStride = root3 * 468f;
        float z = zStride * row;
        z += evenOffset;
        z += zOrigin;
        return new ProjectedMapPoint(hexId, x, z);
    }

    /// <summary>Maps a source terrain point to a physical Hex table slot.
    /// The three float32 distances and their ordered ties are intentional;
    /// this is not the fitted-grid nearest-center policy.</summary>
    public static bool TryPick(float worldX, float worldZ, out int hexId)
    {
        hexId = -1;
        float x = F.Subtract(worldX, (float)OriginX);
        float z = F.Subtract(worldZ, (float)InitializedOriginZ);
        float rowStep = F.Multiply(MathF.Sqrt(3f), 468f);
        float row = F.Add(MathF.Floor(F.Divide(z, rowStep)), 1f);
        float column = F.Add(MathF.Floor(F.Divide(x, 702f)), 1f);
        // CVTTSS2SI is signed32, followed by an unsigned bound check.
        uint r = unchecked((uint)BattleUnitSourceMath.TruncateSingleToInt32(row));
        uint c = unchecked((uint)BattleUnitSourceMath.TruncateSingleToInt32(column));
        if (r > Height || c > Width) return false;
        float leftX = F.Multiply(unchecked((int)(c - 1)), 702f);
        float lowerZ = F.Multiply(unchecked((int)(r - 1)), rowStep);
        float rightX = F.Add(leftX, 702f);
        bool odd = (c & 1) != 0;
        float leftZ = odd ? F.Add(F.Multiply(rowStep, 0.5f), lowerZ) : lowerZ;
        float rightZ = odd ? lowerZ : F.Add(F.Multiply(rowStep, 0.5f), lowerZ);
        float upperX = odd ? rightX : leftX;
        float rightDx = F.Subtract(rightX, x), rightDz = F.Subtract(rightZ, z);
        float leftDx = F.Subtract(leftX, x), leftDz = F.Subtract(leftZ, z);
        float upperDx = F.Subtract(upperX, x), upperDz = F.Subtract(F.Add(lowerZ, rowStep), z);
        float rightDistance = F.Add(F.Multiply(rightDz, rightDz), F.Multiply(rightDx, rightDx));
        float leftDistance = F.Add(F.Multiply(leftDz, leftDz), F.Multiply(leftDx, leftDx));
        float upperDistance = F.Add(F.Multiply(upperDz, upperDz), F.Multiply(upperDx, upperDx));
        int candidate = leftDistance <= rightDistance
            ? (upperDistance <= leftDistance ? 2 : 1)
            : (rightDistance < upperDistance ? 0 : 2);
        uint basis = r * Width + c;
        uint slot;
        if (candidate == 0)
        {
            if (c == Width) return false;
            slot = unchecked(basis - Width);
        }
        else if (candidate == 1)
        {
            if (c == 0) return false;
            slot = unchecked(basis - Width - 1);
        }
        else if (odd) slot = basis;
        else
        {
            if (c == 0) return false;
            slot = unchecked(basis - 1);
        }
        if (slot >= Width * Height) return false;
        hexId = (int)slot;
        return true;
    }

    /// <summary>Resolves an actual terrain hit using the source selection plane.
    /// A terrain miss must be handled by the caller before invoking this method.</summary>
    public static bool TryPickTerrainHit(Vector3 origin, Vector3 direction, Vector3 terrainHit, out int hexId)
    {
        if (terrainHit.Y < 445f)
        {
            // Preserve the general ray/plane dot products, including zero
            // products and unordered values. Only ordered negative t fails.
            float numerator = F.Add(F.Add(F.Multiply(0f, F.Subtract(0f, origin.X)),
                F.Multiply(1f, F.Subtract(445f, origin.Y))), F.Multiply(0f, F.Subtract(0f, origin.Z)));
            float denominator = F.Add(F.Add(F.Multiply(direction.X, 0f), F.Multiply(1f, direction.Y)),
                F.Multiply(0f, direction.Z));
            float t = F.Divide(numerator, denominator);
            if (t < 0f) { hexId = -1; return false; }
            terrainHit = new(F.Add(F.Multiply(direction.X, t), origin.X),
                F.Add(F.Multiply(direction.Y, t), origin.Y), F.Add(F.Multiply(direction.Z, t), origin.Z));
        }
        return TryPick(terrainHit.X, terrainHit.Z, out hexId);
    }
}
