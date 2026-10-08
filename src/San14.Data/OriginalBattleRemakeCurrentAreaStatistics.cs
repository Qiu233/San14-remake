using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using San14.Core;

namespace San14.Data;

public sealed record OriginalBattleRemakeCurrentAreaStatisticsReceipt(ushort Owned68, ushort Terrain76,
    byte Force78, byte NeighborMask66, string Revision)
{
    //3311E0 includes neutral0 in CountOwned; later33E590 excludes0.
    //Its first-player cadence is not closed. Preserve the saved stage word,
    //but do not advertise neutral68 as a current runtime counter.
    public bool Owned68Current { get; init; } = true;
}

/// <summary>One synchronous operation's immutable copied area values. Capture
/// validates map ownership once; discard this snapshot after state changes.</summary>
public sealed class OriginalBattleRemakeCurrentAreaStatisticsSnapshot
{
    private readonly byte[] cities;
    private readonly ushort[] hexes;
    private readonly OriginalBattleRemakeCurrentAreaStatisticsReceipt[] statistics;
    private readonly int[] registeredSlots;
    private readonly string scenarioUri, scenarioHash;
    internal OriginalBattleRemakeCurrentAreaStatisticsSnapshot(byte[] cityFields, ushort[] hexFields,
        OriginalBattleRemakeCurrentAreaStatisticsReceipt[] currentFields, int[] registeredAreas, string sourceUri, string sourceHash)
    { cities = cityFields; hexes = hexFields; statistics = currentFields; registeredSlots = registeredAreas; scenarioUri = sourceUri; scenarioHash = sourceHash; }
    public OriginalBattleRemakeCurrentAreaStatisticsReceipt? Read(int physicalAreaSlot)
    {
        if (physicalAreaSlot is < 0 or > 500) throw new InvalidDataException("Current area snapshot requires native physical slot0..500.");
        return statistics[physicalAreaSlot];
    }
    public bool MaterializeIntoCurrent(OriginalEventInitializationContext current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.StaticSource?.ResourceUri != scenarioUri || current.StaticSource.Sha256 != scenarioHash) return false;
        for (int slot = 0; slot < 501; slot++)
        {
            OriginalBattleRemakeCurrentAreaStatisticsReceipt stats = statistics[slot];
            current.SetAreaTablePointer(slot, slot); current.SetAreaValidityCode(slot, cities[slot]);
            current.SetAreaHexIndex(slot, hexes[slot]); current.SetAreaRawField68(slot, stats.Owned68Current ? stats.Owned68 : null);
            current.SetAreaRawField76(slot, stats.Terrain76); current.SetAreaRawField78(slot, stats.Force78);
        }
        current.SetRegisteredAreaReferences(registeredSlots.Select(slot => (OriginalAreaReference?)new(slot)).ToArray());
        return true;
    }
}

/// <summary>Explicit fresh3311E0 statistics and fixed-row64 masks, saved in the
/// current parameter owner. Queries never recount Core territory or import a
/// missing save field. A changed ownership/topology input invalidates this finite
/// receipt until a source-backed writer supplies a new owner.</summary>
public static class OriginalBattleRemakeCurrentAreaStatistics
{
    public const string ProfileId = "san14:ce-owned-area-statistics-20261002";
    private const string Prefix = "ce-area-statistics-current:";
    public const int CurrentVersion = 2;
    private const int Version = CurrentVersion;
    private static string OwnerKey => Prefix + "profile-version";
    private static string RuntimeCounterKey(int slot) => $"{Prefix}area:{slot}:runtime-33E590";
    private static string RegistryCountKey => Prefix + "registered-area-count";
    private static string RegistrySlotKey(int ordinal) => $"{Prefix}registered-area:{ordinal}";
    public static string FieldKey(int physicalAreaSlot, int nativeOffset) =>
        $"{Prefix}area:{physicalAreaSlot}:field:{nativeOffset:X2}";
    private static string FingerprintKey(string kind, int ordinal) => $"{Prefix}fingerprint:{kind}:{ordinal}";

