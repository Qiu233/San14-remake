using San14.Core;

namespace San14.Data;

/// <summary>Project rules for fresh Army destruction parameters. Installation preserves saved mutable ownership and never repairs an existing allocation.</summary>
public sealed class OriginalArmyDestructionParameterSeed
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
    public const int CaptureAdjustmentTcRva = 0x27D37AC;
    public const int CaptureAdjustmentScRva = 0x27D480C;
    public const int RelationFirstThresholdTcRva = 0x27D3834;
    public const int RelationFirstThresholdScRva = 0x27D4894;
    public const int LordReleaseRelationThresholdTcRva = 0x27D37D0;
    public const int LordReleaseRelationThresholdScRva = 0x27D4830;
    public const int LordExecuteRelationPenaltyTcRva = 0x27D37D4;
    public const int LordExecuteRelationPenaltyScRva = 0x27D4834;
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentParameterField> Fields { get; }
    private OriginalArmyDestructionParameterSeed(string language, SourceProvenance source,
        OriginalBattleRemakeCurrentParameterField[] fields) => (Language, Source, Fields) = (language, source, fields);

    public static OriginalArmyDestructionParameterSeed Load(OriginalGameRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = SimulationRuleSources.Provenance("san14-remake://rules/army-destruction/v1", language);
        // Legacy addresses identify existing saved keys; the values are project rules.
        OriginalBattleRemakeCurrentParameterField[] fields = [
            new(0x1A4AFE8, language == "TC" ? 0x1A49FB8 : 0x1A4AFE8, PersonalityDefaultRadius, string.Empty),
            new(0x1A4A090, language == "TC" ? 0x1A49060 : 0x1A4A090, ForceExperienceWoundedDivisor, string.Empty),
            new(0x1A4A0E4, language == "TC" ? 0x1A490B4 : 0x1A4A0E4, ForceExperienceTraitMultiplier, string.Empty),
            new(0x1A4ABE8, language == "TC" ? 0x1A49BB8 : 0x1A4ABE8, ForceExperienceLandAdvantageMultiplier, string.Empty),
            new(0x1A4A7C4, language == "TC" ? 0x1A49794 : 0x1A4A7C4, ForceExperienceFlagBonusPercent, string.Empty),
            new(0x1A4C250, language == "TC" ? 0x1A4B220 : 0x1A4C250, RelationTierTwoThreshold, string.Empty),
            new(0x1A4C25C, language == "TC" ? 0x1A4B22C : 0x1A4C25C, RelationTierThreeThreshold, string.Empty),
            new(0x1A4C264, language == "TC" ? 0x1A4B234 : 0x1A4C264, RelationTierFourThreshold, string.Empty),
            new(0x1A4C278, language == "TC" ? 0x1A4B248 : 0x1A4C278, RelationLikeBonus, string.Empty),
            new(0x1A4C284, language == "TC" ? 0x1A4B254 : 0x1A4C284, RelationHatePenalty, string.Empty),
            new(0x1A4C28C, language == "TC" ? 0x1A4B25C : 0x1A4C28C, RelationSwornBonus, string.Empty),
            new(0x1A4C298, language == "TC" ? 0x1A4B268 : 0x1A4C298, RelationSpouseBonus, string.Empty),
            new(0x1A4C2A4, language == "TC" ? 0x1A4B274 : 0x1A4C2A4, RelationParentBonus, string.Empty),
            new(0x1A4C2B0, language == "TC" ? 0x1A4B280 : 0x1A4C2B0, RelationSiblingBonus, string.Empty),
            new(0x1A4C2BC, language == "TC" ? 0x1A4B28C : 0x1A4C2BC, RelationClanBonus, string.Empty),
            new(0x1A4C2C8, language == "TC" ? 0x1A4B298 : 0x1A4C2C8, RelationAffinityWeight, string.Empty),
            new(0x1A4C2D4, language == "TC" ? 0x1A4B2A4 : 0x1A4C2D4, RelationFamilyMultiplier, string.Empty),
            new(0x1A4C2E0, language == "TC" ? 0x1A4B2B0 : 0x1A4C2E0, RelationForceBonus, string.Empty),
            new(0x1A4C4B0, language == "TC" ? 0x1A4B480 : 0x1A4C4B0, DefeatedResourcesHomePercent, string.Empty),
            new(0x1A4C4BC, language == "TC" ? 0x1A4B48C : 0x1A4C4BC, DefeatedResourcesWinnerPercent, string.Empty),
            new(0x1A4C4C8, language == "TC" ? 0x1A4B498 : 0x1A4C4C8, DefeatedResourcesSpecialHomePercent, string.Empty),
            new(0x1A4C4D4, language == "TC" ? 0x1A4B4A4 : 0x1A4C4D4, DefeatedResourcesSpecialWinnerPercent, string.Empty),
            new(0x1A4C520, language == "TC" ? 0x1A4B4F0 : 0x1A4C520, BattleDeathBaseProbability, string.Empty),
            new(0x1A4C52C, language == "TC" ? 0x1A4B4FC : 0x1A4C52C, BattleDeathModeTwoPercent, string.Empty),
            new(0x1A4C538, language == "TC" ? 0x1A4B508 : 0x1A4C538, BattleDeathModeZeroPercent, string.Empty),
            new(0x1A4C544, language == "TC" ? 0x1A4B514 : 0x1A4C544, SpecialForceHealthyInjuryBase, string.Empty),
            new(0x1A4C550, language == "TC" ? 0x1A4B520 : 0x1A4C550, SpecialForceInjuredInjuryBase, string.Empty),
            new(0x1A4C558, language == "TC" ? 0x1A4B528 : 0x1A4C558, SpecialForceSevereInjuryBase, string.Empty),
            new(0x1A4C560, language == "TC" ? 0x1A4B530 : 0x1A4C560, SpecialForceHealthyAbilityDivisor, string.Empty),
            new(0x1A4C56C, language == "TC" ? 0x1A4B53C : 0x1A4C56C, SpecialForceInjuredAbilityDivisor, string.Empty),
            new(0x1A4C578, language == "TC" ? 0x1A4B548 : 0x1A4C578, SpecialForceSevereAbilityDivisor, string.Empty),
            new(0x1A4C584, language == "TC" ? 0x1A4B554 : 0x1A4C584, OrdinaryForceHealthyInjuryBase, string.Empty),
            new(0x1A4C590, language == "TC" ? 0x1A4B560 : 0x1A4C590, OrdinaryForceInjuredInjuryBase, string.Empty),
            new(0x1A4C59C, language == "TC" ? 0x1A4B56C : 0x1A4C59C, OrdinaryForceSevereInjuryBase, string.Empty),
            new(0x1A4C5A8, language == "TC" ? 0x1A4B578 : 0x1A4C5A8, OrdinaryForceHealthyAbilityDivisor, string.Empty),
            new(0x1A4C5B4, language == "TC" ? 0x1A4B584 : 0x1A4C5B4, OrdinaryForceInjuredAbilityDivisor, string.Empty),
            new(0x1A4C5C0, language == "TC" ? 0x1A4B590 : 0x1A4C5C0, OrdinaryForceSevereAbilityDivisor, string.Empty),
            new(0x1A4C5D8, language == "TC" ? 0x1A4B5A8 : 0x1A4C5D8, HighDifficultyPlayerInjuryPercent, string.Empty),
            new(0x1A4C5E4, language == "TC" ? 0x1A4B5B4 : 0x1A4C5E4, LoyaltyLossLoyaltyDivisor, string.Empty),
            new(0x1A4C5F0, language == "TC" ? 0x1A4B5C0 : 0x1A4C5F0, LoyaltyLossPersonWordDivisor, string.Empty),
            new(0x1A4C5FC, language == "TC" ? 0x1A4B5CC : 0x1A4C5FC, LoyaltyLossRelationBase, string.Empty),
            new(0x1A4C608, language == "TC" ? 0x1A4B5D8 : 0x1A4C608, MoraleWaveLowPostThreshold, string.Empty),
            new(0x1A4C614, language == "TC" ? 0x1A4B5E4 : 0x1A4C614, MoraleWaveGainCap, string.Empty),
            new(0x1A4C620, language == "TC" ? 0x1A4B5F0 : 0x1A4C620, MoraleWaveGainPercent, string.Empty),
            new(0x1A4C62C, language == "TC" ? 0x1A4B5FC : 0x1A4C62C, MoraleWaveLossCap, string.Empty),
            new(0x1A4C638, language == "TC" ? 0x1A4B608 : 0x1A4C638, MoraleWaveLossPercent, string.Empty),
            new(0x1A4B84C, language == "TC" ? 0x1A4A81C : 0x1A4B84C, CaptureAbilityBaseline, string.Empty),
            new(0x1A4B854, language == "TC" ? 0x1A4A824 : 0x1A4B854, CaptureAbilityPercent, string.Empty),
            new(0x1A4B860, language == "TC" ? 0x1A4A830 : 0x1A4B860, CaptureThreatPercent, string.Empty),
            new(0x1A4B86C, language == "TC" ? 0x1A4A83C : 0x1A4B86C, CaptureFormationFourPercent, string.Empty),
            new(0x1A4B878, language == "TC" ? 0x1A4A848 : 0x1A4B878, HighDifficultyPlayerWinnerCapturePercent, string.Empty),
            new(0x1A4B884, language == "TC" ? 0x1A4A854 : 0x1A4B884, CaptureModeOnePercent, string.Empty),
            new(0x1A4B890, language == "TC" ? 0x1A4A860 : 0x1A4B890, CaptureRulerOfficerPercent, string.Empty),
            new(0x1A4BBE0, language == "TC" ? 0x1A4ABB0 : 0x1A4BBE0, RulerReleaseRelationThreshold, string.Empty),
            new(0x1A4BC8C, language == "TC" ? 0x1A4AC5C : 0x1A4BC8C, RulerReleaseRelationGain, string.Empty),
            new(0x1A4A91C, language == "TC" ? 0x1A498EC : 0x1A4A91C, RulerReleasePopularityGain, string.Empty),
            new(0x1A4A8D4, language == "TC" ? 0x1A498A4 : 0x1A4A8D4, PersonFreedomPopularityPenalty, string.Empty),
            new(0x1A4A1B0, language == "TC" ? 0x1A49180 : 0x1A4A1B0, AdvisorHighThreshold, string.Empty),
            new(0x1A4A1BC, language == "TC" ? 0x1A4918C : 0x1A4A1BC, AdvisorLowThreshold, string.Empty),
            new(0x1A4A944, language == "TC" ? 0x1A49914 : 0x1A4A944, BaseCaptureResultGain, string.Empty),
            new(0x1A4A09C, language == "TC" ? 0x1A4906C : 0x1A4A09C, BaseCaptureExperienceGain, string.Empty),
            new(0x1A4A0A8, language == "TC" ? 0x1A49078 : 0x1A4A0A8, StateCaptureExperienceGain, string.Empty),
            new(0x1A4A0B4, language == "TC" ? 0x1A49084 : 0x1A4A0B4, RegionCaptureExperienceGain, string.Empty),
            new(0x1A4BD24, language == "TC" ? 0x1A4ACF4 : 0x1A4BD24, UnassignedBaseCommanderMerit, string.Empty),
            new(0x1A4BD2C, language == "TC" ? 0x1A4ACFC : 0x1A4BD2C, GateCaptureCommanderMerit, string.Empty),
            new(0x1A4BD38, language == "TC" ? 0x1A4AD08 : 0x1A4BD38, CityCaptureCommanderMerit, string.Empty),
            new(0x1A4BD74, language == "TC" ? 0x1A4AD44 : 0x1A4BD74, CaptiveReturnCommanderMerit, string.Empty),
            new(0x1A4BDE8, language == "TC" ? 0x1A4ADB8 : 0x1A4BDE8, TransportReturnCommanderMerit, string.Empty),
            new(0x1A4A4AC, language == "TC" ? 0x1A4947C : 0x1A4A4AC, TransportCommandCost, string.Empty),
            new(0x1A4BA58, language == "TC" ? 0x1A4AA28 : 0x1A4BA58, GateCaptiveGuardMultiplier, string.Empty),
            new(0x1A4BA64, language == "TC" ? 0x1A4AA34 : 0x1A4BA64, CaptiveTroopRequirementAddend, string.Empty),
            new(0x1A4C17C, language == "TC" ? 0x1A4B14C : 0x1A4C17C, CaptiveGuardOfficeMultiplier, string.Empty),
            new(0x1A4C1F4, language == "TC" ? 0x1A4B1C4 : 0x1A4C1F4, CaptiveGuardFlaggedAmount, string.Empty),
            new(0x1A4C0A4, language == "TC" ? 0x1A4B074 : 0x1A4C0A4, CaptiveGuardTraitMultiplier, string.Empty),
            new(0x1A4AC58, language == "TC" ? 0x1A49C28 : 0x1A4AC58, CaptiveGuardLandMultiplier, string.Empty),
            new(0x27D480C, language == "TC" ? 0x27D37AC : 0x27D480C, CaptureThreatAdjustment, string.Empty),
            new(0x27D4894, language == "TC" ? 0x27D3834 : 0x27D4894, RelationTierOneThreshold, string.Empty),
            new(0x27D4830, language == "TC" ? 0x27D37D0 : 0x27D4830, RulerReleasePersonRelationThreshold, string.Empty),
            new(0x27D4834, language == "TC" ? 0x27D37D4 : 0x27D4834, RulerExecutionRelationPenalty, string.Empty)
        ];
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
            string key = OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva);
            if (simulation.Parameters.Float32Values.ContainsKey(key) || integers.TryGetValue(key, out int existing) && existing != field.Value)
                throw new InvalidDataException("Destruction parameter disagrees with an already installed source field.");
            integers.TryAdd(key, field.Value);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = integers } } };
    }

    public static int? ReadCurrentTc(GameState state, int tcRva) =>
        state.NativeArmySimulation?.Parameters is { } owner && owner.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        owner.Int32Values.TryGetValue(OriginalTacticRemakeParameterSeed.Key(tcRva switch {
            CaptureAdjustmentTcRva => CaptureAdjustmentScRva,
            RelationFirstThresholdTcRva => RelationFirstThresholdScRva,
            LordReleaseRelationThresholdTcRva => LordReleaseRelationThresholdScRva,
            LordExecuteRelationPenaltyTcRva => LordExecuteRelationPenaltyScRva,
            _ => tcRva + 0x1030 }), out int value) ? value : null;

}
