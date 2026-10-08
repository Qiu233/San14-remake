namespace San14.Data;

public sealed record OriginalUnitManagerReference(int? Identity);
public sealed record OriginalUnitModelReference(int? Identity);
public sealed record OriginalUnitModelPostLoadResult(int MapWrites, int ModelPointerWrites);

public sealed partial class OriginalEventInitializationContext
{
    private readonly Dictionary<int, ushort?> _armyUnitHexes2A = [], _hexRawValues1C = [];
    private readonly Dictionary<OriginalRegistryObjectReference, OriginalUnitModelReference?> _unitModelPointers = [];
    public OriginalUnitManagerReference? CurrentUnitManager { get; set; }
    public Func<OriginalUnitManagerReference?>? UnitManagerReader { get; set; }
    public Func<OriginalEventInitializationContext?>? UnitModelListContextReader { get; set; }
    /// <summary>Optional2C8720 override. Null uses the built-in current query;
    /// true skips the map write and army factory.</summary>
    public Func<OriginalArmyUnitReference, bool?>? UnitMapWritePredicate { get; set; }
    /// <summary>Optional231AB0 override; null uses the built-in query.</summary>
    public Func<OriginalAreaReference, bool?>? AreaUnitModelSkipPredicate { get; set; }
    public Func<OriginalObjectReference, bool?>? ObjectUnitModelPredicate { get; set; }
    /// <summary>Optional whole-factory override. Null uses the registered
    /// CUnitManager business implementation and its required resource host.</summary>
    public Func<OriginalUnitManagerReference, OriginalRegistryObjectReference, OriginalUnitModelReference?>? UnitModelFactory { get; set; }
    /// <summary>Optional1C0870 override. Null uses the built-in pair stage.</summary>
    public Action? ObjectPairUnitModelPostProcessor { get; set; }

    public void SetArmyUnitRawHex2A(int physical, ushort? value)
    { Check(physical, 501, "army raw2A receiver"); _armyUnitHexes2A[physical] = value; }
    public ushort? ReadArmyUnitRawHex2A(OriginalArmyUnitReference army)
        => _armyUnitHexes2A.GetValueOrDefault(MapPhysical(army.PhysicalSlot, 501, "army raw2A"));
    public ushort? ReadHexRaw1C(OriginalHexReference hex)
        => _hexRawValues1C.GetValueOrDefault(MapPhysical(hex.PhysicalSlot, 48400, "hex raw1C"));
    public void SetHexRaw1C(int physical, ushort? value)
    { Check(physical, 48400, "hex raw1C receiver"); _hexRawValues1C[physical] = value; }
    public OriginalUnitModelReference? ReadUnitModelPointer(OriginalRegistryObjectReference receiver)
    { _ = UnitModelPhysical(receiver); return _unitModelPointers.GetValueOrDefault(receiver); }
    public void SetUnitModelPointer(OriginalRegistryObjectReference receiver, OriginalUnitModelReference? model)
    { _ = UnitModelPhysical(receiver); if (model?.Identity is int identity) CheckOpaque(identity, "unit-model pointer"); _unitModelPointers[receiver] = model; }

    /// <summary>Direct32EE00 sequence with explicit remaining predicates,
    /// renderer factories and pair processing. Never changes Core readiness.</summary>
    public OriginalUnitModelPostLoadResult ApplyUnitModelPostLoad()
    {
        int mapWrites = ApplyUnitMapWritePrepass(); //Unconditional2ED210.
        var manager = ReadCurrentUnitManager();
        if (!manager.Identity.HasValue) return new(mapWrites, 0);
        int modelWrites = 0;
        UnitModelListSnapshot snapshot = CaptureUnitModelLists();
        foreach (var kind in new[] { OriginalRegistryKind.Army, OriginalRegistryKind.Area, OriginalRegistryKind.City,
            OriginalRegistryKind.Gate, OriginalRegistryKind.Object })
        {
            var node = ResolveUnitModelHead(snapshot, kind);
            bool nonempty = node.Identity.HasValue;
            var visited = new HashSet<OriginalRegistryListNodeReference>();
            while (node.Identity.HasValue)
            {
                if (!visited.Add(node)) throw new InvalidDataException("Current unit-model registry contains a cycle.");
                var receiver = ReadUnitModelPayload(node, kind);
                if (AttachUnitModel(receiver)) modelWrites++;
                node = ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current unit-model next is unknown.");
            }
            //Only the first four nonempty lists refresh globals for the next
            //list. Object goes straight to1C0870 without another snapshot.
            if (nonempty && kind != OriginalRegistryKind.Object) snapshot = CaptureUnitModelLists();
        }
        if (ObjectPairUnitModelPostProcessor is not null) ObjectPairUnitModelPostProcessor();
        else _ = InitializeObjectPairModels();
        var scenery = ResolveFreshUnitModelHead(OriginalRegistryKind.Scenery);
        var seenScenery = new HashSet<OriginalRegistryListNodeReference>();
        while (scenery.Identity.HasValue)
        {
            if (!seenScenery.Add(scenery)) throw new InvalidDataException("Current scenery-model registry contains a cycle.");
            if (AttachUnitModel(ReadUnitModelPayload(scenery, OriginalRegistryKind.Scenery))) modelWrites++;
            scenery = ReadRegistryNodeNext(scenery) ?? throw new InvalidDataException("Current scenery-model next is unknown.");
        }
        return new(mapWrites, modelWrites);
    }

