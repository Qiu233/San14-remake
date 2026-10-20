using San14.Core;

namespace San14.Data;

/// <summary>Fresh CE238790 Item47 and actual Item registry110 producer.
/// Current holders are always read from this game's saved Item state.</summary>
public sealed class PersonArmyCaptureInputs
{
    public const string VersionKey = "ce-person-army-capture-current:version";
    public static string ItemSubtype47Key(int physical) => $"ce-person-army-capture-current:item:{physical}:47";
    public static string ItemLifespanExtensionKey(int physical) => $"ce-person-army-capture-current:item:{physical}:lifespan-extension";
    public static string TerrainFlags2CKey(int physical) => $"ce-person-army-capture-current:terrain:{physical}:2C";
    private readonly LoadedScenario _source;
    private readonly string _language;
    private readonly byte[] _subtypes;
    private readonly byte[] _lifespanExtensions;
    private readonly byte[] _terrainFlags;
    private PersonArmyCaptureInputs(LoadedScenario source, string language, byte[] subtypes, byte[] lifespanExtensions, byte[] terrainFlags)
        => (_source, _language, _subtypes, _lifespanExtensions, _terrainFlags) = (source, language, subtypes, lifespanExtensions, terrainFlags);

    public static PersonArmyCaptureInputs Load(LoadedScenario source, LoadedTerrainTypes terrain, string language)
    {
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != language || language is not ("SC" or "TC") ||
            source.Snapshot.ScenarioItems is not { Items.Count: 202 } items || terrain.Catalog.Types.Count != 31 ||
            terrain.Source.Sha256 != source.FixedDataSource.Sha256)
            throw new InvalidDataException("Person capture Item source requires complete CE202 scenario rows.");
        var subtypes = new byte[202];
        var lifespanExtensions = new byte[202];
        foreach (var row in items.Items) {
            if (row.Slot is < 0 or >= 202 || row.RecordStride != 65 || row.RawBytes is not { Length: 65 } ||
                row.EmbeddedDefinition is null || row.EmbeddedDefinition.Slot != row.Slot)
                throw new InvalidDataException("Person capture Item source row has another identity/layout.");
            //238790 mode1: owner4A(2),location4C(2),50(1),4F(1),
            //name10(14),description1E(38),44..49(6),51(1).
            subtypes[row.Slot] = row.RawBytes[61];
            lifespanExtensions[row.Slot] = row.RawBytes[62];
        }
        return new(source, language, subtypes, lifespanExtensions, terrain.Catalog.Types.Select(row => row.RawField2C).ToArray());
    }

    public GameState InstallFresh(GameState state, SourceWorldContext current)
    {
        if (state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != _language ||
            state.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri) != _source.ScenarioSource.Sha256 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            simulation.Parameters.Int32Values.ContainsKey(VersionKey) || state.ReadArmyLifecycle() is not { } arena ||
            current.ReadOwnedCurrentListPool() is not { } pool || current.ReadRegistryDescriptor(RegistryKind.Item) is not null ||
            state.Items is not { Count: 201 } itemTable || Enumerable.Range(1, 201).Any(id => !itemTable.ContainsKey(id)))
            throw new InvalidDataException("Capture Item fields/registry require their unused complete fresh owner.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        for (int item = 0; item < 202; item++) values.Add(ItemSubtype47Key(item), _subtypes[item]);
        for (int item = 0; item < 202; item++) values.Add(ItemLifespanExtensionKey(item), _lifespanExtensions[item]);
        for (int terrain = 0; terrain < 31; terrain++) values.Add(TerrainFlags2CKey(terrain), _terrainFlags[terrain]);
        var next = state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
        BindCurrent(next, current);
        var descriptor = current.CreateNearbyTemporary();
        current.SetRegistryDescriptor(RegistryKind.Item, new(descriptor.Identity));
        for (int physical = 0; physical < 202; physical++) {
            var item = current.ReadRegistryTableReference(RegistryKind.Item, physical);
            if (item is null || current.ReadRegistryObjectNativeValidity(item) is null)
                throw new InvalidDataException("Fresh capture Item native validity is unknown.");
            _ = ObjectRegistration.Register(item, current);
        }
        return next.WithArmyLifecycle(arena with { Indexes = pool.ExportOwnedState() });
    }

    public static void BindCurrent(GameState state, SourceWorldContext current)
    {
        if (state.NativeArmySimulation?.Parameters.Int32Values is not { } bank || bank.GetValueOrDefault(VersionKey) != 1) return;
        for (int physical = 0; physical < 202; physical++) {
            var item = state.Items?.GetValueOrDefault(physical);
            current.SetRegistryTablePointer(RegistryKind.Item, physical, physical);
            current.SetRegistryItemStatus(physical, physical == 0 ? ItemStatusKnowledge.Exact(0) : item?.Lifecycle?.Status);
            current.SetCaptureItemOwner4A(physical, physical == 0 ? (ushort)0 : item?.Holder is { } holder &&
                (holder.OfficerId is null || holder.OfficerId is >= 0 and <= 65535) ? (ushort)(holder.OfficerId ?? 0) : null);
            current.SetCaptureItemSubtype47(physical, bank.TryGetValue(ItemSubtype47Key(physical), out int raw) && raw is >= 0 and <= 255 ? (byte)raw : null);
            current.SetItemLifespanExtension(physical, bank.TryGetValue(ItemLifespanExtensionKey(physical), out int extension) && extension is >= 0 and <= 255 ? (byte)extension : null);
        }
    }

    public static byte? ReadTerrainFlags2C(GameState state, int physical) =>
        state.NativeArmySimulation?.Parameters.Int32Values is { } bank && bank.GetValueOrDefault(VersionKey) == 1 &&
        bank.TryGetValue(TerrainFlags2CKey(physical), out int value) && value is >= 0 and <= 255 ? (byte)value : null;
}

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, ushort?> _captureItemOwners4A = [];
    private readonly Dictionary<int, byte?> _captureItemSubtypes47 = [];
    private readonly Dictionary<int, byte?> _itemLifespanExtensions = [];
    public void SetItemLifespanExtension(int physical, byte? value)
    { Check(physical, 202, "item lifespan extension"); _itemLifespanExtensions[physical] = value; }
    public byte? ReadItemLifespanExtension(int physical) => _itemLifespanExtensions.GetValueOrDefault(physical);
    public void SetCaptureItemOwner4A(int physical, ushort? value)
    { Check(physical, 202, "capture Item4A"); _captureItemOwners4A[physical] = value; }
    public ushort? ReadCaptureItemOwner4A(int physical) => _captureItemOwners4A.GetValueOrDefault(physical);
    public void SetCaptureItemSubtype47(int physical, byte? value)
    { Check(physical, 202, "capture Item47"); _captureItemSubtypes47[physical] = value; }
    public byte? ReadCaptureItemSubtype47(int physical) => _captureItemSubtypes47.GetValueOrDefault(physical);
}
