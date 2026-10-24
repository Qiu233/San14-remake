using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Explicit geometry policy and source identities; optional source formula is validated separately.</summary>
public sealed record MapWorldProjectionPolicy(string Id, int Width, int Height, string MapSha256,
    string FieldResourceUri, string FieldDecodedSha256, double OriginX, double OriginZ,
    double ColumnStep, double RowStep, double EvenColumnRowOffset)
{
    // Null keeps historical fitted policies usable for custom and future maps.
    public string? SourceFormulaId { get; init; }
}

public readonly record struct ProjectedMapPoint(int HexId, double X, double Z);

/// <summary>
/// Projects source map IDs through a caller-selected policy. Validates the
/// original count/ID/raw-field serialization, without changing map data, topology
/// or movement rules. Field identities are retained, not loaded or decoded here.
/// </summary>
public sealed class MapWorldProjection
{
    public MapWorldProjectionPolicy Policy { get; }
    public bool WorldMappingFidelityVerified => false;
    private readonly int _count;

    private MapWorldProjection(MapWorldProjectionPolicy policy, int count) => (Policy, _count) = (policy, count);

    public static MapWorldProjection Build(MapDocument map, MapWorldProjectionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(policy);
        if (string.IsNullOrWhiteSpace(policy.Id))
            throw new InvalidDataException("Map world projection policy requires an ID.");
        if (map.Width <= 0 || map.Height <= 0 || map.Cells is null ||
            (long)map.Width * map.Height != map.Cells.Length ||
            policy.Width != map.Width || policy.Height != map.Height)
            throw new InvalidDataException($"Map world projection '{policy.Id}' dimensions and cell count differ.");
        ValidateSha(policy.MapSha256, "map", policy.Id);
        ValidateSha(policy.FieldDecodedSha256, "field", policy.Id);
        try
        {
            if (string.IsNullOrWhiteSpace(policy.FieldResourceUri) || GameResourceId.Parse(policy.FieldResourceUri).Kind != "rdb")
                throw new FormatException("Field identity must be an RDB resource URI.");
        }
        catch (FormatException error)
        {
            throw new InvalidDataException($"Map world projection '{policy.Id}' field resource URI is invalid: {policy.FieldResourceUri}.", error);
        }
        if (!double.IsFinite(policy.OriginX) || !double.IsFinite(policy.OriginZ) ||
            !double.IsFinite(policy.ColumnStep) || policy.ColumnStep <= 0 ||
            !double.IsFinite(policy.RowStep) || policy.RowStep <= 0 ||
            !double.IsFinite(policy.EvenColumnRowOffset))
            throw new InvalidDataException($"Map world projection '{policy.Id}' requires finite origins/offset and finite positive steps.");
        if (policy.SourceFormulaId is not null &&
            (policy.SourceFormulaId != CeHexWorldCoordinates.FormulaId ||
             policy.Width != CeHexWorldCoordinates.Width ||
             policy.Height != CeHexWorldCoordinates.Height ||
             policy.OriginX != CeHexWorldCoordinates.OriginX ||
             policy.OriginZ != CeHexWorldCoordinates.InitializedOriginZ ||
             policy.ColumnStep != CeHexWorldCoordinates.ColumnStep ||
             policy.RowStep != CeHexWorldCoordinates.RowStep ||
             policy.EvenColumnRowOffset != CeHexWorldCoordinates.EvenColumnRowOffset))
            throw new InvalidDataException($"Map world projection '{policy.Id}' source formula parameters differ from the verified CE code.");

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> countBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(countBytes, map.Cells.Length);
        hash.AppendData(countBytes);
        Span<byte> recordBytes = stackalloc byte[16];
        for (int index = 0; index < map.Cells.Length; index++)
        {
            MapCell cell = map.Cells[index];
            if (cell.Id != index)
                throw new InvalidDataException($"Map world projection '{policy.Id}' cell ID {cell.Id} differs from source index {index}.");
            BinaryPrimitives.WriteInt32LittleEndian(recordBytes, cell.Id);
            BinaryPrimitives.WriteUInt32LittleEndian(recordBytes[4..], cell.Field1);
            BinaryPrimitives.WriteUInt32LittleEndian(recordBytes[8..], cell.Field2);
            BinaryPrimitives.WriteUInt32LittleEndian(recordBytes[12..], cell.Field3);
            hash.AppendData(recordBytes);
        }
        string actual = Convert.ToHexString(hash.GetHashAndReset());
        if (!actual.Equals(policy.MapSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Map world projection '{policy.Id}' map source SHA256 differs: expected {policy.MapSha256}, actual {actual}.");

        // Every row is monotone for the positive source steps. Check both column
        // parities that actually occur, including their first/last row endpoints.
        ValidateEndpoint(0, 0);
        ValidateEndpoint(0, map.Height - 1);
        int lastEven = (map.Width - 1) & ~1;
        ValidateEndpoint(lastEven, 0);
        ValidateEndpoint(lastEven, map.Height - 1);
        if (map.Width > 1)
        {
            int lastOdd = ((map.Width - 2) / 2) * 2 + 1;
            ValidateEndpoint(1, 0);
            ValidateEndpoint(1, map.Height - 1);
            ValidateEndpoint(lastOdd, 0);
            ValidateEndpoint(lastOdd, map.Height - 1);
        }
        return new(policy, map.Cells.Length);

        void ValidateEndpoint(int column, int row)
        {
            double x = policy.OriginX + column * policy.ColumnStep;
            double z = policy.OriginZ + (row + ((column & 1) == 0 ? policy.EvenColumnRowOffset : 0)) * policy.RowStep;
            if (!double.IsFinite(x) || !double.IsFinite(z))
                throw new InvalidDataException($"Map world projection '{policy.Id}' grid endpoint ({column}, {row}) exceeds finite coordinate arithmetic.");
        }
    }

    public ProjectedMapPoint Project(int hexId)
    {
        if ((uint)hexId >= (uint)_count)
            throw new ArgumentOutOfRangeException(nameof(hexId), hexId, "Hex ID lies outside the fitted source map; coordinates are not clamped.");
        int column = hexId % Policy.Width, row = hexId / Policy.Width;
        if (Policy.SourceFormulaId == CeHexWorldCoordinates.FormulaId)
            return CeHexWorldCoordinates.Project(hexId);
        return new(hexId, Policy.OriginX + column * Policy.ColumnStep,
            Policy.OriginZ + (row + ((column & 1) == 0 ? Policy.EvenColumnRowOffset : 0)) * Policy.RowStep);
    }

    /// <summary>
    /// Source-formula policies use the original float32 three-candidate picker.
    /// Fitted policies pick the nearest center of the infinite grid, then accept
    /// only legal columns/rows. Fitted ties select the smallest legal ID.
    /// Returns false for nonfinite inputs or extreme scales/coordinates that
    /// cannot retain the resolution required by this double-precision adapter.
    /// </summary>
    public bool TryPick(double x, double z, out int hexId)
    {
        if (Policy.SourceFormulaId == CeHexWorldCoordinates.FormulaId)
            return CeHexWorldCoordinates.TryPick((float)x, (float)z, out hexId);
        hexId = -1;
        if (!double.IsFinite(x) || !double.IsFinite(z)) return false;
        double scale = Math.Max(Policy.ColumnStep, Policy.RowStep);
        double columnScale = Policy.ColumnStep / scale, rowScale = Policy.RowStep / scale;
        double smallerScale = Math.Min(columnScale, rowScale);
        // Reject extreme aspect ratios before normalized squared distances can
        // underflow. This is a numerical support limit, not a map-rule limit.
        if (smallerScale * smallerScale < 2.2250738585072014e-308) return false;
        if (!HasStepResolution(x, Policy.ColumnStep) || !HasStepResolution(z, Policy.RowStep)) return false;
        double columnPosition = (x - Policy.OriginX) / Policy.ColumnStep;
        double oddRowPosition = (z - Policy.OriginZ) / Policy.RowStep;
        double evenRowPosition = oddRowPosition - Policy.EvenColumnRowOffset;
        if (!double.IsFinite(columnPosition) || !double.IsFinite(oddRowPosition) || !double.IsFinite(evenRowPosition)) return false;
        // Every nearest parity-column is within one column of the query. This
        // fast outside rejection does not move the query onto a finite edge.
        if (columnPosition < -2 || columnPosition > (double)Policy.Width + 1) return false;
        if (Math.Abs(oddRowPosition) >= 4503599627370496d || Math.Abs(evenRowPosition) >= 4503599627370496d) return false;

        var candidates = new PickCandidate[8];
        int count = 0, nearest = -1;
        for (int parity = 0; parity < 2; parity++)
        {
            double columnFloor = parity + 2 * Math.Floor((columnPosition - parity) / 2);
            double rowPosition = parity == 0 ? evenRowPosition : oddRowPosition;
            double rowFloor = Math.Floor(rowPosition);
            for (int columnChoice = 0; columnChoice < 2; columnChoice++)
            {
                double column = columnFloor + 2 * columnChoice;
                double dx = (column - columnPosition) * columnScale;
                for (int rowChoice = 0; rowChoice < 2; rowChoice++)
                {
                    double row = rowFloor + rowChoice;
                    double dz = (row - rowPosition) * rowScale;
                    candidates[count] = new(column, row, dx, dz);
                    if (nearest < 0 || DistanceDifference(candidates[count], candidates[nearest]) < 0) nearest = count;
                    count++;
                }
            }
        }

        PickCandidate best = candidates[nearest];
        int chosen = int.MaxValue;
        foreach (PickCandidate candidate in candidates)
        {
            // Coordinate tie margin: step*2^-42 on each axis (~2.27e-13).
            // Sensitivity uses differences of centers, so a shared large X
            // distance cannot swallow a much smaller row-distance distinction.
            double tolerance = 4.547473508864641e-13 *
                (Math.Abs(candidate.DeltaX - best.DeltaX) * columnScale +
                 Math.Abs(candidate.DeltaZ - best.DeltaZ) * rowScale);
            if (DistanceDifference(candidate, best) > tolerance) continue;
            if (candidate.Column < 0 || candidate.Column >= Policy.Width ||
                candidate.Row < 0 || candidate.Row >= Policy.Height) continue;
            int id = checked((int)candidate.Row * Policy.Width + (int)candidate.Column);
            chosen = Math.Min(chosen, id);
        }
        if (chosen == int.MaxValue) return false;
        hexId = chosen;
        return true;
    }

    private readonly record struct PickCandidate(double Column, double Row, double DeltaX, double DeltaZ);

    private static double DistanceDifference(PickCandidate a, PickCandidate b) =>
        (a.DeltaX - b.DeltaX) * (a.DeltaX + b.DeltaX) +
        (a.DeltaZ - b.DeltaZ) * (a.DeltaZ + b.DeltaZ);

    private static bool HasStepResolution(double coordinate, double step) =>
        double.IsFinite(coordinate + step) && double.IsFinite(coordinate - step) &&
        coordinate + step != coordinate && coordinate - step != coordinate;

    private static void ValidateSha(string? value, string kind, string policyId)
    {
        if (value is null || value.Length != 64 || value.Any(c => !char.IsAsciiHexDigit(c)))
            throw new InvalidDataException($"Map world projection '{policyId}' {kind} SHA256 must contain exactly 64 hexadecimal digits.");
    }
}
