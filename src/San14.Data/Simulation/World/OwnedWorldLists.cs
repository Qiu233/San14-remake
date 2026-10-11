namespace San14.Data;

public enum OwnedWorldCpuState { Unknown, Null, Present }
public enum OwnedWorldRegistration { AlreadyPresent, ReplacedStale, Appended, UnknownExistingCpu, AllocationFailed }

/// <summary>A Data identity local to one managed world. Present only describes
/// the Data+20 null gate; it supplies no CPU type, light or rendering receipt.</summary>
public sealed class OwnedWorldData
{
    internal OwnedWorldData(OwnedWorldLists owner, long identity, OwnedWorldCpuState cpuState)
    { Owner = owner; Identity = identity; CpuState = cpuState; }
    internal OwnedWorldLists Owner { get; }
    public long Identity { get; }
    public OwnedWorldCpuState CpuState { get; internal set; }
    /// <summary>References retained by these lists only, not native packed refcount.</summary>
    public int ListReferences { get; internal set; }
}

public sealed record OwnedWorldListEntry(long DataIdentity, OwnedWorldCpuState CpuState);
public sealed record OwnedWorldListSnapshot(long Frame, uint ObjectId, string ResourceUri,
    string DecodedSha256, int Capacity, IReadOnlyList<OwnedWorldListEntry?> Entries);

/// <summary>Managed ListContainer membership, preserving append order, pointer
/// equality and CPU-null stale replacement. It does not execute actor callbacks,
/// native destruction, an allocator, FrameSync scheduling or render publication.</summary>
public sealed class OwnedWorldLists
{
    private sealed class ListState(OwnedWorldListDeclaration declaration)
    {
        public OwnedWorldListDeclaration Declaration { get; } = declaration;
        public List<OwnedWorldData?> Entries { get; } = new(declaration.InitialCapacity);
        public int Capacity { get; set; } = declaration.InitialCapacity;
    }

    private readonly object gate = new();
    private readonly Dictionary<uint, ListState> lists;
    private readonly uint[] clearNames;
    private readonly int maximumCapacity;
    private readonly string definitionResourceUri, definitionSha256;
    private long nextDataIdentity;
    private long frame;

    public OwnedWorldLists(OwnedWorldListSource source, long firstFrame = 0, int maximumCapacity = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (firstFrame < 0) throw new ArgumentOutOfRangeException(nameof(firstFrame));
        if (maximumCapacity < source.Declarations.Max(d => d.InitialCapacity))
            throw new ArgumentOutOfRangeException(nameof(maximumCapacity));
        lists = source.Declarations.ToDictionary(d => d.ObjectId, d => new ListState(d));
        clearNames = source.FrameSyncClearNames.ToArray();
        definitionResourceUri = source.DefinitionResourceUri;
        definitionSha256 = source.DefinitionSha256;
        this.maximumCapacity = maximumCapacity; frame = firstFrame;
    }

    public OwnedWorldData CreateData(OwnedWorldCpuState cpuState)
    {
        if (!Enum.IsDefined(cpuState)) throw new ArgumentOutOfRangeException(nameof(cpuState));
        lock (gate) return new(this, checked(++nextDataIdentity), cpuState);
    }

    public void SetCpuState(OwnedWorldData data, OwnedWorldCpuState cpuState)
    {
        if (!Enum.IsDefined(cpuState)) throw new ArgumentOutOfRangeException(nameof(cpuState));
        lock (gate) { RequireData(data); data.CpuState = cpuState; }
    }

    /// <summary>B45A40/B45C30: append every supplied Data in order, including
    /// duplicate and null pointers. Requires a nonempty batch. Capacity is kept
    /// after clear. maximumCapacity is a managed allocation guard, not source cap.</summary>
    public bool Append(long expectedFrame, uint name, IReadOnlyList<OwnedWorldData?> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var batch = data.ToArray();
        if (batch.Length == 0) throw new ArgumentException("Empty native append is outside the supported positive-count scope.", nameof(data));
        lock (gate)
        {
            var list = RequireList(expectedFrame, name);
            foreach (var entry in batch) if (entry is not null) RequireData(entry);
            if (!EnsureCapacity(list, batch.Length)) return false;
            foreach (var entry in batch)
            {
                list.Entries.Add(entry);
                if (entry is not null) entry.ListReferences++;
            }
            return true;
        }
    }

