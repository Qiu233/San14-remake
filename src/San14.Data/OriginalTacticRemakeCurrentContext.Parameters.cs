using San14.Core;

namespace San14.Data;

public sealed record OriginalTacticRemakeParameterSeedField(int SemanticScRva,
    int PhysicalRva, int Value, string Sha256);

/// <summary>Project-owned tactic rule defaults and an explicit producer of this game's own
/// current parameter allocation. This is never a read of original process state.</summary>
public sealed class OriginalTacticRemakeParameterSeed
{
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<OriginalTacticRemakeParameterSeedField> Fields { get; }

    private OriginalTacticRemakeParameterSeed(string language, SourceProvenance source,
        OriginalTacticRemakeParameterSeedField[] fields)
    { Language = language; Source = source; Fields = Array.AsReadOnly(fields); }

    public static string Key(int semanticScRva) => $"sc-rva:{semanticScRva:X8}";

    public static OriginalTacticRemakeParameterSeed LoadPinnedSeed(OriginalGameRepository repository,
        string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        SourceProvenance source = SimulationRuleSources.Provenance(
            "san14-remake://rules/tactic-parameters/v1", language);
        int delta = language == "TC" ? 0x1030 : 0;
        return new(language, source, InitialDefaults.Select(field => new OriginalTacticRemakeParameterSeedField(
            field.SemanticScRva, field.SemanticScRva - delta, field.Value, string.Empty)).ToArray());
    }

    /// <summary>Explicit copy into a new current owner. Later writes occur in
    /// that owner's dictionaries and are not reset from source at query time.</summary>
    public NativeArmyParameterState AllocateOwnedCurrentParameters(string revision)
    {
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Owned parameter allocation revision is missing.");
        return new($"{revision}:rule-seed", Language, Source.RuleVersion!,
            Fields.ToDictionary(field => Key(field.SemanticScRva), field => field.Value), []);
    }

    /// <summary>Only a fresh, empty owned Army session can construct these
    /// Force/World/manager records. Save decode never calls this producer.</summary>
    public GameState CreateFreshOwnedSimulation(GameState state, string revision)
    {
        if (state.NativeArmySimulation is not null || state.NativeClock is not null || state.Units.Count != 0)
            throw new InvalidDataException("Fresh owned native simulation cannot replace a saved or populated instance.");
        GameState initialized = PlayableArmyCreation.InitializeClock(state, revision);
        return initialized with { NativeArmySimulation = new(AllocateOwnedCurrentParameters(revision),
            initialized.Forces.ToDictionary(pair => pair.Key, pair => new NativeArmyForceState(0)
            {
                Flags12 = 0, Flags194 = 0,
                RelationsEA = pair.Value.SourceIndexedBytesEA?.ToArray(),
                ActivePolicyBitmap1CB = new byte[10], SuppressedPolicyBitmap1D5 = new byte[10]
            }),
            new NativeArmyWorldState(0) { Flags165C = 0, PendingArmyIds = [], ObjectPairs = [] }) };
    }

    private const int AbnormalCounterUpperBound = 120;
    private const int PrimaryWaitingCap = 100;
    private const int SecondaryWaitingCap = 150;
    private const int TertiaryWaitingCap = 200;
    private const int OrdinaryWaitingRate = 1;
    private const int InitialWaiting = 40;
    private const int Formation1CreationWaitingAdjustment = 8;
    private const int Formation2CreationWaitingAdjustment = -8;
    private const int Formation14CreationWaitingAdjustment = -8;
    private const int Formation1AutomaticWaitingAdjustment = 8;
    private const int Formation2AutomaticWaitingAdjustment = -8;
    private const int Formation14AutomaticWaitingAdjustment = -8;
    private const int ForceFlagWaitingReduction = 4;
    private const int NonplayerAutomaticTrialRate = 4;
    private const int PlayerDifficulty0TrialRate = 16;
    private const int PlayerDifficulty1TrialRate = 8;
    private const int PlayerDifficulty2TrialRate = 4;
    private const int HighDifficultyNonplayerTrialRate = 16;
    private const int EffectStageScale = 25;
    private const int StrengthEffectDivisor = 150;
    private const int IntelligenceEffectDivisor = 150;
    private const int HealingMinimum = 500;
    private const int StrengthDifferenceDivisor = 50;
    private const int IntelligenceDifferenceDivisor = 50;
    private const int EffectFactorFloorPercent = 10;
    private const int AbnormalGateBaseline = 90;
    private const int AbnormalGateHigherAbilityScale = 4;
    private const int AbnormalGateLowerAbilityScale = 100;
    private const int AbnormalGateTraitRate = 1;
    private const int Waiting0SharedFactorPercent = 180;
    private const int Waiting1SharedFactorPercent = 140;
    private const int SpecialForceEffectPercent = 120;
    private const int AbnormalGeneralResistanceRate = 1;
    private const int AbnormalType0ResistanceRate = 1;
    private const int AbnormalType1ResistanceRate = 1;
    private const int AbnormalType2ResistanceRate = 1;
    private const int AbnormalSourceTraitRate = 1;
    private const int MoraleBaseline = 100;
    private const int MoraleEffectCoefficient = 1;
    private const int ReservedTacticScale = 3;
    private const int HealingCapacityCoefficient = 1;
    private const int ActivationContribution = 60;
    private const int HealthyAbilityPercent = 100;
    private const int InjuryLevel1AbilityPercent = 80;
    private const int InjuryLevel2AbilityPercent = 50;
    private const int InjuryLevel3AbilityPercent = 20;
    private const int AbilitySecondaryBaseline = 100;
    private const int AbilitySecondaryAdjustment = 90;

