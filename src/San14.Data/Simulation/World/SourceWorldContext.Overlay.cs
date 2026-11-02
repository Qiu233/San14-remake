namespace San14.Data;

public sealed partial class SourceWorldContext : IHexOverlayState, IMapOverlayInitializationState
{
    private readonly Dictionary<int, ushort?> _hexObjectLinks = [];
    private IReadOnlyList<ObjectReference?>? _registeredObjectReferences;
    private IReadOnlyList<SceneryReference?>? _registeredSceneryReferences;

    public int? ReadHexPhysicalSlot(HexReference reference) =>
        MapPhysical(reference.PhysicalSlot, MapTileCount, "hex");
    public ushort? ReadSceneryHexIndex(SceneryReference reference) =>
        _sceneryHexes.GetValueOrDefault(MapPhysical(reference.PhysicalSlot, 101, "scenery"));
    public void SetHexObjectLink(int physicalSlot, ushort? value)
    { Check(physicalSlot, MapTileCount, "hex physical slot"); _hexObjectLinks[physicalSlot] = value; }
    public ushort? ReadHexObjectLink(HexReference reference)
    {
        int physical = MapPhysical(reference.PhysicalSlot, MapTileCount, "hex");
        return _hexObjectLinks.TryGetValue(physical, out ushort? link) ? link :
            _emptyHexObjectLinksKnown ? (ushort)0 : null;
    }
    public void WriteHexObjectLink(HexReference reference, ushort value) =>
        SetHexObjectLink(MapPhysical(reference.PhysicalSlot, MapTileCount, "hex"), value);
    public void WriteHexSceneryLink(HexReference reference, byte value) =>
        SetHexScenerySlot(MapPhysical(reference.PhysicalSlot, MapTileCount, "hex"), value);
    public bool AssignHexObject(HexReference hex, int requestedObject, int checkPosition) =>
        HexOverlay.AssignObject(hex, requestedObject, checkPosition, this);
    public bool AssignHexScenery(HexReference hex, int requestedScenery, int checkPosition) =>
        HexOverlay.AssignScenery(hex, requestedScenery, checkPosition, this);
    public void SetRegisteredObjectReferences(IReadOnlyList<ObjectReference?>? values)
    {
        if (values is not null)
            foreach (ObjectReference? value in values)
                if (value?.PhysicalSlot is int physical) Check(physical, 3001, "registered object physical slot");
        UseResolvedRegistrySequence(RegistryKind.Object);
        _registeredObjectReferences = values is null ? null : Array.AsReadOnly(values.ToArray());
    }
    public void SetRegisteredSceneryReferences(IReadOnlyList<SceneryReference?>? values)
    {
        if (values is not null)
            foreach (SceneryReference? value in values)
                if (value?.PhysicalSlot is int physical) Check(physical, 101, "registered scenery physical slot");
        UseResolvedRegistrySequence(RegistryKind.Scenery);
        _registeredSceneryReferences = values is null ? null : Array.AsReadOnly(values.ToArray());
    }
    public IReadOnlyList<ObjectReference?>? ReadRegisteredObjectReferences() => RegistryConfigured(RegistryKind.Object)
        ? ProjectRegistry<ObjectReference?>(RegistryKind.Object,reference=>reference is null ? null : new(reference.PhysicalSlot))
        : _registeredObjectReferences;
    public IReadOnlyList<SceneryReference?>? ReadRegisteredSceneryReferences() => RegistryConfigured(RegistryKind.Scenery)
        ? ProjectRegistry<SceneryReference?>(RegistryKind.Scenery,reference=>reference is null ? null : new(reference.PhysicalSlot))
        : _registeredSceneryReferences;
    public int? ReadObjectPhysicalSlot(ObjectReference reference) =>
        MapPhysical(reference.PhysicalSlot, 3001, "object");
    public int? ReadSceneryPhysicalSlot(SceneryReference reference) =>
        MapPhysical(reference.PhysicalSlot, 101, "scenery");
    public BaseReference? ReadGateTableReference(int slot)
    { Check(slot, 11, "gate lookup slot"); return _gatePointers.TryGetValue(slot, out int? physical) ? new(BaseKind.Gate, physical) : null; }
    public void WriteBaseObjectLink(BaseReference reference, ushort value)
    {
        int physical = BasePhysical(reference);
        if (reference.Kind == BaseKind.City) _cityLinks[physical] = value;
        else _gateLinks[physical] = value;
    }
    public void ApplyStaticOverlayOwnership(int staticFlag) => StaticHexOwnership.Apply(staticFlag, this);
    public bool RebuildMapOverlays(int staticFlag) => MapOverlayInitialization.Apply(staticFlag, this);
}
