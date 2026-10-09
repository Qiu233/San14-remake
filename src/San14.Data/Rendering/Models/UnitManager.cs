namespace San14.Data;

public enum UnitModelKind { Army, Area, City, Gate, Object, Scenery, ObjectPair, Auxiliary }

public interface IUnitModelQueries
{
    byte? ReadArmyUnitFlags150(ArmyUnitReference army);
}

/// <summary>The renderer owns model storage, active/free chains and virtual
/// resource operations. These operations are required; no placeholder model
/// or successful empty operation is supplied by the data layer.</summary>
public interface IUnitModelResources
{
    UnitModelReference? ReadActiveModelHead(UnitManagerReference manager, UnitModelKind kind);
    RegistryObjectReference? ReadModelData(UnitModelReference model);
    UnitModelReference? ReadActiveModelNext(UnitModelReference model);
    void RetireModel(UnitModelReference model); //slot8
    void ReleaseModelResources(UnitModelReference model); //slot3
    void ReturnModelToPool(UnitManagerReference manager, UnitModelKind kind, UnitModelReference model);
    UnitModelReference? AllocateModel(UnitManagerReference manager, UnitModelKind kind, byte active, int argument);
    void InitializeModel(UnitModelReference model); //slot2
    void ConfigureModel(UnitModelReference model, RegistryObjectReference data); //slot7
    void ConfigureObjectPairModel(UnitModelReference model, ObjectReference first, ObjectReference second); //slot7
    int? ReadArmyModelRaw748(UnitModelReference model);
    void WriteArmyModelRaw748(UnitModelReference model, int value);
    int? ReadArmyModelRaw594(UnitModelReference model);
    void WriteArmyModelRaw594(UnitModelReference model, int value);
    bool? QueryArmyModelVisibility(UnitModelReference model); //1C50B0
}

/// <summary>Business layer of the six CUnitManager factories. All old-model
/// searches compare actual data pointer identities and stop at the first
/// match. Rendering and intrusive pool storage remain with the resource host.</summary>
public sealed class UnitManager
{
    private readonly IUnitModelQueries _queries;
    private readonly IUnitModelResources _resources;
    public UnitManagerReference Reference { get; }
    public ulong? RawField488 { get; private set; }
    public uint? RawField490 { get; private set; }
    public uint? RawField498 { get; private set; }

    public void WriteRawField488(ulong? value) => RawField488 = value;
    public void WriteRawField490(uint? value) => RawField490 = value;
    public void WriteRawField498(uint? value) => RawField498 = value;
    internal void InitializeConstructorTailFields()
    { RawField488 = 0; RawField490 = 0x7F7FFFFF; RawField498 = 0; }
    internal void CompletePoolInitialization()
    { RawField488 = 0; RawField490 = 0x7F7FFFFF; }

    public UnitManager(UnitManagerReference reference, IUnitModelQueries queries,
        IUnitModelResources resources)
    {
        ArgumentNullException.ThrowIfNull(reference);
        RequireModelIdentity(reference.Identity, "unit manager");
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(resources);
        Reference = reference;
        _queries = queries;
        _resources = resources;
    }

    /// <summary>1C0DA0/1C0F40/1C0FF0/1C1150/1C1200/1C12D0.
    /// Qualification belongs to the caller, not these factories.</summary>
    public UnitModelReference Create(RegistryObjectReference data)
    {
        ArgumentNullException.ThrowIfNull(data);
        RequireModelIdentity(data.PhysicalSlot, "unit-model data pointer");
        UnitModelKind kind = ModelKind(data.Kind);
        if (kind == UnitModelKind.Army) RemoveArmyModel(data);
        else if (kind != UnitModelKind.Object)
        {
            var old = FindFirstActiveModel(kind, data);
            if (old.Identity.HasValue) ReleaseOldModel(kind, old);
        }
        UnitModelReference model = Allocate(kind);
        if (model.Identity.HasValue)
        {
            Initialize(model);
            _resources.ConfigureModel(model, data);
        }
        return model;
    }

