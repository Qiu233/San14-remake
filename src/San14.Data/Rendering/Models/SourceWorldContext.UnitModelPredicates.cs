namespace San14.Data;

public sealed record ObjectDefinitionReference(int? PhysicalSlot);

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, byte?> _objectForceSlots13 = [], _worldForceSlots3A = [], _objectDefinitionValues2C = [];
    private readonly Dictionary<(int Force, int Index), byte?> _forceRelationBytesEA = [];

    public void SetObjectForceSlot13(int physical, byte? value)
    { Check(physical, 3001, "object force13 receiver"); _objectForceSlots13[physical] = value; }
    public byte? ReadObjectForceSlot13(ObjectReference reference)
        => _objectForceSlots13.GetValueOrDefault(MapPhysical(reference.PhysicalSlot, 3001, "object force13"));
    public void SetWorldForceSlot3A(int identity, byte? value)
    { CheckOpaque(identity, "world force3A receiver"); _worldForceSlots3A[identity] = value; }
    /// <summary>Only325740's proved byte3A reset. Static loading omits this field.</summary>
    public void ResetWorldForceSlot3A(int identity) => SetWorldForceSlot3A(identity, 0);
    public byte? ReadWorldForceSlot3A(StartupWorldReference world)
    {
        int identity = world.Identity ?? throw new InvalidDataException("Current world force3A receiver is null.");
        CheckOpaque(identity, "world force3A receiver");
        return _worldForceSlots3A.GetValueOrDefault(identity);
    }
    public void SetForceRelationByteEA(int physical, int index, byte? value)
    { Check(physical, 52, "force relation receiver"); Check(index, 52, "force relation index"); _forceRelationBytesEA[(physical, index)] = value; }
    public byte? ReadForceRelationByteEA(ForceReference force, int index)
    { Check(index, 52, "force relation index"); return _forceRelationBytesEA.GetValueOrDefault((MapPhysical(force.PhysicalSlot, 52, "force relation receiver"), index)); }
    public void SetObjectDefinitionRaw2C(int physical, byte? value)
    { Check(physical, 31, "object definition receiver"); _objectDefinitionValues2C[physical] = value; }
    public ObjectDefinitionReference? ReadObjectDefinitionReference(int slot)
    { var reference = ReadStaticTableReference(0x73958, slot); return reference is null ? null : new(reference.PhysicalSlot); }
    public bool? ReadObjectDefinitionValidity(ObjectDefinitionReference definition) => definition.PhysicalSlot.HasValue &&
        MapPhysical(definition.PhysicalSlot, 31, "object definition receiver") != 0;
    public byte? ReadObjectDefinitionRaw2C(ObjectDefinitionReference definition)
    {
        int physical = MapPhysical(definition.PhysicalSlot, 31, "object definition receiver");
        return _objectDefinitionValues2C.TryGetValue(physical, out var value) ? value : _initialObjectScalarRules?.ReadCategory(physical);
    }

    /// <summary>22C0A0 returns raw District10, after independent current Person
    /// and District lookups. It is not a Force physical identity.</summary>
    public byte ReadArmyUnitRawForceSlot(ArmyUnitReference army)
    {
        ushort commander = PredicateKnown(ReadArmyUnitCommanderId(army), "army commander12");
        var person = UnitCommander(UnitModelContext(), commander);
        int districtSlot = 0;
        if (UnitPersonValid(person))
        {
            byte raw = PredicateKnown(ReadPersonDistrictSlot(person.PhysicalSlot!.Value), "commander district118");
            districtSlot = raw <= 51 ? raw : 0;
        }
        var district = UnitModelContext().ReadDistrictTableReference(districtSlot)
            ?? throw new InvalidDataException("Current army district pointer is unknown.");
        return PredicateKnown(ReadDistrictNativeValidity(district), "army district validity")
            ? PredicateKnown(ReadDistrictForceSlot(district), "army district force10") : (byte)0;
    }
    public ForceReference ReadArmyUnitForceReference(ArmyUnitReference army)
    {
        var context = UnitModelContext(); //22BCB0 saves context before22C0A0.
        byte raw = ReadArmyUnitRawForceSlot(army);
        return context.ReadForceTableReference(raw <= 51 ? raw : 0)
            ?? throw new InvalidDataException("Current army force pointer is unknown.");
    }
    public bool IsArmyUnitBoundToSpecialForce(ArmyUnitReference army)
    {
        ushort commander = PredicateKnown(ReadArmyUnitCommanderId(army), "special army commander12");
        var context = UnitModelContext();
        var person = UnitCommander(context, commander);
        int physical = MapPhysical(person.PhysicalSlot, 1851, "special army commander"); //No person validity guard.
        ushort link = PredicateKnown(ReadPersonFirstLocation(physical), "special commander first11A");
        if (unchecked((uint)(link - 1)) > 60) return false;
        byte raw = ReadArmyUnitRawForceSlot(army);
        var force = context.ReadForceTableReference(raw <= 51 ? raw : 0)
            ?? throw new InvalidDataException("Current special army force pointer is unknown.");
        int forcePhysical = PredicateKnown(ReadForcePhysicalSlot(force), "special army force physical slot");
        return unchecked((uint)(forcePhysical - 46)) <= 4;
    }
    public BaseReference FindArmyUnitCurrentBase(ArmyUnitReference army)
    {
        if (!PredicateKnown(ReadArmyUnitNativeValidity(army), "army base-query validity")) return new(BaseKind.City, null);
        ushort commander = PredicateKnown(ReadArmyUnitCommanderId(army), "base-query commander12");
        var person = UnitCommander(UnitModelContext(), commander);
        if (!UnitPersonValid(person)) return new(BaseKind.City, null);
        ushort location = PredicateKnown(ReadPersonFirstLocation(person.PhysicalSlot!.Value), "base-query first11A");
        var selected = BaseLookup.Find(location, UnitModelContext());
        return PredicateKnown(ReadBaseNativeValidity(selected), "army selected base validity") ? selected : new(BaseKind.City, null);
    }
    public ushort ReadBaseCurrentHexIndex(BaseReference selected)
    {
        ushort link = PredicateKnown(ReadBaseObjectLink(selected), "base object4E");
        var obj = UnitModelContext().ReadObjectTableReference(link <= 3000 ? link : 0)
            ?? throw new InvalidDataException("Current base-linked object pointer is unknown.");
        return PredicateKnown(ReadObjectNativeValidity(obj), "base-linked object validity")
            ? PredicateKnown(ReadObjectHexIndex(obj), "base-linked object hex10") : (ushort)48400;
    }
    public bool IsArmyUnitMapWriteSkipped(ArmyUnitReference army)
    {
        if (IsStartupArmyPendingOrLinked(new(RegistryKind.Army, army.PhysicalSlot))) return false;
        var force = ReadArmyUnitForceReference(army);
        if (PredicateKnown(ReadForcePhysicalSlot(force), "first army force physical slot") == 51) return false;
        force = ReadArmyUnitForceReference(army);
        int physical = PredicateKnown(ReadForcePhysicalSlot(force), "second army force physical slot");
        if (unchecked((uint)(physical - 46)) <= 4 && !IsArmyUnitBoundToSpecialForce(army)) return false;
        var selected = FindArmyUnitCurrentBase(army);
        if (!PredicateKnown(ReadBaseNativeValidity(selected), "saved army base validity")) return false;
        ushort savedHex = ReadBaseCurrentHexIndex(selected);
        return savedHex == PredicateKnown(ReadArmyUnitRawHex2A(army), "fresh army hex2A");
    }
    public bool IsAreaUnitModelSkipped(AreaReference area)
    {
        byte raw = PredicateKnown(ReadAreaCitySlot(area), "area city35");
        var city = UnitModelContext().ReadCityTableReference(raw <= 51 ? raw : 0)
            ?? throw new InvalidDataException("Current area city pointer is unknown.");
        if (!PredicateKnown(ReadBaseNativeValidity(city), "area city validity")) return false;
        ushort saved = ReadBaseCurrentHexIndex(city);
        return saved == PredicateKnown(ReadAreaHexIndex(area), "fresh area hex3A");
    }
    public bool IsObjectUnitModelVisible(ObjectReference obj)
    {
        if (!PredicateKnown(ReadObjectNativeValidity(obj), "object visibility validity")) return false;
        byte kind = ReadPhysicalObject(MapPhysical(obj.PhysicalSlot, 3001, "object visibility receiver"))!.RawField12;
        if (unchecked((uint)(kind - 1)) <= 1) return false;
        byte raw = PredicateKnown(ReadObjectForceSlot13(obj), "object force13");
        var context = UnitModelContext();
        var objectForce = context.ReadForceTableReference(raw <= 51 ? raw : 0)
            ?? throw new InvalidDataException("Current object force pointer is unknown.");
        var firstWorld = context.ReadStaticCompletionWorld() ?? throw new InvalidDataException("Current object world pointer is unknown.");
        var worldForce = ReadUnitWorldForce(firstWorld);
        int worldPhysical = PredicateKnown(ReadForcePhysicalSlot(worldForce), "first world force physical slot");
        if (PredicateKnown(ReadForcePhysicalSlot(objectForce), "saved object force physical slot") == worldPhysical) return true;
        var definition = UnitModelContext().ReadObjectDefinitionReference(kind <= 30 ? kind : 0)
            ?? throw new InvalidDataException("Current object definition pointer is unknown.");
        if (!PredicateKnown(ReadObjectDefinitionValidity(definition), "object definition validity")) return true;
        if (PredicateKnown(ReadObjectDefinitionRaw2C(definition), "object definition2C") != 4) return true;
        var secondWorld = UnitModelContext().ReadStaticCompletionWorld() ?? throw new InvalidDataException("Current second object world is unknown.");
        worldForce = ReadUnitWorldForce(secondWorld);
        if (!PredicateKnown(ReadForceNativeValidity(worldForce), "second world force validity")) return false;
        int objectPhysical = PredicateKnown(ReadForcePhysicalSlot(objectForce), "fresh saved object force physical slot");
        return (PredicateKnown(ReadForceRelationByteEA(worldForce, objectPhysical), "world force indexedEA") & 0x7F) != 0;
    }
    private ForceReference ReadUnitWorldForce(StartupWorldReference world)
    {
        byte raw = PredicateKnown(ReadWorldForceSlot3A(world), "world force3A");
        return UnitModelContext().ReadForceTableReference(raw <= 51 ? raw : 0)
            ?? throw new InvalidDataException("Current world force table pointer is unknown.");
    }
    private static TransferPersonReference UnitCommander(SourceWorldContext context, ushort commander)
        => (commander <= 6000 ? context.ReadPersonLookupReference(commander) : context.ReadTransferPersonTableReference(0))
            ?? throw new InvalidDataException("Current army commander lookup pointer is unknown.");
    private bool UnitPersonValid(TransferPersonReference person)
        => person.PhysicalSlot.HasValue && PredicateKnown(ReadPersonNativeValidity(person.PhysicalSlot.Value), "army commander validity");
    private static T PredicateKnown<T>(T? value, string name) where T : struct
        => value ?? throw new InvalidDataException($"Current unit-model predicate input {name} is unknown.");
}
