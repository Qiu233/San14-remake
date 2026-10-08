using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record LoadedGrowthModifierDefinitions(S14GrowthModifierCatalog Catalog, SourceProvenance Source);
public sealed record OriginalGrowthAdvantageReceipt(int Value, int[] QualifiedStateSlots, int[] OwnedCityCounts);

/// <summary>Actual2AD9E0(7), registered City count and saved native modifiers.
/// The source-created field bank is allocated once; all later reads use saved
/// current fields. Current City District/Force/validity come from this capture.</summary>
public sealed class OriginalPersonGrowthCurrentQueries
{
    private const string Prefix = "ce-growth-current:";
    public static string VersionKey => Prefix + "version";
    public static string WorldMode40Key => Prefix + "world:40";
    public static string StateAdvantageKey(int slot) => Prefix + $"state:{slot}:30";
    public static string CityStateKey(int slot) => Prefix + $"city:{slot}:CE";
    public static string CityDistrict26Key(int slot) => Prefix + $"city:{slot}:26";
    public static string RequirementKey(int slot) => Prefix + $"advantage:{slot}:B3";
    public static string EffectKey(int slot, int index) => Prefix + $"advantage:{slot}:effect:{index}";
    public static string MagnitudeKey(int slot, int index) => Prefix + $"advantage:{slot}:magnitude:{index}";
    private static string SourceKey(string part, int index) => Prefix + $"source:{part}:{index}";
    private static readonly int[] Globals = [0x1A4C1D8, 0x1A4ABF4, 0x1A4A7D0, 0x1A4C1CC];
    private readonly LoadedScenario _scenario;
    private readonly LoadedGrowthModifierDefinitions _definitions;
    private readonly string _language, _executableHash;
    private readonly int[] _initialGlobals;

    private OriginalPersonGrowthCurrentQueries(LoadedScenario scenario, LoadedGrowthModifierDefinitions definitions,
        string language, string executableHash, int[] initialGlobals)
        => (_scenario, _definitions, _language, _executableHash, _initialGlobals) =
            (scenario, definitions, language, executableHash, initialGlobals);

    public static OriginalPersonGrowthCurrentQueries Load(OriginalGameRepository repository, LoadedScenario scenario, string language)
    {
        var definitions = repository.LoadGrowthModifierDefinitions(scenario.FixedDataSource.SourcePath);
        if (scenario.Snapshot.Locale != language || definitions.Source.Sha256 != scenario.FixedDataSource.Sha256)
            throw new InvalidDataException("Growth modifier source identities differ.");
        string expected = language switch { "SC" => OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256,
            "TC" => OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256,
            _ => throw new InvalidDataException("Growth modifier language is unsupported.") };
        byte[] exe = File.ReadAllBytes(Path.Combine(repository.SourceDirectory, $"SAN14CE_{language}.exe"));
        if (Convert.ToHexString(SHA256.HashData(exe)) != expected)
            throw new InvalidDataException("Growth globals require the pinned current executable.");
        using var pe = new PEReader(new MemoryStream(exe, false));
        if (pe.PEHeaders.PEHeader is not { Magic: PEMagic.PE32Plus, ImageBase: 0x140000000UL } ||
            pe.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            throw new InvalidDataException("Growth globals have a different ABI.");
        int[] globals = Globals.Select(semantic => {
            int rva = language == "SC" ? semantic : semantic - 0x1030;
            var section = pe.PEHeaders.SectionHeaders.Single(value => value.Name == ".data" &&
                rva >= value.VirtualAddress && (long)rva + 4 <= (long)value.VirtualAddress + value.SizeOfRawData);
            return BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(section.PointerToRawData + rva - section.VirtualAddress, 4));
        }).ToArray();
        return new(scenario, definitions, language, expected, globals);
    }

    public GameState InstallFresh(GameState state)
    {
        if (!Bound(state) || state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            _scenario.Snapshot.WorldParameters is not { RawBytes.Length: >= 51 } world ||
            Convert.ToHexString(SHA256.HashData(world.RawBytes)) != world.WorldSha256 ||
            state.NativeArmySimulation!.Parameters.Int32Values.Keys.Concat(state.NativeArmySimulation.Parameters.Float32Values.Keys)
                .Any(key => key.StartsWith(Prefix, StringComparison.Ordinal)) ||
            !state.Cities.Keys.Order().SequenceEqual(_scenario.Snapshot.Cities.Where(c => c.Slot != 0).Select(c => c.Slot).Order()))
            throw new InvalidDataException("Fresh growth modifiers cannot replace a saved or incomplete owner.");
        var values = new Dictionary<string, int>(state.NativeArmySimulation.Parameters.Int32Values);
        var floats = state.NativeArmySimulation.Parameters.Float32Values;
        values.Add(VersionKey, 1);
        values.Add(WorldMode40Key, BinaryPrimitives.ReadInt32LittleEndian(world.RawBytes.AsSpan(47, 4)));
        byte[] uri = SHA256.HashData(Encoding.UTF8.GetBytes(_definitions.Source.ResourceUri));
        byte[] hash = Convert.FromHexString(_definitions.Source.Sha256);
        for (int i = 0; i < 8; i++)
        {
            values.Add(SourceKey("uri", i), BinaryPrimitives.ReadInt32LittleEndian(uri.AsSpan(i * 4, 4)));
            values.Add(SourceKey("file", i), BinaryPrimitives.ReadInt32LittleEndian(hash.AsSpan(i * 4, 4)));
        }
        foreach (var row in _definitions.Catalog.States) values.Add(StateAdvantageKey(row.Slot), row.RawAdvantage30);
        foreach (var row in _definitions.Catalog.Advantages)
        {
            values.Add(RequirementKey(row.Slot), row.RequiredCitiesB3);
            for (int i = 0; i < 2; i++)
            { values.Add(EffectKey(row.Slot, i), row.EffectSlotsB4[i]); values.Add(MagnitudeKey(row.Slot, i), row.MagnitudesB6[i]); }
        }
        foreach (var row in _scenario.Snapshot.Cities)
        { values.Add(CityStateKey(row.Slot), row.GroupCode); values.Add(CityDistrict26Key(row.Slot), row.DistrictSlot); }
        for (int i = 0; i < Globals.Length; i++)
        {
            string key = OriginalTacticRemakeParameterSeed.Key(Globals[i]);
            if (values.ContainsKey(key) || floats.ContainsKey(key)) throw new InvalidDataException("Growth global already has a current owner.");
            values.Add(key, _initialGlobals[i]);
        }
        return state with { NativeArmySimulation = state.NativeArmySimulation with {
            Parameters = state.NativeArmySimulation.Parameters with { Int32Values = values } } };
    }