    /// <summary>B45A60: duplicate pointer wins; otherwise replace the first
    /// Data whose CPU is known null, or append. A reached null Data slot is not
    /// a stale CPU slot and is rejected at the native Data+20 dereference.</summary>
    public OwnedWorldRegistration Register(long expectedFrame, uint name, OwnedWorldData data)
    {
        lock (gate)
        {
            RequireData(data);
            var list = RequireList(expectedFrame, name);
            int firstStale = -1;
            for (int i = 0; i < list.Entries.Count; i++)
            {
                var entry = list.Entries[i];
                if (ReferenceEquals(entry, data)) return OwnedWorldRegistration.AlreadyPresent;
                // After the first CPU-null slot native code reads only Data
                // pointer equality, so later unknown CPUs are not reached.
                if (firstStale >= 0) continue;
                if (entry is null) throw new InvalidOperationException("Native registration cannot scan a null Data pointer.");
                if (entry.CpuState == OwnedWorldCpuState.Unknown) return OwnedWorldRegistration.UnknownExistingCpu;
                if (entry.CpuState == OwnedWorldCpuState.Null) firstStale = i;
            }
            if (firstStale >= 0)
            {
                list.Entries[firstStale]!.ListReferences--;
                list.Entries[firstStale] = data; data.ListReferences++;
                return OwnedWorldRegistration.ReplacedStale;
            }
            if (!EnsureCapacity(list, 1)) return OwnedWorldRegistration.AllocationFailed;
            list.Entries.Add(data); data.ListReferences++;
            return OwnedWorldRegistration.Appended;
        }
    }

    public OwnedWorldListSnapshot Snapshot(long expectedFrame, uint name)
    {
        lock (gate)
        {
            var list = RequireList(expectedFrame, name);
            var entries = list.Entries.Select(entry => entry is null ? null :
                new OwnedWorldListEntry(entry.Identity, entry.CpuState)).ToArray();
            return new(frame, name, definitionResourceUri,
                definitionSha256, list.Capacity, Array.AsReadOnly(entries));
        }
    }

    /// <summary>Explicit ListContainer.v40/B45DE0; release one retained list
    /// reference per occurrence, clear all used entries, keep array capacity.</summary>
    public void Clear(long expectedFrame, uint name)
    { lock (gate) Clear(RequireList(expectedFrame, name)); }

    /// <summary>Execute only the clear-name intersection of this owner and the
    /// audited FrameSync node. 204AB825 is outside this clear-name set;
    /// unrelated task calls and other lifecycle writers are omitted.</summary>
    public IReadOnlyList<uint> FrameSync(long expectedFrame, long nextFrame)
    {
        lock (gate)
        {
            RequireFrame(expectedFrame);
            if (nextFrame <= frame) throw new ArgumentOutOfRangeException(nameof(nextFrame));
            uint[] cleared = clearNames.Where(lists.ContainsKey).ToArray();
            foreach (uint name in cleared) Clear(lists[name]);
            frame = nextFrame;
            return Array.AsReadOnly(cleared);
        }
    }

    private bool EnsureCapacity(ListState list, int appendedCount)
    {
        long required = (long)list.Entries.Count + appendedCount;
        if (required <= list.Capacity) return true;
        // Source rounds the appended count, not the capacity deficit.
        long step = list.Declaration.GrowthStep;
        long capacity = list.Capacity + ((appendedCount + step - 1) / step) * step;
        if (capacity > maximumCapacity || capacity < required) return false;
        try { list.Entries.EnsureCapacity((int)capacity); }
        catch (OutOfMemoryException) { return false; }
        list.Capacity = (int)capacity;
        return true;
    }

    private static void Clear(ListState list)
    {
        foreach (var entry in list.Entries) if (entry is not null) entry.ListReferences--;
        list.Entries.Clear();
    }

    private ListState RequireList(long expectedFrame, uint name)
    {
        RequireFrame(expectedFrame);
        return lists.TryGetValue(name, out var list) ? list : throw new ArgumentOutOfRangeException(nameof(name));
    }
    private void RequireFrame(long expectedFrame)
    { if (frame != expectedFrame) throw new InvalidOperationException("World list operation uses a stale owned frame."); }
    private void RequireData(OwnedWorldData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!ReferenceEquals(data.Owner, this)) throw new ArgumentException("Data belongs to a different owned world.", nameof(data));
    }
}