    /// <summary>One explicit ordinary-CE fresh initialization stage. The source
    /// records establish fixed Area34/3A/66 and identity; the supplied operation
    /// capture must own every current Hex10/12/14 and terrain2A input. The reused
    /// native counter preserves original pointer-table order and classification.</summary>
    public static GameState InstallFresh(GameState state, LoadedScenario source, LoadedMap map,
        OriginalEventInitializationContext current)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(map); ArgumentNullException.ThrowIfNull(current);
        if (state.NativeArmySimulation is not { } simulation || !ProfileMatches(simulation.Parameters) ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != simulation.Parameters.Language ||
            source.Snapshot.Areas.Count != 501 || map.Map is not { Width: 220, Height: 220, Cells.Length: 48400 } ||
            state.MapSha256 != map.Source.Sha256 || !SourceMatches(state, source.ScenarioSource) ||
            !SourceMatches(state, source.FixedDataSource) || !SourceMatches(state, map.Source) ||
            current.StaticSource?.ResourceUri != source.ScenarioSource.ResourceUri ||
            current.StaticSource.Sha256 != source.ScenarioSource.Sha256 ||
            source.Snapshot.WorldParameters?.RawField48 is not int mode || unchecked((uint)mode - 100u) <= 99u ||
            simulation.Parameters.Int32Values.Keys.Concat(simulation.Parameters.Float32Values.Keys)
                .Any(key => key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Fresh area statistics require an unowned ordinary CE source allocation and complete current map capture.");

        if (!source.Snapshot.Areas.Select(row => row.Slot).SequenceEqual(Enumerable.Range(0, 501)) ||
            !state.Areas.Keys.Order().SequenceEqual(source.Snapshot.Areas.Where(row => row.Slot != 0 && row.CitySlot is >= 1 and <= 51)
                .Select(row => row.Slot).Order()) || WorldFingerprint(state) is not byte[] worldFingerprint ||
            TerrainFingerprint(current, source, map) is not byte[] terrainFingerprint)
            throw new InvalidDataException("Fresh area statistics have incomplete or contradictory current map identities.");
        _ = ScenarioTerritory.Build(map.Map, source.Snapshot);
        var native = new OriginalEventInitializationContext { SpecialModeWorldPresent = true, RawWorldField48 = mode };
        var masks = new byte[501];
        foreach (AreaPreviewRecord row in source.Snapshot.Areas)
        {
            if (row.CitySlot > 51 || row.HexId >= 48400 || row.RawNeighborSlots is not { Count: 8 } ||
                row.RemainingBytesHex is not { Length: 52 })
                throw new InvalidDataException("Fresh area statistics need the full fixed65-byte row and all eight raw neighbor slots.");
            byte[] fixedTail;
            try { fixedTail = Convert.FromHexString(row.RemainingBytesHex); }
            catch (FormatException exception) { throw new InvalidDataException("Fixed area source bytes are malformed.", exception); }
            for (int index = 0; index < 8; index++)
                if (row.RawNeighborSlots[index] != BinaryPrimitives.ReadUInt16LittleEndian(fixedTail.AsSpan(8 + index * 2, 2)))
                    throw new InvalidDataException("Fresh area neighbor slots disagree with their fixed source bytes.");
            masks[row.Slot] = fixedTail[25]; //Fixed row64, not scenario flags38.
            native.SetAreaTablePointer(row.Slot, row.Slot);
            native.SetAreaValidityCode(row.Slot, row.CitySlot);
            native.SetAreaHexIndex(row.Slot, row.HexId);
            //Ctor/reset zero68/76/78 before the static statistics producer;
            //invalid source slots are skipped and retain these explicit values.
            native.SetAreaRawField68(row.Slot, 0); native.SetAreaRawField76(row.Slot, 0); native.SetAreaRawField78(row.Slot, 0);
            if (row.Slot != 0 && row.CitySlot != 0 && (!state.Areas.TryGetValue(row.Slot, out AreaState? area) ||
                area.Id != row.Slot || area.CityId != row.CitySlot || area.HexId != row.HexId))
                throw new InvalidDataException("Fresh current area identity differs from its fixed source.");
        }
        for (int slot = 0; slot < 31; slot++)
        {
            OriginalSurfaceTypeReference? surface = current.ReadSurfaceTypeTableReference(slot);
            if (surface?.PhysicalSlot != slot || current.ReadSurfaceTypeRawField2A(surface) is not ushort field)
                throw new InvalidDataException("Fresh area statistics have an unknown native terrain2A table input.");
            native.SetSurfaceTypeTablePointer(slot, slot); native.SetSurfaceTypeRawField2A(slot, field);
        }
        for (int slot = 0; slot < 48400; slot++)
        {
            var cell = map.Map.Cells[slot];
            OriginalHexReference? hex = current.ReadHexTableReference(slot);
            if (cell.Id != slot || cell.AreaSlot > 500 || hex?.PhysicalSlot != slot ||
                current.ReadHexAreaSlot(hex) != cell.AreaSlot || current.ReadHexSurfaceCode(hex) is not byte surface ||
                current.ReadHexForceSlot(hex) is not byte force || force > 51 ||
                !state.Territory.TryGetValue(slot, out TerritoryState? territory) || territory.Id != slot ||
                (territory.AreaId ?? 0) != cell.AreaSlot || (territory.ForceId ?? 0) != force)
                throw new InvalidDataException("Fresh area statistics have an unknown or contradictory current Hex receiver.");
            native.SetHexTablePointer(slot, slot); native.SetHexAreaSlot(slot, cell.AreaSlot);
            native.SetHexSurfaceCode(slot, surface); native.SetHexForceSlot(slot, force);
        }
        _ = OriginalAreaMapStatisticsInitialization.Apply(1, native);
        //Actual source31F510 registration, in the501 table's native order.
        //The runtime33E590 writer walks this+B0 list, not the whole area table.
        var areaPool = new OriginalCurrentListPool(native, 1, 501);
        native.SetRegistryDescriptor(OriginalRegistryKind.Area, new(areaPool.AllocateDescriptor().Identity));
        for (int slot = 0; slot < 501; slot++)
            _ = OriginalObjectRegistration.Register(new(OriginalRegistryKind.Area, slot), native);
        IReadOnlyList<OriginalAreaReference?> registered = native.ReadRegisteredAreaReferences()
            ?? throw new InvalidDataException("Fresh area registration sequence is unknown.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        values.Add(OwnerKey, Version);
        values.Add(RegistryCountKey, registered.Count);
        for (int ordinal = 0; ordinal < registered.Count; ordinal++)
            values.Add(RegistrySlotKey(ordinal), registered[ordinal]?.PhysicalSlot
                ?? throw new InvalidDataException("Fresh registered area receiver is null."));
        SaveFingerprint(values, "world", worldFingerprint);
        SaveFingerprint(values, "environment", WorldFingerprint(state, includeTerritory: false)!);
        SaveFingerprint(values, "terrain", terrainFingerprint);
        SaveFingerprint(values, "scenario", SourceFingerprint(source.ScenarioSource));
        SaveFingerprint(values, "fixed", SourceFingerprint(source.FixedDataSource));
        SaveFingerprint(values, "map", SourceFingerprint(map.Source));
        foreach (AreaPreviewRecord row in source.Snapshot.Areas)
        {
            var area = new OriginalAreaReference(row.Slot);
            values.Add(FieldKey(row.Slot, 0x34), row.CitySlot);
            values.Add(FieldKey(row.Slot, 0x3A), row.HexId);
            values.Add(FieldKey(row.Slot, 0x66), masks[row.Slot]);
            values.Add(FieldKey(row.Slot, 0x68), native.ReadAreaRawField68(area)!.Value);
            values.Add(FieldKey(row.Slot, 0x76), native.ReadAreaRawField76(area)!.Value);
            values.Add(FieldKey(row.Slot, 0x78), native.ReadAreaRawField78(area)!.Value);
        }
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    /// <summary>Reached CTurnState step37's first call, SC42FDB4→33E590,
    /// before its2EC3E0 saved-Army152 loop. Recounts only native68 and78 over
    /// the complete captured Hex table, including the source neutral0 exclusion.
    /// Fixed topology/terrain inputs must remain bound;76 and66 are retained.
    /// A v1 owner upgrades only here after its fixed receivers/terrain are
    /// independently proven. Loading and read-only queries never upgrade it.</summary>
    public static GameState RefreshAtNativeRegionStep(GameState state, LoadedScenario source, LoadedMap map,
        OriginalEventInitializationContext current)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(map); ArgumentNullException.ThrowIfNull(current);
        Dictionary<string, int>? saved = SourceBoundValues(state);
        if (saved is null || ReadRegisteredSlots(saved) is not int[] registered || source.Metadata.FormatMarker != 0x66 || source.Snapshot.Areas.Count != 501 ||
            map.Map is not { Width: 220, Height: 220, Cells.Length: 48400 } ||
            source.Snapshot.Locale != state.NativeArmySimulation!.Parameters.Language ||
            current.StaticSource?.ResourceUri != source.ScenarioSource.ResourceUri ||
            current.StaticSource.Sha256 != source.ScenarioSource.Sha256 ||
            !MatchesFingerprint(saved, "scenario", SourceFingerprint(source.ScenarioSource)) ||
            !MatchesFingerprint(saved, "fixed", SourceFingerprint(source.FixedDataSource)) ||
            !MatchesFingerprint(saved, "map", SourceFingerprint(map.Source)) ||
            WorldFingerprint(state, includeTerritory: false) is not byte[] environment ||
            WorldFingerprint(state) is not byte[] world ||
            TerrainFingerprint(current, source, map) is not byte[] terrainFingerprint ||
            !HasFingerprint(saved, "environment") ||
            (saved[OwnerKey] == Version && (!MatchesFingerprint(saved, "environment", environment) ||
                !MatchesFingerprint(saved, "terrain", terrainFingerprint))) ||
            !state.Areas.Keys.Order().SequenceEqual(source.Snapshot.Areas.Where(row => row.Slot != 0 && row.CitySlot is >= 1 and <= 51)
                .Select(row => row.Slot).Order()))
            throw new InvalidDataException("Native step37 area refresh requires its complete owned ordinary map and unchanged fixed terrain/topology.");

        var cityCodes = new byte[501]; var centers = new ushort[501]; var forces = new byte[48400];
        var memberships = new ushort[48400]; var representativeForces = new byte[501]; var counts = new int[501];
        for (int slot = 0; slot < 501; slot++)
        {
            AreaPreviewRecord row = source.Snapshot.Areas[slot];
            if (row.Slot != slot || ReadFields(saved, slot) is null || Byte(saved, FieldKey(slot, 0x34)) is not byte city || city > 51 ||
                Word(saved, FieldKey(slot, 0x3A)) is not ushort center || center >= 48400 || city != row.CitySlot || center != row.HexId ||
                current.ReadAreaTableReference(slot)?.PhysicalSlot != slot || current.ReadAreaValidityCode(new(slot)) != city ||
                current.ReadAreaHexIndex(new(slot)) != center)
                throw new InvalidDataException("Native step37 area refresh has an unknown current Area receiver.");
            if (slot != 0 && city is >= 1 and <= 51 &&
                (!state.Areas.TryGetValue(slot, out AreaState? area) || area.Id != slot || area.HexId != center ||
                 current.ReadAreaCitySlot(new(slot)) != area.CityId))
                throw new InvalidDataException("Native step37 area refresh has an unknown or contradictory current Area35 city-circle receiver.");
            cityCodes[slot] = city; centers[slot] = center;
        }
        for (int slot = 0; slot < 48400; slot++)
        {
            OriginalHexReference? hex = current.ReadHexTableReference(slot);
            var cell = map.Map.Cells[slot];
            if (cell.Id != slot || hex?.PhysicalSlot != slot || current.ReadHexAreaSlot(hex) is not ushort area ||
                area != cell.AreaSlot || area > 500 || current.ReadHexForceSlot(hex) is not byte force || force > 51 ||
                !state.Territory.TryGetValue(slot, out TerritoryState? territory) || territory.Id != slot ||
                (territory.AreaId ?? 0) != area || (territory.ForceId ?? 0) != force)
                throw new InvalidDataException("Native step37 area refresh has an unknown or contradictory current Hex receiver.");
            memberships[slot] = area; forces[slot] = force;
        }
        if (saved[OwnerKey] == 1)
        {
            //v1 hashed mutable CityId/Structures but stored no terrain-input
            //receipt. Independently prove its retained76 against the captured
            //fixed terrain before upgrading; do not reconstruct its old
            //ownership counts, raw34, anchors, registry or missing save keys.
            var terrainCounts = new int[501];
            for (int slot = 0; slot < 48400; slot++)
            {
                int area = memberships[slot];
                byte surface = map.Map.Cells[slot].SurfaceCode;
                ushort terrain = current.ReadSurfaceTypeRawField2A(new(surface <= 30 ? surface : 0))!.Value;
                if (area != 0 && cityCodes[area] is >= 1 and <= 51 && terrain != 0)
                    terrainCounts[area] = unchecked(terrainCounts[area] + 1);
            }
            for (int slot = 0; slot < 501; slot++)
                if (Word(saved, FieldKey(slot, 0x76)) != OriginalAreaMapStatisticsInitialization.CountToNativeWord(terrainCounts[slot]))
                    throw new InvalidDataException("Legacy area owner cannot prove its retained terrain76 at the reached source writer.");
        }
        for (int slot = 0; slot < 501; slot++) representativeForces[slot] = forces[centers[slot]];
        //33E590 traverses the48400 Hex pointers in native table order. Its
        //saved area physical identity indexes the local dword count array.
        for (int slot = 0; slot < 48400; slot++)
        {
            int area = memberships[slot]; byte force = representativeForces[area];
            if (area != 0 && cityCodes[area] is >= 1 and <= 51 && force is >= 1 and <= 51 && forces[slot] == force)
                counts[area] = unchecked(counts[area] + 1);
        }
        var values = new Dictionary<string, int>(saved);
        var summary = new OriginalEventInitializationContext();
        for (int slot = 0; slot < 501; slot++)
        {
            summary.SetAreaTablePointer(slot, slot); summary.SetAreaValidityCode(slot, cityCodes[slot]);
            summary.SetAreaRawField68(slot, Word(values, FieldKey(slot, 0x68)));
        }
        foreach (int slot in registered)
        {
            //No extra validity guard in this list traversal. The saved receiver
            //and repeated entries retain the exact registration order.
            values[FieldKey(slot, 0x68)] = OriginalAreaMapStatisticsInitialization.CountToNativeWord(counts[slot]);
            values[FieldKey(slot, 0x78)] = representativeForces[slot];
            values[RuntimeCounterKey(slot)] = Version;
            summary.SetAreaRawField68(slot, Word(values, FieldKey(slot, 0x68)));
            //Reached928D10 is the single-area68→summary word copy. It tests
            //the receiver's native validity and does not clear dirty bits.
            if (summary.ReadAreaNativeValidity(new(slot)) == true)
                summary.WriteAreaMapStatisticsSummaryWord(slot, Word(values, FieldKey(slot, 0x68))!.Value);
        }
        ReplaceFingerprint(values, "world", world);
        ReplaceFingerprint(values, "environment", environment);
        ReplaceFingerprint(values, "terrain", terrainFingerprint);
        values[OwnerKey] = Version;
        NativeArmySimulationState simulation = state.NativeArmySimulation!;
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public static OriginalBattleRemakeCurrentAreaStatisticsReceipt? Read(GameState state, int physicalAreaSlot)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (physicalAreaSlot is < 0 or > 500) throw new InvalidDataException("Current area statistics require native physical slot0..500.");
        Dictionary<string, int>? values = BoundValues(state);
        return values is null ? null : ReadFields(values, physicalAreaSlot);
    }

    /// <summary>The caller supplies an independent operation capture. Validate
    /// the complete saved owner before writing any Area pointer/field into it.
    /// This method does not modify or replace the saved GameState.</summary>
    public static bool MaterializeIntoCurrent(GameState state, OriginalEventInitializationContext current)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(current);
        return Capture(state)?.MaterializeIntoCurrent(current) ?? false;
    }

    /// <summary>Validate the full owner and world fingerprint once, then copy
    /// the finite501 values. No mutable dictionary or GameState reference escapes
    /// into the snapshot, and no cross-operation cache is maintained.</summary>
    public static OriginalBattleRemakeCurrentAreaStatisticsSnapshot? Capture(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Dictionary<string, int>? values = BoundValues(state);
        if (values is null || ReadRegisteredSlots(values) is not int[] registered) return null;
        var cities = new byte[501]; var hexes = new ushort[501];
        var statistics = new OriginalBattleRemakeCurrentAreaStatisticsReceipt[501];
        for (int slot = 0; slot < 501; slot++)
        {
            if (ReadFields(values, slot) is not { } stats || Byte(values, FieldKey(slot, 0x34)) is not byte city || city > 51 ||
                Word(values, FieldKey(slot, 0x3A)) is not ushort hex || hex >= 48400) return null;
            cities[slot] = city; hexes[slot] = hex; statistics[slot] = stats;
        }
        KeyValuePair<string, string> source = state.ContentHashes.First(pair => Hash(pair.Value) is not null &&
            MatchesFingerprint(values, "scenario", SourceFingerprint(pair.Key, pair.Value)));
        return new(cities, hexes, statistics, registered, source.Key, source.Value);
    }

    private static OriginalBattleRemakeCurrentAreaStatisticsReceipt? ReadFields(Dictionary<string, int> values, int slot) =>
        Word(values, FieldKey(slot, 0x68)) is ushort owned && Word(values, FieldKey(slot, 0x76)) is ushort terrain &&
        Byte(values, FieldKey(slot, 0x78)) is byte force && force <= 51 && Byte(values, FieldKey(slot, 0x66)) is byte mask
            ? new(owned, terrain, force, mask, ProfileId)
                { Owned68Current = force != 0 || values.GetValueOrDefault(RuntimeCounterKey(slot)) is 1 or Version } : null;
    private static ushort? Word(Dictionary<string, int> values, string key) =>
        values.TryGetValue(key, out int value) && value is >= 0 and <= 65535 ? (ushort)value : null;
    private static byte? Byte(Dictionary<string, int> values, string key) =>
        values.TryGetValue(key, out int value) && value is >= 0 and <= 255 ? (byte)value : null;
    private static Dictionary<string, int>? BoundValues(GameState state)
    {
        Dictionary<string, int>? values = SourceBoundValues(state);
        if (values is null || WorldFingerprint(state, version: values[OwnerKey]) is not byte[] world ||
            !MatchesFingerprint(values, "world", world)) return null;
        return values;
    }
    private static Dictionary<string, int>? SourceBoundValues(GameState state)
    {
        if (state.NativeArmySimulation?.Parameters is not { } parameters || !ProfileMatches(parameters) ||
            state.NativeClock is null || parameters.Int32Values.GetValueOrDefault(OwnerKey) is not (1 or Version) ||
            ReadRegisteredSlots(parameters.Int32Values) is null) return null;
        Dictionary<string, int> values = parameters.Int32Values;
        if (!HasFingerprint(values, "world") || !HasFingerprint(values, "environment")) return null;
        if (values[OwnerKey] == Version && !HasFingerprint(values, "terrain")) return null;
        foreach (string kind in new[] { "scenario", "fixed", "map" })
            if (!state.ContentHashes.Any(pair => Hash(pair.Value) is not null &&
                (kind != "map" || pair.Value == state.MapSha256) &&
                MatchesFingerprint(values, kind, SourceFingerprint(pair.Key, pair.Value)))) return null;
        return values;
    }
    private static byte[]? WorldFingerprint(GameState state, bool includeTerritory = true, int version = Version) =>
        version switch { 1 => LegacyWorldFingerprint(state, includeTerritory), Version => CurrentWorldFingerprint(state, includeTerritory), _ => null };

    /// <summary>Exact v1 byte layout for reading existing saved owners only.
    /// Its historical CityId/Structures constraints remain in force until the
    /// actual source writer can prove and commit a v2 owner.</summary>
    private static byte[]? LegacyWorldFingerprint(GameState state, bool includeTerritory)
    {
        if (state.MapWidth != 220 || state.MapHeight != 220 || state.Territory.Count != 48400 || state.Structures is not { } structures ||
            state.MapSha256 is not string mapHash || Hash(mapHash) is null) return null;
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(mapHash);
        writer.Write(includeTerritory);
        for (int slot = 0; includeTerritory && slot < 48400; slot++)
        {
            if (!state.Territory.TryGetValue(slot, out TerritoryState? cell) || cell.Id != slot ||
                cell.AreaId is < 0 or > 500 || cell.ForceId is < 0 or > 51) return null;
            writer.Write(slot); writer.Write(cell.AreaId ?? 0); writer.Write(cell.ForceId ?? 0);
        }
        writer.Write(state.Areas.Count);
        foreach (var pair in state.Areas.OrderBy(pair => pair.Key))
        {
            AreaState area = pair.Value;
            if (pair.Key != area.Id || area.Id is < 1 or > 500 || area.CityId is < 1 or > 51 || area.HexId is < 0 or >= 48400) return null;
            writer.Write(area.Id); writer.Write(area.CityId); writer.Write(area.HexId ?? -1);
            if (includeTerritory) writer.Write(area.ForceId ?? -1);
        }
        writer.Write(structures.Count);
        foreach (var pair in structures.OrderBy(pair => pair.Key))
        {
            StructureState structure = pair.Value;
            writer.Write(pair.Key); writer.Write(structure.Id); writer.Write(structure.KindId);
            writer.Write(structure.HexId); writer.Write(structure.ForceId ?? -1); writer.Write(structure.Durability);
            writer.Write(structure.Completed); writer.Write(structure.SourceCityId ?? -1);
        }
        writer.Flush(); return SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }
    private static byte[]? CurrentWorldFingerprint(GameState state, bool includeTerritory)
    {
        if (state.MapWidth != 220 || state.MapHeight != 220 || state.Territory.Count != 48400 ||
            state.MapSha256 is not string mapHash || Hash(mapHash) is null) return null;
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(mapHash); writer.Write(Version); writer.Write(includeTerritory);
        //Membership is fixed terrain/topology even in the environment receipt.
        //Owner14 is mutable and only belongs to the full statistics snapshot.
        for (int slot = 0; slot < 48400; slot++)
        {
            if (!state.Territory.TryGetValue(slot, out TerritoryState? cell) || cell.Id != slot ||
                cell.AreaId is < 0 or > 500 || cell.ForceId is < 0 or > 51) return null;
            writer.Write(slot); writer.Write(cell.AreaId ?? 0);
            if (includeTerritory) writer.Write(cell.ForceId ?? 0);
        }
        writer.Write(state.Areas.Count);
        foreach (var pair in state.Areas.OrderBy(pair => pair.Key))
        {
            AreaState area = pair.Value;
            if (pair.Key != area.Id || area.Id is < 1 or > 500 || area.CityId is < 1 or > 51 ||
                area.HexId is not (>= 0 and < 48400) ||
                !state.Cities.TryGetValue(area.CityId, out CityState? city) || city.Id != area.CityId) return null;
            writer.Write(area.Id); writer.Write(area.HexId.Value);
            if (includeTerritory) { writer.Write(area.CityId); writer.Write(area.ForceId ?? -1); }
        }
        //Neither33E590 nor the preserved terrain76 counter reads Core
        //facilities. Their unknown/current contents are not map surface inputs.
        writer.Flush(); return SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }
    private static byte[]? TerrainFingerprint(OriginalEventInitializationContext current, LoadedScenario source, LoadedMap map)
    {
        if (current.TerrainTypeSource is not { } terrain || terrain.ResourceUri != source.FixedDataSource.ResourceUri ||
            terrain.Sha256 != source.FixedDataSource.Sha256 || Hash(current.TerrainTypeSourceSha256 ?? "") is null) return null;
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(current.TerrainTypeSourceSha256!);
        for (int slot = 0; slot < 31; slot++)
        {
            OriginalSurfaceTypeReference? surface = current.ReadSurfaceTypeTableReference(slot);
            if (surface?.PhysicalSlot != slot || current.ReadSurfaceTypeRawField2A(surface) is not ushort field) return null;
            writer.Write(field);
        }
        for (int slot = 0; slot < 48400; slot++)
        {
            OriginalHexReference? hex = current.ReadHexTableReference(slot);
            if (hex?.PhysicalSlot != slot || current.ReadHexSurfaceCode(hex) is not byte surface || surface != map.Map.Cells[slot].SurfaceCode)
                return null;
            writer.Write(surface);
        }
        writer.Flush(); return SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }
    private static int[]? ReadRegisteredSlots(Dictionary<string, int> values)
    {
        if (!values.TryGetValue(RegistryCountKey, out int count) || count is < 0 or > 501) return null;
        var slots = new int[count];
        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            if (!values.TryGetValue(RegistrySlotKey(ordinal), out int slot) || slot is < 0 or > 500) return null;
            slots[ordinal] = slot;
        }
        return slots;
    }
    private static bool ProfileMatches(NativeArmyParameterState parameters) => parameters.ExecutableSha256 ==
        (parameters.Language switch { "SC" => OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256,
            "TC" => OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256, _ => null });
    private static bool SourceMatches(GameState state, SourceProvenance source) =>
        !string.IsNullOrWhiteSpace(source.ResourceUri) && Hash(source.Sha256) is not null &&
        state.ContentHashes.GetValueOrDefault(source.ResourceUri) == source.Sha256;
    private static byte[] SourceFingerprint(SourceProvenance source) => SourceFingerprint(source.ResourceUri, source.Sha256);
    private static byte[] SourceFingerprint(string uri, string hash)
    {
        if (string.IsNullOrWhiteSpace(uri) || Hash(hash) is not byte[] bytes) throw new InvalidDataException("Area statistics source binding is malformed.");
        return SHA256.HashData(Encoding.UTF8.GetBytes(uri).Concat(bytes).ToArray());
    }
    private static byte[]? Hash(string hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit) ? Convert.FromHexString(hash) : null;
    private static void SaveFingerprint(Dictionary<string, int> values, string kind, byte[] hash)
    {
        for (int ordinal = 0; ordinal < 8; ordinal++) values.Add(FingerprintKey(kind, ordinal), BinaryPrimitives.ReadInt32LittleEndian(hash.AsSpan(ordinal * 4, 4)));
    }
    private static void ReplaceFingerprint(Dictionary<string, int> values, string kind, byte[] hash)
    {
        for (int ordinal = 0; ordinal < 8; ordinal++) values[FingerprintKey(kind, ordinal)] = BinaryPrimitives.ReadInt32LittleEndian(hash.AsSpan(ordinal * 4, 4));
    }
    private static bool MatchesFingerprint(Dictionary<string, int> values, string kind, byte[] hash) =>
        Enumerable.Range(0, 8).All(ordinal => values.TryGetValue(FingerprintKey(kind, ordinal), out int value) &&
            value == BinaryPrimitives.ReadInt32LittleEndian(hash.AsSpan(ordinal * 4, 4)));
    private static bool HasFingerprint(Dictionary<string, int> values, string kind) =>
        Enumerable.Range(0, 8).All(ordinal => values.ContainsKey(FingerprintKey(kind, ordinal)));
}