    public OriginalGrowthAdvantageReceipt? ReadAdvantage7(GameState state, OriginalEventInitializationContext current,
        OriginalForceReference force)
    {
        if (!SavedBank(state) || current.ReadForceNativeValidity(force) is not bool forceValid) return null;
        if (!forceValid) return new(0, [], []); //Actual2AD9E0 early default0.
        if (force.PhysicalSlot is not int forceId || ReadInt(state, WorldMode40Key) is not int mode) return null;
        if (mode is 2 or 4) return new(0, [], []); //Actual candidate producer skips.
        var reached = new List<int>(); var counts = new List<int>(); int total = 0;
        for (int slot = 0; slot < 23; slot++)
        {
            if (current.ReadStaticTableReference(0xDC80, slot)?.PhysicalSlot is not int physical || physical != slot) return null;
            if (physical == 0) continue;
            if (ReadByte(state, StateAdvantageKey(slot)) is not byte raw) return null;
            int advantage = raw <= 17 ? raw : 0;
            if (current.ReadStaticTableReference(0x85990, advantage)?.PhysicalSlot != advantage) return null;
            if (advantage == 0) continue;
            int matching = -1;
            for (int i = 0; i < 2; i++)
            {
                if (ReadByte(state, EffectKey(advantage, i)) is not byte effect) return null;
                if (effect == 7) { matching = i; break; }
            }
            if (matching < 0) continue;
            if (ReadByte(state, RequirementKey(advantage)) is not byte required ||
                ReadByte(state, MagnitudeKey(advantage, matching)) is not byte value ||
                current.ReadRegistryReferences(OriginalRegistryKind.City) is not { } cities) return null;
            int count = 0;
            foreach (var city in cities)
            {
                if (city is null || city.Kind != OriginalRegistryKind.City || city.PhysicalSlot is not int cityId ||
                    current.ReadRegistryObjectNativeValidity(city) is not bool valid) return null;
                if (!valid) continue;
                if (ReadByte(state, CityStateKey(cityId)) is not byte province) return null;
                if (province != slot) continue;
                if (!state.Cities.TryGetValue(cityId, out CityState? saved)) return null;
                if (saved.DistrictId is null)
                {
                    //Core null does not itself establish native District0.
                    //Use only a saved byte whose actual source allocation was
                    //known0/out-of-range; a missing owner or departed nonzero
                    //native district remains unresolved. The original getter
                    //maps >51 to the real District0 pointer before validity.
                    if (ReadByte(state, CityDistrict26Key(cityId)) is not byte district || district is > 0 and <= 51)
                        return null;
                    current.SetCityDistrictSlot(cityId, district);
                }
                if (current.ReadBaseForceSlot(new(OriginalBaseKind.City, cityId)) is not int owner ||
                    (saved.ForceId ?? 0) != owner) return null;
                if (owner == forceId) count++;
            }
            counts.Add(count);
            if (count < required) continue;
            reached.Add(slot); total = unchecked(total + value);
        }
        return new(total, reached.ToArray(), counts.ToArray());
    }

    public int? ReadGlobal(GameState state, int ordinal) => SavedBank(state) && ordinal is >= 0 and < 4
        ? ReadInt(state, OriginalTacticRemakeParameterSeed.Key(Globals[ordinal])) : null;

    private bool Bound(GameState state) => state.NativeArmySimulation?.Parameters is { } p &&
        p.Language == _language && p.ExecutableSha256 == _executableHash &&
        state.ContentHashes.GetValueOrDefault(_scenario.ScenarioSource.ResourceUri) == _scenario.ScenarioSource.Sha256 &&
        state.ContentHashes.GetValueOrDefault(_definitions.Source.ResourceUri) == _definitions.Source.Sha256;
    private bool SavedBank(GameState state)
    {
        if (!Bound(state) || ReadInt(state, VersionKey) != 1) return false;
        byte[] uri = SHA256.HashData(Encoding.UTF8.GetBytes(_definitions.Source.ResourceUri));
        byte[] hash = Convert.FromHexString(_definitions.Source.Sha256);
        return Enumerable.Range(0, 8).All(i =>
            ReadInt(state, SourceKey("uri", i)) == BinaryPrimitives.ReadInt32LittleEndian(uri.AsSpan(i * 4, 4)) &&
            ReadInt(state, SourceKey("file", i)) == BinaryPrimitives.ReadInt32LittleEndian(hash.AsSpan(i * 4, 4)));
    }
    private static int? ReadInt(GameState state, string key) => state.NativeArmySimulation?.Parameters is { } p &&
        !p.Float32Values.ContainsKey(key) && p.Int32Values.TryGetValue(key, out int value) ? value : null;
    private static byte? ReadByte(GameState state, string key) => ReadInt(state, key) is int value && value is >= 0 and <= 255 ? (byte)value : null;
}