    public int ApplyUnitMapWritePrepass()
    {
        int writes = 0;
        var node = ResolveFreshUnitModelHead(OriginalRegistryKind.Army);
        var seen = new HashSet<OriginalRegistryListNodeReference>();
        while (node.Identity.HasValue)
        {
            if (!seen.Add(node)) throw new InvalidDataException("Current army map-write registry contains a cycle.");
            var receiver = ReadUnitModelPayload(node, OriginalRegistryKind.Army);
            var army = new OriginalArmyUnitReference(receiver.PhysicalSlot);
            if (!QueryArmyUnitMapPredicate(army))
            {
                ushort raw = ReadArmyUnitRawHex2A(army) ?? throw new InvalidDataException("Current army raw2A is unknown.");
                int physical = MapPhysical(army.PhysicalSlot, 501, "army physical identity");
                var hex = UnitModelContext().ReadHexTableReference(raw <= 48399 ? raw : 0)
                    ?? throw new InvalidDataException("Current army map-write hex pointer is unknown.");
                if (hex.PhysicalSlot.HasValue) { SetHexRaw1C(MapPhysical(hex.PhysicalSlot, 48400, "army map-write hex"), (ushort)Math.Clamp(physical, 1, 500)); writes++; }
            }
            node = ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current army map-write next is unknown.");
        }
        return writes;
    }

    public bool IsAreaUnitModelTerrain17(OriginalAreaReference area)
    {
        ushort raw = ReadAreaHexIndex(area) ?? throw new InvalidDataException("Current area raw3A is unknown.");
        var hex = UnitModelContext().ReadHexTableReference(raw <= 48399 ? raw : 0)
            ?? throw new InvalidDataException("Current area terrain hex pointer is unknown.");
        return (ReadHexSurfaceCode(hex) ?? throw new InvalidDataException("Current area terrain hex10 is unknown.")) == 17;
    }
    private bool AttachUnitModel(OriginalRegistryObjectReference receiver)
    {
        int physical = UnitModelPhysical(receiver);
        switch (receiver.Kind)
        {
            case OriginalRegistryKind.Army:
                if (QueryArmyUnitMapPredicate(new(physical))) return false;
                break;
            case OriginalRegistryKind.Area:
                if (physical < 1) return false;
                var area = new OriginalAreaReference(physical);
                byte citySlot = ReadAreaCitySlot(area) ?? throw new InvalidDataException("Current area city35 is unknown.");
                var city = UnitModelContext().ReadCityTableReference(citySlot <= 51 ? citySlot : 0)
                    ?? throw new InvalidDataException("Current area model city pointer is unknown.");
                if (!(ReadBaseNativeValidity(city) ?? throw new InvalidDataException("Current area model city validity is unknown."))) return false;
                if ((AreaUnitModelSkipPredicate is null ? IsAreaUnitModelSkipped(area) : AreaUnitModelSkipPredicate(area))
                    ?? throw new InvalidDataException("Current area model skip predicate is unknown.")) return false;
                if (IsAreaUnitModelTerrain17(area)) return false;
                break;
            case OriginalRegistryKind.City or OriginalRegistryKind.Gate:
                if (physical < 1) return false;
                break;
            case OriginalRegistryKind.Object:
                if (!((ObjectUnitModelPredicate is null ? IsObjectUnitModelVisible(new(physical)) : ObjectUnitModelPredicate(new(physical)))
                    ?? throw new InvalidDataException("Current object model predicate is unknown."))) return false;
                break;
            case OriginalRegistryKind.Scenery:
                if (physical < 1) return false;
                byte flags = ReadSceneryFlags47(new(physical)) ?? throw new InvalidDataException("Current scenery flags47 is unknown.");
                if ((flags & 1) == 0) return false;
                break;
        }
        var manager = ReadCurrentUnitManager(); //Each qualifying helper reads fresh2145480.
        if (!manager.Identity.HasValue) throw new InvalidDataException("Current unit-model factory manager is null.");
        var model = (UnitModelFactory is null ? GetUnitManager(manager).Create(receiver) : UnitModelFactory(manager, receiver))
            ?? throw new InvalidDataException("Current unit-model factory result is unknown.");
        SetUnitModelPointer(receiver, model); //Known-null allocation is still stored.
        return true;
    }

