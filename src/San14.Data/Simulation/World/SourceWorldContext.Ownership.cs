namespace San14.Data;

public sealed partial class SourceWorldContext : IStaticAreaCityInitializationState,
    IStaticHexOwnershipState
{
    private IReadOnlyList<int?>? _registeredPersonPhysicalSlots;
    private readonly Dictionary<int, byte?> _areaScenarioForces = [];

    public void SetRegisteredPersonPhysicalSlots(IReadOnlyList<int?>? values)
    {
        if (values is not null)
            foreach (int? value in values)
                if (value.HasValue) Check(value.Value, 1851, "registered person physical slot");
        UseResolvedRegistrySequence(RegistryKind.Person);
        _registeredPersonPhysicalSlots = values is null ? null : Array.AsReadOnly(values.ToArray());
    }

    public IReadOnlyList<int?>? ReadRegisteredPersonPhysicalSlots() => RegistryConfigured(RegistryKind.Person)
        ? ProjectRegistry<int?>(RegistryKind.Person,reference=>reference?.PhysicalSlot)
        : _registeredPersonPhysicalSlots;
    public int? ReadAreaPhysicalSlot(AreaReference area) => MapPhysical(area.PhysicalSlot, 501, "area");
    public byte? ReadAreaValidityCode(AreaReference area) =>
        _areaValidityCodes.GetValueOrDefault(MapPhysical(area.PhysicalSlot, 501, "area"));
    public void WriteAreaCitySlot(AreaReference area, byte citySlot) =>
        SetAreaCitySlot(MapPhysical(area.PhysicalSlot, 501, "area"), citySlot);
    public AreaCityAssignmentResult AssignAreaCity(AreaReference area, int requestedCity) =>
        AreaCityAssignment.Apply(area, requestedCity, this);
    public IReadOnlyList<AreaCityAssignmentResult> InitializeStaticAreaCities(int staticFlag) =>
        AreaCityAssignment.InitializeStaticCities(staticFlag, this);
    public void SetAreaScenarioForceSlot(int physicalSlot, byte? value)
    { Check(physicalSlot, 501, "area physical slot"); _areaScenarioForces[physicalSlot] = value; }
    public byte? ReadAreaScenarioForceSlot(AreaReference area) =>
        _areaScenarioForces.GetValueOrDefault(MapPhysical(area.PhysicalSlot, 501, "area"));
    public void AssignStaticHexForce(HexReference hex, int requestedForce) =>
        HexOwnership.Apply(hex, requestedForce, this);
    public IReadOnlyList<StaticHexOwnershipWrite> InitializeStaticHexOwnership(int staticFlag) =>
        StaticHexOwnership.Apply(staticFlag, this);
}
