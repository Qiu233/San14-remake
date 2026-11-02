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
        if (state.NativeArmySimulation is not null || state.NativeClock is not null || state.CommanderSelection is not null || state.Units.Count != 0)
            throw new InvalidDataException("Fresh owned native simulation cannot replace a saved or populated instance.");
        GameState initialized = PlayableArmyCreation.InitializeClock(state, revision);
        return AutomaticTacticDamageCurrentParameters.InstallFresh(initialized with { Forces = initialized.Forces.ToDictionary(pair => pair.Key,
            pair => pair.Value with { PolicyEffects = ForcePolicyEffects.Empty }),
            NativeArmySimulation = new(AllocateOwnedCurrentParameters(revision),
            initialized.Forces.ToDictionary(pair => pair.Key, pair => new NativeArmyForceState(0)
            {
                Flags12 = 0, Flags194 = 0, DiplomacyFlags178 = 0,
                RelationsEA = pair.Value.SourceIndexedBytesEA?.ToArray()
            }),
            new NativeArmyWorldState { Flags165C = 0, PendingArmyIds = [], ObjectPairs = [] }),
            CommanderSelection = new(0, null) });
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
    private const int ManualInterventionMinimumPeriods = 3;
    private const int ChainSearchRadius = 2;
    private const int Formation1ManualWaitingAdjustment = 8;
    private const int Formation2ManualWaitingAdjustment = -8;
    private const int Formation14ManualWaitingAdjustment = -8;
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

    // Paired CE_TC/CE_SC data-only extraction: original high-difficulty
    // multi-candidate coefficients. Allocate once into the fresh current
    // owner; restored saves retain their independently saved knowledge.
    private const int AutomaticTargetCountScore = 5;
    private const int AutomaticTerrainContextScore = 5;
    private const int AutomaticEnemyNearOwnTerrainScore = 5;
    private const int AutomaticSpecialDefinitionScore = 9;

    private static readonly (string Key, int Value)[] InitialDefaults =
    [
        (SimulationParameterKeys.Tactics.AbnormalCounterUpperBound, AbnormalCounterUpperBound),
        (SimulationParameterKeys.Tactics.PrimaryWaitingCap, PrimaryWaitingCap),
        (SimulationParameterKeys.Tactics.SecondaryWaitingCap, SecondaryWaitingCap),
        (SimulationParameterKeys.Tactics.TertiaryWaitingCap, TertiaryWaitingCap),
        (SimulationParameterKeys.Tactics.OrdinaryWaitingRate, OrdinaryWaitingRate),
        //TC302430 consumes the selected commander's first nonzero waiting
        //byte. Original static rates are1/4/6; difficulty above2 uses1/2/3.
        (SimulationParameterKeys.Tactics.SelectedCommanderSecondaryWaitingRate, 4),
        (SimulationParameterKeys.Tactics.SelectedCommanderTertiaryWaitingRate, 6),
        (SimulationParameterKeys.Tactics.HighDifficultySelectedCommanderSecondaryWaitingRate, 2),
        (SimulationParameterKeys.Tactics.HighDifficultySelectedCommanderTertiaryWaitingRate, 3),
        (SimulationParameterKeys.Tactics.InitialWaiting, InitialWaiting),
        (SimulationParameterKeys.Tactics.Formation1CreationWaitingAdjustment, Formation1CreationWaitingAdjustment),
        (SimulationParameterKeys.Tactics.Formation2CreationWaitingAdjustment, Formation2CreationWaitingAdjustment),
        (SimulationParameterKeys.Tactics.Formation14CreationWaitingAdjustment, Formation14CreationWaitingAdjustment),
        (SimulationParameterKeys.Tactics.Formation1AutomaticWaitingAdjustment, Formation1AutomaticWaitingAdjustment),
        (SimulationParameterKeys.Tactics.Formation2AutomaticWaitingAdjustment, Formation2AutomaticWaitingAdjustment),
        (SimulationParameterKeys.Tactics.Formation14AutomaticWaitingAdjustment, Formation14AutomaticWaitingAdjustment),
        (SimulationParameterKeys.Tactics.ForceFlagWaitingReduction, ForceFlagWaitingReduction),
        (SimulationParameterKeys.Tactics.ManualInterventionMinimumPeriods, ManualInterventionMinimumPeriods),
        (SimulationParameterKeys.Tactics.ChainSearchRadius, ChainSearchRadius),
        (SimulationParameterKeys.Tactics.Formation1ManualWaitingAdjustment, Formation1ManualWaitingAdjustment),
        (SimulationParameterKeys.Tactics.Formation2ManualWaitingAdjustment, Formation2ManualWaitingAdjustment),
        (SimulationParameterKeys.Tactics.Formation14ManualWaitingAdjustment, Formation14ManualWaitingAdjustment),
        (SimulationParameterKeys.Tactics.NonplayerAutomaticTrialRate, NonplayerAutomaticTrialRate),
        (SimulationParameterKeys.Tactics.PlayerDifficulty0TrialRate, PlayerDifficulty0TrialRate),
        (SimulationParameterKeys.Tactics.PlayerDifficulty1TrialRate, PlayerDifficulty1TrialRate),
        (SimulationParameterKeys.Tactics.PlayerDifficulty2TrialRate, PlayerDifficulty2TrialRate),
        (SimulationParameterKeys.Tactics.HighDifficultyNonplayerTrialRate, HighDifficultyNonplayerTrialRate),
        (TacticRemakeCurrentContext.AutomaticTargetCountScoreKey, AutomaticTargetCountScore),
        (TacticRemakeCurrentContext.AutomaticTerrainContextScoreKey, AutomaticTerrainContextScore),
        (TacticRemakeCurrentContext.AutomaticEnemyNearOwnTerrainScoreKey, AutomaticEnemyNearOwnTerrainScore),
        (TacticRemakeCurrentContext.AutomaticSpecialDefinitionScoreKey, AutomaticSpecialDefinitionScore),
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
        (SimulationParameterKeys.Tactics.SpecialOfficerCommandCapacity, SimulationParameterKeys.Tactics.InitialSpecialOfficerCommandCapacity),
        (SimulationParameterKeys.Tactics.ActivationContribution, ActivationContribution),
        (SimulationParameterKeys.Tactics.HealthyAbilityPercent, HealthyAbilityPercent),
        (SimulationParameterKeys.Tactics.InjuryLevel1AbilityPercent, InjuryLevel1AbilityPercent),
        (SimulationParameterKeys.Tactics.InjuryLevel2AbilityPercent, InjuryLevel2AbilityPercent),
        (SimulationParameterKeys.Tactics.InjuryLevel3AbilityPercent, InjuryLevel3AbilityPercent),
        (SimulationParameterKeys.Tactics.AbilitySecondaryBaseline, AbilitySecondaryBaseline),
        (SimulationParameterKeys.Tactics.AbilitySecondaryAdjustment, AbilitySecondaryAdjustment),
        //Independent TC/SC initial data: relation1/2 loader-zero, relation3..5 rates.
        ("simulation.tactics.linked.player0.relation1-percent", 0),
        ("simulation.tactics.linked.player0.relation2-percent", 0),
        ("simulation.tactics.linked.player0.relation3-percent", 40),
        ("simulation.tactics.linked.player0.relation4-percent", 50),
        ("simulation.tactics.linked.player0.relation5-percent", 80),
        ("simulation.tactics.linked.player1.relation1-percent", 0),
        ("simulation.tactics.linked.player1.relation2-percent", 0),
        ("simulation.tactics.linked.player1.relation3-percent", 30),
        ("simulation.tactics.linked.player1.relation4-percent", 40),
        ("simulation.tactics.linked.player1.relation5-percent", 70),
        ("simulation.tactics.linked.player2.relation1-percent", 0),
        ("simulation.tactics.linked.player2.relation2-percent", 0),
        ("simulation.tactics.linked.player2.relation3-percent", 20),
        ("simulation.tactics.linked.player2.relation4-percent", 30),
        ("simulation.tactics.linked.player2.relation5-percent", 60),
        ("simulation.tactics.linked.nonplayer.relation1-percent", 0),
        ("simulation.tactics.linked.nonplayer.relation2-percent", 0),
        ("simulation.tactics.linked.nonplayer.relation3-percent", 20),
        ("simulation.tactics.linked.nonplayer.relation4-percent", 30),
        ("simulation.tactics.linked.nonplayer.relation5-percent", 60),
        //Counter-zero presentation inputs. These initial values were read
        //independently from current TC and SC source data. Fresh allocation
        //owns them thereafter; loading does not backfill missing current keys.
        ("presentation.tactics.results.large-damage-threshold", 3000),
        ("presentation.tactics.results.large-wall-damage-threshold", 500),
        ("presentation.tactics.results.fire-scale-percent", 175),
        ("presentation.tactics.results.large-wall-damage-scale-percent", 100),
        ("presentation.tactics.results.abnormal-clear-scale-percent", 100),
    ];
}
