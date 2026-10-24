using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record LoadedGrowthModifierDefinitions(S14GrowthModifierCatalog Catalog, SourceProvenance Source);
public sealed record GrowthAdvantageReceipt(int Value, int[] QualifiedStateSlots, int[] OwnedCityCounts);

/// <summary>Actual2AD9E0(7), registered City count and saved native modifiers.
/// The source-created field bank is allocated once; all later reads use saved
/// current fields. Current City District/Force/validity come from this capture.</summary>
public sealed class PersonGrowthCurrentQueries
{
    private const string Prefix = "ce-growth-current:";
    private const int TraitGrowthScale = 1;
    private const int ProvinceAdvantageGrowthScale = 1;
    private const int DoctrineGrowthScale = 5;
    private const int PolicyGrowthScale = 1;
    public static string VersionKey => Prefix + "version";
    public static string WorldMode40Key => Prefix + "world:40";
    public static string StateAdvantageKey(int slot) => Prefix + $"state:{slot}:30";
    public static string CityStateKey(int slot) => Prefix + $"city:{slot}:CE";
    public static string RequirementKey(int slot) => Prefix + $"advantage:{slot}:B3";
    public static string EffectKey(int slot, int index) => Prefix + $"advantage:{slot}:effect:{index}";
    public static string MagnitudeKey(int slot, int index) => Prefix + $"advantage:{slot}:magnitude:{index}";
    private static readonly string[] ParameterKeys = [SimulationParameterKeys.PersonGrowth.TraitGrowthScale, SimulationParameterKeys.PersonGrowth.ProvinceAdvantageGrowthScale, SimulationParameterKeys.PersonGrowth.DoctrineGrowthScale, SimulationParameterKeys.PersonGrowth.PolicyGrowthScale];
    private readonly LoadedScenario _scenario;
    private readonly LoadedGrowthModifierDefinitions _definitions;
    internal LoadedGrowthModifierDefinitions Definitions => _definitions;
    private readonly string _language, _ruleVersion;
    private readonly int[] _initialParameters;

    private PersonGrowthCurrentQueries(LoadedScenario scenario, LoadedGrowthModifierDefinitions definitions,
        string language, string ruleVersion, int[] initialParameters)
        => (_scenario, _definitions, _language, _ruleVersion, _initialParameters) =
            (scenario, definitions, language, ruleVersion, initialParameters);

    public static PersonGrowthCurrentQueries Load(GameResourceRepository repository, LoadedScenario scenario, string language)
    {
        var definitions = repository.LoadGrowthModifierDefinitions(scenario.FixedDataSource.SourcePath);
        if (scenario.Snapshot.Locale != language || definitions.Source.Sha256 != scenario.FixedDataSource.Sha256)
            throw new InvalidDataException("Growth modifier source identities differ.");
        return new(scenario, definitions, language, SimulationRuleIdentity.ForLocale(language),
            [TraitGrowthScale, ProvinceAdvantageGrowthScale, DoctrineGrowthScale, PolicyGrowthScale]);
    }

    public GameState InstallFresh(GameState state)
    {
        if (!Bound(state) || state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            _scenario.Snapshot.WorldParameters is not { RawBytes.Length: >= 51 } world ||
            state.NativeArmySimulation!.Parameters.Int32Values.Keys.Concat(state.NativeArmySimulation.Parameters.Float32Values.Keys)
                .Any(key => key.StartsWith(Prefix, StringComparison.Ordinal)) ||
            !state.Cities.Keys.Order().SequenceEqual(_scenario.Snapshot.Cities.Where(c => c.Slot != 0).Select(c => c.Slot).Order()))
            throw new InvalidDataException("Fresh growth modifiers cannot replace a saved or incomplete owner.");
        var values = new Dictionary<string, int>(state.NativeArmySimulation.Parameters.Int32Values);
        var floats = state.NativeArmySimulation.Parameters.Float32Values;
        values.Add(VersionKey, 1);
        values.Add(WorldMode40Key, BinaryPrimitives.ReadInt32LittleEndian(world.RawBytes.AsSpan(47, 4)));
        foreach (var row in _definitions.Catalog.States) values.Add(StateAdvantageKey(row.Slot), row.RawAdvantage30);
        foreach (var row in _definitions.Catalog.Advantages)
        {
            values.Add(RequirementKey(row.Slot), row.RequiredCitiesB3);
            for (int i = 0; i < 2; i++)
            { values.Add(EffectKey(row.Slot, i), row.EffectSlotsB4[i]); values.Add(MagnitudeKey(row.Slot, i), row.MagnitudesB6[i]); }
        }
        foreach (var row in _scenario.Snapshot.Cities)
            values.Add(CityStateKey(row.Slot), row.GroupCode);
        for (int i = 0; i < ParameterKeys.Length; i++)
        {
            string key = ParameterKeys[i];
            if (values.ContainsKey(key) || floats.ContainsKey(key)) throw new InvalidDataException("Growth global already has a current owner.");
            values.Add(key, _initialParameters[i]);
        }
        return state with { NativeArmySimulation = state.NativeArmySimulation with {
            Parameters = state.NativeArmySimulation.Parameters with { Int32Values = values } } };
    }

