namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalStartupModelQueries
{
    private readonly Dictionary<int, ushort?> _startupArmyTroops58 = [];
    private readonly Dictionary<int, OriginalStartupVisualReference?> _startupTroopsModels30 = [];
    private readonly Dictionary<int, OriginalStartupModels> _startupModels = [];
    public OriginalRegistryListHandle? StartupPendingArmyListHandle { get; set; }

    public void SetStartupArmyTroopsSlot58(int physical, ushort? value)
    { Check(physical, 501, "startup army physical slot"); _startupArmyTroops58[physical] = value; }
    public void SetStartupTroopsModelPointer(int physical, OriginalStartupVisualReference? visual)
    { Check(physical, 501, "startup troops physical slot"); _startupTroopsModels30[physical] = visual; }
    public OriginalStartupModels RegisterStartupModels(int identity, IOriginalStartupModelResources resources)
    {
        CheckOpaque(identity, "startup model identity");
        var model = new OriginalStartupModels(this, resources);
        _startupModels.Add(identity, model);
        StartupModelResetter = (reference, argument) => GetStartupModels(reference).Reset(argument);
        StartupModelBuilder = (reference, argument) => GetStartupModels(reference).Build(argument);
        return model;
    }
    public OriginalStartupModels GetStartupModels(OriginalStartupModelReference reference)
        => reference.Identity.HasValue && _startupModels.TryGetValue(reference.Identity.Value, out var model) ? model
            : throw new InvalidDataException("Current startup model instance is not registered.");

    public IEnumerable<OriginalStartupDataReference?>? ReadStartupForces()
    {
        IReadOnlyList<OriginalRegistryObjectReference?>? references = ReadRegistryReferences(OriginalRegistryKind.Force);
        return references?.Select(reference => ModelData(reference, OriginalRegistryKind.Force, OriginalStartupModelKind.Force));
    }
    public IEnumerable<OriginalStartupDataReference?> ReadStartupDistricts(OriginalStartupDataReference force)
    {
        var forceReference = new OriginalForceReference(StartupIdentity(force, OriginalStartupModelKind.Force, 52));
        // D20F0 constructs end first, then begin saves the current head before
        // its independent physical getter. Neither call checks force validity.
        _ = StartupKnown(ReadForcePhysicalSlot(forceReference), "district iterator end force index");
        OriginalRegistryListNodeReference node = StartupRegistryHead(ReadRegistryDescriptor(OriginalRegistryKind.District));
        int savedForce = StartupKnown(ReadForcePhysicalSlot(forceReference), "district iterator begin force index");
        while (node.Identity.HasValue)
        {
            if (unchecked((uint)(savedForce - 1)) <= 50)
            {
                OriginalStartupDataReference district = ModelData(ReadRegistryNodePayload(node),
                    OriginalRegistryKind.District, OriginalStartupModelKind.District);
                int physical = StartupIdentity(district, OriginalStartupModelKind.District, 52);
                if (StartupKnown(ReadDistrictForceSlot(new(physical)), "district force10") == savedForce) yield return district;
            }
            node = StartupNext(node);
        }
    }
    public IEnumerable<OriginalStartupDataReference?> ReadStartupTroops(OriginalStartupDataReference district)
    {
        int districtPhysical = StartupIdentity(district, OriginalStartupModelKind.District, 52);
        for (int slot = 0; slot < 501; slot++)
        {
            if (unchecked((uint)(districtPhysical - 1)) > 50) continue;
            OriginalStaticTableReference reference = ReadStaticTableReference(0x7F8D8, slot)
                ?? throw new InvalidDataException("Current startup troops table pointer is unknown.");
            var troops = new OriginalStartupDataReference(OriginalStartupModelKind.Troops, reference.PhysicalSlot);
            if (ReadStartupTroopsDistrict(troops) == districtPhysical) yield return troops;
        }
    }
    public IEnumerable<OriginalStartupDataReference?> ReadStartupArmyUnits(OriginalStartupDataReference troops)
    {
        int savedTroops = StartupIdentity(troops, OriginalStartupModelKind.Troops, 501);
        OriginalRegistryListNodeReference node = StartupRegistryHead(ReadRegistryDescriptor(OriginalRegistryKind.Army));
        while (node.Identity.HasValue)
        {
            if (unchecked((uint)(savedTroops - 1)) <= 499)
            {
                OriginalStartupDataReference unit = ModelData(ReadRegistryNodePayload(node),
                    OriginalRegistryKind.Army, OriginalStartupModelKind.ArmyUnit);
                int physical = StartupIdentity(unit, OriginalStartupModelKind.ArmyUnit, 501);
                if (StartupKnown(_startupArmyTroops58.GetValueOrDefault(physical), "army troops58") == savedTroops &&
                    !IsStartupArmyPendingOrLinked(new(OriginalRegistryKind.Army, physical))) yield return unit;
            }
            node = StartupNext(node);
        }
    }
    public byte ReadStartupTroopsDistrict(OriginalStartupDataReference troops)
    {
        //22B110 stops at the first accepted current army unit, never consuming
        // its next node or selecting another commander after invalidity.
        foreach (OriginalStartupDataReference? candidate in ReadStartupArmyUnits(troops))
        {
            int physical = StartupIdentity(candidate!, OriginalStartupModelKind.ArmyUnit, 501);
            var person = StartupCommander(physical);
            return StartupPersonValid(person) ? StartupKnown(ReadPersonDistrictSlot(person.PhysicalSlot!.Value), "person district118") : (byte)0;
        }
        return 0;
    }
    public OriginalStartupVisualReference? ReadStartupTroopsModel(OriginalStartupDataReference troops)
        => _startupTroopsModels30.GetValueOrDefault(StartupIdentity(troops, OriginalStartupModelKind.Troops, 501));
    public void WriteStartupTroopsModel(OriginalStartupDataReference troops, OriginalStartupVisualReference visual)
        => SetStartupTroopsModelPointer(StartupIdentity(troops, OriginalStartupModelKind.Troops, 501), visual);

    public bool IsStartupArmyPendingOrLinked(OriginalRegistryObjectReference target)
    {
        if (OwnedDestructionQueue is not null)
            return ReadOwnedArmyExclusion233560(target)
                ?? throw new InvalidDataException("Current owned Army exclusion owner is unknown.");
        OriginalRegistryListNodeReference pending = StartupRegistryHead(StartupPendingArmyListHandle);
        while (pending.Identity.HasValue)
        {
            OriginalRegistryObjectReference payload = ReadRegistryNodePayload(pending)
                ?? throw new InvalidDataException("Current pending army payload pointer is unknown.");
            if (payload == target) return true;
            pending = StartupNext(pending);
        }
        OriginalRegistryListHandle descriptor = StaticCompletionListHandle
            ?? throw new InvalidDataException("Current startup association descriptor140 is unknown.");
        int key = RegistryKey(descriptor); // Null descriptor still queries key0x14000.
        OriginalStartupAssociationPool pool = AssociationPool();
        OriginalStartupAssociationNode node = pool.ReadHead(key)
            ?? throw new InvalidDataException("Current startup association head is unknown.");
        while (node.Identity.HasValue)
        {
            OriginalStartupAssociationPayload snapshot = pool.ReadPayload(node)
                ?? throw new InvalidDataException("Current32-byte startup association payload is unknown.");
            if (StartupObjectValid(snapshot.Field8) && StartupObjectValid(snapshot.Field10))
            {
                byte force = ReadStartupArmyForce(snapshot.Field10!);
                if (force != 51 && unchecked((uint)(force - 46)) > 4 && snapshot.Field10 == target) return true;
            }
            if (StartupObjectValid(snapshot.Field0) && snapshot.Field0 == target) return true;
            node = pool.ReadNext(node) ?? throw new InvalidDataException("Current startup association next30 is unknown.");
        }
        return false;
    }
    public byte ReadStartupArmyForce(OriginalRegistryObjectReference army)
    {
        if (army.Kind != OriginalRegistryKind.Army || !army.PhysicalSlot.HasValue)
            throw new InvalidDataException("Startup army force query requires a current ArmyUnit receiver.");
        OriginalTransferPersonReference person = StartupCommander(army.PhysicalSlot.Value);
        int districtSlot = 0;
        if (StartupPersonValid(person))
        {
            byte raw = StartupKnown(ReadPersonDistrictSlot(person.PhysicalSlot!.Value), "army commander district118");
            districtSlot = raw <= 51 ? raw : 0;
        }
        OriginalDistrictReference district = ReadDistrictTableReference(districtSlot)
            ?? throw new InvalidDataException("Current army district table pointer is unknown.");
        return StartupKnown(ReadDistrictNativeValidity(district), "army district validity")
            ? StartupKnown(ReadDistrictForceSlot(district), "army district force10") : (byte)0;
    }
    private OriginalTransferPersonReference StartupCommander(int physical)
    {
        ushort id = StartupKnown(ReadArmyUnitCommanderId(new(physical)), "army commander12");
        return (id <= 6000 ? ReadPersonLookupReference(id) : ReadTransferPersonTableReference(0))
            ?? throw new InvalidDataException("Current startup commander ID lookup pointer is unknown.");
    }
    private bool StartupPersonValid(OriginalTransferPersonReference person)
        => person.PhysicalSlot.HasValue && StartupKnown(ReadPersonNativeValidity(person.PhysicalSlot.Value), "startup person validity");
    private bool StartupObjectValid(OriginalRegistryObjectReference? reference)
        => StartupKnown(ReadRegistryObjectNativeValidity(reference
            ?? throw new InvalidDataException("Current association validity receiver pointer is unknown.")), "association object validity");
    private OriginalRegistryListNodeReference StartupRegistryHead(OriginalRegistryListHandle? handle)
        => ReadRegistryHead(handle ?? throw new InvalidDataException("Current startup registry descriptor is unknown."))
            ?? throw new InvalidDataException("Current startup registry head is unknown.");
    private OriginalRegistryListNodeReference StartupNext(OriginalRegistryListNodeReference node)
        => ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current startup registry next is unknown.");
    private static OriginalStartupDataReference ModelData(OriginalRegistryObjectReference? reference,
        OriginalRegistryKind kind, OriginalStartupModelKind modelKind)
    {
        if (reference is null) throw new InvalidDataException("Current startup data payload is unknown.");
        if (reference.Kind != kind) throw new InvalidDataException("Current startup data payload has another object kind.");
        return new(modelKind, reference.PhysicalSlot);
    }
    private static int StartupIdentity(OriginalStartupDataReference data, OriginalStartupModelKind kind, int count)
    {
        if (data.Kind != kind) throw new InvalidDataException("Current startup data receiver has another model kind.");
        return MapPhysical(data.Identity, count, "startup data identity");
    }
    private static T StartupKnown<T>(T? value, string field) where T : struct
        => value ?? throw new InvalidDataException($"Current {field} is unknown.");
}
