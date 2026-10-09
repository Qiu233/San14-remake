using San14.Core;

namespace San14.Data;

/// <summary>TC174EC0 current globals and the independent Force13/1BE,
/// Area3C/3E/40/52 ctor/static checkpoint. None of these producers runs on load.</summary>
public sealed class OriginalNormalArmyAftermathInputs
{
    public const string VersionKey = "ce-normal-army-aftermath-current:version";
    public const string Revision = "san14:ce-normal-army-aftermath-current-20261007";
    private const int AreaDevelopmentReduction = 1;
    private const int SecurityLossProbabilityNumerator = 40;
    private const int SecurityLossProbabilityDivisor = 40;
    private const int SecurityLossBaseMultiplier = 3;
    private const int SecurityLossDenominatorOffset = 30;
    private const int AreaQuantityFixedReduction = 50;
    private const int AreaQuantityRandomReductionBound = 50;
    private const int HostileRelationCeiling = 15;
    private const int HostileRelationAdjustment = -1;
    private const int RelationBandOneMinimum = 1;
    private const int RelationBandTwoMinimum = 30;
    private const int RelationBandThreeMinimum = 50;
    private const int RelationBandFourMinimum = 70;
    private const int RelationBandFiveMinimum = 100;
    private static readonly int[] TcRvas = [0x1A49964, 0x1A49970, 0x1A4997C,
        0x1A4AF78, 0x1A4AF84, 0x1A49988, 0x1A49994, 0x1A499B4, 0x1A499C0,
        0x1A491D8, 0x1A491B4, 0x1A491BC, 0x1A491C4, 0x1A491D0, 0x1A49844];
    private readonly string language, ruleVersion;
    private readonly int[] initial;
    public OriginalCeInitialAreaDevelopment Development { get; }

    private OriginalNormalArmyAftermathInputs(string locale, string version, int[] values,
        OriginalCeInitialAreaDevelopment development) => (language, ruleVersion, initial, Development) =
        (locale, version, values, development);

    public static OriginalNormalArmyAftermathInputs Load(OriginalGameRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var development = OriginalCeInitialAreaDevelopment.LoadCurrent(repository.SourceDirectory, language);
        int[] values = [AreaDevelopmentReduction, SecurityLossProbabilityNumerator, SecurityLossProbabilityDivisor,
            SecurityLossBaseMultiplier, SecurityLossDenominatorOffset, AreaQuantityFixedReduction,
            AreaQuantityRandomReductionBound, HostileRelationCeiling, HostileRelationAdjustment,
            RelationBandOneMinimum, RelationBandTwoMinimum, RelationBandThreeMinimum, RelationBandFourMinimum,
            RelationBandFiveMinimum, development.ImageInitialLevelChangeCoefficient];
        return new(language, development.RuleVersion, values, development);
    }

    public GameState InstallFresh(GameState state, LoadedScenario scenario)
    {
        if (!Bound(state) || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            scenario.Snapshot.Locale != language || scenario.Metadata.FormatMarker != 0x66 ||
            state.ContentHashes.GetValueOrDefault(scenario.ScenarioSource.ResourceUri) != scenario.ScenarioSource.Sha256 ||
            state.NativeArmySimulation!.Parameters.Int32Values.ContainsKey(VersionKey) ||
            state.NativeArmySimulation.Forces.Values.Any(f => f.NormalAftermath is not null) ||
            state.Areas.Values.Any(a => a.NativeNormalAftermath is not null))
            throw new InvalidDataException("Normal aftermath requires the unused own ctor/static allocation.");
        var sourceForces = scenario.Snapshot.Forces.ToDictionary(f => f.Slot);
        var sourceAreas = scenario.Snapshot.Areas.ToDictionary(a => a.Slot);
        var simulation = state.NativeArmySimulation!;
        var forces = simulation.Forces.ToDictionary(row => row.Key, row => {
            byte[] source = sourceForces[row.Key].RawIndexedBytes13?.ToArray()
                ?? throw new InvalidDataException("Fresh Force13 static source is unknown.");
            //227070 qword1BC=0 includes signed1BE. 237CC0's static8C!=0
            //does not transfer its runtime1BE branch.13 loads unconditionally.
            return row.Value with { NormalAftermath = new(source, 0) };
        });
        var areas = state.Areas.ToDictionary(row => row.Key, row => row.Value with
        {
            //331F50/336750 reset every concrete Area with226950. Its
            //qword3A=BD10 makes3C/3E/40 all0; static235800 skips these
            //three runtime-only words. Later source writers own changes.
            Development = new(0, 0, 0),
            NativeNormalAftermath = new(sourceAreas[row.Key].Amount0)
        }); //235800 first transferred word.
        var integers = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        for (int i = 0; i < TcRvas.Length; i++)
        {
            string key = Key(TcRvas[i]);
            if (simulation.Parameters.Float32Values.ContainsKey(key) || integers.TryGetValue(key, out int old) && old != initial[i])
                throw new InvalidDataException("Fresh normal aftermath parameter conflicts with another source seed.");
            integers[key] = initial[i];
        }
        GameState result = state with { Areas = areas, NativeArmySimulation = simulation with { Forces = forces,
            Parameters = simulation.Parameters with { Int32Values = integers } } };
        NativeNormalArmyAftermathStateValidation.Validate(result); return result;
    }

    public int? ReadTc(GameState state, int rva) => Bound(state) &&
        state.NativeArmySimulation!.Parameters is { } parameters && parameters.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        TcRvas.Contains(rva) && !parameters.Float32Values.ContainsKey(Key(rva)) &&
        parameters.Int32Values.TryGetValue(Key(rva), out int value) ? value : null;
    public static string Key(int tcRva) => OriginalTacticRemakeParameterSeed.Key(checked(tcRva + 0x1030));
    private bool Bound(GameState state) => state.NativeArmySimulation?.Parameters is { } p &&
        p.Language == language && p.RuleVersion == ruleVersion;
}
