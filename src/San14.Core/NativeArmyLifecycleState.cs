namespace San14.Core;

/// <summary>Pure owned opaque identities. No Data object/delegate or foreign
/// pointer is serialized. Null payload is unknown; typed null Identity is a
/// known null receiver. Free nodes retain their previous payload.</summary>
public sealed record NativeSharedListHandle(int? Identity);
public sealed record NativeSharedListPayload(string Kind, int? Identity);
public sealed record NativeSharedListNode(int? Next, int? Previous, NativeSharedListPayload? Payload);
public sealed record NativeSharedListDescriptor(int? Head, int? Tail, int? Count);
public sealed record NativeSharedListPoolState(int DescriptorCapacity, int NodeCapacity, int ActiveNodeCount,
    int CreatedNodeCount, int? FreeNodeHead, int? NullDescriptorCount, int[] FreeDescriptors, int[] LiveDescriptors,
    Dictionary<int, NativeSharedListDescriptor> Descriptors, Dictionary<int, NativeSharedListNode> Nodes);
public enum NativeArmyBaseKind { City, Gate }
public sealed record NativeArmyBaseListBinding(NativeArmyBaseKind Kind, int PhysicalSlot,
    NativeSharedListHandle? HexDescriptor, NativeSharedListHandle? Person88Descriptor);
/// <summary>Complete pointer-table order and current receiver header. Pending
/// image is retained independently after its active gameplay projection leaves
/// Units; this is not authority that original header cleanup already ran.</summary>
public sealed record NativeArmyLifecycleSlot(int PhysicalSlot, ushort? Commander12, byte? Status10,
    ushort? Priority14, int? ActiveUnitId, UnitState? PendingImage, int? Gold5C, int? Supplies60,
    ushort[]? AuxiliaryAC, ushort[]? AuxiliaryF4, NativeSharedListHandle[]? Trait160Descriptors)
{
    /// <summary>Free receiver reset/current source writes. Active/Pending
    /// images keep their own mutable raw fields; a missing reset is unknown.</summary>
    public NativeArmyOverrideTargetRuntime? OverrideTarget { get; init; }
    //Free receiver current value; active/Pending images own their direction.
    public byte? Direction36 { get; init; }
    public byte? MapUiByte13C { get; init; }
    public byte? CommandControl140 { get; init; }
    public byte? RouteHold56 { get; init; }
    public ushort? RouteGoal1F8 { get; init; }
}
public sealed partial record NativeArmyLifecycleState(string Revision, string Language, string ExecutableSha256,
    string SourceScenarioUri, string SourceScenarioSha256, NativeSharedListPoolState Pool,
    NativeArmyLifecycleSlot[] Slots, Dictionary<string, NativeSharedListHandle?> RegistryDescriptors,
    NativeSharedListHandle? Pending70Descriptor, NativeSharedListHandle? BaseRegistryDescriptor, NativeArmyBaseListBinding[] BaseLists,
    Dictionary<int, ushort?> HexRaw1C);
public sealed partial record GameState
{
    public NativeArmyLifecycleState? NativeArmyLifecycle { get; init; }
}

