namespace San14.Data;

public sealed record OriginalObjectPairModelResult(int PairCount, int ModelAllocations);

public sealed partial class OriginalEventInitializationContext
{
    public Func<OriginalUnitManagerReference, OriginalObjectReference, OriginalObjectReference, OriginalUnitModelReference?>? ObjectPairModelAllocator { get; set; }
    public Action<OriginalUnitModelReference>? ObjectPairModelInitializer { get; set; }
    public Action<OriginalUnitModelReference, OriginalObjectReference, OriginalObjectReference>? ObjectPairModelConfigurator { get; set; }
    public Func<int, bool?>? ObjectPairVectorReservation { get; set; }

    /// <summary>Complete274B00 business path. Output contains Hex pointers;
    /// invalid Object leaves the preexisting output untouched.</summary>
    public int PopulateObjectNeighborHexes(OriginalObjectReference obj, OriginalHexListHandle output)
    {
        if (!PredicateKnown(ReadObjectNativeValidity(obj), "neighbor source object validity")) return 0;
        var record = ReadPhysicalObject(MapPhysical(obj.PhysicalSlot, 3001, "neighbor source object"))!;
        byte kind = record.RawField12;
        ushort raw = record.RawField10;
        var center = UnitModelContext().ReadHexTableReference(raw <= 48399 ? raw : 0)
            ?? throw new InvalidDataException("Current object neighbor center is unknown.");
        //All nonnull CHex receivers have slot3=true. Its result is ignored here;
        //a null center still reaches the later unsafe physical getter.
        ClearObjectPairWrapper(output);
        OriginalHexListHandle inner = AllocateObjectPairTemporary();
        _ = OriginalBaseNeighborhoodInitialization.PopulateRadiusOne(center, inner, this, () => ClearObjectPairWrapper(inner));
        var node = ObjectPairHexHead(inner);
        var seen = new HashSet<OriginalHexListNodeReference>();
        while (node.Identity.HasValue)
        {
            if (!seen.Add(node)) throw new InvalidDataException("Object neighbor radius list contains a cycle.");
            var hex = ReadHexListNodePayload(node) ?? throw new InvalidDataException("Current neighbor Hex payload is unknown.");
            var neighbor = ReadObjectAtHex(hex);
            if (PredicateKnown(ReadObjectNativeValidity(neighbor), "neighbor object validity") &&
                ReadPhysicalObject(MapPhysical(neighbor.PhysicalSlot, 3001, "neighbor object"))!.RawField12 == kind)
                CopyObjectPairHexNode(output, node);
            node = ReadHexListNodeNext(node) ?? throw new InvalidDataException("Current radius list next is unknown.");
        }
        int key = RegistryKey(new(output.Identity));
        int count = _hexListCounts.GetValueOrDefault(key) ?? throw new InvalidDataException("Current neighbor output count is unknown.");
        ReleaseObjectPairTemporary(inner);
        return count;
    }

