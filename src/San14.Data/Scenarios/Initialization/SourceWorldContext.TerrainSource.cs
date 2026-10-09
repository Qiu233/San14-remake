using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    public string? TerrainTypeSourceSha256 { get; private set; }
    public SourceProvenance? TerrainTypeSource { get; private set; }
    private readonly Dictionary<(int Physical, int Category), ushort?> _surfaceTacticRateWords = [];
    private readonly Dictionary<int, byte?> _surfaceWoundedCategories42 = [];

    public void SetSurfaceTypeWoundedCategory42(int physicalSlot, byte? value)
    { Check(physicalSlot, 31, "surface type physical slot"); _surfaceWoundedCategories42[physicalSlot] = value; }
    public byte? ReadSurfaceTypeWoundedCategory42(SurfaceTypeReference surface) =>
        _surfaceWoundedCategories42.GetValueOrDefault(MapPhysical(surface.PhysicalSlot, 31, "surface type"));

    public void SetSurfaceTypeTacticRateWord(int physicalSlot, int category, ushort? value)
    {
        Check(physicalSlot, 31, "surface type physical slot");
        Check(category, 9, "surface tactic rate category");
        _surfaceTacticRateWords[(physicalSlot, category)] = value;
    }

    /// <summary>Native Terrain2E..3F, loaded by current SC240A80/TC240800
    /// as nine u16 words. A missing current field is not a rate of100.</summary>
    public ushort? ReadSurfaceTypeTacticRateWord(SurfaceTypeReference surface, int category)
    {
        Check(category, 9, "surface tactic rate category");
        return _surfaceTacticRateWords.GetValueOrDefault((
            MapPhysical(surface.PhysicalSlot, 31, "surface type"), category));
    }

    /// <summary>Explicit successful native fixed-table allocation/load profile.
    /// Parses every row before replacing surface pointers/raw2A. It does not reset
    /// area statistics, current map ownership, registration or other current tables.</summary>
    public void MaterializeFixedTerrainTypes(S14Document fixedData)
        => ApplyCatalog(S14TerrainTypeReader.Read(fixedData), null);

    /// <summary>Preserves the selected repository source (including overrides)
    /// alongside its decoded hash. The document-only entry has no source URI.</summary>
    public void MaterializeFixedTerrainTypes(LoadedTerrainTypes loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(loaded.Source);
        ApplyCatalog(loaded.Catalog, loaded.Source);
    }

    private void ApplyCatalog(S14TerrainTypeCatalog catalog, SourceProvenance? source)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        int expectedOffset = catalog.FormatMarker switch { 0x33 => 182098, 0x5C or 0x66 => 182121, _ => -1 };
        if (expectedOffset < 0 || catalog.RecordOffset != expectedOffset || catalog.RecordStride != 51 ||
            catalog.Types is null || catalog.Types.Count != 31 || catalog.DecodedSha256 is null ||
            catalog.DecodedSha256.Length != 64 || !catalog.DecodedSha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Current terrain source requires a complete verified31-slot fixed profile.");
        for (int slot = 0; slot < catalog.Types.Count; slot++)
        {
            S14TerrainTypeDefinition item = catalog.Types[slot];
            if (item is null || item.Slot != slot || item.RawBytes is null || item.RawBytes.Length != 51 ||
                BinaryPrimitives.ReadUInt16LittleEndian(item.RawBytes.AsSpan(26,2)) != item.RawField2A ||
                item.RawBytes[49] != item.RawField42)
                throw new InvalidDataException($"Current terrain source row differs at physical slot {slot}.");
        }
        for (int slot = 0; slot < catalog.Types.Count; slot++)
        {
            SetSurfaceTypeRawField2A(slot, catalog.Types[slot].RawField2A);
            SetSurfaceTypeTablePointer(slot, slot);
            //240A80 serializes native byte42 from fixed row byte49.
            SetSurfaceTypeWoundedCategory42(slot, catalog.Types[slot].RawField42);
            //The fixed row's third18-byte window is a nine-word array in the
            //native serializer, despite the legacy preview's ThirdText label.
            for (int category = 0; category < 9; category++)
                SetSurfaceTypeTacticRateWord(slot, category, BinaryPrimitives.ReadUInt16LittleEndian(
                    catalog.Types[slot].RawBytes.AsSpan(29 + category * 2, 2)));
        }
        TerrainTypeSourceSha256 = catalog.DecodedSha256;
        TerrainTypeSource = source;
    }
}
