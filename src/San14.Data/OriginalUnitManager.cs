namespace San14.Data;

public enum OriginalUnitModelKind { Army, Area, City, Gate, Object, Scenery, ObjectPair, Auxiliary }

public interface IOriginalUnitModelQueries
{
    byte? ReadArmyUnitFlags150(OriginalArmyUnitReference army);
}

/// <summary>The renderer owns model storage, active/free chains and virtual
/// resource operations. These operations are required; no placeholder model
/// or successful empty operation is supplied by the data layer.</summary>
public interface IOriginalUnitModelResources
{
    OriginalUnitModelReference? ReadActiveModelHead(OriginalUnitManagerReference manager, OriginalUnitModelKind kind);
    OriginalRegistryObjectReference? ReadModelData(OriginalUnitModelReference model);
    OriginalUnitModelReference? ReadActiveModelNext(OriginalUnitModelReference model);
    void RetireModel(OriginalUnitModelReference model); //slot8
    void ReleaseModelResources(OriginalUnitModelReference model); //slot3
    void ReturnModelToPool(OriginalUnitManagerReference manager, OriginalUnitModelKind kind, OriginalUnitModelReference model);
    OriginalUnitModelReference? AllocateModel(OriginalUnitManagerReference manager, OriginalUnitModelKind kind, byte active, int argument);
    void InitializeModel(OriginalUnitModelReference model); //slot2
    void ConfigureModel(OriginalUnitModelReference model, OriginalRegistryObjectReference data); //slot7
    void ConfigureObjectPairModel(OriginalUnitModelReference model, OriginalObjectReference first, OriginalObjectReference second); //slot7
    int? ReadArmyModelRaw748(OriginalUnitModelReference model);
    void WriteArmyModelRaw748(OriginalUnitModelReference model, int value);
    int? ReadArmyModelRaw594(OriginalUnitModelReference model);
    void WriteArmyModelRaw594(OriginalUnitModelReference model, int value);
    bool? QueryArmyModelVisibility(OriginalUnitModelReference model); //1C50B0
}

/// <summary>Business layer of the six CUnitManager factories. All old-model
/// searches compare actual data pointer identities and stop at the first
/// match. Rendering and intrusive pool storage remain with the resource host.</summary>
public sealed class OriginalUnitManager
{
    private readonly IOriginalUnitModelQueries _queries;
    private readonly IOriginalUnitModelResources _resources;
    public OriginalUnitManagerReference Reference { get; }
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

    public OriginalUnitManager(OriginalUnitManagerReference reference, IOriginalUnitModelQueries queries,
        IOriginalUnitModelResources resources)
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
    public OriginalUnitModelReference Create(OriginalRegistryObjectReference data)
    {
        ArgumentNullException.ThrowIfNull(data);
        RequireModelIdentity(data.PhysicalSlot, "unit-model data pointer");
        OriginalUnitModelKind kind = ModelKind(data.Kind);
        if (kind == OriginalUnitModelKind.Army) RemoveArmyModel(data);
        else if (kind != OriginalUnitModelKind.Object)
        {
            var old = FindFirstActiveModel(kind, data);
            if (old.Identity.HasValue) ReleaseOldModel(kind, old);
        }
        OriginalUnitModelReference model = Allocate(kind);
        if (model.Identity.HasValue)
        {
            Initialize(model);
            _resources.ConfigureModel(model, data);
        }
        return model;
    }

    /// <summary>1C30F0. Mode is unused in the original body. The caller still
    /// allocates a replacement when this leaves a retiring army model active.</summary>
    public void RemoveArmyModel(OriginalRegistryObjectReference army)
    {
        ArgumentNullException.ThrowIfNull(army);
        if (army.Kind != OriginalRegistryKind.Army) throw new InvalidDataException("Army model removal requires an Army receiver.");
        RequireModelIdentity(army.PhysicalSlot, "army model data pointer");
        var old = FindFirstActiveModel(OriginalUnitModelKind.Army, army);
        if (!old.Identity.HasValue) return; //No flag read without a match.
        byte flags = _queries.ReadArmyUnitFlags150(new(army.PhysicalSlot))
            ?? throw new InvalidDataException("Current army flags150 are unknown.");
        if ((flags & 2) == 0) { ReleaseOldModel(OriginalUnitModelKind.Army, old); return; }
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
    public OriginalUnitModelReference AllocateObjectPair() => Allocate(OriginalUnitModelKind.ObjectPair);
    public void Initialize(OriginalUnitModelReference model)
    { RequireModel(model); _resources.InitializeModel(model); }
    public void ConfigureObjectPair(OriginalUnitModelReference model, OriginalObjectReference first, OriginalObjectReference second)
    {
        RequireModel(model);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        RequireModelIdentity(first.PhysicalSlot, "first pair object pointer");
        RequireModelIdentity(second.PhysicalSlot, "second pair object pointer");
        _resources.ConfigureObjectPairModel(model, first, second);
    }

    private OriginalUnitModelReference Allocate(OriginalUnitModelKind kind)
    {
        var model = KnownModel(_resources.AllocateModel(Reference, kind, 1, 0), "allocation result");
        return model;
    }
    private OriginalUnitModelReference FindFirstActiveModel(OriginalUnitModelKind kind, OriginalRegistryObjectReference data)
    {
        var node = KnownModel(_resources.ReadActiveModelHead(Reference, kind), "active model head");
        var visited = new HashSet<OriginalUnitModelReference>();
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
    private void ReleaseOldModel(OriginalUnitModelKind kind, OriginalUnitModelReference model)
    {
        _resources.RetireModel(model);
        _resources.ReleaseModelResources(model);
        _resources.ReturnModelToPool(Reference, kind, model);
    }
    private static OriginalUnitModelKind ModelKind(OriginalRegistryKind kind) => kind switch
    {
        OriginalRegistryKind.Army => OriginalUnitModelKind.Army,
        OriginalRegistryKind.Area => OriginalUnitModelKind.Area,
        OriginalRegistryKind.City => OriginalUnitModelKind.City,
        OriginalRegistryKind.Gate => OriginalUnitModelKind.Gate,
        OriginalRegistryKind.Object => OriginalUnitModelKind.Object,
        OriginalRegistryKind.Scenery => OriginalUnitModelKind.Scenery,
        _ => throw new InvalidDataException("Unsupported unit-model factory data kind.")
    };
    private static OriginalUnitModelReference KnownModel(OriginalUnitModelReference? model, string operation)
    {
        if (model is null) throw new InvalidDataException($"Current {operation} is unknown.");
        if (model.Identity is int identity && identity < 0) throw new InvalidDataException($"Negative {operation} identity.");
        return model;
    }
    private static void RequireModel(OriginalUnitModelReference model)
    { ArgumentNullException.ThrowIfNull(model); RequireModelIdentity(model.Identity, "model pointer"); }
    private static void RequireModelIdentity(int? identity, string operation)
    { if (!identity.HasValue || identity.Value < 0) throw new InvalidDataException($"Null or negative {operation}."); }
}