public static class NativeArmyLifecycleStateValidation
{
    public static void Validate(GameState state)
    {
        if (state.NativeArmyLifecycle is not { } lifecycle) return;
        if (string.IsNullOrWhiteSpace(lifecycle.Revision) || lifecycle.Language is not ("SC" or "TC") ||
            !Hash(lifecycle.ExecutableSha256) || !Hash(lifecycle.SourceScenarioSha256) ||
            string.IsNullOrWhiteSpace(lifecycle.SourceScenarioUri) ||
            state.ContentHashes.GetValueOrDefault(lifecycle.SourceScenarioUri) != lifecycle.SourceScenarioSha256 ||
            state.NativeArmySimulation?.Parameters is not { } parameters || parameters.Language != lifecycle.Language ||
            parameters.ExecutableSha256 != lifecycle.ExecutableSha256 || lifecycle.Slots is not { Length: > 0 } ||
            lifecycle.RegistryDescriptors is null || lifecycle.BaseLists is null || lifecycle.Pool is null || lifecycle.HexRaw1C is null ||
            lifecycle.HexRaw1C.Any(x => x.Key < 0 || x.Key >= (long)state.MapWidth * state.MapHeight))
            throw new InvalidDataException("Native Army lifecycle has missing or contradictory source ownership.");
        ValidatePool(lifecycle.Pool);
        NativeArmyLifecycleAuxiliariesStateValidation.Validate(lifecycle);
        NativeArmyDestructionQueueStateValidation.Validate(lifecycle);
        NativeArmyDestructorFlagsStateValidation.Validate(lifecycle);
        NativeArmyLifecycleTargetStateValidation.Validate(lifecycle);
        var liveDescriptors = lifecycle.Pool.LiveDescriptors.ToHashSet();
        if (lifecycle.ChangedLists is { } changed)
        {
            if (string.IsNullOrWhiteSpace(changed.Revision) || changed.Work8 is null || changed.Pending18 is null ||
                changed.Work8.Identity == changed.Pending18.Identity)
                throw new InvalidDataException("Native changed Army list owners are incomplete or alias each other.");
            Descriptor(changed.Work8, liveDescriptors); Descriptor(changed.Pending18, liveDescriptors);
        }
        var units = new HashSet<int>(); var bases = new HashSet<(NativeArmyBaseKind,int)>();
        for (int i = 0; i < lifecycle.Slots.Length; i++)
        {
            var slot = lifecycle.Slots[i];
            if (slot is null || slot.PhysicalSlot != i || slot.ActiveUnitId is < 0 ||
                slot.AuxiliaryAC is { Length: not 36 } || slot.AuxiliaryF4 is { Length: not 36 } ||
                slot.Trait160Descriptors is { Length: not 9 } || slot.Trait160Descriptors?.Any(x => x is null) == true ||
                slot.ActiveUnitId is not null && slot.PendingImage is not null)
                throw new InvalidDataException("Native Army pointer-table/current slot image is incomplete or malformed.");
            if (slot.ActiveUnitId is int id)
            {
                if (!units.Add(id) || !state.Units.TryGetValue(id, out var army) || army.NativeArmyOrigin?.NativeSlotIndex != i ||
                    slot.Commander12 != army.OfficerId || slot.Status10 != army.NativeTacticRuntime?.Status10)
                    throw new InvalidDataException("Native Army active slot does not identify its current owned unit/header.");
            }
            if (slot.PendingImage is { } pending &&
                (state.Units.ContainsKey(pending.Id) || !units.Add(pending.Id) || pending.NativeArmyOrigin?.NativeSlotIndex != i ||
                    slot.Commander12 != pending.OfficerId || slot.Status10 != pending.NativeTacticRuntime?.Status10))
                throw new InvalidDataException("Native Army pending image aliases an active projection or a different source slot.");
            if (slot.PendingImage is { } image) ValidatePendingImage(state,image);
            if (slot.Trait160Descriptors is { } descriptors)
                foreach (var key in descriptors) Descriptor(key, liveDescriptors);
        }
        foreach (var army in state.Units.Values)
            if (army.NativeArmyOrigin is not null && !units.Contains(army.Id))
                throw new InvalidDataException("Source-created unit has no actual complete-pool lifecycle owner.");
        foreach (var pair in lifecycle.RegistryDescriptors)
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) throw new InvalidDataException("Native registry has an empty identity kind.");
            Descriptor(pair.Value, liveDescriptors);
        }
        Descriptor(lifecycle.Pending70Descriptor, liveDescriptors); Descriptor(lifecycle.BaseRegistryDescriptor, liveDescriptors);
        foreach (var binding in lifecycle.BaseLists)
        {
            if (binding is null || binding.Kind is not (NativeArmyBaseKind.City or NativeArmyBaseKind.Gate) || binding.PhysicalSlot < 0 ||
                !bases.Add((binding.Kind, binding.PhysicalSlot))) throw new InvalidDataException("Native Base list binding has duplicate or invalid identity.");
            Descriptor(binding.HexDescriptor, liveDescriptors); Descriptor(binding.Person88Descriptor, liveDescriptors);
        }
    }
    private static void ValidatePendingImage(GameState state,UnitState image)
    {
        //A pending receiver may have zero ready soldiers and its commander is
        //already home/dead. Do not impose active Deployed/positive-troop gates.
        //The image still needs valid value domains and saved module ownership.
        long size=(long)state.MapWidth*state.MapHeight;
        if (image.Id<0 || !state.Forces.ContainsKey(image.ForceId) || !state.Officers.ContainsKey(image.OfficerId) ||
            image.HexId<0 || image.HexId>=size || image.DestinationHexId<0 || image.DestinationHexId>=size ||
            image.Troops<0 || image.Supplies<0 ||
            image.SourceLocationId is int source && !state.Cities.ContainsKey(source) && !state.Gates.ContainsKey(source) ||
            image.FormationId is int formation && !state.Formations.ContainsKey(formation) ||
            image.TacticIds is { } tactics && (tactics.Distinct().Count()!=tactics.Length || tactics.Any(t=>!state.Tactics.ContainsKey(t))) ||
            image.Behavior is { } behavior && !Enum.IsDefined(behavior.AfterAction) ||
            image.Mission is { } mission && (!Enum.IsDefined(mission.Kind) || !Enum.IsDefined(mission.Arrival) || mission.CargoGold<0))
            throw new InvalidDataException("Native pending Army image has invalid quantities, locations or typed references.");
        //Validation-only view: reuse existing source/module validators on this
        //one stored image. It is never published or treated as an active world.
        var view=state with { Units=new() { [image.Id]=image } };
        NativeSubdayStateValidation.Validate(view);
        NativeArmyMarchStateValidation.Validate(view);
        PlayableMilitaryStateValidation.Validate(view);
        PlayableTacticStateValidation.Validate(view);
    }
    public static void ValidatePool(NativeSharedListPoolState pool)
    {
        if (pool.DescriptorCapacity <= 0 || pool.NodeCapacity < 0 || pool.NullDescriptorCount is < 0 || pool.CreatedNodeCount < 0 || pool.CreatedNodeCount > pool.NodeCapacity ||
            pool.ActiveNodeCount < 0 || pool.ActiveNodeCount > pool.CreatedNodeCount || pool.FreeDescriptors is null || pool.LiveDescriptors is null ||
            pool.Descriptors is null || pool.Nodes is null || pool.Descriptors.Count != pool.DescriptorCapacity || pool.Nodes.Count != pool.CreatedNodeCount ||
            pool.Descriptors.Any(x => x.Value is null || x.Value.Count is < 0))
            throw new InvalidDataException("Native shared pool has invalid capacity, descriptors or allocation bookkeeping.");
        var liveDescriptors = pool.LiveDescriptors.ToHashSet();
        var freeDescriptors = pool.FreeDescriptors.ToHashSet();
        if (liveDescriptors.Count != pool.LiveDescriptors.Length || pool.FreeDescriptors.Any(k => k < 0 || k >= pool.DescriptorCapacity) ||
            pool.LiveDescriptors.Any(k => k < 0 || k >= pool.DescriptorCapacity) ||
            Enumerable.Range(0,pool.DescriptorCapacity).Any(k => !pool.Descriptors.ContainsKey(k) ||
                !liveDescriptors.Contains(k) && !freeDescriptors.Contains(k)))
            throw new InvalidDataException("Native shared pool has invalid capacity, descriptors or allocation bookkeeping.");
        var liveNodes = new HashSet<int>(); int total = 0;
        foreach (int key in pool.LiveDescriptors)
        {
            var row = pool.Descriptors[key] ?? throw new InvalidDataException("Native descriptor row is unknown.");
            if (row.Count is not int count || count < 0) throw new InvalidDataException("Native live descriptor count is unknown or negative.");
            int? node = row.Head, previous = null; int observed = 0;
            while (node is int id)
            {
                if (!liveNodes.Add(id) || !pool.Nodes.TryGetValue(id,out var value) || value is null || value.Previous != previous)
                    throw new InvalidDataException("Native live chain has a cycle, shared node, missing field or invalid previous pointer.");
                previous = id; node = value.Next; observed++;
            }
            if (previous != row.Tail || count != observed) throw new InvalidDataException("Native live chain count/tail is inconsistent.");
            total = checked(total + observed);
        }
        var free = new HashSet<int>(); int? at = pool.FreeNodeHead;
        while (at is int id)
        {
            if (!free.Add(id) || liveNodes.Contains(id) || !pool.Nodes.TryGetValue(id,out var node) || node is null)
                throw new InvalidDataException("Native free chain aliases live storage, has a cycle or lacks a node.");
            at = node.Next;
        }
        if (total != pool.ActiveNodeCount || liveNodes.Count + free.Count != pool.CreatedNodeCount ||
            pool.Nodes.Any(x => x.Key <= 0 || x.Key > pool.CreatedNodeCount || x.Value is null ||
                x.Value.Payload is { } payload && (string.IsNullOrWhiteSpace(payload.Kind) || payload.Identity is < 0)))
            throw new InvalidDataException("Native node counts or pure payload identities are inconsistent.");
    }
    private static void Descriptor(NativeSharedListHandle? key, HashSet<int> liveDescriptors)
    { if (key?.Identity is int value && !liveDescriptors.Contains(value)) throw new InvalidDataException("Native current descriptor does not belong to the saved live pool."); }
    private static bool Hash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