    /// <summary>Complete1C0870 business sequence. Model allocation and actual
    /// slot2/slot7 resources remain explicit host responsibilities.</summary>
    public OriginalObjectPairModelResult InitializeObjectPairModels()
    {
        var pairs = new List<(OriginalObjectReference First, OriginalObjectReference Second)>();
        int allocatedModels = 0;
        var context = UnitModelContext();
        var node = ResolveFreshUnitModelHead(OriginalRegistryKind.Object, context);
        var seenRegistry = new HashSet<OriginalRegistryListNodeReference>();
        while (node.Identity.HasValue)
        {
            if (!seenRegistry.Add(node)) throw new InvalidDataException("Object-pair registry contains a cycle.");
            var payload = ReadUnitModelPayload(node, OriginalRegistryKind.Object);
            var obj = new OriginalObjectReference(payload.PhysicalSlot);
            byte kind = ReadPhysicalObject(MapPhysical(obj.PhysicalSlot, 3001, "pair source object"))?.RawField12
                ?? throw new InvalidDataException("Current pair source object kind12 is unknown.");
            var definition = context.ReadObjectDefinitionReference(kind <= 30 ? kind : 0)
                ?? throw new InvalidDataException("Current pair definition pointer is unknown.");
            if (PredicateKnown(ReadObjectDefinitionValidity(definition), "pair definition validity") && definition.PhysicalSlot is 9 or 10 or 16)
            {
                var temporary = AllocateObjectPairTemporary();
                int count = PopulateObjectNeighborHexes(obj, temporary);
                if (count > 0)
                {
                    var candidateNode = ObjectPairHexHead(temporary);
                    var seenCandidates = new HashSet<OriginalHexListNodeReference>();
                    while (candidateNode.Identity.HasValue)
                    {
                        if (!seenCandidates.Add(candidateNode)) throw new InvalidDataException("Object-pair neighbor list contains a cycle.");
                        var hex = ReadHexListNodePayload(candidateNode) ?? throw new InvalidDataException("Current pair Hex payload is unknown.");
                        var candidate = ReadObjectAtHex(hex);
                        if (PredicateKnown(ReadObjectNativeValidity(candidate), "pair candidate validity") &&
                            !pairs.Any(pair => (pair.First == obj && pair.Second == candidate) || (pair.First == candidate && pair.Second == obj)))
                        {
                            var manager = ReadCurrentUnitManager();
                            if (!manager.Identity.HasValue) throw new InvalidDataException("Current pair-model manager is null.");
                            var model = (ObjectPairModelAllocator is null ? AllocateRegisteredObjectPair(manager) : ObjectPairModelAllocator(manager, obj, candidate))
                                ?? throw new InvalidDataException("Current pair-model allocation result is unknown.");
                            if (model.Identity is int identity)
                            {
                                CheckOpaque(identity, "pair-model identity");
                                if (ObjectPairModelInitializer is null) ObjectPairModelOwner(model).Initialize(model);
                                else ObjectPairModelInitializer(model);
                                if (ObjectPairModelConfigurator is null) ObjectPairModelOwner(model).ConfigureObjectPair(model, obj, candidate);
                                else ObjectPairModelConfigurator(model, obj, candidate);
                                allocatedModels++;
                            }
                            //Allocation-null still commits the pair. Reservation
                            //happens after the model's two observable callbacks.
                            if (pairs.Count == pairs.Capacity)
                            {
                                int capacity = Math.Max(checked(pairs.Count + 1), checked(pairs.Capacity + pairs.Capacity / 2));
                                if (ObjectPairVectorReservation is not null && !PredicateKnown(ObjectPairVectorReservation(capacity), "pair vector reservation"))
                                    throw new InvalidDataException("Current pair vector reservation failed.");
                                pairs.Capacity = capacity;
                            }
                            pairs.Add((obj, candidate));
                        }
                        candidateNode = ReadHexListNodeNext(candidateNode) ?? throw new InvalidDataException("Current pair neighbor next is unknown.");
                    }
                }
                ReleaseObjectPairTemporary(temporary);
            }
            node = ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current object-pair registry next is unknown.");
            if (node.Identity.HasValue) context = UnitModelContext();
        }
        return new(pairs.Count, allocatedModels);
    }

    private OriginalObjectReference ReadObjectAtHex(OriginalHexReference hex)
    {
        ushort raw = PredicateKnown(ReadHexObjectLink(hex), "neighbor Hex1A");
        return UnitModelContext().ReadObjectTableReference(raw <= 3000 ? raw : 0)
            ?? throw new InvalidDataException("Current neighbor object pointer is unknown.");
    }
    private OriginalHexListHandle AllocateObjectPairTemporary()
    {
        ConditionalRegistryEnter();
        var temporary = CreateTemporaryHexList() ?? throw new InvalidDataException("Current pair temporary allocation is unknown.");
        ConditionalRegistryExit();
        return temporary;
    }
    private OriginalHexListNodeReference ObjectPairHexHead(OriginalHexListHandle handle)
    {
        bool pool = KnownPoolPresent(); //Both collectors save this before descriptor guards.
        if (!handle.Identity.HasValue || !pool) return new(null);
        int key = RegistryKey(new(handle.Identity));
        return UnitModelHeadsPresent() && NativeListKeyInRange(key) ? _hexListHeads.GetValueOrDefault(key)
            ?? throw new InvalidDataException("Current pair temporary head is unknown.") : new(null);
    }
    private void ClearObjectPairWrapper(OriginalHexListHandle handle)
    {
        if (!handle.Identity.HasValue || !KnownPoolPresent()) return;
        ConditionalRegistryEnter();
        if (KnownPoolPresent()) ClearObjectPairKey(RegistryKey(new(handle.Identity)));
        ConditionalRegistryExit();
    }
    private void ClearObjectPairKey(int key)
    { if (UnitModelHeadsPresent() && NativeListKeyInRange(key)) ClearAddressableHexList(key); }
    private void CopyObjectPairHexNode(OriginalHexListHandle output, OriginalHexListNodeReference source)
    {
        ConditionalRegistryEnter();
        //9104D0 reads source+0 after entering, before allocating the destination.
        var hex = ReadHexListNodePayload(source) ?? throw new InvalidDataException("Current copied Hex payload is unknown.");
        int key = RegistryKey(new(output.Identity));
        var destination = AllocateNativeListNode(key);
        if (destination.Identity.HasValue) SetHexListNodePayload(destination, hex);
        ConditionalRegistryExit();
    }
    private void ReleaseObjectPairTemporary(OriginalHexListHandle handle)
    {
        if (!handle.Identity.HasValue || !KnownPoolPresent()) return;
        ConditionalRegistryEnter();
        ClearObjectPairKey(RegistryKey(new(handle.Identity)));
        ReleaseTemporaryHexList(handle);
        ConditionalRegistryExit();
    }
}
