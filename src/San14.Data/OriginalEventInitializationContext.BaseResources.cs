namespace San14.Data;

public enum OriginalBaseResourceKind { Gold, Supplies, Troops }

public sealed partial class OriginalEventInitializationContext
{
    private readonly Dictionary<(OriginalBaseReference Base, OriginalBaseResourceKind Kind), int?> _baseResources = [];
    private readonly Dictionary<OriginalBaseReference, byte?> _baseMorale44 = [];
    private readonly Dictionary<OriginalBaseReference, byte?> _basePostLoadFlag66 = [];
    private readonly Dictionary<int, ushort?> _objectRawFields14 = [];

    /// <summary>Current signed native resource fields34/38/3C. Source uint32
    /// bits are preserved; the later setters apply the original signed bounds.</summary>
    public int? ReadBaseResource(OriginalBaseReference reference, OriginalBaseResourceKind kind)
    { ValidateBaseResource(reference, kind); return _baseResources.GetValueOrDefault((reference, kind)); }
    public void SetBaseResource(OriginalBaseReference reference, OriginalBaseResourceKind kind, int? value)
    { ValidateBaseResource(reference, kind); _baseResources[(reference, kind)] = value; }
    public byte? ReadBaseMorale44(OriginalBaseReference reference)
    { _ = BasePhysical(reference); return _baseMorale44.GetValueOrDefault(reference); }
    public void SetBaseMorale44(OriginalBaseReference reference, byte? value)
    { _ = BasePhysical(reference); _baseMorale44[reference] = value; }
    public byte? ReadBasePostLoadFlag66(OriginalBaseReference reference)
    { _ = BasePhysical(reference); return _basePostLoadFlag66.GetValueOrDefault(reference); }
    public void SetBasePostLoadFlag66(OriginalBaseReference reference, byte? value)
    { _ = BasePhysical(reference); _basePostLoadFlag66[reference] = value; }
    public ushort? ReadObjectRawField14(OriginalObjectReference reference)
        => _objectRawFields14.GetValueOrDefault(MapPhysical(reference.PhysicalSlot, 3001, "object field14 receiver"));
    public void SetObjectRawField14(int physical, ushort? value)
    { Check(physical, 3001, "object field14 receiver"); _objectRawFields14[physical] = value; }
    private static void ValidateBaseResource(OriginalBaseReference reference, OriginalBaseResourceKind kind)
    {
        _ = BasePhysical(reference);
        if (kind is not (OriginalBaseResourceKind.Gold or OriginalBaseResourceKind.Supplies or OriginalBaseResourceKind.Troops))
            throw new InvalidDataException("Unknown current base resource field.");
    }
}
