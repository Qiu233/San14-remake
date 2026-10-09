using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Once-created Base globals and panel coefficients, followed by
/// saved-current queries. Missing legacy fields remain unknown. This supplies
/// resource/capacity inputs; it does not execute Army return or retirement.</summary>
public sealed class OriginalOwnedBaseCapacityInputs
{
    public const string Revision = "san14:ce-owned-base-capacity-inputs-20261007";
    public const string VersionKey = "ce-base-capacity-current:version";
    public static string TroopsExtraKey => SimulationParameterKeys.BaseCapacity.ExtraTroopCapacity;
    public static string PanelKey(int slot) => $"ce-base-capacity-current:panel:{slot}:36";
    private readonly LoadedBaseParameters _base;
    private readonly LoadedDoctrinePanels _panels;
    private readonly LoadedScenario _scenario;
    private readonly string _language;
    private OriginalOwnedBaseCapacityInputs(LoadedBaseParameters source, LoadedDoctrinePanels panels,
        LoadedScenario scenario, string language) => (_base, _panels, _scenario, _language) = (source, panels, scenario, language);

    public static OriginalOwnedBaseCapacityInputs Load(OriginalGameRepository repository, LoadedScenario scenario, string language)
    {
        var source = repository.LoadBaseParameters(language);
        var panels = repository.LoadDoctrinePanels(scenario.FixedDataSource.SourcePath);
        if (language is not ("SC" or "TC") || scenario.Snapshot.Locale != language ||
            panels.Source.Sha256 != scenario.FixedDataSource.Sha256 || panels.Catalog.Panels.Count != 80 ||
            panels.Catalog.Panels.Where((row, i) => row.Slot != i).Any() || source.Catalog.RuntimeTroopsExtraInitial is null)
            throw new InvalidDataException("Owned Base inputs require supported CE rules and coherent fixed-data sources.");
        return new(source, panels, scenario, language);
    }

    public GameState InstallFresh(GameState state)
    {
        if (!Bound(state) || state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeArmySimulation!.Parameters.Int32Values.ContainsKey(VersionKey) ||
            state.NativeArmySimulation.Parameters.Int32Values.ContainsKey(TroopsExtraKey) ||
            state.NativeArmySimulation.Parameters.Float32Values.ContainsKey(TroopsExtraKey))
            throw new InvalidDataException("Owned Base inputs require an unused fresh allocation.");
        var simulation = state.NativeArmySimulation!;
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values)
            { [VersionKey] = 1, [TroopsExtraKey] = _base.Catalog.RuntimeTroopsExtraInitial!.Value };
        foreach (var field in _base.Catalog.Values)
        {
            string key = field.Key;
            if (simulation.Parameters.Float32Values.ContainsKey(key) || values.TryGetValue(key, out int existing) && existing != field.Value)
                throw new InvalidDataException("Fresh Base globals disagree with an existing current source seed.");
            values.TryAdd(key, field.Value);
        }
        foreach (var panel in _panels.Catalog.Panels)
            if (!values.TryAdd(PanelKey(panel.Slot), panel.RawValue))
                throw new InvalidDataException("Fresh Base panel allocation already exists.");
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    /// <summary>Binds one operation-local current Context. No source reload,
    /// repair or mutation of the saved game takes place here.</summary>
    public bool BindCurrent(GameState state, OriginalEventInitializationContext context)
    {
        if (!Bound(state) || state.NativeArmySimulation!.Parameters.Int32Values.GetValueOrDefault(VersionKey) != 1)
            return false;
        foreach (var city in state.Cities.Values)
            context.SetCitySizeB8(city.Id, city.Size is { } size && Enum.IsDefined(size) ? (byte)((int)size + 1) : null);
        context.BasePostLoadParameterReader = key => Parameter(state, key);
        context.BaseRuntimeTroopsExtraReader = () => Parameter(state, TroopsExtraKey);
        context.BaseCapacityPercentageEnabledReader = () => OriginalBattleRemakeCurrentSettings.ReadAdvancedSettings(state);
        context.BaseCapacityPercentageReader = () => OriginalBattleRemakeCurrentSettings.ReadAdvancedParameter(state, 39);
        context.BaseForcePolicyEffectReader = (force, effect) =>
            context.ReadForceNativeValidity(force) is bool valid
                ? !valid ? 0 : context.ReadForcePhysicalSlot(force) is int id && state.Forces.TryGetValue(id, out var owner)
                    ? OriginalBattleRemakeCurrentForcePolicies.ReadForceEffect(state, owner, effect) : null
                : null;
        context.BaseForceModifierReader = (force, effect) =>
            context.ReadForcePhysicalSlot(force) is int id ? ReadPanelModifier(state, id, effect) : null;
        return true;
    }

    public int? ReadPanelModifier(GameState state, int forceId, int effect)
    {
        if (!Bound(state) || effect is < 1 or > 79 ||
            state.NativeArmySimulation!.Parameters.Int32Values.GetValueOrDefault(VersionKey) != 1 ||
            state.NativeArmySimulation.Forces.GetValueOrDefault(forceId) is not { } force ||
            force.ActivePolicyBitmap1CB is not { Length: 10 } active) return null;
        int index = effect - 1, mask = 1 << (index & 7);
        if ((active[index >> 3] & mask) == 0) return 0;
        if (force.SuppressedPolicyBitmap1D5 is not { Length: 10 } suppressed) return null;
        if ((suppressed[index >> 3] & mask) != 0) return 0;
        int? value = Parameter(state, PanelKey(effect));
        return value is >= short.MinValue and <= short.MaxValue ? value : null;
    }

    private bool Bound(GameState state) => state.NativeClock is not null &&
        state.NativeArmySimulation?.Parameters is { } parameters && parameters.Language == _language &&
        state.ContentHashes.GetValueOrDefault(_scenario.ScenarioSource.ResourceUri) == _scenario.ScenarioSource.Sha256 &&
        state.ContentHashes.GetValueOrDefault(_scenario.FixedDataSource.ResourceUri) == _scenario.FixedDataSource.Sha256;
    private static int? Parameter(GameState state, string key) =>
        state.NativeArmySimulation?.Parameters is { } parameters && !parameters.Float32Values.ContainsKey(key) &&
        parameters.Int32Values.TryGetValue(key, out int value) ? value : null;
}
