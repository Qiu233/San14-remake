namespace San14.Data;

/// <summary>The finite ordering consumers used by the six automatic assignments.
/// Predicates remain lazy bool consumers: neither equality nor unordered floats
/// are converted to a three-way comparison.</summary>
internal static class AreaAdministrationAutomaticOrder
{
    internal static void Sort<T>(List<T> rows, Func<T, T, bool> before) =>
        Sort(rows, 0, rows.Count, rows.Count, before);

    // The Area linked-list consumer compares the adjacent predecessor first.
    // Keeping this separate preserves its read order, including duplicate nodes.
    internal static void SortAreas<T>(List<T> rows, Func<T, T, bool> before)
    {
        for (int index = 1; index < rows.Count; index++)
        {
            T value = rows[index];
            int at = index;
            while (at > 0 && before(value, rows[at - 1]))
            {
                rows[at] = rows[at - 1];
                at--;
            }
            rows[at] = value;
        }
    }

    // The score and ratio payloads use the same buffered, stable consumer.
    // Ordinary operation owns the full half-size buffer, as in its source path.
    internal static void StableSort<T>(List<T> rows, Func<T, T, bool> before)
    {
        if (rows.Count <= 32) { Insert(rows, 0, rows.Count, before); return; }
        int half = (rows.Count + 1) / 2;
        var buffer = new T[half];
        SortRuns(rows, 0, half, buffer, before);
        SortRuns(rows, half, rows.Count - half, buffer, before);
        int right = rows.Count - half;
        if (half <= right)
        {
            rows.CopyTo(0, buffer, 0, half);
            Forward(buffer, 0, half, rows, half, rows.Count, rows, 0, true, before);
        }
        else
        {
            rows.CopyTo(half, buffer, 0, right);
            int leftAt = half - 1, rightAt = right - 1, output = rows.Count - 1;
            while (leftAt >= 0 && rightAt >= 0)
                rows[output--] = before(buffer[rightAt], rows[leftAt]) ? rows[leftAt--] : buffer[rightAt--];
            while (rightAt >= 0) rows[output--] = buffer[rightAt--];
        }
    }

    private static void Sort<T>(List<T> rows, int first, int last, int ideal, Func<T, T, bool> before)
    {
        while (last - first > 32)
        {
            if (ideal < 1)
            {
                Heap(rows, first, last, before);
                return;
            }
            var (equalFirst, equalLast) = Partition(rows, first, last, before);
            ideal = ideal / 2 + (ideal / 2) / 2;
            if (equalFirst - first < last - equalLast)
            {
                Sort(rows, first, equalFirst, ideal, before);
                first = equalLast;
            }
            else
            {
                Sort(rows, equalLast, last, ideal, before);
                last = equalFirst;
            }
        }
        if (last - first > 1) Insert(rows, first, last - first, before);
    }

    private static (int First, int Last) Partition<T>(List<T> rows, int first, int last,
        Func<T, T, bool> before)
    {
        int middle = first + (last - first) / 2;
        Median(rows, first, middle, last - 1, before);
        int equalFirst = middle, equalLast = middle + 1;
        while (first < equalFirst && !before(rows[equalFirst - 1], rows[equalFirst])
            && !before(rows[equalFirst], rows[equalFirst - 1])) equalFirst--;
        while (equalLast < last && !before(rows[equalLast], rows[equalFirst])
            && !before(rows[equalFirst], rows[equalLast])) equalLast++;
        int greaterFirst = equalLast, greaterLast = equalFirst;
        for (;;)
        {
            for (; greaterFirst < last; greaterFirst++)
            {
                if (before(rows[equalFirst], rows[greaterFirst])) continue;
                if (before(rows[greaterFirst], rows[equalFirst])) break;
                if (equalLast != greaterFirst) Swap(rows, equalLast, greaterFirst);
                equalLast++;
            }
            for (; first < greaterLast; greaterLast--)
            {
                if (before(rows[greaterLast - 1], rows[equalFirst])) continue;
                if (before(rows[equalFirst], rows[greaterLast - 1])) break;
                equalFirst--;
                if (equalFirst != greaterLast - 1) Swap(rows, equalFirst, greaterLast - 1);
            }
            if (greaterLast == first && greaterFirst == last) return (equalFirst, equalLast);
            if (greaterLast == first)
            {
                if (equalLast != greaterFirst) Swap(rows, equalFirst, equalLast);
                equalLast++;
                Swap(rows, equalFirst, greaterFirst);
                equalFirst++;
                greaterFirst++;
            }
            else if (greaterFirst == last)
            {
                greaterLast--;
                equalFirst--;
                if (greaterLast != equalFirst) Swap(rows, greaterLast, equalFirst);
                equalLast--;
                Swap(rows, equalFirst, equalLast);
            }
            else
            {
                greaterLast--;
                Swap(rows, greaterFirst, greaterLast);
                greaterFirst++;
            }
        }
    }

