namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalHexOverlayState, IOriginalMapOverlayInitializationState
{
    private readonly Dictionary<int, ushort?> _hexObjectLinks = [];
    private IReadOnlyList<OriginalObjectReference?>? _registeredObjectReferences;
    private IReadOnlyList<OriginalSceneryReference?>? _registeredSceneryReferences;

    public int? ReadHexPhysicalSlot(OriginalHexReference reference) =>
        MapPhysical(reference.PhysicalSlot, 48400, "hex");
    public ushort? ReadSceneryHexIndex(OriginalSceneryReference reference) =>
        _sceneryHexes.GetValueOrDefault(MapPhysical(reference.PhysicalSlot, 101, "scenery"));
    public void SetHexObjectLink(int physicalSlot, ushort? value)
    { Check(physicalSlot, 48400, "hex physical slot"); _hexObjectLinks[physicalSlot] = value; }
    public ushort? ReadHexObjectLink(OriginalHexReference reference) =>
        _hexObjectLinks.GetValueOrDefault(MapPhysical(reference.PhysicalSlot, 48400, "hex"));
    public void WriteHexObjectLink(OriginalHexReference reference, ushort value) =>
        SetHexObjectLink(MapPhysical(reference.PhysicalSlot, 48400, "hex"), value);
    public void WriteHexSceneryLink(OriginalHexReference reference, byte value) =>
        SetHexScenerySlot(MapPhysical(reference.PhysicalSlot, 48400, "hex"), value);
    public bool AssignHexObject(OriginalHexReference hex, int requestedObject, int checkPosition) =>
        OriginalHexOverlay.AssignObject(hex, requestedObject, checkPosition, this);
    public bool AssignHexScenery(OriginalHexReference hex, int requestedScenery, int checkPosition) =>
        OriginalHexOverlay.AssignScenery(hex, requestedScenery, checkPosition, this);
    public void SetRegisteredObjectReferences(IReadOnlyList<OriginalObjectReference?>? values)
    {
        if (values is not null)
            foreach (OriginalObjectReference? value in values)
                if (value?.PhysicalSlot is int physical) Check(physical, 3001, "registered object physical slot");
        UseResolvedRegistrySequence(OriginalRegistryKind.Object);
        _registeredObjectReferences = values is null ? null : Array.AsReadOnly(values.ToArray());
    }
    public void SetRegisteredSceneryReferences(IReadOnlyList<OriginalSceneryReference?>? values)
    {
        if (values is not null)
            foreach (OriginalSceneryReference? value in values)
                if (value?.PhysicalSlot is int physical) Check(physical, 101, "registered scenery physical slot");
        UseResolvedRegistrySequence(OriginalRegistryKind.Scenery);
        _registeredSceneryReferences = values is null ? null : Array.AsReadOnly(values.ToArray());
    }
    public IReadOnlyList<OriginalObjectReference?>? ReadRegisteredObjectReferences() => RegistryConfigured(OriginalRegistryKind.Object)
        ? ProjectRegistry<OriginalObjectReference?>(OriginalRegistryKind.Object,reference=>reference is null ? null : new(reference.PhysicalSlot))
        : _registeredObjectReferences;
    public IReadOnlyList<OriginalSceneryReference?>? ReadRegisteredSceneryReferences() => RegistryConfigured(OriginalRegistryKind.Scenery)
        ? ProjectRegistry<OriginalSceneryReference?>(OriginalRegistryKind.Scenery,reference=>reference is null ? null : new(reference.PhysicalSlot))
        : _registeredSceneryReferences;
    public int? ReadObjectPhysicalSlot(OriginalObjectReference reference) =>
        MapPhysical(reference.PhysicalSlot, 3001, "object");
    public int? ReadSceneryPhysicalSlot(OriginalSceneryReference reference) =>
        MapPhysical(reference.PhysicalSlot, 101, "scenery");
    public OriginalBaseReference? ReadGateTableReference(int slot)
    { Check(slot, 11, "gate lookup slot"); return _gatePointers.TryGetValue(slot, out int? physical) ? new(OriginalBaseKind.Gate, physical) : null; }
    public void WriteBaseObjectLink(OriginalBaseReference reference, ushort value)
    {
        int physical = BasePhysical(reference);
        if (reference.Kind == OriginalBaseKind.City) _cityLinks[physical] = value;
        else _gateLinks[physical] = value;
    }
    public void ApplyStaticOverlayOwnership(int staticFlag) => OriginalStaticHexOwnership.Apply(staticFlag, this);
    public bool RebuildMapOverlays(int staticFlag) => OriginalMapOverlayInitialization.Apply(staticFlag, this);
}
