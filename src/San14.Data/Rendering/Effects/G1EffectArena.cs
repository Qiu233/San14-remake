namespace San14.Data;

/// <summary>The context's bounded particle arena and the source's separate
/// standalone pool allocations. Shared growth never falls back to the heap.</summary>
public sealed class G1EffectArena : IDisposable
{
    private sealed class Region(int offset, int size, Region? parent = null)
    {
        internal readonly int Offset = offset;
        internal readonly int Size = size;
        internal readonly Region? Parent = parent;
        internal Region? Left, Right;
        internal bool Allocated;
    }

    private sealed record Allocation(G1EffectParticlePool Pool, Region? Region, int HeapBytes);
    private readonly object gate = new();
    private Memory<byte> storage;
    private readonly Region? root;
    private readonly Dictionary<Memory<byte>, Allocation> allocations = [];
    private bool disposed;

    private G1EffectArena(Memory<byte> storage)
    {
        this.storage = storage;
        if (!storage.IsEmpty) root = new(0, storage.Length);
    }

    public int CapacityBytes => storage.Length;
    public int AllocatedBytes { get; private set; }
    public long StandaloneAllocatedBytes { get; private set; }

    /// <summary>Allocate the actual context region once. A zero request disables
    /// shared allocation; nonzero requests round up to a power of two, at least
    /// 1024 bytes. The owner's 64-byte allocation header is outside that budget.</summary>
    public static bool TryCreate(int requestedBytes, out G1EffectArena? arena)
    {
        if (requestedBytes < 0) throw new ArgumentOutOfRangeException(nameof(requestedBytes));
        arena = null;
        if (requestedBytes == 0) { arena = new(Memory<byte>.Empty); return true; }
        int size = RoundBlockBytes(requestedBytes);
        try
        {
            var owner = new byte[checked(size + 64)];
            arena = new(owner.AsMemory(64, size));
            return true;
        }
        catch (OutOfMemoryException) { return false; }
    }

    /// <summary>Resource initialization's eager standalone pool. A lazy flag or
    /// zero capacity deliberately leaves no allocated block.</summary>
    public bool InitializePool(G1EffectParticlePool pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var declaration = pool.Declaration;
            if (declaration.UsesDirectBirth || declaration.InitialCapacity == 0 ||
                (declaration.Flags & 0x40) != 0 || pool.BlockCount != 0) return true;
            return AllocateStandalone(pool);
        }
    }

    /// <summary>Ensure an actual free slot block is present. This does not take a
    /// slot or execute its birth program. Lazy first allocation uses the bounded
    /// context arena even when the pool's later growth uses standalone storage.</summary>
    public bool TryAllocateParticleBlock(G1EffectParticlePool pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (pool.Declaration.UsesDirectBirth) return false;
            if (pool.Capacity > pool.LiveCount) return true;
            return pool.BlockCount == 0 ? AllocateShared(pool) : Grow(pool);
        }
    }

    public bool TryGrow(G1EffectParticlePool pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return Grow(pool);
        }
    }

    private bool Grow(G1EffectParticlePool pool)
    {
        if (pool.Declaration.UsesDirectBirth || !pool.Declaration.UsesExpandableBlocks ||
            pool.BlockCount == 0) return false;
        return pool.Declaration.UsesSharedArenaGrowth ? AllocateShared(pool) : AllocateStandalone(pool);
    }

    private bool AllocateShared(G1EffectParticlePool pool)
    {
        if (root is null) return false;
        int bytes = pool.Declaration.RequiredExpandableBlockBytes();
        var region = Reserve(root, bytes);
        if (region is null) return false;
        Memory<byte> block = storage.Slice(region.Offset, region.Size);
        try { pool.AddAllocatedBlock(block); }
        catch { Free(region); throw; }
        allocations.Add(block, new(pool, region, 0));
        AllocatedBytes += region.Size;
        return true;
    }

    private bool AllocateStandalone(G1EffectParticlePool pool)
    {
        int payload = checked(pool.Declaration.Stride * pool.Declaration.InitialCapacity);
        int blockBytes = checked(payload + 48), heapBytes = checked(blockBytes + 64);
        byte[] owner;
        // This is the source's separate allocator path, not an arena-overflow
        // fallback. Return failure when real managed storage cannot be obtained.
        try { owner = new byte[heapBytes]; }
        catch (OutOfMemoryException) { return false; }
        Memory<byte> block = owner.AsMemory(64, blockBytes);
        pool.AddAllocatedBlock(block, retained: true);
        allocations.Add(block, new(pool, null, heapBytes));
        StandaloneAllocatedBytes += heapBytes;
        return true;
    }

    private static Region? Reserve(Region region, int bytes)
    {
        if (region.Allocated || bytes > region.Size) return null;
        if (region.Left is not null)
            return Reserve(region.Left, bytes) ?? Reserve(region.Right!, bytes);
        if (region.Size == bytes) { region.Allocated = true; return region; }
        int half = region.Size / 2;
        region.Left = new(region.Offset, half, region);
        region.Right = new(region.Offset + half, half, region);
        return Reserve(region.Left, bytes);
    }

    private static void Free(Region region)
    {
        region.Allocated = false;
        while (region.Parent is { } parent)
        {
            if (parent.Left is not { Allocated: false, Left: null } ||
                parent.Right is not { Allocated: false, Left: null }) break;
            parent.Left = parent.Right = null;
            region = parent;
        }
    }

    internal static int RoundBlockBytes(int bytes)
    {
        int size = 1024;
        while (size < bytes) size = checked(size * 2);
        return size;
    }

    /// <summary>Return a block after the pool has detached it. This releases its
    /// arena reservation or standalone owner; it does not kill live particles.</summary>
    public void Return(Memory<byte> storageToReturn)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!allocations.Remove(storageToReturn, out var allocation))
                throw new InvalidOperationException("Particle block is not owned by this arena.");
            if (allocation.Region is { } region)
            {
                Free(region);
                AllocatedBytes -= region.Size;
            }
            else StandaloneAllocatedBytes -= allocation.HeapBytes;
        }
    }

    /// <summary>Call after actual generic retirement has released its external
    /// particle resources. Invalidates slots and returns all backing storage.</summary>
    public void ReleasePool(G1EffectParticlePool pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            foreach (var block in pool.DetachAllBlocks()) Return(block);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            if (allocations.Count != 0)
                throw new InvalidOperationException("Retire effect particle pools before disposing their arena.");
            storage = default;
            disposed = true;
        }
    }
}
