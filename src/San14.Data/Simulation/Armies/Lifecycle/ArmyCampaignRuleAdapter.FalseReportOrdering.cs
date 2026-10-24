namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //24A270/248D10:16-byte candidate rows, with caller-owned lazy comparator.
    //No extra identity tie-break, stable-sort substitution or cached abilities.
    private static void FSort(List<(int Physical, int Score)> candidates,
        Func<(int Physical, int Score), (int Physical, int Score), bool> before)
    {
        Sort(0, candidates.Count, candidates.Count);

        void Sort(int first, int last, int budget)
        {
            while (true)
            {
                int count = last - first;
                if (count <= 32)
                {
                    if (count > 1) Insertion(first, last);
                    return;
                }
                if (budget < 1)
                {
                    MakeHeap(first, last);
                    SortHeap(first, last);
                    return;
                }
                var equal = Partition(first, last);
                budget = budget / 2 + (budget / 2) / 2;
                //The equal-size case recurses on the right and loops left.
                if (equal.First - first < last - equal.Last)
                {
                    Sort(first, equal.First, budget);
                    first = equal.Last;
                }
                else
                {
                    Sort(equal.Last, last, budget);
                    last = equal.First;
                }
            }
        }

        //247B20 tests against the first row before entering the reverse scan.
        void Insertion(int first, int last)
        {
            for (int next = first + 1; next < last; next++)
            {
                var value = candidates[next];
                int hole = next;
                if (before(value, candidates[first]))
                {
                    do { candidates[hole] = candidates[hole - 1]; hole--; } while (hole != first);
                }
                else
                {
                    while (before(value, candidates[hole - 1]))
                    {
                        candidates[hole] = candidates[hole - 1];
                        hole--;
                    }
                }
                candidates[hole] = value;
            }
        }

        //248310 preserves all three conditional comparator calls, including
        //the final middle/first call only after last/middle succeeded.
        void MedianThree(int first, int middle, int last)
        {
            if (before(candidates[middle], candidates[first])) Swap(first, middle);
            if (before(candidates[last], candidates[middle]))
            {
                Swap(middle, last);
                if (before(candidates[middle], candidates[first])) Swap(first, middle);
            }
        }

        void GuessMedian(int first, int middle, int last)
        {
            //245C10 receives an inclusive last row. Its >40 span means
            //ninther begins at42 rows; the step uses (span+1)/8.
            if (last - first > 40)
            {
                int step = (last - first + 1) / 8;
                MedianThree(first, first + step, first + step * 2);
                MedianThree(middle - step, middle, middle + step);
                MedianThree(last - step * 2, last - step, last);
                MedianThree(first + step, middle, last - step);
            }
            else MedianThree(first, middle, last);
        }

        (int First, int Last) Partition(int first, int last)
        {
            int pivotFirst = first + (last - first) / 2;
            GuessMedian(first, pivotFirst, last - 1);
            int pivotLast = pivotFirst + 1;
            while (first < pivotFirst &&
                !before(candidates[pivotFirst - 1], candidates[pivotFirst]) &&
                !before(candidates[pivotFirst], candidates[pivotFirst - 1])) pivotFirst--;
            while (pivotLast < last &&
                !before(candidates[pivotLast], candidates[pivotFirst]) &&
                !before(candidates[pivotFirst], candidates[pivotLast])) pivotLast++;
            int forward = pivotLast, backward = pivotFirst;
            while (true)
            {
                for (; forward < last; forward++)
                {
                    if (before(candidates[pivotFirst], candidates[forward])) continue;
                    if (before(candidates[forward], candidates[pivotFirst])) break;
                    if (pivotLast != forward) Swap(pivotLast, forward);
                    pivotLast++;
                }
                for (; first < backward; backward--)
                {
                    int previous = backward - 1;
                    if (before(candidates[previous], candidates[pivotFirst])) continue;
                    if (before(candidates[pivotFirst], candidates[previous])) break;
                    pivotFirst--;
                    if (pivotFirst != previous) Swap(pivotFirst, previous);
                }
                if (backward == first)
                {
                    if (forward == last) return (pivotFirst, pivotLast);
                    if (pivotLast != forward && pivotFirst != pivotLast) Swap(pivotFirst, pivotLast);
                    pivotLast++;
                    Swap(pivotFirst, forward);
                    pivotFirst++;
                    forward++;
                }
                else if (forward == last)
                {
                    backward--;
                    pivotFirst--;
                    Swap(backward, pivotFirst);
                    pivotLast--;
                    Swap(pivotFirst, pivotLast);
                }
                else
                {
                    backward--;
                    Swap(forward, backward);
                    forward++;
                }
            }
        }

        //247EC0 and249ED0 use the same249C00 push and2495A0 hole descent.
        void MakeHeap(int first, int last)
        {
            int count = last - first;
            for (int parent = count / 2; parent > 0;)
            {
                parent--;
                var value = candidates[first + parent];
                AdjustHeap(first, parent, count, value);
            }
        }

        void SortHeap(int first, int last)
        {
            while (last - first > 1)
            {
                last--;
                var value = candidates[last];
                candidates[last] = candidates[first];
                AdjustHeap(first, 0, last - first, value);
            }
        }

        void AdjustHeap(int first, int hole, int count, (int Physical, int Score) value)
        {
            int top = hole;
            int lastParent = (count - 1) / 2;
            while (hole < lastParent)
            {
                int right = hole * 2 + 2;
                //Compare right before left. Equivalence selects right.
                int child = before(candidates[first + right], candidates[first + right - 1])
                    ? right - 1 : right;
                candidates[first + hole] = candidates[first + child];
                hole = child;
            }
            if (hole == lastParent && (count & 1) == 0)
            {
                candidates[first + hole] = candidates[first + count - 1];
                hole = count - 1;
            }
            while (top < hole)
            {
                int parent = (hole - 1) / 2;
                if (!before(candidates[first + parent], value)) break;
                candidates[first + hole] = candidates[first + parent];
                hole = parent;
            }
            candidates[first + hole] = value;
        }

        void Swap(int left, int right)
        {
            if (left == right) return;
            var value = candidates[left];
            candidates[left] = candidates[right];
            candidates[right] = value;
        }
    }
}
