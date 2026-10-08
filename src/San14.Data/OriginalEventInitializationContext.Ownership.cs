namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalStaticAreaCityInitializationState,
    IOriginalStaticHexOwnershipState
{
    private IReadOnlyList<int?>? _registeredPersonPhysicalSlots;
    private readonly Dictionary<int, byte?> _areaScenarioForces = [];

    public void SetRegisteredPersonPhysicalSlots(IReadOnlyList<int?>? values)
    {
        if (values is not null)
            foreach (int? value in values)
                if (value.HasValue) Check(value.Value, 1851, "registered person physical slot");
        UseResolvedRegistrySequence(OriginalRegistryKind.Person);
        _registeredPersonPhysicalSlots = values is null ? null : Array.AsReadOnly(values.ToArray());
    }

    public IReadOnlyList<int?>? ReadRegisteredPersonPhysicalSlots() => RegistryConfigured(OriginalRegistryKind.Person)
        ? ProjectRegistry<int?>(OriginalRegistryKind.Person,reference=>reference?.PhysicalSlot)
        : _registeredPersonPhysicalSlots;
    public int? ReadAreaPhysicalSlot(OriginalAreaReference area) => MapPhysical(area.PhysicalSlot, 501, "area");
    public byte? ReadAreaValidityCode(OriginalAreaReference area) =>
        _areaValidityCodes.GetValueOrDefault(MapPhysical(area.PhysicalSlot, 501, "area"));
    public void WriteAreaCitySlot(OriginalAreaReference area, byte citySlot) =>
        SetAreaCitySlot(MapPhysical(area.PhysicalSlot, 501, "area"), citySlot);
    public OriginalAreaCityAssignmentResult AssignAreaCity(OriginalAreaReference area, int requestedCity) =>
        OriginalAreaCityAssignment.Apply(area, requestedCity, this);
    public IReadOnlyList<OriginalAreaCityAssignmentResult> InitializeStaticAreaCities(int staticFlag) =>
        OriginalAreaCityAssignment.InitializeStaticCities(staticFlag, this);
    public void SetAreaScenarioForceSlot(int physicalSlot, byte? value)
    { Check(physicalSlot, 501, "area physical slot"); _areaScenarioForces[physicalSlot] = value; }
    public byte? ReadAreaScenarioForceSlot(OriginalAreaReference area) =>
        _areaScenarioForces.GetValueOrDefault(MapPhysical(area.PhysicalSlot, 501, "area"));
    public void AssignStaticHexForce(OriginalHexReference hex, int requestedForce) =>
        OriginalHexOwnership.Apply(hex, requestedForce, this);
    public IReadOnlyList<OriginalStaticHexOwnershipWrite> InitializeStaticHexOwnership(int staticFlag) =>
        OriginalStaticHexOwnership.Apply(staticFlag, this);
}
