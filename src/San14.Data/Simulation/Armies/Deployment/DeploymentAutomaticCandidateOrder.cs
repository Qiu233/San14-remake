namespace San14.Data;

/// <summary>Automatic-draft candidate ordering, including the original
/// partition and heap tie permutations. The result contains input indices.</summary>
internal static class DeploymentAutomaticCandidateOrder
{
    internal static int[] Sort(IReadOnlyList<int> people, IReadOnlyList<int> scores)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(scores);
        if (people.Count != scores.Count) throw new ArgumentException("Candidate identities and scores must have equal lengths.");
        int[] order = Enumerable.Range(0, people.Count).ToArray();
        SortRange(0, order.Length, order.Length);
        return order;

        // Native pointer equality denotes the same Person. Its apparent ID
        // tiebreak compares that Person's ID with itself and is always false.
        bool Before(int left, int right) => people[left] != people[right] && scores[left] > scores[right];
        bool AtBefore(int left, int right) => Before(order[left], order[right]);
        void Swap(int left, int right)
        {
            if (left != right) (order[left], order[right]) = (order[right], order[left]);
        }
        void MedianThree(int first, int middle, int last)
        {
            if (AtBefore(middle, first)) Swap(middle, first);
            if (AtBefore(last, middle))
            {
                Swap(last, middle);
                if (AtBefore(middle, first)) Swap(middle, first);
            }
        }
        void GuessMedian(int first, int middle, int last)
        {
            // last is inclusive: native threshold is last-first > 40.
            if (last - first > 40)
            {
                int step = (last - first + 1) / 8;
                MedianThree(first, first + step, first + 2 * step);
                MedianThree(middle - step, middle, middle + step);
                MedianThree(last - 2 * step, last - step, last);
                MedianThree(first + step, middle, last - step);
            }
            else MedianThree(first, middle, last);
        }
        (int First, int Last) Partition(int first, int last)
        {
            int middle = first + (last - first) / 2;
            GuessMedian(first, middle, last - 1);
            int equalFirst = middle, equalLast = middle + 1;
            while (first < equalFirst && !AtBefore(equalFirst - 1, equalFirst) && !AtBefore(equalFirst, equalFirst - 1)) --equalFirst;
            while (equalLast < last && !AtBefore(equalLast, equalFirst) && !AtBefore(equalFirst, equalLast)) ++equalLast;
            int right = equalLast, left = equalFirst;
            while (true)
            {
                for (; right < last; ++right)
                {
                    if (AtBefore(equalFirst, right)) continue;
                    if (AtBefore(right, equalFirst)) break;
                    if (equalLast != right) Swap(equalLast, right);
                    ++equalLast;
                }
                for (; first < left; --left)
                {
                    if (AtBefore(left - 1, equalFirst)) continue;
                    if (AtBefore(equalFirst, left - 1)) break;
                    --equalFirst;
                    Swap(equalFirst, left - 1);
                }
                if (left == first && right == last) return (equalFirst, equalLast);
                if (left == first)
                {
                    if (equalLast != right) Swap(equalFirst, equalLast);
                    ++equalLast;
                    Swap(equalFirst, right);
                    ++equalFirst;
                    ++right;
                }
                else if (right == last)
                {
                    --left;
                    --equalFirst;
                    Swap(left, equalFirst);
                    --equalLast;
                    Swap(equalFirst, equalLast);
                }
                else
                {
                    --left;
                    Swap(right, left);
                    ++right;
                }
            }
        }
        void Insertion(int first, int last)
        {
            for (int next = first + 1; next < last; ++next)
            {
                int value = order[next], hole = next;
                if (Before(value, order[first]))
                {
                    while (hole > first) { order[hole] = order[hole - 1]; --hole; }
                }
                else
                {
                    while (hole > first && Before(value, order[hole - 1])) { order[hole] = order[hole - 1]; --hole; }
                }
                order[hole] = value;
            }
        }
        void AdjustHeap(int first, int top, int count, int value)
        {
            int hole = top, lastParent = (count - 1) / 2;
            while (hole < lastParent)
            {
                int child = hole * 2 + 2;
                if (AtBefore(first + child, first + child - 1)) --child;
                order[first + hole] = order[first + child];
                hole = child;
            }
            if (hole == lastParent && (count & 1) == 0)
            {
                order[first + hole] = order[first + count - 1];
                hole = count - 1;
            }
            while (top < hole)
            {
                int parent = (hole - 1) / 2;
                if (!Before(order[first + parent], value)) break;
                order[first + hole] = order[first + parent];
                hole = parent;
            }
            order[first + hole] = value;
        }
        void HeapSort(int first, int last)
        {
            int count = last - first;
            for (int parent = count / 2 - 1; parent >= 0; --parent)
            {
                int value = order[first + parent];
                AdjustHeap(first, parent, count, value);
            }
            for (int remaining = count - 1; remaining > 0; --remaining)
            {
                int value = order[first + remaining];
                order[first + remaining] = order[first];
                AdjustHeap(first, 0, remaining, value);
            }
        }
        void SortRange(int first, int last, int ideal)
        {
            while (last - first > 32)
            {
                if (ideal < 1) { HeapSort(first, last); return; }
                (int equalFirst, int equalLast) = Partition(first, last);
                ideal = ideal / 2 + (ideal / 2) / 2;
                if (equalFirst - first < last - equalLast)
                {
                    SortRange(first, equalFirst, ideal);
                    first = equalLast;
                }
                else
                {
                    SortRange(equalLast, last, ideal);
                    last = equalFirst;
                }
            }
            if (last - first > 1) Insertion(first, last);
        }
    }
}
