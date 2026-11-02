using San14.Core;

namespace San14.Data;

/// <summary>Project rules for fresh Army destruction parameters. Installation preserves saved mutable ownership and never repairs an existing allocation.</summary>
public sealed class ArmyDestructionParameterSeed
{
    private const int PersonalityDefaultRadius = 3;
    private const int ForceExperienceWoundedDivisor = 100;
    private const int ForceExperienceTraitMultiplier = 1;
    private const int ForceExperienceLandAdvantageMultiplier = 1;
    private const int ForceExperienceFlagBonusPercent = 10;
    private const int RelationTierTwoThreshold = 11;
    private const int RelationTierThreeThreshold = 30;
    private const int RelationTierFourThreshold = 47;
    private const int RelationLikeBonus = 15;
    private const int RelationHatePenalty = 20;
    private const int RelationSwornBonus = 50;
    private const int RelationSpouseBonus = 40;
    private const int RelationParentBonus = 15;
    private const int RelationSiblingBonus = 15;
    private const int RelationClanBonus = 10;
    private const int RelationAffinityWeight = 10;
    private const int RelationFamilyMultiplier = 2;
    private const int RelationForceBonus = 10;
    private const int DefeatedResourcesHomePercent = 50;
    private const int DefeatedResourcesWinnerPercent = 50;
    private const int DefeatedResourcesSpecialHomePercent = 70;
    private const int DefeatedResourcesSpecialWinnerPercent = 30;
    private const int BattleDeathBaseProbability = 10;
    private const int BattleDeathModeTwoPercent = 200;
    private const int BattleDeathModeZeroPercent = 50;
    private const int SpecialForceHealthyInjuryBase = 60;
    private const int SpecialForceInjuredInjuryBase = 40;
    private const int SpecialForceSevereInjuryBase = 100;
    private const int SpecialForceHealthyAbilityDivisor = 10;
    private const int SpecialForceInjuredAbilityDivisor = 15;
    private const int SpecialForceSevereAbilityDivisor = 20;
    private const int OrdinaryForceHealthyInjuryBase = 60;
    private const int OrdinaryForceInjuredInjuryBase = 80;
    private const int OrdinaryForceSevereInjuryBase = 100;
    private const int OrdinaryForceHealthyAbilityDivisor = 10;
    private const int OrdinaryForceInjuredAbilityDivisor = 15;
    private const int OrdinaryForceSevereAbilityDivisor = 20;
    private const int HighDifficultyPlayerInjuryPercent = 130;
    private const int LoyaltyLossLoyaltyDivisor = 7;
    private const int LoyaltyLossPersonWordDivisor = 5;
    private const int LoyaltyLossRelationBase = 3;
    private const int MoraleWaveLowPostThreshold = 8;
    private const int MoraleWaveGainCap = 10;
    private const int MoraleWaveGainPercent = 100;
    private const int MoraleWaveLossCap = 10;
    private const int MoraleWaveLossPercent = 100;
    private const int CaptureAbilityBaseline = 120;
    private const int CaptureAbilityPercent = 103;
    private const int CaptureThreatPercent = 33;
    private const int CaptureFormationFourPercent = 60;
    private const int HighDifficultyPlayerWinnerCapturePercent = 70;
    private const int CaptureModeOnePercent = 130;
    private const int CaptureRulerOfficerPercent = 90;
    private const int RulerReleaseRelationThreshold = 70;
    private const int RulerReleaseRelationGain = 10;
    private const int RulerReleasePopularityGain = 10;
    private const int PersonFreedomPopularityPenalty = 3;
    private const int AdvisorHighThreshold = 10;
    private const int AdvisorLowThreshold = 5;
    private const int BaseCaptureResultGain = 1000;
    private const int BaseCaptureExperienceGain = 1000;
    private const int StateCaptureExperienceGain = 3000;
    private const int RegionCaptureExperienceGain = 5000;
    private const int UnassignedBaseCommanderMerit = 120;
    private const int GateCaptureCommanderMerit = 240;
    private const int CityCaptureCommanderMerit = 360;
    private const int CaptiveReturnCommanderMerit = 40;
    private const int TransportReturnCommanderMerit = 75;
    private const int TransportCommandCost = 1;
    private const int GateCaptiveGuardMultiplier = 2;
    private const int CaptiveTroopRequirementAddend = 50;
    private const int CaptiveGuardOfficeMultiplier = 10;
    private const int CaptiveGuardFlaggedAmount = 10;
    private const int CaptiveGuardTraitMultiplier = 1;
    private const int CaptiveGuardLandMultiplier = 1;
    private const int CaptureThreatAdjustment = 0;
    private const int RelationTierOneThreshold = 0;
    private const int RulerReleasePersonRelationThreshold = 0;
    private const int RulerExecutionRelationPenalty = 0;
    public const string Revision = "san14:ce-owned-army-destruction-parameters-20261007";
    public const string VersionKey = "ce-army-destruction-current:version";
                                    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<BattleRemakeCurrentParameterField> Fields { get; }
    private ArmyDestructionParameterSeed(string language, SourceProvenance source,
        BattleRemakeCurrentParameterField[] fields) => (Language, Source, Fields) = (language, source, fields);