    // Legacy parameter keys remain stable for saved current values.
    private static readonly (int SemanticScRva, int Value)[] InitialDefaults =
    [
        (0x1A4A080, AbnormalCounterUpperBound),
        (0x1A4A08C, PrimaryWaitingCap),
        (0x1A4A098, SecondaryWaitingCap),
        (0x1A4A0A4, TertiaryWaitingCap),
        (0x1A4A0B0, OrdinaryWaitingRate),
        (0x1A4A094, InitialWaiting),
        (0x1A4A04C, Formation1CreationWaitingAdjustment),
        (0x1A4A058, Formation2CreationWaitingAdjustment),
        (0x1A4A064, Formation14CreationWaitingAdjustment),
        (0x1A4AA64, Formation1AutomaticWaitingAdjustment),
        (0x1A4AA70, Formation2AutomaticWaitingAdjustment),
        (0x1A4AA08, Formation14AutomaticWaitingAdjustment),
        (0x1A4AC30, ForceFlagWaitingReduction),
        (0x1A4A9FC, NonplayerAutomaticTrialRate),
        (0x1A4AA14, PlayerDifficulty0TrialRate),
        (0x1A4AA20, PlayerDifficulty1TrialRate),
        (0x1A4AA2C, PlayerDifficulty2TrialRate),
        (0x1A4AAAC, HighDifficultyNonplayerTrialRate),
        (0x1A4AAB4, EffectStageScale),
        (0x1A4AB14, StrengthEffectDivisor),
        (0x1A4AB1C, IntelligenceEffectDivisor),
        (0x1A4AB54, HealingMinimum),
        (0x1A4AAF0, StrengthDifferenceDivisor),
        (0x1A4AAFC, IntelligenceDifferenceDivisor),
        (0x1A4AB08, EffectFactorFloorPercent),
        (0x1A4AB30, AbnormalGateBaseline),
        (0x1A4AB3C, AbnormalGateHigherAbilityScale),
        (0x1A4AB48, AbnormalGateLowerAbilityScale),
        (0x1A4ABEC, AbnormalGateTraitRate),
        (0x1A4ABB4, Waiting0SharedFactorPercent),
        (0x1A4ABA8, Waiting1SharedFactorPercent),
        (0x1A4AC18, SpecialForceEffectPercent),
        (0x1A4ABC0, AbnormalGeneralResistanceRate),
        (0x1A4ABCC, AbnormalType0ResistanceRate),
        (0x1A4ABD8, AbnormalType1ResistanceRate),
        (0x1A4ABE0, AbnormalType2ResistanceRate),
        (0x1A4C4EC, AbnormalSourceTraitRate),
        (0x1A4B79C, MoraleBaseline),
        (0x1A4B7A8, MoraleEffectCoefficient),
        (0x1A4AFBC, ReservedTacticScale),
        (0x1A49D4C, HealingCapacityCoefficient),
        (0x1A4BD44, ActivationContribution),
        (0x1A4BD54, HealthyAbilityPercent),
        (0x1A4BD60, InjuryLevel1AbilityPercent),
        (0x1A4BD6C, InjuryLevel2AbilityPercent),
        (0x1A4BD78, InjuryLevel3AbilityPercent),
        (0x1A4BD84, AbilitySecondaryBaseline),
        (0x1A4BD90, AbilitySecondaryAdjustment),
    ];
}
