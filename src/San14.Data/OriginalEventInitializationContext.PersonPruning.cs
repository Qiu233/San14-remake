namespace San14.Data;

public sealed record OriginalRegisteredPersonPruningResult(int CapturedCount, int RemovedCount);

public sealed partial class OriginalEventInitializationContext
{
    /// <summary>Optional explicit replacement for the871E0 container stage.
    /// The normal path uses the current associated container and payloads.</summary>
    public Action? AssociatedObjectPostProcessor { get; set; }

    /// <summary>324D00 after its43 table calls. Captures first, then performs
    /// fresh pointer searches;335AE0 removes all matching nodes per invocation.</summary>
    public OriginalRegisteredPersonPruningResult PostProcessRegisteredPersons()
    {
        ConditionalRegistryEnter();
        OriginalHexListHandle temporary = CreateTemporaryHexList()
            ?? throw new InvalidDataException("Current person pruning temporary descriptor is unknown.");
        ConditionalRegistryExit();
        int captured = 0, removed = 0;
        OriginalRegistryListNodeReference node = PersonPruningHead();
        while (node.Identity.HasValue)
        {
            OriginalRegistryObjectReference person = PruningPayload(node);
            byte status = ReadRegistryPersonStatus(person)
                ?? throw new InvalidDataException("Current person status11E is unknown at pruning.");
            if (status is not (>= 1 and <= 6) && status != 8)
            {
                ConditionalRegistryEnter();
                OriginalRegistryListNodeReference allocated = AllocateRegistryNode(new(temporary.Identity))
                    ?? throw new InvalidDataException("Current person pruning node allocation is unknown.");
                if (allocated.Identity.HasValue) { WriteRegistryNodePayload(allocated, person); captured++; }
                ConditionalRegistryExit();
            }
            node = PruningNext(node);
        }
        node = ReadRegistryHead(new(temporary.Identity))
            ?? throw new InvalidDataException("Current captured person list head is unknown.");
        while (node.Identity.HasValue)
        {
            OriginalRegistryObjectReference saved = PruningPayload(node);
            OriginalRegistryListNodeReference current = PersonPruningHead();
            while (current.Identity.HasValue)
            {
                if (PruningPayload(current) == saved)
                {
                    removed += RemoveAllRegisteredPersonReferences(saved);
                    break;
                }
                current = PruningNext(current);
            }
            node = PruningNext(node);
        }
        if (AssociatedObjectPostProcessor is not null) AssociatedObjectPostProcessor();
        else _ = PostProcessAssociatedObjects();
        OriginalStartupWorldReference world = ReadStaticCompletionWorld()
            ?? throw new InvalidDataException("Current post-load world slot7 receiver is unknown.");
        if (!world.Identity.HasValue) throw new InvalidDataException("Current post-load world slot7 receiver is null.");
        // The supported CWorldData slot7 target370500 returns without field writes.
        ConditionalRegistryEnter();
        ClearTemporaryHexList(temporary);
        ReleaseTemporaryHexList(temporary);
        ConditionalRegistryExit();
        return new(captured, removed);
    }

    public int RemoveAllRegisteredPersonReferences(OriginalRegistryObjectReference saved)
    {
        ConditionalRegistryEnter();
        OriginalRegistryListHandle descriptor = ReadRegistryDescriptor(OriginalRegistryKind.Person)
            ?? throw new InvalidDataException("Current locked person descriptor100 is unknown.");
        int key = RegistryKey(descriptor);
        //335AE0 guards the head table and unsigned key, without a pool-presence test.
        bool heads = (RegistryHeadTablePresenceReader is null ? RegistryHeadTablePresent : RegistryHeadTablePresenceReader())
            ?? throw new InvalidDataException("Current locked person head table is unknown.");
        OriginalRegistryListNodeReference node = heads && NativeListKeyInRange(key)
            ? ReadHexListHead(new(key)) is { } head ? new(head.Identity)
                : throw new InvalidDataException("Current locked person head is unknown.")
            : new(null);
        int removed = 0;
        while (node.Identity.HasValue)
        {
            if (PruningPayload(node) == saved)
            {
                OriginalRegistryListNodeReference next = PruningNext(node);
                RemoveAddressableRegistryNode(key, node);
                removed++;
                node = next;
            }
            else node = PruningNext(node);
        }
        ConditionalRegistryExit();
        return removed;
    }

    private void RemoveAddressableRegistryNode(int key, OriginalRegistryListNodeReference node)
        => _orderedLists.Remove(key, NodeIdentity(new(node.Identity)));
    private OriginalRegistryListNodeReference PersonPruningHead()
        => ReadRegistryHead(ReadRegistryDescriptor(OriginalRegistryKind.Person)
            ?? throw new InvalidDataException("Current person descriptor100 is unknown at pruning."))
            ?? throw new InvalidDataException("Current registered person head is unknown at pruning.");
    private OriginalRegistryObjectReference PruningPayload(OriginalRegistryListNodeReference node)
        => ReadRegistryNodePayload(node) ?? throw new InvalidDataException("Current pruning payload pointer is unknown.");
    private OriginalRegistryListNodeReference PruningNext(OriginalRegistryListNodeReference node)
        => ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current pruning live next is unknown.");
    private void ConditionalRegistryEnter()
    { if (ReadRegistryLockEnabled() ?? throw new InvalidDataException("Current pruning lock flag is unknown.")) EnterRegistryLock(); }
    private void ConditionalRegistryExit()
    { if (ReadRegistryLockEnabled() ?? throw new InvalidDataException("Current pruning unlock flag is unknown.")) ExitRegistryLock(); }
}
