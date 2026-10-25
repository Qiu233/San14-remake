using System.Buffers.Binary;

namespace San14.Data;

/// <summary>An allocated source slot. Bytes beyond the initialized header must
/// be written by the actual ordered birth program before a renderer reads them.</summary>
public sealed class G1EffectParticleSlot
{
    internal G1EffectParticleSlot(G1EffectParticlePool.Block block, int index, Memory<byte> bytes)
    { Block = block; Index = index; Bytes = bytes; }
    internal G1EffectParticlePool.Block Block { get; }
    internal LinkedListNode<G1EffectParticleSlot>? LiveNode;
    public int Index { get; }
    public Memory<byte> Bytes { get; }
    public bool IsAllocated => LiveNode is not null;
}

/// <summary>The original generic's block/slot ownership, separate from birth
/// writers and generic update dispatch. Storage must actually be allocated by
/// the module arena owner; this pool does not synthesize allocation success.</summary>
public sealed class G1EffectParticlePool(G1EffectParticleDeclaration declaration)
{
    internal sealed class Block(G1EffectParticlePool owner, Memory<byte> storage, bool retained)
    {
        internal readonly G1EffectParticlePool Owner = owner;
        internal readonly Memory<byte> Storage = storage;
        internal readonly bool Retained = retained;
        internal readonly Stack<G1EffectParticleSlot> Free = new();
        internal readonly LinkedList<G1EffectParticleSlot> Live = new();
        internal int Capacity;
    }

    private readonly List<Block> blocks = [];
    public G1EffectParticleDeclaration Declaration => declaration;
    public int LiveCount { get; private set; }
    public int BlockCount => blocks.Count;
    public int Capacity { get; private set; }

    /// <summary>Register actual arena storage. Expandable blocks include the
    /// source 48-byte header and use every whole slot that fits; fixed blocks
    /// retain exactly the declared capacity. The initial free order is ascending.</summary>
    public void AddAllocatedBlock(Memory<byte> storage, bool retained = false)
    {
        if (declaration.UsesDirectBirth)
            throw new NotSupportedException("Direct-birth generic does not allocate ordinary particle slots.");
        if (blocks.Count != 0 && !declaration.UsesExpandableBlocks)
            throw new InvalidOperationException("Fixed particle pool cannot grow another block.");
        int count = declaration.UsesExpandableBlocks
            ? (storage.Length - 48) / declaration.Stride : declaration.InitialCapacity;
        if (count < 1 || count > ushort.MaxValue || storage.Length < checked(48 + count * declaration.Stride))
            throw new ArgumentException("Allocated particle block does not contain the required slots.", nameof(storage));
        var block = new Block(this, storage, retained) { Capacity = count };
        for (int index = count - 1; index >= 0; index--)
        {
            var bytes = storage.Slice(48 + index * declaration.Stride, declaration.Stride);
            declaration.Layout.Span[..32].CopyTo(bytes.Span);
            block.Free.Push(new(block, index, bytes));
        }
        blocks.Add(block); Capacity += count;
    }

    /// <summary>Scan blocks in source order, take the free head, then prepend
    /// to the active list. This allocates a slot; it does not execute birth.</summary>
    public G1EffectParticleSlot? TryAcquire()
    {
        if (declaration.UsesDirectBirth) return null;
        foreach (var block in blocks)
        {
            if (!block.Free.TryPop(out var slot)) continue;
            BinaryPrimitives.WriteUInt16LittleEndian(slot.Bytes.Span[6..], 7);
            slot.LiveNode = block.Live.AddFirst(slot);
            LiveCount++;
            return slot;
        }
        return null;
    }

    public void Release(G1EffectParticleSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        if (slot.Block.Owner != this || slot.LiveNode is null)
            throw new InvalidOperationException("Particle slot is not allocated by this pool.");
        var block = slot.Block;
        block.Live.Remove(slot.LiveNode); slot.LiveNode = null;
        BinaryPrimitives.WriteUInt16LittleEndian(slot.Bytes.Span[6..], 0);
        block.Free.Push(slot); LiveCount--;
    }

    /// <summary>Visit each block's actual active list in its native head order.
    /// A consumer may release its current slot during iteration.</summary>
    public IEnumerable<G1EffectParticleSlot> EnumerateAllocated()
    {
        foreach (var block in blocks)
        {
            var current = block.Live.First;
            while (current is not null)
            {
                var next = current.Next;
                yield return current.Value;
                current = next;
            }
        }
    }

    /// <summary>After the real generic update, remove at most the last eligible
    /// empty block using the source free-capacity threshold. Active slots are
    /// never discarded, and a retained arena block cannot be pruned.</summary>
    public bool PruneEmptyBlock(out Memory<byte> releasedStorage)
    {
        releasedStorage = default;
        Block? candidate = null;
        int free = 0;
        foreach (var block in blocks)
        {
            free += block.Capacity - block.Live.Count;
            if (block.Live.Count == 0 && !block.Retained) candidate = block;
        }
        if (candidate is null || blocks.Count > 1 && candidate.Capacity >= (free - candidate.Capacity) * 2)
            return false;
        blocks.Remove(candidate); Capacity -= candidate.Capacity;
        releasedStorage = candidate.Storage;
        return true;
    }

    /// <summary>Detach storage only when its generic owner retires. Every
    /// outstanding slot becomes invalid before the arena receives its blocks.</summary>
    public IReadOnlyList<Memory<byte>> DetachAllBlocks()
    {
        var storage = new List<Memory<byte>>(blocks.Count);
        foreach (var block in blocks)
        {
            foreach (var slot in block.Live) slot.LiveNode = null;
            block.Live.Clear(); block.Free.Clear(); storage.Add(block.Storage);
        }
        blocks.Clear(); Capacity = 0; LiveCount = 0;
        return storage.AsReadOnly();
    }
}
