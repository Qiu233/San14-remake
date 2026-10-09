namespace San14.Data;

public sealed partial class SourceWorldContext : IStartupModelQueries
{
    private readonly Dictionary<int, ushort?> _startupArmyTroops58 = [];
    private readonly Dictionary<int, StartupVisualReference?> _startupTroopsModels30 = [];
    private readonly Dictionary<int, StartupModels> _startupModels = [];
    public RegistryListHandle? StartupPendingArmyListHandle { get; set; }

    public void SetStartupArmyTroopsSlot58(int physical, ushort? value)
    { Check(physical, 501, "startup army physical slot"); _startupArmyTroops58[physical] = value; }
    public void SetStartupTroopsModelPointer(int physical, StartupVisualReference? visual)
    { Check(physical, 501, "startup troops physical slot"); _startupTroopsModels30[physical] = visual; }
    public StartupModels RegisterStartupModels(int identity, IStartupModelResources resources)
    {
        CheckOpaque(identity, "startup model identity");
        var model = new StartupModels(this, resources);
        _startupModels.Add(identity, model);
        StartupModelResetter = (reference, argument) => GetStartupModels(reference).Reset(argument);
        StartupModelBuilder = (reference, argument) => GetStartupModels(reference).Build(argument);
        return model;
    }
    public StartupModels GetStartupModels(StartupModelReference reference)
        => reference.Identity.HasValue && _startupModels.TryGetValue(reference.Identity.Value, out var model) ? model
            : throw new InvalidDataException("Current startup model instance is not registered.");

    public IEnumerable<StartupDataReference?>? ReadStartupForces()
    {
        IReadOnlyList<RegistryObjectReference?>? references = ReadRegistryReferences(RegistryKind.Force);
        return references?.Select(reference => ModelData(reference, RegistryKind.Force, StartupModelKind.Force));
    }
    public IEnumerable<StartupDataReference?> ReadStartupDistricts(StartupDataReference force)
    {
        var forceReference = new ForceReference(StartupIdentity(force, StartupModelKind.Force, 52));
        // D20F0 constructs end first, then begin saves the current head before
        // its independent physical getter. Neither call checks force validity.
        _ = StartupKnown(ReadForcePhysicalSlot(forceReference), "district iterator end force index");
        RegistryListNodeReference node = StartupRegistryHead(ReadRegistryDescriptor(RegistryKind.District));
        int savedForce = StartupKnown(ReadForcePhysicalSlot(forceReference), "district iterator begin force index");
        while (node.Identity.HasValue)
        {
            if (unchecked((uint)(savedForce - 1)) <= 50)
            {
                StartupDataReference district = ModelData(ReadRegistryNodePayload(node),
                    RegistryKind.District, StartupModelKind.District);
                int physical = StartupIdentity(district, StartupModelKind.District, 52);
                if (StartupKnown(ReadDistrictForceSlot(new(physical)), "district force10") == savedForce) yield return district;
            }
            node = StartupNext(node);
        }
    }
    public IEnumerable<StartupDataReference?> ReadStartupTroops(StartupDataReference district)
    {
        int districtPhysical = StartupIdentity(district, StartupModelKind.District, 52);
        for (int slot = 0; slot < 501; slot++)
        {
            if (unchecked((uint)(districtPhysical - 1)) > 50) continue;
            StaticTableReference reference = ReadStaticTableReference(0x7F8D8, slot)
                ?? throw new InvalidDataException("Current startup troops table pointer is unknown.");
            var troops = new StartupDataReference(StartupModelKind.Troops, reference.PhysicalSlot);
            if (ReadStartupTroopsDistrict(troops) == districtPhysical) yield return troops;
        }
    }
    public IEnumerable<StartupDataReference?> ReadStartupArmyUnits(StartupDataReference troops)
    {
        int savedTroops = StartupIdentity(troops, StartupModelKind.Troops, 501);
        RegistryListNodeReference node = StartupRegistryHead(ReadRegistryDescriptor(RegistryKind.Army));
        while (node.Identity.HasValue)
        {
            if (unchecked((uint)(savedTroops - 1)) <= 499)
            {
                StartupDataReference unit = ModelData(ReadRegistryNodePayload(node),
                    RegistryKind.Army, StartupModelKind.ArmyUnit);
                int physical = StartupIdentity(unit, StartupModelKind.ArmyUnit, 501);
                if (StartupKnown(_startupArmyTroops58.GetValueOrDefault(physical), "army troops58") == savedTroops &&
                    !IsStartupArmyPendingOrLinked(new(RegistryKind.Army, physical))) yield return unit;
            }
            node = StartupNext(node);
        }
    }
    public byte ReadStartupTroopsDistrict(StartupDataReference troops)
    {
        //22B110 stops at the first accepted current army unit, never consuming
        // its next node or selecting another commander after invalidity.
        foreach (StartupDataReference? candidate in ReadStartupArmyUnits(troops))
        {
            int physical = StartupIdentity(candidate!, StartupModelKind.ArmyUnit, 501);
            var person = StartupCommander(physical);
            return StartupPersonValid(person) ? StartupKnown(ReadPersonDistrictSlot(person.PhysicalSlot!.Value), "person district118") : (byte)0;
        }
        return 0;
    }
    public StartupVisualReference? ReadStartupTroopsModel(StartupDataReference troops)
        => _startupTroopsModels30.GetValueOrDefault(StartupIdentity(troops, StartupModelKind.Troops, 501));
    public void WriteStartupTroopsModel(StartupDataReference troops, StartupVisualReference visual)
        => SetStartupTroopsModelPointer(StartupIdentity(troops, StartupModelKind.Troops, 501), visual);