    private sealed record UnitModelListSnapshot(OriginalEventInitializationContext Context, int Capacity, bool Heads, bool Pool);
    private UnitModelListSnapshot CaptureUnitModelLists()
    {
        var context = UnitModelContext();
        int capacity = Profile().ListKeyCapacity ?? throw new InvalidDataException("Current unit-model list capacity is unknown.");
        if (capacity < 0) throw new InvalidDataException("Negative unit-model list capacity.");
        bool heads = UnitModelHeadsPresent();
        bool pool = KnownPoolPresent();
        return new(context, capacity, heads, pool);
    }
    private OriginalRegistryListNodeReference ResolveUnitModelHead(UnitModelListSnapshot snapshot, OriginalRegistryKind kind)
    {
        var descriptor = snapshot.Context.ReadRegistryDescriptor(kind) ?? throw new InvalidDataException("Current unit-model descriptor is unknown.");
        if (!descriptor.Identity.HasValue || !snapshot.Pool) return new(null);
        int key = RegistryKey(descriptor);
        return snapshot.Heads && unchecked((uint)key) < (uint)snapshot.Capacity ? UnitModelStoredHead(key) : new(null);
    }
    private OriginalRegistryListNodeReference ResolveFreshUnitModelHead(OriginalRegistryKind kind, OriginalEventInitializationContext? context = null)
    {
        var descriptor = (context ?? UnitModelContext()).ReadRegistryDescriptor(kind) ?? throw new InvalidDataException("Current fresh unit-model descriptor is unknown.");
        if (!descriptor.Identity.HasValue || !KnownPoolPresent()) return new(null);
        int key = RegistryKey(descriptor); //2ED210/final150 save key before the head table.
        return UnitModelHeadsPresent() && NativeListKeyInRange(key) ? UnitModelStoredHead(key) : new(null);
    }
    private OriginalRegistryListNodeReference UnitModelStoredHead(int key)
    {
        var node = _hexListHeads.GetValueOrDefault(key) ?? throw new InvalidDataException("Current unit-model list head is unknown.");
        return new(node.Identity);
    }
    private bool UnitModelHeadsPresent() => (RegistryHeadTablePresenceReader is null ? RegistryHeadTablePresent : RegistryHeadTablePresenceReader())
        ?? throw new InvalidDataException("Current unit-model head table is unknown.");
    private OriginalEventInitializationContext UnitModelContext() => UnitModelListContextReader is null ? this :
        UnitModelListContextReader() ?? throw new InvalidDataException("Current unit-model registry context is unknown.");
    private OriginalUnitManagerReference ReadCurrentUnitManager()
    {
        var manager = (UnitManagerReader is null ? CurrentUnitManager : UnitManagerReader())
            ?? throw new InvalidDataException("Current CUnitManager pointer is unknown.");
        if (manager.Identity is int identity) CheckOpaque(identity, "unit manager");
        return manager;
    }
    private bool QueryArmyUnitMapPredicate(OriginalArmyUnitReference army) =>
        (UnitMapWritePredicate is null ? IsArmyUnitMapWriteSkipped(army) : UnitMapWritePredicate(army))
        ?? throw new InvalidDataException("Current army map-write predicate is unknown.");
    private OriginalRegistryObjectReference ReadUnitModelPayload(OriginalRegistryListNodeReference node, OriginalRegistryKind kind)
    {
        var payload = ReadRegistryNodePayload(node) ?? throw new InvalidDataException("Current unit-model payload is unknown.");
        if (payload.Kind != kind) throw new InvalidDataException("Current unit-model payload kind contradicts its registry.");
        return payload;
    }
    private static int UnitModelPhysical(OriginalRegistryObjectReference receiver) => MapPhysical(receiver.PhysicalSlot, receiver.Kind switch
    { OriginalRegistryKind.Army or OriginalRegistryKind.Area => 501, OriginalRegistryKind.City => 52,
        OriginalRegistryKind.Gate => 11, OriginalRegistryKind.Object => 3001, OriginalRegistryKind.Scenery => 101,
        _ => throw new InvalidDataException("Unsupported unit-model payload kind.") }, "unit-model receiver");
}
