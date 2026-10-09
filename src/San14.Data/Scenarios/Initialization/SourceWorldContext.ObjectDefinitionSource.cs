using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
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
                row.RawField2C != row.RawBytes[28]).Any())
            throw new InvalidDataException("Object definition source requires complete consistent CE section9 rows31x53.");
        foreach (S14ObjectDefinition row in catalog.Definitions)
        {
            ObjectDefinitionReference reference = ReadObjectDefinitionReference(row.Slot)
                ?? throw new InvalidDataException("Current object definition table pointer is unknown.");
            int physical = MapPhysical(reference.PhysicalSlot, S14ObjectDefinitionReader.Count, "fixed object definition receiver");
            SetObjectDefinitionRaw2C(physical, row.RawField2C);
        }
        ObjectDefinitionSource = loaded.Source;
        ObjectDefinitionDecodedSha256 = catalog.DecodedSha256;
    }
}
