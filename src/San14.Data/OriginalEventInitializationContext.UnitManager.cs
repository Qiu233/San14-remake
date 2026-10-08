namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalUnitModelQueries
{
    private readonly Dictionary<int, byte?> _armyUnitFlags150 = [];
    private readonly Dictionary<int, OriginalUnitManager> _unitManagers = [];
    private readonly Dictionary<int, OriginalUnitModelStorage> _unitModelStorages = [];
    private readonly Dictionary<int, OriginalUnitManager> _objectPairModelOwners = [];

    public void SetArmyUnitFlags150(int physical, byte? value)
    { Check(physical, 501, "army flags150 receiver"); _armyUnitFlags150[physical] = value; }
    public byte? ReadArmyUnitFlags150(OriginalArmyUnitReference army)
        => _armyUnitFlags150.GetValueOrDefault(MapPhysical(army.PhysicalSlot, 501, "army flags150"));
    /// <summary>Only the proved single-byte225CD0 reset store. Static army
    /// rows omit this field; materializing a scenario must preserve it.</summary>
    public void ResetArmyUnitFlags150(int physical) => SetArmyUnitFlags150(physical, 0);

    /// <summary>Registers an actual resource host without selecting the
    /// current singleton or replacing optional factory overrides.</summary>
    public OriginalUnitManager RegisterUnitManager(int identity, IOriginalUnitModelResources resources)
    {
        CheckOpaque(identity, "unit manager identity");
        var manager = new OriginalUnitManager(new(identity), this, resources);
        _unitManagers.Add(identity, manager);
        return manager;
    }
    public OriginalUnitManager GetUnitManager(OriginalUnitManagerReference reference)
        => reference.Identity.HasValue && _unitManagers.TryGetValue(reference.Identity.Value, out var manager)
            ? manager : throw new InvalidDataException("Current CUnitManager resource host is not registered.");

    public OriginalUnitManagerStartupResult InitializeUnitManager(IOriginalUnitModelStartupResources resources,
        IReadOnlyList<OriginalUnitModelPoolRequest>? configuration = null)
        => OriginalUnitManagerStartup.Initialize(this, resources, configuration);
    public OriginalUnitModelStorage GetUnitModelStorage(OriginalUnitManagerReference reference)
        => reference.Identity.HasValue && _unitModelStorages.TryGetValue(reference.Identity.Value, out var storage)
            ? storage : throw new InvalidDataException("Current CUnitManager owned model storage is not registered.");
    internal void RegisterUnitModelStorage(OriginalUnitManagerReference reference, OriginalUnitModelStorage storage)
        => _unitModelStorages.Add(reference.Identity!.Value, storage);

    private OriginalUnitModelReference AllocateRegisteredObjectPair(OriginalUnitManagerReference reference)
    {
        OriginalUnitManager owner = GetUnitManager(reference);
        OriginalUnitModelReference model = owner.AllocateObjectPair();
        if (model.Identity is int identity) _objectPairModelOwners[identity] = owner;
        return model;
    }
    private OriginalUnitManager ObjectPairModelOwner(OriginalUnitModelReference model)
        => model.Identity.HasValue && _objectPairModelOwners.TryGetValue(model.Identity.Value, out var manager)
            ? manager : throw new InvalidDataException("Current object-pair model resource owner is unknown.");
}
