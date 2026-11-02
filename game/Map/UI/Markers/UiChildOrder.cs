/// <summary>Retains a UI owner's actual child order and moves only children
/// outside a longest common subsequence of the requested order.</summary>
internal sealed class UiChildOrder<T>(Action<T,int> move) where T : class
{
    private readonly List<T> current = [];
    private readonly Dictionary<T,int> positions = new(ReferenceEqualityComparer.Instance);
    private int[] tails = [], previous = [];
    private bool[] retained = [];

    internal void Add(T child)
    {
        positions.Add(child,current.Count);
        current.Add(child);
    }

    internal void Remove(T child)
    {
        if(!positions.Remove(child,out int index))return;
        current.RemoveAt(index);
        for(int i=index;i<current.Count;i++)positions[current[i]]=i;
    }

    internal void Apply(IReadOnlyList<T> desired)
    {
        int count=desired.Count;
        if(count!=current.Count)throw new InvalidOperationException("UI child order has an unmatched owner mutation.");
        bool unchanged=true;
        for(int i=0;i<count;i++)
            if(!ReferenceEquals(current[i],desired[i])){unchanged=false;break;}
        if(unchanged)return;
        if(tails.Length<count)
        {
            Array.Resize(ref tails,count);
            Array.Resize(ref previous,count);
            Array.Resize(ref retained,count);
        }
        Array.Clear(retained,0,count);
        int length=0;
        for(int i=0;i<count;i++)
        {
            int position=positions[desired[i]],low=0,high=length;
            while(low<high)
            {
                int middle=low+(high-low)/2;
                if(positions[desired[tails[middle]]]<position)low=middle+1;
                else high=middle;
            }
            previous[i]=low==0?-1:tails[low-1];
            tails[low]=i;
            if(low==length)length++;
        }
        for(int index=tails[length-1];index>=0;index=previous[index])retained[index]=true;
        for(int i=count-1;i>=0;i--)
        {
            if(retained[i])continue;
            T child=desired[i];
            int from=positions[child];
            int destination=i+1==count?count:positions[desired[i+1]];
            if(from<destination)destination--;
            if(from==destination)continue;
            move(child,destination);
            current.RemoveAt(from);
            current.Insert(destination,child);
            for(int index=Math.Min(from,destination);index<=Math.Max(from,destination);index++)
                positions[current[index]]=index;
        }
    }
}