    public static ArmyDestructionParameterSeed Load(GameResourceRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = SimulationRuleSources.Provenance("san14-remake://rules/army-destruction/v1", language);
        // Project rules install their named keys in the saved parameter bank.
        BattleRemakeCurrentParameterField[] fields = [
            new(SimulationParameterKeys.ArmyDestruction.PersonalityDefaultRadius, PersonalityDefaultRadius),
            new(SimulationParameterKeys.ArmyDestruction.ForceExperienceWoundedDivisor, ForceExperienceWoundedDivisor),
            new(SimulationParameterKeys.ArmyDestruction.ForceExperienceTraitMultiplier, ForceExperienceTraitMultiplier),
            new(SimulationParameterKeys.ArmyDestruction.ForceExperienceLandAdvantageMultiplier, ForceExperienceLandAdvantageMultiplier),
            new(SimulationParameterKeys.ArmyDestruction.ForceExperienceFlagBonusPercent, ForceExperienceFlagBonusPercent),
            new(SimulationParameterKeys.ArmyDestruction.RelationTierTwoThreshold, RelationTierTwoThreshold),
            new(SimulationParameterKeys.ArmyDestruction.RelationTierThreeThreshold, RelationTierThreeThreshold),
            new(SimulationParameterKeys.ArmyDestruction.RelationTierFourThreshold, RelationTierFourThreshold),
            new(SimulationParameterKeys.ArmyDestruction.RelationLikeBonus, RelationLikeBonus),
            new(SimulationParameterKeys.ArmyDestruction.RelationHatePenalty, RelationHatePenalty),
            new(SimulationParameterKeys.ArmyDestruction.RelationSwornBonus, RelationSwornBonus),
            new(SimulationParameterKeys.ArmyDestruction.RelationSpouseBonus, RelationSpouseBonus),
            new(SimulationParameterKeys.ArmyDestruction.RelationParentBonus, RelationParentBonus),
            new(SimulationParameterKeys.ArmyDestruction.RelationSiblingBonus, RelationSiblingBonus),
            new(SimulationParameterKeys.ArmyDestruction.RelationClanBonus, RelationClanBonus),
            new(SimulationParameterKeys.ArmyDestruction.RelationAffinityWeight, RelationAffinityWeight),
            new(SimulationParameterKeys.ArmyDestruction.RelationFamilyMultiplier, RelationFamilyMultiplier),
            new(SimulationParameterKeys.ArmyDestruction.RelationForceBonus, RelationForceBonus),
            new(SimulationParameterKeys.ArmyDestruction.DefeatedResourcesHomePercent, DefeatedResourcesHomePercent),
            new(SimulationParameterKeys.ArmyDestruction.DefeatedResourcesWinnerPercent, DefeatedResourcesWinnerPercent),
            new(SimulationParameterKeys.ArmyDestruction.DefeatedResourcesSpecialHomePercent, DefeatedResourcesSpecialHomePercent),
            new(SimulationParameterKeys.ArmyDestruction.DefeatedResourcesSpecialWinnerPercent, DefeatedResourcesSpecialWinnerPercent),
            new(SimulationParameterKeys.ArmyDestruction.BattleDeathBaseProbability, BattleDeathBaseProbability),
            new(SimulationParameterKeys.ArmyDestruction.BattleDeathModeTwoPercent, BattleDeathModeTwoPercent),
            new(SimulationParameterKeys.ArmyDestruction.BattleDeathModeZeroPercent, BattleDeathModeZeroPercent),
            new(SimulationParameterKeys.ArmyDestruction.SpecialForceHealthyInjuryBase, SpecialForceHealthyInjuryBase),
            new(SimulationParameterKeys.ArmyDestruction.SpecialForceInjuredInjuryBase, SpecialForceInjuredInjuryBase),
            new(SimulationParameterKeys.ArmyDestruction.SpecialForceSevereInjuryBase, SpecialForceSevereInjuryBase),
            new(SimulationParameterKeys.ArmyDestruction.SpecialForceHealthyAbilityDivisor, SpecialForceHealthyAbilityDivisor),
            new(SimulationParameterKeys.ArmyDestruction.SpecialForceInjuredAbilityDivisor, SpecialForceInjuredAbilityDivisor),
            new(SimulationParameterKeys.ArmyDestruction.SpecialForceSevereAbilityDivisor, SpecialForceSevereAbilityDivisor),
            new(SimulationParameterKeys.ArmyDestruction.OrdinaryForceHealthyInjuryBase, OrdinaryForceHealthyInjuryBase),
            new(SimulationParameterKeys.ArmyDestruction.OrdinaryForceInjuredInjuryBase, OrdinaryForceInjuredInjuryBase),
            new(SimulationParameterKeys.ArmyDestruction.OrdinaryForceSevereInjuryBase, OrdinaryForceSevereInjuryBase),
            new(SimulationParameterKeys.ArmyDestruction.OrdinaryForceHealthyAbilityDivisor, OrdinaryForceHealthyAbilityDivisor),
            new(SimulationParameterKeys.ArmyDestruction.OrdinaryForceInjuredAbilityDivisor, OrdinaryForceInjuredAbilityDivisor),
            new(SimulationParameterKeys.ArmyDestruction.OrdinaryForceSevereAbilityDivisor, OrdinaryForceSevereAbilityDivisor),
            new(SimulationParameterKeys.ArmyDestruction.HighDifficultyPlayerInjuryPercent, HighDifficultyPlayerInjuryPercent),
            new(SimulationParameterKeys.ArmyDestruction.LoyaltyLossLoyaltyDivisor, LoyaltyLossLoyaltyDivisor),
            new(SimulationParameterKeys.ArmyDestruction.LoyaltyLossPersonWordDivisor, LoyaltyLossPersonWordDivisor),
            new(SimulationParameterKeys.ArmyDestruction.LoyaltyLossRelationBase, LoyaltyLossRelationBase),
            new(SimulationParameterKeys.ArmyDestruction.MoraleWaveLowPostThreshold, MoraleWaveLowPostThreshold),
            new(SimulationParameterKeys.ArmyDestruction.MoraleWaveGainCap, MoraleWaveGainCap),
            new(SimulationParameterKeys.ArmyDestruction.MoraleWaveGainPercent, MoraleWaveGainPercent),
            new(SimulationParameterKeys.ArmyDestruction.MoraleWaveLossCap, MoraleWaveLossCap),
            new(SimulationParameterKeys.ArmyDestruction.MoraleWaveLossPercent, MoraleWaveLossPercent),
            new(SimulationParameterKeys.ArmyDestruction.CaptureAbilityBaseline, CaptureAbilityBaseline),
            new(SimulationParameterKeys.ArmyDestruction.CaptureAbilityPercent, CaptureAbilityPercent),
            new(SimulationParameterKeys.ArmyDestruction.CaptureThreatPercent, CaptureThreatPercent),
            new(SimulationParameterKeys.ArmyDestruction.CaptureFormationFourPercent, CaptureFormationFourPercent),
            new(SimulationParameterKeys.ArmyDestruction.HighDifficultyPlayerWinnerCapturePercent, HighDifficultyPlayerWinnerCapturePercent),
            new(SimulationParameterKeys.ArmyDestruction.CaptureModeOnePercent, CaptureModeOnePercent),
            new(SimulationParameterKeys.ArmyDestruction.CaptureRulerOfficerPercent, CaptureRulerOfficerPercent),
            new(SimulationParameterKeys.ArmyDestruction.RulerReleaseRelationThreshold, RulerReleaseRelationThreshold),
            new(SimulationParameterKeys.ArmyDestruction.RulerReleaseRelationGain, RulerReleaseRelationGain),
            new(SimulationParameterKeys.ArmyDestruction.RulerReleasePopularityGain, RulerReleasePopularityGain),
            new(SimulationParameterKeys.ArmyDestruction.PersonFreedomPopularityPenalty, PersonFreedomPopularityPenalty),
            new(SimulationParameterKeys.ArmyDestruction.AdvisorHighThreshold, AdvisorHighThreshold),
            new(SimulationParameterKeys.ArmyDestruction.AdvisorLowThreshold, AdvisorLowThreshold),
            new(SimulationParameterKeys.ArmyDestruction.BaseCaptureResultGain, BaseCaptureResultGain),
            new(SimulationParameterKeys.ArmyDestruction.BaseCaptureExperienceGain, BaseCaptureExperienceGain),
            new(SimulationParameterKeys.ArmyDestruction.StateCaptureExperienceGain, StateCaptureExperienceGain),
            new(SimulationParameterKeys.ArmyDestruction.RegionCaptureExperienceGain, RegionCaptureExperienceGain),
            new(SimulationParameterKeys.ArmyDestruction.UnassignedBaseCommanderMerit, UnassignedBaseCommanderMerit),
            new(SimulationParameterKeys.ArmyDestruction.GateCaptureCommanderMerit, GateCaptureCommanderMerit),
            new(SimulationParameterKeys.ArmyDestruction.CityCaptureCommanderMerit, CityCaptureCommanderMerit),
            new(SimulationParameterKeys.ArmyDestruction.CaptiveReturnCommanderMerit, CaptiveReturnCommanderMerit),
            new(SimulationParameterKeys.ArmyDestruction.TransportReturnCommanderMerit, TransportReturnCommanderMerit),
            new(SimulationParameterKeys.ArmyDestruction.TransportCommandCost, TransportCommandCost),
            new(SimulationParameterKeys.ArmyDestruction.GateCaptiveGuardMultiplier, GateCaptiveGuardMultiplier),
            new(SimulationParameterKeys.ArmyDestruction.CaptiveTroopRequirementAddend, CaptiveTroopRequirementAddend),
            new(SimulationParameterKeys.ArmyDestruction.CaptiveGuardOfficeMultiplier, CaptiveGuardOfficeMultiplier),
            new(SimulationParameterKeys.ArmyDestruction.CaptiveGuardFlaggedAmount, CaptiveGuardFlaggedAmount),
            new(SimulationParameterKeys.ArmyDestruction.CaptiveGuardTraitMultiplier, CaptiveGuardTraitMultiplier),
            new(SimulationParameterKeys.ArmyDestruction.CaptiveGuardLandMultiplier, CaptiveGuardLandMultiplier),
            new(SimulationParameterKeys.ArmyDestruction.CaptureThreatAdjustment, CaptureThreatAdjustment),
            new(SimulationParameterKeys.ArmyDestruction.RelationTierOneThreshold, RelationTierOneThreshold),
            new(SimulationParameterKeys.ArmyDestruction.RulerReleasePersonRelationThreshold, RulerReleasePersonRelationThreshold),
            new(SimulationParameterKeys.ArmyDestruction.RulerExecutionRelationPenalty, RulerExecutionRelationPenalty)
        ];
        fields = [.. fields, .. CaptiveTreatmentParameters.Defaults.Select(value =>
            new BattleRemakeCurrentParameterField(CaptiveTreatmentParameters.Key(value.Key), value.Value))];
        return new(language, source, fields);
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != Language ||
            simulation.Parameters.RuleVersion != Source.RuleVersion || simulation.Parameters.Int32Values.ContainsKey(VersionKey))
            throw new InvalidDataException("Fresh destruction parameters cannot replace a saved current allocation.");
        var integers = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        foreach (var field in Fields)
        {
            string key = field.Key;
            if (simulation.Parameters.Float32Values.ContainsKey(key) || integers.TryGetValue(key, out int existing) && existing != field.Value)
                throw new InvalidDataException("Destruction parameter disagrees with an already installed source field.");
            integers.TryAdd(key, field.Value);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = integers } } };
    }

    public static int? ReadCurrent(GameState state, string key) =>
        state.NativeArmySimulation?.Parameters is { } owner && owner.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        owner.Int32Values.TryGetValue(key, out int value) ? value : null;

}
