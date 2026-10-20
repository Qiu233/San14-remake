using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, FacilityEffectDefinition> _facilityEffectDefinitions = [];
    internal FacilityEffectDefinition? ReadFacilityEffectDefinition(ObjectDefinitionReference receiver) =>
        receiver.PhysicalSlot is int physical ? _facilityEffectDefinitions.GetValueOrDefault(physical) : null;
    public SourceProvenance? ObjectDefinitionSource { get; private set; }
    public string? ObjectDefinitionDecodedSha256 { get; private set; }

    /// <summary>Only the proved2261D0 raw2C reset for31 normally constructed
    /// receivers. Does not allocate current table pointers or other fields.</summary>
    public void InitializeObjectDefinitionStorage()
    {
        for (int physical = 0; physical < S14ObjectDefinitionReader.Count; physical++)
            SetObjectDefinitionRaw2C(physical, 0);
    }

    public void MaterializeFixedObjectDefinitions(LoadedObjectDefinitions loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        S14ObjectDefinitionCatalog catalog = loaded.Catalog;
        if (catalog.FormatMarker != 0x66 || catalog.RecordOffset != S14ObjectDefinitionReader.RecordStart ||
            catalog.RecordStride != S14ObjectDefinitionReader.RecordStride || catalog.Definitions.Count != S14ObjectDefinitionReader.Count ||
            catalog.Definitions.Where((row, slot) => row.Slot != slot || row.RawBytes.Length != S14ObjectDefinitionReader.RecordStride ||
                row.RawField2C != row.RawBytes[28] ||
                row.FirstChannel != new S14FacilityEffectChannel(row.RawBytes[37], row.RawBytes[38], row.RawBytes[39]) ||
                row.SecondChannel != new S14FacilityEffectChannel(row.RawBytes[40], row.RawBytes[41], row.RawBytes[42]) ||
                row.MinimumRange != row.RawBytes[43] || row.MaximumRange != row.RawBytes[44]).Any())
            throw new InvalidDataException("Object definition source requires complete consistent CE section9 rows31x53.");
        _facilityEffectDefinitions.Clear();
        _facilityConstructionDefinitions.Clear();
        foreach (S14ObjectDefinition row in catalog.Definitions)
        {
            ObjectDefinitionReference reference = ReadObjectDefinitionReference(row.Slot)
                ?? throw new InvalidDataException("Current object definition table pointer is unknown.");
            int physical = MapPhysical(reference.PhysicalSlot, S14ObjectDefinitionReader.Count, "fixed object definition receiver");
            SetObjectDefinitionRaw2C(physical, row.RawField2C);
            SetObjectDefinitionBaseDurability(physical, row.BaseDurability);
            SetFacilityConstructionDefinition(physical, row);
            _facilityEffectDefinitions[physical] = new(row.FirstChannel, row.SecondChannel, row.MinimumRange, row.MaximumRange)
                { TriggerRange = row.TriggerRange };
        }
        ObjectDefinitionSource = loaded.Source;
        ObjectDefinitionDecodedSha256 = catalog.DecodedSha256;
    }
}
