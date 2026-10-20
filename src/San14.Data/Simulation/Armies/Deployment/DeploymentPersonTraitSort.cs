namespace San14.Data;

/// <summary>The ordinary trait column's non-strict predicate consumer. Its
/// insertion and merge call order matters for equal and invalid traits. Uses
/// our own full temporary buffer; allocator-failure permutations are not part
/// of this ordinary operation.</summary>
internal static class DeploymentPersonTraitSort
{
    internal static void Sort(int[] rows, Func<int, int, bool> before)
    {
        if (rows.Length <= 32) { Insert(rows, 0, rows.Length, before); return; }
        int half = (rows.Length + 1) / 2;
        var buffer = new int[half];
        SortRuns(rows, 0, half, buffer, before);
        SortRuns(rows, half, rows.Length - half, buffer, before);
        int right = rows.Length - half;
        if (half <= right)
        {
            Array.Copy(rows, 0, buffer, 0, half);
            Forward(buffer, 0, half, rows, half, rows.Length, rows, 0, true, before);
        }
        else
        {
            Array.Copy(rows, half, buffer, 0, right);
            int leftAt = half - 1, rightAt = right - 1, output = rows.Length - 1;
            while (leftAt >= 0 && rightAt >= 0)
                rows[output--] = before(buffer[rightAt], rows[leftAt]) ? rows[leftAt--] : buffer[rightAt--];
            if (rightAt >= 0) Array.Copy(buffer, 0, rows, output - rightAt, rightAt + 1);
        }
    }

    private static void Insert(int[] rows, int start, int count, Func<int, int, bool> before)
    {
        int end = start + count;
        for (int index = start + 1; index < end; index++)
        {
            int value = rows[index];
            if (before(value, rows[start]))
            {
                Array.Copy(rows, start, rows, start + 1, index - start);
                rows[start] = value;
            }
            else
            {
                int at = index;
                // The initial comparison also guards the beginning. Repeating
                // that comparison here matches the original lazy consumer.
                while (before(value, rows[at - 1])) { rows[at] = rows[at - 1]; at--; }
                rows[at] = value;
            }
        }
    }

    private static void SortRuns(int[] rows, int start, int count, int[] buffer, Func<int, int, bool> before)
    {
        for (int at = 0; at < count; at += 32) Insert(rows, start + at, Math.Min(32, count - at), before);
        for (int run = 32; run < count; run *= 4)
        {
            MergeRuns(rows, start, buffer, 0, count, run, before);
            MergeRuns(buffer, 0, rows, start, count, run * 2, before);
        }
    }

    private static void MergeRuns(int[] source, int from, int[] target, int to,
        int count, int run, Func<int, int, bool> before)
    {
        for (int offset = 0; offset < count; offset += run * 2)
        {
            int middle = from + Math.Min(offset + run, count), end = from + Math.Min(offset + run * 2, count);
            Forward(source, from + offset, middle, source, middle, end, target, to + offset, false, before);
        }
    }

    private static void Forward(int[] left, int at, int end, int[] right, int other, int otherEnd,
        int[] output, int write, bool rightAlreadyInPlace, Func<int, int, bool> before)
    {
        while (at < end && other < otherEnd)
            output[write++] = before(right[other], left[at]) ? right[other++] : left[at++];
        int remaining = end - at;
        if (remaining != 0) Array.Copy(left, at, output, write, remaining);
        if (!rightAlreadyInPlace && other != otherEnd)
            Array.Copy(right, other, output, write + remaining, otherEnd - other);
    }
}
