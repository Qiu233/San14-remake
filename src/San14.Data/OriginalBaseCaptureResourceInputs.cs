using San14.Core;

namespace San14.Data;

/// <summary>Project capture resource rules, installed once in a fresh parameter
/// bank. Current capture operations continue to read saved values.</summary>
public sealed class OriginalBaseCaptureResourceInputs
{
    public const string VersionKey = "ce-base-capture-resources:version";
    private const int MinimumResourceKeepPercent = 10;
    private const int CommanderCharmDivisor = 3;
    private const int CapturedCityGoldFloor = 5000;
    private const int CentralTerritoryRadius = 3;
    private const int CapturedCitySuppliesFloor = 20000;
    private const int CapturedWallFloorPercent = 40;
    public static readonly int[] TcRvas = [0x1A4A784, 0x1A4A790, 0x1A4A79C, 0x1A4A7A8, 0x1A4A7B4, 0x1A4A7CC];
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentParameterField> Fields { get; }
    private OriginalBaseCaptureResourceInputs(string language, SourceProvenance source,
        OriginalBattleRemakeCurrentParameterField[] fields) => (Language, Source, Fields) = (language, source, fields);

    public static OriginalBaseCaptureResourceInputs Load(OriginalGameRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = SimulationRuleSources.Provenance("san14-rule://simulation/base-capture-resources", language);
        int[] rules = [MinimumResourceKeepPercent, CommanderCharmDivisor, CapturedCityGoldFloor,
            CentralTerritoryRadius, CapturedCitySuppliesFloor, CapturedWallFloorPercent];
        // Legacy addresses retain the existing saved parameter keys.
        var fields = TcRvas.Select((tc, index) =>
        {
            int semantic = tc + 0x1030, physical = language == "SC" ? semantic : tc;
            return new OriginalBattleRemakeCurrentParameterField(semantic, physical, rules[index], string.Empty);
        }).ToArray();
        return new(language, source, fields);
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != Language ||
            simulation.Parameters.RuleVersion != Source.RuleVersion || simulation.Parameters.Int32Values.ContainsKey(VersionKey))
            throw new InvalidDataException("Fresh Base capture parameters cannot replace current saved values.");
        var integers = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        foreach (var field in Fields)
        {
            string key = OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva);
            if (simulation.Parameters.Float32Values.ContainsKey(key) || integers.TryGetValue(key, out int old) && old != field.Value)
                throw new InvalidDataException("Base capture parameters disagree with an installed source field.");
            integers.TryAdd(key, field.Value);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = integers } } };
    }
    public static int? ReadCurrentTc(GameState state, int tcRva) =>
        state.NativeArmySimulation?.Parameters is { } bank && bank.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        bank.Int32Values.TryGetValue(OriginalTacticRemakeParameterSeed.Key(tcRva + 0x1030), out int value) ? value : null;
}