    private static void Median<T>(List<T> rows, int first, int middle, int last, Func<T, T, bool> before)
    {
        if (last - first > 40)
        {
            int step = (last - first + 1) / 8;
            SortThree(rows, first, first + step, first + step * 2, before);
            SortThree(rows, middle - step, middle, middle + step, before);
            SortThree(rows, last - step * 2, last - step, last, before);
            SortThree(rows, first + step, middle, last - step, before);
        }
        else SortThree(rows, first, middle, last, before);
    }

    private static void SortThree<T>(List<T> rows, int first, int middle, int last,
        Func<T, T, bool> before)
    {
        if (before(rows[middle], rows[first])) Swap(rows, middle, first);
        if (before(rows[last], rows[middle]))
        {
            Swap(rows, last, middle);
            if (before(rows[middle], rows[first])) Swap(rows, middle, first);
        }
    }

    private static void Heap<T>(List<T> rows, int first, int last, Func<T, T, bool> before)
    {
        int count = last - first;
        for (int parent = count / 2; parent > 0;)
        {
            parent--;
            AdjustHeap(rows, first, parent, count, rows[first + parent], before);
        }
        for (int end = last - 1; end > first; end--)
        {
            T value = rows[end];
            rows[end] = rows[first];
            AdjustHeap(rows, first, 0, end - first, value, before);
        }
    }

    private static void AdjustHeap<T>(List<T> rows, int first, int hole, int count, T value,
        Func<T, T, bool> before)
    {
        int top = hole, lastParent = (count - 1) / 2;
        while (hole < lastParent)
        {
            int child = hole * 2 + 2;
            if (before(rows[first + child], rows[first + child - 1])) child--;
            rows[first + hole] = rows[first + child];
            hole = child;
        }
        if (hole == lastParent && (count & 1) == 0)
        {
            rows[first + hole] = rows[first + count - 1];
            hole = count - 1;
        }
        while (top < hole)
        {
            int parent = (hole - 1) / 2;
            if (!before(rows[first + parent], value)) break;
            rows[first + hole] = rows[first + parent];
            hole = parent;
        }
        rows[first + hole] = value;
    }

    private static void Insert<T>(IList<T> rows, int start, int count, Func<T, T, bool> before)
    {
        for (int index = start + 1; index < start + count; index++)
        {
            T value = rows[index];
            if (before(value, rows[start]))
            {
                for (int at = index; at > start; at--) rows[at] = rows[at - 1];
                rows[start] = value;
            }
            else
            {
                int at = index;
                while (before(value, rows[at - 1])) { rows[at] = rows[at - 1]; at--; }
                rows[at] = value;
            }
        }
    }

    private static void SortRuns<T>(List<T> rows, int start, int count, T[] buffer,
        Func<T, T, bool> before)
    {
        for (int at = 0; at < count; at += 32) Insert(rows, start + at, Math.Min(32, count - at), before);
        for (int run = 32; run < count; run *= 4)
        {
            MergeRuns(rows, start, buffer, 0, count, run, before);
            MergeRuns(buffer, 0, rows, start, count, run * 2, before);
        }
    }

    private static void MergeRuns<T>(IList<T> source, int from, IList<T> target, int to,
        int count, int run, Func<T, T, bool> before)
    {
        for (int offset = 0; offset < count; offset += run * 2)
        {
            int middle = from + Math.Min(offset + run, count), end = from + Math.Min(offset + run * 2, count);
            Forward(source, from + offset, middle, source, middle, end, target, to + offset, false, before);
        }
    }

    private static void Forward<T>(IList<T> left, int at, int end, IList<T> right, int other,
        int otherEnd, IList<T> output, int write, bool rightAlreadyInPlace, Func<T, T, bool> before)
    {
        while (at < end && other < otherEnd)
            output[write++] = before(right[other], left[at]) ? right[other++] : left[at++];
        while (at < end) output[write++] = left[at++];
        if (!rightAlreadyInPlace) while (other < otherEnd) output[write++] = right[other++];
    }

    private static void Swap<T>(List<T> rows, int left, int right) =>
        (rows[left], rows[right]) = (rows[right], rows[left]);
}