    /// <summary>1C30F0. Mode is unused in the original body. The caller still
    /// allocates a replacement when this leaves a retiring army model active.</summary>
    public void RemoveArmyModel(RegistryObjectReference army)
    {
        ArgumentNullException.ThrowIfNull(army);
        if (army.Kind != RegistryKind.Army) throw new InvalidDataException("Army model removal requires an Army receiver.");
        RequireModelIdentity(army.PhysicalSlot, "army model data pointer");
        var old = FindFirstActiveModel(UnitModelKind.Army, army);
        if (!old.Identity.HasValue) return; //No flag read without a match.
        byte flags = _queries.ReadArmyUnitFlags150(new(army.PhysicalSlot))
            ?? throw new InvalidDataException("Current army flags150 are unknown.");
        if ((flags & 2) == 0) { ReleaseOldModel(UnitModelKind.Army, old); return; }
        int previous = _resources.ReadArmyModelRaw748(old)
            ?? throw new InvalidDataException("Current army model raw748 is unknown.");
        if (previous != 0) return;
        bool visible = _resources.QueryArmyModelVisibility(old)
            ?? throw new InvalidDataException("Current army model visibility is unknown.");
        _resources.WriteArmyModelRaw748(old, visible ? 1 : 2);
        if (visible)
        {
            int level = _resources.ReadArmyModelRaw594(old)
                ?? throw new InvalidDataException("Current army model raw594 is unknown.");
            if (level < 3) _resources.WriteArmyModelRaw594(old, 3);
        }
    }

    //Pair business invokes these three operations separately, retaining each
    //completed allocation/virtual operation if a later operation fails.
    public UnitModelReference AllocateObjectPair() => Allocate(UnitModelKind.ObjectPair);
    public void Initialize(UnitModelReference model)
    { RequireModel(model); _resources.InitializeModel(model); }
    public void ConfigureObjectPair(UnitModelReference model, ObjectReference first, ObjectReference second)
    {
        RequireModel(model);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        RequireModelIdentity(first.PhysicalSlot, "first pair object pointer");
        RequireModelIdentity(second.PhysicalSlot, "second pair object pointer");
        _resources.ConfigureObjectPairModel(model, first, second);
    }

    private UnitModelReference Allocate(UnitModelKind kind)
    {
        var model = KnownModel(_resources.AllocateModel(Reference, kind, 1, 0), "allocation result");
        return model;
    }
    private UnitModelReference FindFirstActiveModel(UnitModelKind kind, RegistryObjectReference data)
    {
        var node = KnownModel(_resources.ReadActiveModelHead(Reference, kind), "active model head");
        var visited = new HashSet<UnitModelReference>();
        while (node.Identity.HasValue)
        {
            if (!visited.Add(node)) throw new InvalidDataException("Current active model chain contains a cycle.");
            var association = _resources.ReadModelData(node)
                ?? throw new InvalidDataException("Current model data40 is unknown.");
            if (association == data) return node;
            node = KnownModel(_resources.ReadActiveModelNext(node), "active model next");
        }
        return new(null);
    }
    private void ReleaseOldModel(UnitModelKind kind, UnitModelReference model)
    {
        _resources.RetireModel(model);
        _resources.ReleaseModelResources(model);
        _resources.ReturnModelToPool(Reference, kind, model);
    }
    private static UnitModelKind ModelKind(RegistryKind kind) => kind switch
    {
        RegistryKind.Army => UnitModelKind.Army,
        RegistryKind.Area => UnitModelKind.Area,
        RegistryKind.City => UnitModelKind.City,
        RegistryKind.Gate => UnitModelKind.Gate,
        RegistryKind.Object => UnitModelKind.Object,
        RegistryKind.Scenery => UnitModelKind.Scenery,
        _ => throw new InvalidDataException("Unsupported unit-model factory data kind.")
    };
    private static UnitModelReference KnownModel(UnitModelReference? model, string operation)
    {
        if (model is null) throw new InvalidDataException($"Current {operation} is unknown.");
        if (model.Identity is int identity && identity < 0) throw new InvalidDataException($"Negative {operation} identity.");
        return model;
    }
    private static void RequireModel(UnitModelReference model)
    { ArgumentNullException.ThrowIfNull(model); RequireModelIdentity(model.Identity, "model pointer"); }
    private static void RequireModelIdentity(int? identity, string operation)
    { if (!identity.HasValue || identity.Value < 0) throw new InvalidDataException($"Null or negative {operation}."); }
}
