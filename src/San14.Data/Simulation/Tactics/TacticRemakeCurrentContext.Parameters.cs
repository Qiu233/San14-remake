using San14.Core;

namespace San14.Data;

public sealed record TacticRemakeParameterSeedField(string Key, int Value);

/// <summary>Project-owned tactic rule defaults and an explicit producer of this game's own
/// current parameter allocation. This is never a read of original process state.</summary>
public sealed class TacticRemakeParameterSeed
{
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<TacticRemakeParameterSeedField> Fields { get; }

    private TacticRemakeParameterSeed(string language, SourceProvenance source,
        TacticRemakeParameterSeedField[] fields)
    { Language = language; Source = source; Fields = Array.AsReadOnly(fields); }

    public static TacticRemakeParameterSeed LoadPinnedSeed(GameResourceRepository repository,
        string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        SourceProvenance source = SimulationRuleSources.Provenance(
            "san14-remake://rules/tactic-parameters/v1", language);
        return new(language, source, InitialDefaults.Select(field => new TacticRemakeParameterSeedField(
            field.Key, field.Value)).ToArray());
    }

    /// <summary>Explicit copy into a new current owner. Later writes occur in
    /// that owner's dictionaries and are not reset from source at query time.</summary>
    public NativeArmyParameterState AllocateOwnedCurrentParameters(string revision)
    {
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Owned parameter allocation revision is missing.");
        return new($"{revision}:rule-seed", Language, Source.RuleVersion!,
            Fields.ToDictionary(field => field.Key, field => field.Value), []);
    }

