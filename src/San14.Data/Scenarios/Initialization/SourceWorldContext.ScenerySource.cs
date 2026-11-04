using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext : ISceneryModelQueries
{
    private readonly Dictionary<int, byte?> _sceneryKinds46 = [];
    private readonly Dictionary<int, ushort?> _sceneryAngles4A = [];
    public void SetSceneryKind46(int physical, byte? value)
    { Check(physical, 101, "scenery kind46 receiver"); _sceneryKinds46[physical] = value; }
    public byte? ReadSceneryKind46(SceneryReference scenery)
        => _sceneryKinds46.GetValueOrDefault(MapPhysical(scenery.PhysicalSlot, 101, "scenery kind46"));
    public void SetSceneryAngle4A(int physical, ushort? value)
    { Check(physical, 101, "scenery angle4A receiver"); _sceneryAngles4A[physical] = value; }
    public ushort? ReadSceneryAngle4A(SceneryReference scenery)
        => _sceneryAngles4A.GetValueOrDefault(MapPhysical(scenery.PhysicalSlot, 101, "scenery angle4A"));
    public S14SceneryCatalog? SceneryDefinitions { get; private set; }
    public SourceProvenance? SceneryDefinitionSource { get; private set; }
    public SourceProvenance? SceneryStateSource { get; private set; }

    public void MaterializeFixedScenery(S14Document document) => ApplyFixedScenery(S14SceneryReader.ReadFixed(document), null);
    public void MaterializeFixedScenery(LoadedSceneries loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ApplyFixedScenery(loaded.Catalog, loaded.Source);
    }

    private void ApplyFixedScenery(S14SceneryCatalog catalog, SourceProvenance? source)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.FormatMarker != 0x66 || catalog.RecordOffset != S14SceneryReader.Start ||
            catalog.RecordStride != S14SceneryReader.Stride || catalog.Sceneries.Count != S14SceneryReader.Count)
            throw new InvalidDataException("Current scenery source requires the complete CE fixed catalog.");
        for (int slot = 0; slot < catalog.Sceneries.Count; slot++)
        {
            S14SceneryDefinition row = catalog.Sceneries[slot];
            if (row.Slot != slot || row.RawBytes.Length != S14SceneryReader.Stride ||
                row.RawHex44 != BinaryPrimitives.ReadUInt16LittleEndian(row.RawBytes.AsSpan(52,2)) ||
                row.RawField46 != row.RawBytes[54] || row.RawField48 != row.RawBytes[55])
                throw new InvalidDataException($"Current scenery source row {slot} disagrees with retained bytes.");
        }
        foreach (S14SceneryDefinition row in catalog.Sceneries)
        {
            SetSceneryTablePointer(row.Slot,row.Slot);
            SetSceneryHexIndex(row.Slot,row.RawHex44);
            SetSceneryKind46(row.Slot,row.RawField46);
        }
        SceneryDefinitions = catalog;
        SceneryDefinitionSource = source;
        // Fixed loading does not write scenario47, registration or map overlays.
    }

    // The guarded fresh Landmark producer also borrows these resident scenario
    // flags explicitly. Normal current capture subsequently overlays only saved
    // knowledge; it does not use this source as a missing-current fallback.
    internal void MaterializeStaticSceneryState(LoadedScenario loaded)
    {
        S14ScenarioWorldDocument world = loaded.Snapshot.WorldParameters!;
        if (world.SourceBoundary is null)
        {
            for (int slot = 0; slot < S14SceneryReader.Count; slot++) SetSceneryFlags47(slot,null);
            SceneryStateSource = null;
            return;
        }
        S14SceneryStateCatalog catalog = S14SceneryReader.ReadState(world);
        for (int slot = 0; slot < catalog.Flags47.Count; slot++) SetSceneryFlags47(slot,catalog.Flags47[slot]);
        SceneryStateSource = loaded.ScenarioSource;
    }
}
