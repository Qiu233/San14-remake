namespace San14.Data;

public sealed partial class SourceWorldContext : IUnitModelQueries
{
    private readonly Dictionary<int, byte?> _armyUnitFlags150 = [];
    private readonly Dictionary<int, UnitManager> _unitManagers = [];
    private readonly Dictionary<int, UnitModelStorage> _unitModelStorages = [];
    private readonly Dictionary<int, UnitManager> _objectPairModelOwners = [];

    public void SetArmyUnitFlags150(int physical, byte? value)
    { Check(physical, 501, "army flags150 receiver"); _armyUnitFlags150[physical] = value; }
    public byte? ReadArmyUnitFlags150(ArmyUnitReference army)
        => _armyUnitFlags150.GetValueOrDefault(MapPhysical(army.PhysicalSlot, 501, "army flags150"));
    /// <summary>Only the proved single-byte225CD0 reset store. Static army
    /// rows omit this field; materializing a scenario must preserve it.</summary>
    public void ResetArmyUnitFlags150(int physical) => SetArmyUnitFlags150(physical, 0);

    /// <summary>Registers an actual resource host without selecting the
    /// current singleton or replacing optional factory overrides.</summary>
    public UnitManager RegisterUnitManager(int identity, IUnitModelResources resources)
    {
        CheckOpaque(identity, "unit manager identity");
        var manager = new UnitManager(new(identity), this, resources);
        _unitManagers.Add(identity, manager);
        return manager;
    }
    public UnitManager GetUnitManager(UnitManagerReference reference)
        => reference.Identity.HasValue && _unitManagers.TryGetValue(reference.Identity.Value, out var manager)
            ? manager : throw new InvalidDataException("Current CUnitManager resource host is not registered.");

    public UnitManagerStartupResult InitializeUnitManager(IUnitModelStartupResources resources,
        IReadOnlyList<UnitModelPoolRequest>? configuration = null)
        => UnitManagerStartup.Initialize(this, resources, configuration);
    public UnitModelStorage GetUnitModelStorage(UnitManagerReference reference)
        => reference.Identity.HasValue && _unitModelStorages.TryGetValue(reference.Identity.Value, out var storage)
            ? storage : throw new InvalidDataException("Current CUnitManager owned model storage is not registered.");
    internal void RegisterUnitModelStorage(UnitManagerReference reference, UnitModelStorage storage)
        => _unitModelStorages.Add(reference.Identity!.Value, storage);

    private UnitModelReference AllocateRegisteredObjectPair(UnitManagerReference reference)
    {
        UnitManager owner = GetUnitManager(reference);
        UnitModelReference model = owner.AllocateObjectPair();
        if (model.Identity is int identity) _objectPairModelOwners[identity] = owner;
        return model;
    }
    private UnitManager ObjectPairModelOwner(UnitModelReference model)
        => model.Identity.HasValue && _objectPairModelOwners.TryGetValue(model.Identity.Value, out var manager)
            ? manager : throw new InvalidDataException("Current object-pair model resource owner is unknown.");
}
