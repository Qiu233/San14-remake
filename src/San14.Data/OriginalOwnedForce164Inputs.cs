using San14.Core;

namespace San14.Data;

/// <summary>TC Force164's current Int32 and the real CSan14ExpData table0.
/// Address naming avoids conflating this with Force162 or a UI field whose
/// label has not been proved. ExCe loading reuses the production repository.</summary>
public sealed class OriginalOwnedForce164Inputs
{
    private const string Prefix = "ce-force164-current:";
    public const string VersionKey = Prefix + "version";
    public static string ForceValueKey(int physical) => Prefix + $"force:{physical}:164";
    public static string ThresholdKey(int physical) => Prefix + $"row:{physical}:threshold10";
    public SourceProvenance Source { get; }
    public string Language { get; }
    private readonly LoadedExperienceData _experience;
    private readonly string _ruleVersion;

    private OriginalOwnedForce164Inputs(LoadedExperienceData experience, string language)
    {
        _experience = experience; Source = experience.Source; Language = language;
        _ruleVersion = SimulationRuleIdentity.ForLocale(language);
        if (experience.Document.Tables.Count != 4 || experience.Doctrine is not
            { TableIndex: 0, TableOffset: 36, TableLength: 114, RecordStride: 10 } ||
            experience.Doctrine.Records.Count != 11 ||
            experience.Doctrine.Records.Where((r, i) => r.Index != i).Any() ||
            string.IsNullOrWhiteSpace(Source.ResourceUri))
            throw new InvalidDataException("Force164 requires the complete ExCe eleven-row table0 source.");
    }
    public static OriginalOwnedForce164Inputs Load(OriginalGameRepository repository, string language)
        => FromLoaded(repository.LoadExperienceData(OriginalExperienceVariant.ExCe), language);
    public static OriginalOwnedForce164Inputs FromLoaded(LoadedExperienceData experience, string language)
        => new(experience, language);

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != Language ||
            simulation.Parameters.RuleVersion != _ruleVersion || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            simulation.Parameters.Int32Values.Keys.Any(k => k.StartsWith(Prefix, StringComparison.Ordinal)) ||
            state.ContentHashes.TryGetValue(Source.ResourceUri, out string? hash) && hash != Source.Sha256)
            throw new InvalidDataException("Force164 can only initialize its unused ordinary fresh current owner.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        //227070 qword160=0 initializes164.237CC0's static branch skips its
        //runtime window; all52 concrete Force receivers retain ctor0.
        for (int i = 0; i < 52; i++) values.Add(ForceValueKey(i), 0);
        foreach (var row in _experience.Doctrine.Records) values.Add(ThresholdKey(row.Index), row.SignedThreshold);
        return state with
        {
            ContentHashes = new(state.ContentHashes) { [Source.ResourceUri] = Source.Sha256 },
            NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } }
        };
    }

    public void BindCurrent(GameState state, OriginalEventInitializationContext current)
        => current.Force164CurrentReader = physical => ReadCurrent(state, physical);
    public static int? ReadCurrent(GameState state, int physical)
        => (uint)physical < 52 && state.NativeArmySimulation?.Parameters.Int32Values is { } values &&
            values.GetValueOrDefault(VersionKey) == 1 && values.TryGetValue(ForceValueKey(physical), out int value) ? value : null;

    /// <summary>32C330, in actual pointer-table order. Native row0 is invalid
    /// by242A00's physical offset test. Rows1..10 are concrete valid receivers;
    /// the source returns the last threshold reached, without sorting.</summary>
    public static int? ReadLevel(GameState state, int value)
    {
        if (state.NativeArmySimulation?.Parameters.Int32Values is not { } values || values.GetValueOrDefault(VersionKey) != 1)
            return null;
        int last = 0;
        for (int physical = 1; physical < 11; physical++)
        {
            if (!values.TryGetValue(ThresholdKey(physical), out int threshold)) return null;
            if (value < threshold) return last;
            last = physical;
        }
        return last;
    }
    public static int? ReadMaximumThreshold(GameState state)
        => state.NativeArmySimulation?.Parameters.Int32Values is { } values && values.GetValueOrDefault(VersionKey) == 1 &&
            values.TryGetValue(ThresholdKey(10), out int value) ? value : null;
}

public sealed partial class OriginalEventInitializationContext
{
    private readonly Dictionary<int, int?> _force164CurrentValues = [];
    public Func<int, int?>? Force164CurrentReader { get; set; }
    public int? ReadForce164(OriginalForceReference force)
    {
        int physical = ReadForcePhysicalSlot(force) ?? throw new InvalidDataException("Unknown Force164 physical receiver.");
        return _force164CurrentValues.TryGetValue(physical, out int? value) ? value : Force164CurrentReader?.Invoke(physical);
    }
    public void SetForce164(int physical, int? value)
    { Check(physical, 52, "current Force164 receiver"); _force164CurrentValues[physical] = value; }
}