    public bool IsStartupArmyPendingOrLinked(RegistryObjectReference target)
    {
        if (OwnedDestructionQueue is not null)
            return ReadOwnedArmyExclusion233560(target)
                ?? throw new InvalidDataException("Current owned Army exclusion owner is unknown.");
        RegistryListNodeReference pending = StartupRegistryHead(StartupPendingArmyListHandle);
        while (pending.Identity.HasValue)
        {
            RegistryObjectReference payload = ReadRegistryNodePayload(pending)
                ?? throw new InvalidDataException("Current pending army payload pointer is unknown.");
            if (payload == target) return true;
            pending = StartupNext(pending);
        }
        RegistryListHandle descriptor = StaticCompletionListHandle
            ?? throw new InvalidDataException("Current startup association descriptor140 is unknown.");
        int key = RegistryKey(descriptor); // Null descriptor still queries key0x14000.
        StartupAssociationPool pool = AssociationPool();
        StartupAssociationNode node = pool.ReadHead(key)
            ?? throw new InvalidDataException("Current startup association head is unknown.");
        while (node.Identity.HasValue)
        {
            StartupAssociationPayload snapshot = pool.ReadPayload(node)
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
    public byte ReadStartupArmyForce(RegistryObjectReference army)
    {
        if (army.Kind != RegistryKind.Army || !army.PhysicalSlot.HasValue)
            throw new InvalidDataException("Startup army force query requires a current ArmyUnit receiver.");
        TransferPersonReference person = StartupCommander(army.PhysicalSlot.Value);
        int districtSlot = 0;
        if (StartupPersonValid(person))
        {
            byte raw = StartupKnown(ReadPersonDistrictSlot(person.PhysicalSlot!.Value), "army commander district118");
            districtSlot = raw <= 51 ? raw : 0;
        }
        DistrictReference district = ReadDistrictTableReference(districtSlot)
            ?? throw new InvalidDataException("Current army district table pointer is unknown.");
        return StartupKnown(ReadDistrictNativeValidity(district), "army district validity")
            ? StartupKnown(ReadDistrictForceSlot(district), "army district force10") : (byte)0;
    }
    private TransferPersonReference StartupCommander(int physical)
    {
        ushort id = StartupKnown(ReadArmyUnitCommanderId(new(physical)), "army commander12");
        return (id <= 6000 ? ReadPersonLookupReference(id) : ReadTransferPersonTableReference(0))
            ?? throw new InvalidDataException("Current startup commander ID lookup pointer is unknown.");
    }
    private bool StartupPersonValid(TransferPersonReference person)
        => person.PhysicalSlot.HasValue && StartupKnown(ReadPersonNativeValidity(person.PhysicalSlot.Value), "startup person validity");
    private bool StartupObjectValid(RegistryObjectReference? reference)
        => StartupKnown(ReadRegistryObjectNativeValidity(reference
            ?? throw new InvalidDataException("Current association validity receiver pointer is unknown.")), "association object validity");
    private RegistryListNodeReference StartupRegistryHead(RegistryListHandle? handle)
        => ReadRegistryHead(handle ?? throw new InvalidDataException("Current startup registry descriptor is unknown."))
            ?? throw new InvalidDataException("Current startup registry head is unknown.");
    private RegistryListNodeReference StartupNext(RegistryListNodeReference node)
        => ReadRegistryNodeNext(node) ?? throw new InvalidDataException("Current startup registry next is unknown.");
    private static StartupDataReference ModelData(RegistryObjectReference? reference,
        RegistryKind kind, StartupModelKind modelKind)
    {
        if (reference is null) throw new InvalidDataException("Current startup data payload is unknown.");
        if (reference.Kind != kind) throw new InvalidDataException("Current startup data payload has another object kind.");
        return new(modelKind, reference.PhysicalSlot);
    }
    private static int StartupIdentity(StartupDataReference data, StartupModelKind kind, int count)
    {
        if (data.Kind != kind) throw new InvalidDataException("Current startup data receiver has another model kind.");
        return MapPhysical(data.Identity, count, "startup data identity");
    }
    private static T StartupKnown<T>(T? value, string field) where T : struct
        => value ?? throw new InvalidDataException($"Current {field} is unknown.");
}