    /// <summary>Only a fresh, empty owned Army session can construct these
    /// Force/World/manager records. Save decode never calls this producer.</summary>
    public GameState CreateFreshOwnedSimulation(GameState state, string revision)
    {
        if (state.NativeArmySimulation is not null || state.NativeClock is not null || state.Units.Count != 0)
            throw new InvalidDataException("Fresh owned native simulation cannot replace a saved or populated instance.");
        GameState initialized = PlayableArmyCreation.InitializeClock(state, revision);
        return initialized with { Forces = initialized.Forces.ToDictionary(pair => pair.Key,
            pair => pair.Value with { PolicyEffects = ForcePolicyEffects.Empty }),
            NativeArmySimulation = new(AllocateOwnedCurrentParameters(revision),
            initialized.Forces.ToDictionary(pair => pair.Key, pair => new NativeArmyForceState(0)
            {
                Flags12 = 0, Flags194 = 0,
                RelationsEA = pair.Value.SourceIndexedBytesEA?.ToArray()
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

    private static readonly (string Key, int Value)[] InitialDefaults =
    [
        (SimulationParameterKeys.Tactics.AbnormalCounterUpperBound, AbnormalCounterUpperBound),
        (SimulationParameterKeys.Tactics.PrimaryWaitingCap, PrimaryWaitingCap),
        (SimulationParameterKeys.Tactics.SecondaryWaitingCap, SecondaryWaitingCap),
        (SimulationParameterKeys.Tactics.TertiaryWaitingCap, TertiaryWaitingCap),
        (SimulationParameterKeys.Tactics.OrdinaryWaitingRate, OrdinaryWaitingRate),
        (SimulationParameterKeys.Tactics.InitialWaiting, InitialWaiting),
        (SimulationParameterKeys.Tactics.Formation1CreationWaitingAdjustment, Formation1CreationWaitingAdjustment),
        (SimulationParameterKeys.Tactics.Formation2CreationWaitingAdjustment, Formation2CreationWaitingAdjustment),
        (SimulationParameterKeys.Tactics.Formation14CreationWaitingAdjustment, Formation14CreationWaitingAdjustment),
        (SimulationParameterKeys.Tactics.Formation1AutomaticWaitingAdjustment, Formation1AutomaticWaitingAdjustment),
        (SimulationParameterKeys.Tactics.Formation2AutomaticWaitingAdjustment, Formation2AutomaticWaitingAdjustment),
        (SimulationParameterKeys.Tactics.Formation14AutomaticWaitingAdjustment, Formation14AutomaticWaitingAdjustment),
        (SimulationParameterKeys.Tactics.ForceFlagWaitingReduction, ForceFlagWaitingReduction),
        (SimulationParameterKeys.Tactics.NonplayerAutomaticTrialRate, NonplayerAutomaticTrialRate),
        (SimulationParameterKeys.Tactics.PlayerDifficulty0TrialRate, PlayerDifficulty0TrialRate),
        (SimulationParameterKeys.Tactics.PlayerDifficulty1TrialRate, PlayerDifficulty1TrialRate),
        (SimulationParameterKeys.Tactics.PlayerDifficulty2TrialRate, PlayerDifficulty2TrialRate),
        (SimulationParameterKeys.Tactics.HighDifficultyNonplayerTrialRate, HighDifficultyNonplayerTrialRate),
        (SimulationParameterKeys.Tactics.EffectStageScale, EffectStageScale),
        (SimulationParameterKeys.Tactics.StrengthEffectDivisor, StrengthEffectDivisor),
        (SimulationParameterKeys.Tactics.IntelligenceEffectDivisor, IntelligenceEffectDivisor),
        (SimulationParameterKeys.Tactics.HealingMinimum, HealingMinimum),
        (SimulationParameterKeys.Tactics.StrengthDifferenceDivisor, StrengthDifferenceDivisor),
        (SimulationParameterKeys.Tactics.IntelligenceDifferenceDivisor, IntelligenceDifferenceDivisor),
        (SimulationParameterKeys.Tactics.EffectFactorFloorPercent, EffectFactorFloorPercent),
        (SimulationParameterKeys.Tactics.AbnormalGateBaseline, AbnormalGateBaseline),
        (SimulationParameterKeys.Tactics.AbnormalGateHigherAbilityScale, AbnormalGateHigherAbilityScale),
        (SimulationParameterKeys.Tactics.AbnormalGateLowerAbilityScale, AbnormalGateLowerAbilityScale),
        (SimulationParameterKeys.Tactics.AbnormalGateTraitRate, AbnormalGateTraitRate),
        (SimulationParameterKeys.Tactics.Waiting0SharedFactorPercent, Waiting0SharedFactorPercent),
        (SimulationParameterKeys.Tactics.Waiting1SharedFactorPercent, Waiting1SharedFactorPercent),
        (SimulationParameterKeys.Tactics.SpecialForceEffectPercent, SpecialForceEffectPercent),
        (SimulationParameterKeys.Tactics.AbnormalGeneralResistanceRate, AbnormalGeneralResistanceRate),
        (SimulationParameterKeys.Tactics.AbnormalType0ResistanceRate, AbnormalType0ResistanceRate),
        (SimulationParameterKeys.Tactics.AbnormalType1ResistanceRate, AbnormalType1ResistanceRate),
        (SimulationParameterKeys.Tactics.AbnormalType2ResistanceRate, AbnormalType2ResistanceRate),
        (SimulationParameterKeys.Tactics.AbnormalSourceTraitRate, AbnormalSourceTraitRate),
        (SimulationParameterKeys.Morale.Maximum, MoraleBaseline),
        (SimulationParameterKeys.Morale.PolicyScale, MoraleEffectCoefficient),
        (SimulationParameterKeys.Tactics.ReservedTacticScale, ReservedTacticScale),
        (SimulationParameterKeys.Tactics.HealingCapacityCoefficient, HealingCapacityCoefficient),
        (SimulationParameterKeys.Tactics.ActivationContribution, ActivationContribution),
        (SimulationParameterKeys.Tactics.HealthyAbilityPercent, HealthyAbilityPercent),
        (SimulationParameterKeys.Tactics.InjuryLevel1AbilityPercent, InjuryLevel1AbilityPercent),
        (SimulationParameterKeys.Tactics.InjuryLevel2AbilityPercent, InjuryLevel2AbilityPercent),
        (SimulationParameterKeys.Tactics.InjuryLevel3AbilityPercent, InjuryLevel3AbilityPercent),
        (SimulationParameterKeys.Tactics.AbilitySecondaryBaseline, AbilitySecondaryBaseline),
        (SimulationParameterKeys.Tactics.AbilitySecondaryAdjustment, AbilitySecondaryAdjustment),
    ];
}