    public GrowthAdvantageReceipt? ReadAdvantage7(GameState state, SourceWorldContext current,
        ForceReference force)
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
                current.ReadRegistryReferences(RegistryKind.City) is not { } cities) return null;
            int count = 0;
            foreach (var city in cities)
            {
                if (city is null || city.Kind != RegistryKind.City || city.PhysicalSlot is not int cityId ||
                    current.ReadRegistryObjectNativeValidity(city) is not bool valid) return null;
                if (!valid) continue;
                if (ReadByte(state, CityStateKey(cityId)) is not byte province) return null;
                if (province != slot) continue;
                if (!state.Cities.TryGetValue(cityId, out CityState? saved)) return null;
                // City affiliation is already bound by the operation capture.
                // An explicit unknown current must not revive an initial byte.
                if (current.ReadBaseForceSlot(new(BaseKind.City, cityId)) is not int owner ||
                    (saved.ForceId ?? 0) != owner) return null;
                if (owner == forceId) count++;
            }
            counts.Add(count);
            if (count < required) continue;
            reached.Add(slot); total = unchecked(total + value);
        }
        return new(total, reached.ToArray(), counts.ToArray());
    }

    public int? ReadCurrent(GameState state, string key) => SavedBank(state) && ParameterKeys.Contains(key)
        ? ReadInt(state, key) : null;

    internal byte? ReadCityState(GameState state, int physical) => physical is >= 0 and < 52 && SavedBank(state)
        ? ReadByte(state, CityStateKey(physical)) : null;
    internal byte? ReadStateAdvantage(GameState state, int physical) => physical is >= 0 and < 23 && SavedBank(state)
        ? ReadByte(state, StateAdvantageKey(physical)) : null;
    internal byte? ReadAdvantageRequirement(GameState state, int physical) => physical is >= 0 and < 18 && SavedBank(state)
        ? ReadByte(state, RequirementKey(physical)) : null;
    internal byte? ReadAdvantageEffect(GameState state, int physical, int index) =>
        physical is >= 0 and < 18 && index is >= 0 and < 2 && SavedBank(state)
            ? ReadByte(state, EffectKey(physical, index)) : null;

    private bool Bound(GameState state) => state.NativeArmySimulation?.Parameters is { } p &&
        p.Language == _language && p.RuleVersion == _ruleVersion &&
        state.ContentHashes.GetValueOrDefault(_scenario.ScenarioSource.ResourceUri) == _scenario.ScenarioSource.Sha256 &&
        state.ContentHashes.GetValueOrDefault(_definitions.Source.ResourceUri) == _definitions.Source.Sha256;
    private bool SavedBank(GameState state) => Bound(state) && ReadInt(state, VersionKey) == 1;
    private static int? ReadInt(GameState state, string key) => state.NativeArmySimulation?.Parameters is { } p &&
        !p.Float32Values.ContainsKey(key) && p.Int32Values.TryGetValue(key, out int value) ? value : null;
    private static byte? ReadByte(GameState state, string key) => ReadInt(state, key) is int value && value is >= 0 and <= 255 ? (byte)value : null;
}
