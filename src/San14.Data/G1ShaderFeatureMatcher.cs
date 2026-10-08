using San14.Data.Formats;

namespace San14.Data;

public readonly record struct G1ShaderFeatureScore(int Penalty, int Excess);
public sealed record G1ShaderFeatureSelection(int RowIndex, bool EncounteredZeroPenalty,
    G1ShaderFeatureScore? SelectedScore, int RowsVisited);

/// <summary>Original 96DE40 weighted row selection using explicit current words.
/// A selected feature row is not a physical VS/PS program or a proven active branch.</summary>
public static class G1ShaderFeatureMatcher
{
    public static G1ShaderFeatureScore CalculateScore(ReadOnlySpan<uint> currentWords, G1ShaderFeatureRow row)
    {
        ValidateCurrent(currentWords);
        ArgumentNullException.ThrowIfNull(row);
        if (row.Words.Count != G1ShaderFeatureRowReader.WordCount || row.RawBytes.Length != G1ShaderFeatureRowReader.RecordWidth)
            throw new InvalidDataException("Original G1S feature row has invalid word or source-byte bounds.");
        IReadOnlyList<uint> r = row.Words;
        ReadOnlySpan<uint> c = currentWords;
        unchecked
        {
            uint request = c[0] & 0xF77FFu;
            uint missing = request - (request & r[0]);
            int penalty = (int)(((missing & ~0x80u) << 16) + ((missing >> 7) & 1));
            int excess = (r[0] & ~request) != 0 ? 1 : 0;
            for (int j = 1; j <= 4; j++)
            {
                // The native count comparisons are unsigned; the later
                // category comparisons and scores are signed 32-bit values.
                if (r[j] < c[j]) penalty += (int)((c[j] - r[j]) << 8);
                else if ((r[0] & (1u << (j - 1))) != 0) excess += (int)(r[j] - c[j]);
                else penalty += (int)((r[j] - c[j]) << 4);
            }
            if (c[6] == 0) excess += AbsDifference(r[6], 0);
            else
            {
                if (r[6] != 5 || c[6] is 4 or 7) penalty += AbsDifference(r[6], c[6]);
                penalty += AbsDifference(r[7], c[7]);
            }
            penalty += AbsDifference(c[5], r[5]) << 12;
            if ((r[0] & 0x100) == 0) penalty += AbsDifference(c[8], r[8]);
            else if ((int)r[8] >= 7 && (int)c[8] >= 7 && r[8] != c[8])
                penalty += AbsDifference(c[8], r[8]);
            else if ((int)r[8] < (int)c[8] || ((c[8] & 4) != 0 && (int)r[8] > 7))
                penalty += (int)(c[8] - r[8]); // Preserve the native negative difference.
            else excess += AbsDifference(c[8], r[8]);
            if (c[8] != 0)
            {
                penalty += AbsDifference(c[9], r[9]) << 12;
                if (r[10] != 5 || c[10] == 2)
                    penalty += AbsDifference(c[10], r[10]) << 8;
            }
            // Word11 is retained by the row reader, but not compared here.
            return new(penalty, excess);
        }
    }

    public static G1ShaderFeatureSelection Select(ReadOnlySpan<uint> currentWords, IReadOnlyList<G1ShaderFeatureRow> rows)
    {
        ValidateCurrent(currentWords);
        ArgumentNullException.ThrowIfNull(rows);
        int bestIndex = -1, bestPenalty = int.MaxValue, bestExcess = int.MaxValue, visited = 0;
        bool encounteredZero = false;
        G1ShaderFeatureScore? selected = null;
        for (int i = 0; i < rows.Count; i++)
        {
            G1ShaderFeatureScore score = CalculateScore(currentWords, rows[i]);
            visited++;
            if (score.Penalty == 0)
            {
                encounteredZero = true;
                if (score.Excess < bestExcess)
                { bestExcess = score.Excess; bestIndex = i; selected = score; }
                if (score.Excess == 0) break;
                bestPenalty = 0;
            }
            else if (score.Penalty < bestPenalty)
            { bestPenalty = score.Penalty; bestIndex = i; selected = score; }
        }
        return new(bestIndex, encounteredZero, selected, visited);
    }

    private static int AbsDifference(uint left, uint right)
    {
        int value = unchecked((int)(left - right));
        int sign = value >> 31;
        // Native cdq/xor/sub keeps INT_MIN negative instead of throwing.
        return unchecked((value ^ sign) - sign);
    }

    private static void ValidateCurrent(ReadOnlySpan<uint> words)
    {
        if (words.Length != G1ShaderFeatureRowReader.WordCount)
            throw new InvalidDataException("Original current G1S feature must contain all twelve words.");
    }
}
