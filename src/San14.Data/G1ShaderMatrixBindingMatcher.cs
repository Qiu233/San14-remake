using San14.Data.Formats;

namespace San14.Data;

public sealed record G1ShaderMatrixBindingSelection(int RowIndex, bool FoundZeroPenalty,
    int? SelectedPenalty, int RowsVisited);

/// <summary>Original 96E0F0 selection for the four-word matrix binding dimension.
/// Scores and ties retain the original signed 32-bit arithmetic, including overflow.</summary>
public static class G1ShaderMatrixBindingMatcher
{
    public const int WordCount = 4;

    public static int CalculatePenalty(ReadOnlySpan<uint> currentWords, G1ShaderMatrixBindingRow row)
    {
        ValidateCurrent(currentWords);
        ArgumentNullException.ThrowIfNull(row);
        if (row.Words.Count != WordCount || row.RawBytes.Length != WordCount * 4)
            throw new InvalidDataException("Original G1S matrix binding row has invalid word or source-byte bounds.");
        IReadOnlyList<uint> r = row.Words;
        ReadOnlySpan<uint> c = currentWords;
        unchecked
        {
            uint request = c[0] & 0x1Du;
            int penalty = (int)((request - (request & r[0])) << 16);
            if ((r[0] & 4) == 0 && (r[2] != 0) != (c[2] != 0))
                penalty += r[2] + c[2] == 1 ? 1 : 0x10000;
            if ((c[0] & 0x20) == 0 && (r[0] & 0x20) != 0) penalty += 0x10000;
            if ((c[0] & 0xC0) != (r[0] & 0xC0)) penalty += 0x1000000;

            uint currentCapacity = Math.Max(c[1] + 1, c[2]);
            uint rowCapacity = Math.Max(r[1] + 1, r[2]);
            int difference = (int)(currentCapacity - rowCapacity);
            if (difference > 0) penalty += difference << 12;
            if ((r[0] & 1) == 0) penalty += AbsDifference(r[1], c[1]);

            if (r[3] < c[3])
                penalty += r[3] == 0 ? 0x1000000 : (int)((c[3] - r[3]) << 16);
            else if (c[3] == 0 && r[3] != 0) penalty += 0x1000000;
            return penalty;
        }
    }

    public static G1ShaderMatrixBindingSelection Select(ReadOnlySpan<uint> currentWords,
        IReadOnlyList<G1ShaderMatrixBindingRow> rows)
    {
        ValidateCurrent(currentWords);
        ArgumentNullException.ThrowIfNull(rows);
        int bestIndex = -1, bestPenalty = int.MaxValue, visited = 0;
        int? selectedPenalty = null;
        for (int i = 0; i < rows.Count; i++)
        {
            int score = CalculatePenalty(currentWords, rows[i]);
            visited++;
            if (score == 0) return new(i, true, 0, visited);
            if (score < bestPenalty)
            {
                bestIndex = i;
                bestPenalty = score;
                selectedPenalty = score;
            }
        }
        return new(bestIndex, false, selectedPenalty, visited);
    }

    private static int AbsDifference(uint left, uint right)
    {
        int value = unchecked((int)(left - right));
        int sign = value >> 31;
        return unchecked((value ^ sign) - sign);
    }
    private static void ValidateCurrent(ReadOnlySpan<uint> words)
    {
        if (words.Length != WordCount)
            throw new InvalidDataException("Original current G1S matrix binding must contain all four words.");
    }
}
