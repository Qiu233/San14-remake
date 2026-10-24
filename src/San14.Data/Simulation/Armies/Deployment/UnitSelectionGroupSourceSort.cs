namespace San14.Data;

/// <summary>7BCDF0/7BB320/7B88E0/7B96E0 and their heap fallback. Keeping the
/// original permutation matters: equal paths with different row keys are not
/// ordered by the comparator, and adjacent merging preserves a real first row.</summary>
internal static class UnitSelectionGroupSourceSort
{
    internal static void Sort<T>(T[] rows, Func<T, T, bool> before) =>
        Sort(rows, 0, rows.Length, rows.Length, before);

    private static void Sort<T>(T[] rows, int first, int last, int ideal, Func<T, T, bool> before)
    {
        while (true)
        {
            int count = last - first;
            if (count <= 32) { Insert(rows, first, last, before); return; }
            if (ideal < 1) { Heap(rows, first, last, before); return; }
            var middle = Partition(rows, first, last, before);
            ideal = ideal / 2 + ideal / 2 / 2;
            if (middle.First - first < last - middle.Last)
            {
                Sort(rows, first, middle.First, ideal, before); first = middle.Last;
            }
            else
            {
                Sort(rows, middle.Last, last, ideal, before); last = middle.First;
            }
        }
    }

    private static void Insert<T>(T[] rows, int first, int last, Func<T, T, bool> before)
    {
        for (int i = first + 1; i < last; i++)
        {
            T value = rows[i];
            if (before(value, rows[first]))
            {
                Array.Copy(rows, first, rows, first + 1, i - first); rows[first] = value;
            }
            else
            {
                int at = i;
                while (before(value, rows[at - 1])) { rows[at] = rows[at - 1]; at--; }
                rows[at] = value;
            }
        }
    }

    private static (int First, int Last) Partition<T>(T[] rows, int first, int last, Func<T, T, bool> before)
    {
        int middle = first + (last - first) / 2;
        GuessMedian(rows, first, middle, last - 1, before);
        int equalFirst = middle, equalLast = middle + 1;
        while (first < equalFirst && !before(rows[equalFirst - 1], rows[equalFirst]) &&
            !before(rows[equalFirst], rows[equalFirst - 1])) equalFirst--;
        while (equalLast < last && !before(rows[equalLast], rows[equalFirst]) &&
            !before(rows[equalFirst], rows[equalLast])) equalLast++;
        int scanFirst = equalLast, scanLast = equalFirst;
        while (true)
        {
            while (scanFirst < last)
            {
                if (before(rows[equalFirst], rows[scanFirst])) { scanFirst++; continue; }
                if (before(rows[scanFirst], rows[equalFirst])) break;
                Swap(rows, equalLast, scanFirst); equalLast++; scanFirst++;
            }
            while (first < scanLast)
            {
                int at = scanLast - 1;
                if (before(rows[at], rows[equalFirst])) { scanLast--; continue; }
                if (before(rows[equalFirst], rows[at])) break;
                equalFirst--; Swap(rows, equalFirst, at); scanLast--;
            }
            if (scanLast == first && scanFirst == last) return (equalFirst, equalLast);
            if (scanLast == first)
            {
                if (equalLast != scanFirst) Swap(rows, equalFirst, equalLast);
                Swap(rows, equalFirst, scanFirst); equalFirst++; equalLast++; scanFirst++;
            }
            else if (scanFirst == last)
            {
                scanLast--; equalFirst--; Swap(rows, scanLast, equalFirst);
                equalLast--; Swap(rows, equalFirst, equalLast);
            }
            else { scanLast--; Swap(rows, scanFirst, scanLast); scanFirst++; }
        }
    }

    private static void GuessMedian<T>(T[] rows, int first, int middle, int last, Func<T, T, bool> before)
    {
        if (last - first > 40)
        {
            int step = (last - first + 1) / 8;
            Median(rows, first, first + step, first + 2 * step, before);
            Median(rows, middle - step, middle, middle + step, before);
            Median(rows, last - 2 * step, last - step, last, before);
            Median(rows, first + step, middle, last - step, before);
        }
        else Median(rows, first, middle, last, before);
    }

    private static void Median<T>(T[] rows, int first, int middle, int last, Func<T, T, bool> before)
    {
        if (before(rows[middle], rows[first])) Swap(rows, middle, first);
        if (before(rows[last], rows[middle]))
        {
            Swap(rows, last, middle);
            if (before(rows[middle], rows[first])) Swap(rows, middle, first);
        }
    }

    private static void Heap<T>(T[] rows, int first, int last, Func<T, T, bool> before)
    {
        int count = last - first;
        for (int parent = count / 2; parent > 0;)
        {
            parent--; T value = rows[first + parent];
            AdjustHeap(rows, first, parent, count, value, before);
        }
        for (int end = count - 1; end > 0; end--)
        {
            T value = rows[first + end]; rows[first + end] = rows[first];
            AdjustHeap(rows, first, 0, end, value, before);
        }
    }

    private static void AdjustHeap<T>(T[] rows, int first, int hole, int count, T value, Func<T, T, bool> before)
    {
        int top = hole, lastParent = (count - 1) / 2;
        while (hole < lastParent)
        {
            int child = hole * 2 + 2;
            if (before(rows[first + child], rows[first + child - 1])) child--;
            rows[first + hole] = rows[first + child]; hole = child;
        }
        if (hole == lastParent && count % 2 == 0)
        {
            rows[first + hole] = rows[first + count - 1]; hole = count - 1;
        }
        while (top < hole)
        {
            int parent = (hole - 1) / 2;
            if (!before(rows[first + parent], value)) break;
            rows[first + hole] = rows[first + parent]; hole = parent;
        }
        rows[first + hole] = value;
    }

    private static void Swap<T>(T[] rows, int first, int second) =>
        (rows[first], rows[second]) = (rows[second], rows[first]);
}
