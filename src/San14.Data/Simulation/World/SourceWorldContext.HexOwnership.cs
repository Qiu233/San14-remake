namespace San14.Data;

public sealed partial class SourceWorldContext : IHexOwnershipState, IAreaOwnershipServiceQueries
{
    private readonly Dictionary<int, byte?> _hexSurfaceCodes = [], _hexScenerySlots = [], _sceneryFlags = [];
    private readonly Dictionary<int, ushort?> _sceneryHexes = [];
    private readonly Dictionary<int, int?> _sceneryPointers = [], _sceneryLayoutDirty = [];
    private IReadOnlyList<AreaReference?>? _registeredAreaReferences;

    public SceneryLayoutReference? CurrentSceneryLayout { get; set; }
    public Func<SceneryLayoutReference?>? SceneryLayoutReader { get; set; }
    public void SetHexSurfaceCode(int physicalSlot, byte? value)
    { Check(physicalSlot, 48400, "hex physical slot"); _hexSurfaceCodes[physicalSlot] = value; }
    public byte? ReadHexSurfaceCode(HexReference hex) =>
        _hexSurfaceCodes.GetValueOrDefault(MapPhysical(hex.PhysicalSlot, 48400, "hex"));
    public void WriteHexForceSlot(HexReference hex, byte force) =>
        SetHexForceSlot(MapPhysical(hex.PhysicalSlot, 48400, "hex"), force);
    public void SetHexScenerySlot(int physicalSlot, byte? value)
    { Check(physicalSlot, 48400, "hex physical slot"); _hexScenerySlots[physicalSlot] = value; }
    public byte? ReadHexScenerySlot(HexReference hex) =>
        _hexScenerySlots.GetValueOrDefault(MapPhysical(hex.PhysicalSlot, 48400, "hex"));
    public void SetSceneryTablePointer(int slot, int? physicalSlot) =>
        SetPointer(_sceneryPointers, slot, 101, physicalSlot, 101);
    public SceneryReference? ReadSceneryTableReference(int slot)
    { Check(slot, 101, "scenery table slot"); return _sceneryPointers.TryGetValue(slot, out int? value) ? new(value) : null; }
    public void SetSceneryHexIndex(int physicalSlot, ushort? value)
    { Check(physicalSlot, 101, "scenery physical slot"); _sceneryHexes[physicalSlot] = value; }
    public void SetSceneryFlags47(int physicalSlot, byte? value)
    { Check(physicalSlot, 101, "scenery physical slot"); _sceneryFlags[physicalSlot] = value; }
    public byte? ReadSceneryFlags47(SceneryReference scenery) =>
        _sceneryFlags.GetValueOrDefault(MapPhysical(scenery.PhysicalSlot, 101, "scenery"));
    public bool? ReadSceneryNativeValidity(SceneryReference scenery)
    {
        ArgumentNullException.ThrowIfNull(scenery);
        if (!scenery.PhysicalSlot.HasValue) return false;
        int physical = MapPhysical(scenery.PhysicalSlot, 101, "scenery");
        if (physical == 0) return false;
        ushort? raw = _sceneryHexes.GetValueOrDefault(physical);
        if (!raw.HasValue) return null;
        HexReference? hex = ReadHexTableReference(raw.Value <= 48399 ? raw.Value : 0);
        if (hex is null) return null;
        if (!hex.PhysicalSlot.HasValue) return false; // Every nonnull CHex, including0, has slot3=true.
        ushort? area = ReadHexAreaSlot(hex);
        return area.HasValue ? area.Value != 0 : null;
    }
    public SceneryLayoutReference? ReadSceneryLayoutReference() =>
        SceneryLayoutReader is null ? CurrentSceneryLayout : SceneryLayoutReader();
    public void WriteSceneryLayoutDirty(SceneryLayoutReference layout, int value)
    {
        int identity = layout.Identity ?? throw new InvalidDataException("Current scenery layout pointer is null.");
        if (identity < 0) throw new InvalidDataException("Scenery layout identity must be nonnegative.");
        _sceneryLayoutDirty[identity] = value;
    }
    public int? ReadSceneryLayoutDirty(int identity) => _sceneryLayoutDirty.GetValueOrDefault(identity);
    public void SetRegisteredAreaReferences(IReadOnlyList<AreaReference?>? values)
    {
        if (values is not null)
            foreach (AreaReference? area in values)
                if (area?.PhysicalSlot is int physical) Check(physical, 501, "registered area physical slot");
        UseResolvedRegistrySequence(RegistryKind.Area);
        _registeredAreaReferences = values is null ? null : Array.AsReadOnly(values.ToArray());
    }
    public IReadOnlyList<AreaReference?>? ReadRegisteredAreaReferences() => RegistryConfigured(RegistryKind.Area)
        ? ProjectRegistry<AreaReference?>(RegistryKind.Area,reference=>reference is null ? null : new(reference.PhysicalSlot))
        : _registeredAreaReferences;
    public byte? ReadAreaCurrentForceSlot(AreaReference area)
    {
        ushort? raw = ReadAreaHexIndex(area);
        if (!raw.HasValue) return null;
        HexReference? hex = ReadHexTableReference(raw.Value <= 48399 ? raw.Value : 0);
        return hex is null ? null : ReadHexForceSlot(hex); // Direct native read; known-null is unsafe.
    }
    public bool RefreshAreaOwnershipService() => AreaOwnershipService.Refresh(this,
        TransferService ?? throw new InvalidDataException("Current independent transfer service state is unknown."));
    public void AssignHexForce(HexReference hex, int requestedForce) => HexOwnership.Apply(hex, requestedForce, this);
}
