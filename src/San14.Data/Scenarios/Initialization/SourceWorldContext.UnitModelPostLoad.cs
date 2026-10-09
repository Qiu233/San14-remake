namespace San14.Data;

public sealed record UnitManagerReference(int? Identity);
public sealed record UnitModelReference(int? Identity);
public sealed record UnitModelPostLoadResult(int MapWrites, int ModelPointerWrites);

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, ushort?> _armyUnitHexes2A = [];
    private readonly Dictionary<RegistryObjectReference, UnitModelReference?> _unitModelPointers = [];
    public UnitManagerReference? CurrentUnitManager { get; set; }
    public Func<UnitManagerReference?>? UnitManagerReader { get; set; }
    public Func<SourceWorldContext?>? UnitModelListContextReader { get; set; }
    /// <summary>Optional2C8720 override. Null uses the built-in current query;
    /// true skips the map write and army factory.</summary>
    public Func<ArmyUnitReference, bool?>? UnitMapWritePredicate { get; set; }
    /// <summary>Optional231AB0 override; null uses the built-in query.</summary>
    public Func<AreaReference, bool?>? AreaUnitModelSkipPredicate { get; set; }
    public Func<ObjectReference, bool?>? ObjectUnitModelPredicate { get; set; }
    /// <summary>Optional whole-factory override. Null uses the registered
    /// CUnitManager business implementation and its required resource host.</summary>
    public Func<UnitManagerReference, RegistryObjectReference, UnitModelReference?>? UnitModelFactory { get; set; }
    /// <summary>Optional1C0870 override. Null uses the built-in pair stage.</summary>
    public Action? ObjectPairUnitModelPostProcessor { get; set; }

    public void SetArmyUnitRawHex2A(int physical, ushort? value)
    { Check(physical, 501, "army raw2A receiver"); _armyUnitHexes2A[physical] = value; }
    public ushort? ReadArmyUnitRawHex2A(ArmyUnitReference army)
        => _armyUnitHexes2A.GetValueOrDefault(MapPhysical(army.PhysicalSlot, 501, "army raw2A"));
    public ushort? ReadHexRaw1C(HexReference hex)
        => _armyMapOccupancy?.ReadSourceWord(MapPhysical(hex.PhysicalSlot, 48400, "hex raw1C"));
    public void SetHexRaw1C(int physical, ushort? value)
    { Check(physical, 48400, "hex raw1C receiver"); (_armyMapOccupancy ??= new()).WriteSourceWord(physical, value); }
    public UnitModelReference? ReadUnitModelPointer(RegistryObjectReference receiver)
    { _ = UnitModelPhysical(receiver); return _unitModelPointers.GetValueOrDefault(receiver); }
    public void SetUnitModelPointer(RegistryObjectReference receiver, UnitModelReference? model)
    { _ = UnitModelPhysical(receiver); if (model?.Identity is int identity) CheckOpaque(identity, "unit-model pointer"); _unitModelPointers[receiver] = model; }

    /// <summary>Direct unit-model post-load sequence with explicit remaining predicates,
    /// renderer factories and pair processing. Never changes Core readiness.</summary>
    public UnitModelPostLoadResult ApplyUnitModelPostLoad()
    {
        int mapWrites = ApplyUnitMapWritePrepass(); //Ordered overwrite; no global clear.
        var manager = ReadCurrentUnitManager();
        if (!manager.Identity.HasValue) return new(mapWrites, 0);
        int modelWrites = 0;
        UnitModelListSnapshot snapshot = CaptureUnitModelLists();
        foreach (var kind in new[] { RegistryKind.Army, RegistryKind.Area, RegistryKind.City,
            RegistryKind.Gate, RegistryKind.Object })
        {
            var node = ResolveUnitModelHead(snapshot, kind);
            bool nonempty = node.Identity.HasValue;
            var visited = new HashSet<RegistryListNodeReference>();
            while (node.Identity.HasValue)
            {
                if (!visited.Add(node)) throw new InvalidDataException("Current unit-model registry contains a cycle.");
                var receiver = ReadUnitModelPayload(node, kind);
                if (AttachUnitModel(receiver)) modelWrites++;
                node = ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current unit-model next is unknown.");
            }
            //Only the first four nonempty lists refresh globals for the next
            //list. Object goes straight to1C0870 without another snapshot.
            if (nonempty && kind != RegistryKind.Object) snapshot = CaptureUnitModelLists();
        }
        if (ObjectPairUnitModelPostProcessor is not null) ObjectPairUnitModelPostProcessor();
        else _ = InitializeObjectPairModels();
        var scenery = ResolveFreshUnitModelHead(RegistryKind.Scenery);
        var seenScenery = new HashSet<RegistryListNodeReference>();
        while (scenery.Identity.HasValue)
        {
            if (!seenScenery.Add(scenery)) throw new InvalidDataException("Current scenery-model registry contains a cycle.");
            if (AttachUnitModel(ReadUnitModelPayload(scenery, RegistryKind.Scenery))) modelWrites++;
            scenery = ReadRegistryNodeNext(scenery) ?? throw new InvalidDataException("Current scenery-model next is unknown.");
        }
        return new(mapWrites, modelWrites);
    }

    public int ApplyUnitMapWritePrepass()
    {
        int writes = 0;
        var node = ResolveFreshUnitModelHead(RegistryKind.Army);
        var seen = new HashSet<RegistryListNodeReference>();
        while (node.Identity.HasValue)
        {
            if (!seen.Add(node)) throw new InvalidDataException("Current army map-write registry contains a cycle.");
            var receiver = ReadUnitModelPayload(node, RegistryKind.Army);
            var army = new ArmyUnitReference(receiver.PhysicalSlot);
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

    public bool IsAreaUnitModelTerrain17(AreaReference area)
    {
        ushort raw = ReadAreaHexIndex(area) ?? throw new InvalidDataException("Current area raw3A is unknown.");
        var hex = UnitModelContext().ReadHexTableReference(raw <= 48399 ? raw : 0)
            ?? throw new InvalidDataException("Current area terrain hex pointer is unknown.");
        return (ReadHexSurfaceCode(hex) ?? throw new InvalidDataException("Current area terrain hex10 is unknown.")) == 17;
    }
    private bool AttachUnitModel(RegistryObjectReference receiver)
    {
        int physical = UnitModelPhysical(receiver);
        switch (receiver.Kind)
        {
            case RegistryKind.Army:
                if (QueryArmyUnitMapPredicate(new(physical))) return false;
                break;
            case RegistryKind.Area:
                if (physical < 1) return false;
                var area = new AreaReference(physical);
                byte citySlot = ReadAreaCitySlot(area) ?? throw new InvalidDataException("Current area city35 is unknown.");
                var city = UnitModelContext().ReadCityTableReference(citySlot <= 51 ? citySlot : 0)
                    ?? throw new InvalidDataException("Current area model city pointer is unknown.");
                if (!(ReadBaseNativeValidity(city) ?? throw new InvalidDataException("Current area model city validity is unknown."))) return false;
                if ((AreaUnitModelSkipPredicate is null ? IsAreaUnitModelSkipped(area) : AreaUnitModelSkipPredicate(area))
                    ?? throw new InvalidDataException("Current area model skip predicate is unknown.")) return false;
                if (IsAreaUnitModelTerrain17(area)) return false;
                break;
            case RegistryKind.City or RegistryKind.Gate:
                if (physical < 1) return false;
                break;
            case RegistryKind.Object:
                if (!((ObjectUnitModelPredicate is null ? IsObjectUnitModelVisible(new(physical)) : ObjectUnitModelPredicate(new(physical)))
                    ?? throw new InvalidDataException("Current object model predicate is unknown."))) return false;
                break;
            case RegistryKind.Scenery:
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

    private sealed record UnitModelListSnapshot(SourceWorldContext Context, bool Heads, bool Pool);
    private UnitModelListSnapshot CaptureUnitModelLists()
    {
        var context = UnitModelContext();
        bool heads = UnitModelHeadsPresent();
        bool pool = KnownPoolPresent();
        return new(context, heads, pool);
    }
    private RegistryListNodeReference ResolveUnitModelHead(UnitModelListSnapshot snapshot, RegistryKind kind)
    {
        var descriptor = snapshot.Context.ReadRegistryDescriptor(kind) ?? throw new InvalidDataException("Current unit-model descriptor is unknown.");
        if (!descriptor.Identity.HasValue || !snapshot.Pool) return new(null);
        int key = RegistryKey(descriptor);
        return snapshot.Heads && NativeListKeyInRange(key) ? UnitModelStoredHead(key) : new(null);
    }
    private RegistryListNodeReference ResolveFreshUnitModelHead(RegistryKind kind, SourceWorldContext? context = null)
    {
        var descriptor = (context ?? UnitModelContext()).ReadRegistryDescriptor(kind) ?? throw new InvalidDataException("Current fresh unit-model descriptor is unknown.");
        if (!descriptor.Identity.HasValue || !KnownPoolPresent()) return new(null);
        int key = RegistryKey(descriptor); //2ED210/final150 save key before the head table.
        return UnitModelHeadsPresent() && NativeListKeyInRange(key) ? UnitModelStoredHead(key) : new(null);
    }
    private RegistryListNodeReference UnitModelStoredHead(int key)
    {
        var node = ReadHexListHead(new(key)) ?? throw new InvalidDataException("Current unit-model list head is unknown.");
        return new(node.Identity);
    }
    private bool UnitModelHeadsPresent() => (RegistryHeadTablePresenceReader is null ? RegistryHeadTablePresent : RegistryHeadTablePresenceReader())
        ?? throw new InvalidDataException("Current unit-model head table is unknown.");
    private SourceWorldContext UnitModelContext() => UnitModelListContextReader is null ? this :
        UnitModelListContextReader() ?? throw new InvalidDataException("Current unit-model registry context is unknown.");
    private UnitManagerReference ReadCurrentUnitManager()
    {
        var manager = (UnitManagerReader is null ? CurrentUnitManager : UnitManagerReader())
            ?? throw new InvalidDataException("Current CUnitManager pointer is unknown.");
        if (manager.Identity is int identity) CheckOpaque(identity, "unit manager");
        return manager;
    }
    private bool QueryArmyUnitMapPredicate(ArmyUnitReference army) =>
        (UnitMapWritePredicate is null ? IsArmyUnitMapWriteSkipped(army) : UnitMapWritePredicate(army))
        ?? throw new InvalidDataException("Current army map-write predicate is unknown.");
    private RegistryObjectReference ReadUnitModelPayload(RegistryListNodeReference node, RegistryKind kind)
    {
        var payload = ReadRegistryNodePayload(node) ?? throw new InvalidDataException("Current unit-model payload is unknown.");
        if (payload.Kind != kind) throw new InvalidDataException("Current unit-model payload kind contradicts its registry.");
        return payload;
    }
    private static int UnitModelPhysical(RegistryObjectReference receiver) => MapPhysical(receiver.PhysicalSlot, receiver.Kind switch
    { RegistryKind.Army or RegistryKind.Area => 501, RegistryKind.City => 52,
        RegistryKind.Gate => 11, RegistryKind.Object => 3001, RegistryKind.Scenery => 101,
        _ => throw new InvalidDataException("Unsupported unit-model payload kind.") }, "unit-model receiver");
}
