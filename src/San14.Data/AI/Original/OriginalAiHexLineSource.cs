namespace San14.Data;

/// <summary>The native rectangular-grid line used by the pursuit query. Its
/// order is canonical left-to-right, or low-row-to-high-row for one column; it is
/// not reordered to begin at the first argument.</summary>
internal static class OriginalAiHexLineSource
{
    private const int Width = 220;
    private const int HexCount = Width * Width;

    internal static IReadOnlyList<int> Read(int first, int second)
    {
        var result = new List<int>();
        var seen = new HashSet<int>();
        foreach (int hex in EnumerateCandidates(first, second))
            if (seen.Add(hex)) result.Add(hex);
        return result.ToArray();
    }

    /// <summary>Original append attempts, before successful-list membership
    /// filtering. A pool-backed caller deduplicates against the pointers it
    /// actually appended, so a failed allocation can be retried later.</summary>
    internal static IEnumerable<int> EnumerateCandidates(int first, int second)
    {
        if ((uint)first >= HexCount || (uint)second >= HexCount)
            throw new ArgumentOutOfRangeException(nameof(first), "A line endpoint must identify a map Hex.");
        int firstX = first % Width, firstY = first / Width;
        int secondX = second % Width, secondY = second / Width;
        int deltaX = secondX - firstX;
        if (deltaX == 0)
        {
            for (int y = Math.Min(firstY, secondY); y <= Math.Max(firstY, secondY); y++)
                yield return y * Width + firstX;
            yield break;
        }

        // Keep the two native scalar float32 divisions and separate MULSS /
        // ADDSS rounding. This is not a cube-coordinate or Bresenham line.
        float slope = (float)(secondY - firstY) / (float)deltaX;
        float intercept = (float)(secondX * firstY - secondY * firstX) / (float)deltaX;
        if (slope == 0f)
        {
            for (int x = Math.Min(firstX, secondX); x <= Math.Max(firstX, secondX); x++)
                yield return firstY * Width + x;
            yield break;
        }

        int end = firstX < secondX ? second : first;
        int row = firstX < secondX ? firstY : secondY;
        for (int x = Math.Min(firstX, secondX); x <= Math.Max(firstX, secondX); x++)
        {
            float atColumn = (float)x * slope;
            atColumn += intercept;
            int nextRow = (int)(slope < 0f ? MathF.Floor(atColumn) : MathF.Ceiling(atColumn));
            if (slope < 0f)
            {
                for (int y = row; y >= nextRow; y--)
                {
                    // Preserve the source's signed guard even though ordinary
                    // finite negative-slope inputs take its first alternative.
                    if (nextRow - row >= 2 && y == row) continue;
                    long raw = (long)y * Width + x;
                    yield return (ulong)raw < HexCount ? (int)raw : 0;
                    if (raw == end) break;
                }
            }
            else
            {
                for (int y = row; y <= nextRow; y++)
                {
                    long raw = (long)y * Width + x;
                    yield return (ulong)raw < HexCount ? (int)raw : 0;
                    if (raw == end) break;
                }
            }
            row = nextRow;
        }
    }
}
