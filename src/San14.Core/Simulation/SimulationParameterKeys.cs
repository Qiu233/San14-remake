namespace San14.Core;

/// <summary>Named keys for current simulation parameters. Independent of source binary layouts.</summary>
public static class SimulationParameterKeys
{
    public static class Landmark
    {
        public const string InitialRandomRange = "simulation.landmark.initial-random-range";
        public const string TypeQuota = "simulation.landmark.type-quota";
        public const string SecurityInitialEffect = "simulation.landmark.security-initial-effect";
        public const string DurabilityInitialEffect = "simulation.landmark.durability-initial-effect";
        public const string MoraleInitialEffect = "simulation.landmark.morale-initial-effect";
        public const string CompletionSecurity = "simulation.landmark.completion-security";
        public const string CompletionDurability = "simulation.landmark.completion-durability";
        public const string CompletionMorale = "simulation.landmark.completion-morale";
        public const string CompletionPopularity = "simulation.landmark.completion-popularity";
    }
    public static class SubdayStatus
    {
        public const string BaseAbnormalUpper = "simulation.subday-status.base-abnormal-upper";
        public const string BaseCooldownUpper = "simulation.subday-status.base-cooldown-upper";
        public const string OfficerRumorDecay = "simulation.subday-status.officer-rumor-decay";
    }
    public static class FireCommand
    {
        public const string DurationBase = "simulation.fire-command.duration-base";
        public const string IntelligenceDivisor = "simulation.fire-command.intelligence-divisor";
        public const string DurationPolicyScale = "simulation.fire-command.duration-policy-scale";
        public const string SuccessMerit = "simulation.fire-command.success-merit";
        public const string FailureMorale = "simulation.fire-command.failure-morale";
        public const string TerrainZeroRate = "simulation.fire-command.terrain-zero-rate";
        public const string TerrainOneRate = "simulation.fire-command.terrain-one-rate";
        public const string TerrainTwoRate = "simulation.fire-command.terrain-two-rate";
        public const string TerrainThreeRate = "simulation.fire-command.terrain-three-rate";
        public const string MinimumRate = "simulation.fire-command.minimum-rate";
        public const string TraitRateScale = "simulation.fire-command.trait-rate-scale";
        public const string BackfireBase = "simulation.fire-command.backfire-base";
        public const string BackfireMinimum = "simulation.fire-command.backfire-minimum";
        public const string AbnormalTargetBonus = "simulation.fire-command.abnormal-target-bonus";
        public const string BackfireDefenseScale = "simulation.fire-command.backfire-defense-scale";
        public const string RelationAdjustment = "simulation.fire-command.relation-adjustment";
    }
    public static class ForceRelations
    {
        public static string LevelThreshold(int level) => level is >= 1 and <= 5
            ? $"simulation.force-relations.level-{level}-threshold" : throw new ArgumentOutOfRangeException(nameof(level));
    }
    public static class HexFire
    {
        public const string Decay = "simulation.hex-fire.decay";
        public const string CategoryOneSpread = "simulation.hex-fire.category-one-spread";
        public const string CategoryThreeSpread = "simulation.hex-fire.category-three-spread";
        public const string OtherCategorySpread = "simulation.hex-fire.other-category-spread";
        public const string SpreadDivisor = "simulation.hex-fire.spread-divisor";
        public const string DurationPercentBase = "simulation.hex-fire.duration-percent-base";
        public const string DurationRandomBound = "simulation.hex-fire.duration-random-bound";
    }
    public static class BaseWoundedRecovery
    {
        public const string SlowAmount = "simulation.base-wounded-recovery.slow-amount";
        public const string NormalAmount = "simulation.base-wounded-recovery.normal-amount";
        public const string FastAmount = "simulation.base-wounded-recovery.fast-amount";
        public const string WoundedDivisor = "simulation.base-wounded-recovery.wounded-divisor";
        public const string RecoveredMorale = "simulation.base-wounded-recovery.recovered-morale";
        public const string TraitMultiplier = "simulation.base-wounded-recovery.trait-multiplier";
    }
    public static class ArmyDestruction
    {
        public const string AdvisorHighThreshold = "simulation.army-destruction.advisor-high-threshold";
        public const string AdvisorLowThreshold = "simulation.army-destruction.advisor-low-threshold";
        public const string BaseCaptureExperienceGain = "simulation.army-destruction.base-capture-experience-gain";
        public const string BaseCaptureResultGain = "simulation.army-destruction.base-capture-result-gain";
        public const string BattleDeathBaseProbability = "simulation.army-destruction.battle-death-base-probability";
        public const string BattleDeathModeTwoPercent = "simulation.army-destruction.battle-death-mode-two-percent";
        public const string BattleDeathModeZeroPercent = "simulation.army-destruction.battle-death-mode-zero-percent";
        public const string CaptiveGuardFlaggedAmount = "simulation.army-destruction.captive-guard-flagged-amount";
        public const string CaptiveGuardLandMultiplier = "simulation.army-destruction.captive-guard-land-multiplier";
        public const string CaptiveGuardOfficeMultiplier = "simulation.army-destruction.captive-guard-office-multiplier";
        public const string CaptiveGuardTraitMultiplier = "simulation.army-destruction.captive-guard-trait-multiplier";
        public const string CaptiveReturnCommanderMerit = "simulation.army-destruction.captive-return-commander-merit";
        public const string CaptiveTroopRequirementAddend = "simulation.army-destruction.captive-troop-requirement-addend";
        public const string CaptureAbilityBaseline = "simulation.army-destruction.capture-ability-baseline";
        public const string CaptureAbilityPercent = "simulation.army-destruction.capture-ability-percent";
        public const string CaptureFormationFourPercent = "simulation.army-destruction.capture-formation-four-percent";
        public const string CaptureModeOnePercent = "simulation.army-destruction.capture-mode-one-percent";
        public const string CaptureRulerOfficerPercent = "simulation.army-destruction.capture-ruler-officer-percent";
        public const string CaptureThreatAdjustment = "simulation.army-destruction.capture-threat-adjustment";
        public const string CaptureThreatPercent = "simulation.army-destruction.capture-threat-percent";
        public const string CityCaptureCommanderMerit = "simulation.army-destruction.city-capture-commander-merit";
        public const string DefeatedResourcesHomePercent = "simulation.army-destruction.defeated-resources-home-percent";
        public const string DefeatedResourcesSpecialHomePercent = "simulation.army-destruction.defeated-resources-special-home-percent";
        public const string DefeatedResourcesSpecialWinnerPercent = "simulation.army-destruction.defeated-resources-special-winner-percent";
        public const string DefeatedResourcesWinnerPercent = "simulation.army-destruction.defeated-resources-winner-percent";
        public const string ForceExperienceFlagBonusPercent = "simulation.army-destruction.force-experience-flag-bonus-percent";
        public const string ForceExperienceLandAdvantageMultiplier = "simulation.army-destruction.force-experience-land-advantage-multiplier";
        public const string ForceExperienceTraitMultiplier = "simulation.army-destruction.force-experience-trait-multiplier";
        public const string ForceExperienceWoundedDivisor = "simulation.army-destruction.force-experience-wounded-divisor";
        public const string GateCaptiveGuardMultiplier = "simulation.army-destruction.gate-captive-guard-multiplier";
        public const string GateCaptureCommanderMerit = "simulation.army-destruction.gate-capture-commander-merit";
        public const string HighDifficultyPlayerInjuryPercent = "simulation.army-destruction.high-difficulty-player-injury-percent";
        public const string HighDifficultyPlayerWinnerCapturePercent = "simulation.army-destruction.high-difficulty-player-winner-capture-percent";
        public const string LoyaltyLossLoyaltyDivisor = "simulation.army-destruction.loyalty-loss-loyalty-divisor";
        public const string LoyaltyLossPersonWordDivisor = "simulation.army-destruction.loyalty-loss-person-word-divisor";
        public const string LoyaltyLossRelationBase = "simulation.army-destruction.loyalty-loss-relation-base";
        public const string MoraleWaveGainCap = "simulation.army-destruction.morale-wave-gain-cap";
        public const string MoraleWaveGainPercent = "simulation.army-destruction.morale-wave-gain-percent";
        public const string MoraleWaveLossCap = "simulation.army-destruction.morale-wave-loss-cap";
        public const string MoraleWaveLossPercent = "simulation.army-destruction.morale-wave-loss-percent";
        public const string MoraleWaveLowPostThreshold = "simulation.army-destruction.morale-wave-low-post-threshold";
        public const string OrdinaryForceHealthyAbilityDivisor = "simulation.army-destruction.ordinary-force-healthy-ability-divisor";
        public const string OrdinaryForceHealthyInjuryBase = "simulation.army-destruction.ordinary-force-healthy-injury-base";
        public const string OrdinaryForceInjuredAbilityDivisor = "simulation.army-destruction.ordinary-force-injured-ability-divisor";
        public const string OrdinaryForceInjuredInjuryBase = "simulation.army-destruction.ordinary-force-injured-injury-base";
        public const string OrdinaryForceSevereAbilityDivisor = "simulation.army-destruction.ordinary-force-severe-ability-divisor";
        public const string OrdinaryForceSevereInjuryBase = "simulation.army-destruction.ordinary-force-severe-injury-base";
        public const string PersonFreedomPopularityPenalty = "simulation.army-destruction.person-freedom-popularity-penalty";
        public const string PersonalityDefaultRadius = "simulation.army-destruction.personality-default-radius";
        public const string RegionCaptureExperienceGain = "simulation.army-destruction.region-capture-experience-gain";
        public const string RelationAffinityWeight = "simulation.army-destruction.relation-affinity-weight";
        public const string RelationClanBonus = "simulation.army-destruction.relation-clan-bonus";
        public const string RelationFamilyMultiplier = "simulation.army-destruction.relation-family-multiplier";
        public const string RelationForceBonus = "simulation.army-destruction.relation-force-bonus";
        public const string RelationHatePenalty = "simulation.army-destruction.relation-hate-penalty";
        public const string RelationLikeBonus = "simulation.army-destruction.relation-like-bonus";
        public const string RelationParentBonus = "simulation.army-destruction.relation-parent-bonus";
        public const string RelationSiblingBonus = "simulation.army-destruction.relation-sibling-bonus";
        public const string RelationSpouseBonus = "simulation.army-destruction.relation-spouse-bonus";
        public const string RelationSwornBonus = "simulation.army-destruction.relation-sworn-bonus";
        public const string RelationTierFourThreshold = "simulation.army-destruction.relation-tier-four-threshold";
        public const string RelationTierOneThreshold = "simulation.army-destruction.relation-tier-one-threshold";
        public const string RelationTierThreeThreshold = "simulation.army-destruction.relation-tier-three-threshold";
        public const string RelationTierTwoThreshold = "simulation.army-destruction.relation-tier-two-threshold";
        public const string RulerExecutionRelationPenalty = "simulation.army-destruction.ruler-execution-relation-penalty";
        public const string RulerReleasePersonRelationThreshold = "simulation.army-destruction.ruler-release-person-relation-threshold";
        public const string RulerReleasePopularityGain = "simulation.army-destruction.ruler-release-popularity-gain";
        public const string RulerReleaseRelationGain = "simulation.army-destruction.ruler-release-relation-gain";
        public const string RulerReleaseRelationThreshold = "simulation.army-destruction.ruler-release-relation-threshold";
        public const string SpecialForceHealthyAbilityDivisor = "simulation.army-destruction.special-force-healthy-ability-divisor";
        public const string SpecialForceHealthyInjuryBase = "simulation.army-destruction.special-force-healthy-injury-base";
        public const string SpecialForceInjuredAbilityDivisor = "simulation.army-destruction.special-force-injured-ability-divisor";
        public const string SpecialForceInjuredInjuryBase = "simulation.army-destruction.special-force-injured-injury-base";
        public const string SpecialForceSevereAbilityDivisor = "simulation.army-destruction.special-force-severe-ability-divisor";
        public const string SpecialForceSevereInjuryBase = "simulation.army-destruction.special-force-severe-injury-base";
        public const string StateCaptureExperienceGain = "simulation.army-destruction.state-capture-experience-gain";
        public const string TransportCommandCost = "simulation.army-destruction.transport-command-cost";
        public const string TransportReturnCommanderMerit = "simulation.army-destruction.transport-return-commander-merit";
        public const string UnassignedBaseCommanderMerit = "simulation.army-destruction.unassigned-base-commander-merit";
    }

    public static class ArmyMarch
    {
        public const string SpeedScale = "simulation.army-march.speed-scale";
        public const string MinimumTerrainCost = "simulation.army-march.minimum-terrain-cost";
        public const string DevelopmentLossMultiplier = "simulation.army-march.development-loss-multiplier";
        public const string OccupationTraitScale = "simulation.army-march.occupation-trait-scale";
        public const string OccupationGrowthScale = "simulation.army-march.occupation-growth-scale";
        public const string OccupationMeritScale = "simulation.army-march.occupation-merit-scale";
    }

    public static class ArmyMarchPenalty
    {
        public const string RadiusOneArmyWeight = "simulation.army-march-penalty.radius-one-army-weight";
        public const string RadiusThreeArmyWeight = "simulation.army-march-penalty.radius-three-army-weight";
        public const string RadiusTwoArmyWeight = "simulation.army-march-penalty.radius-two-army-weight";
        public const string TerritoryWeight = "simulation.army-march-penalty.territory-weight";
    }

    public static class ArmyMobility
    {
        public const string EnvironmentTerrainPercent = "simulation.army-mobility.environment-terrain-percent";
        public const string ForceTerrainPercent = "simulation.army-mobility.force-terrain-percent";
        public const string FormationFiveMobilityBonus = "simulation.army-mobility.formation-five-mobility-bonus";
        public const string FormationSixTerrainPercent = "simulation.army-mobility.formation-six-terrain-percent";
        public const string FormationTerrainPercent = "simulation.army-mobility.formation-terrain-percent";
        public const string TraitMobilityLimit = "simulation.army-mobility.trait-mobility-limit";
        public const string TraitMobilityStep = "simulation.army-mobility.trait-mobility-step";
    }

    public static class BaseCapacity
    {
        public const string BaseDurability = "simulation.base-capacity.base-durability";
        public const string BaseTroopCapacity = "simulation.base-capacity.base-troop-capacity";
        public const string DamageTierFirstThreshold = "simulation.base-capacity.damage-tier-first-threshold";
        public const string DamageTierSecondThreshold = "simulation.base-capacity.damage-tier-second-threshold";
        public const string DamageTierThirdThreshold = "simulation.base-capacity.damage-tier-third-threshold";
        public const string DurabilityPerAdditionalSize = "simulation.base-capacity.durability-per-additional-size";
        public const string DurabilityPolicyScale = "simulation.base-capacity.durability-policy-scale";
        public const string ExtraTroopCapacity = "simulation.base-capacity.extra-troop-capacity";
        public const string FoodCapacityPerCitySize = "simulation.base-capacity.food-capacity-per-city-size";
        public const string GoldCapacityPerCitySize = "simulation.base-capacity.gold-capacity-per-city-size";
        public const string LargeCityExtraTroops = "simulation.base-capacity.large-city-extra-troops";
        public const string MaximumDurability = "simulation.base-capacity.maximum-durability";
        public const string MediumCityExtraTroops = "simulation.base-capacity.medium-city-extra-troops";
        public const string MetropolisExtraTroops = "simulation.base-capacity.metropolis-extra-troops";
        public const string SpecialFoodCapacity = "simulation.base-capacity.special-food-capacity";
        public const string SpecialGoldCapacity = "simulation.base-capacity.special-gold-capacity";
        public const string SpecialTroopCapacity = "simulation.base-capacity.special-troop-capacity";
    }

    public static class BaseCapture
    {
        public const string CapturedCityGoldFloor = "simulation.base-capture.captured-city-gold-floor";
        public const string CapturedCitySuppliesFloor = "simulation.base-capture.captured-city-supplies-floor";
        public const string CapturedWallFloorPercent = "simulation.base-capture.captured-wall-floor-percent";
        public const string CentralTerritoryRadius = "simulation.base-capture.central-territory-radius";
        public const string CommanderCharmDivisor = "simulation.base-capture.commander-charm-divisor";
        public const string MinimumResourceKeepPercent = "simulation.base-capture.minimum-resource-keep-percent";
    }

    public static class BattleAttributes
    {
        public const string AlternateFormationPercentScale = "simulation.battle-attributes.alternate-formation-percent-scale";
        public const string AlternateMoraleDivisor = "simulation.battle-attributes.alternate-morale-divisor";
        public const string AlternateMoraleOffset = "simulation.battle-attributes.alternate-morale-offset";
        public const string AttackAbilityLimit = "simulation.battle-attributes.attack-ability-limit";
        public const string AttackAbilityStep = "simulation.battle-attributes.attack-ability-step";
        public const string AttackCoefficientScale = "simulation.battle-attributes.attack-coefficient-scale";
        public const string BaseAttackCoefficient = "simulation.battle-attributes.base-attack-coefficient";
        public const string BaseDefenseCoefficient = "simulation.battle-attributes.base-defense-coefficient";
        public const string BaseWallCoefficient = "simulation.battle-attributes.base-wall-coefficient";
        public const string CommonAdditive = "simulation.battle-attributes.common-additive";
        public const string CommonDivisorNumerator = "simulation.battle-attributes.common-divisor-numerator";
        public const string CommonDivisorOffset = "simulation.battle-attributes.common-divisor-offset";
        public const string CommonLeadershipScale = "simulation.battle-attributes.common-leadership-scale";
        public const string CommonPowerExponent = "simulation.battle-attributes.common-power-exponent";
        public const string CommonRootDivisor = "simulation.battle-attributes.common-root-divisor";
        public const string DefenseAbilityLimit = "simulation.battle-attributes.defense-ability-limit";
        public const string DefenseAbilityStep = "simulation.battle-attributes.defense-ability-step";
        public const string DefenseCoefficientScale = "simulation.battle-attributes.defense-coefficient-scale";
        public const string FlagDenominator = "simulation.battle-attributes.flag-denominator";
        public const string FlagNumerator = "simulation.battle-attributes.flag-numerator";
        public const string Formation14TerrainPercent = "simulation.battle-attributes.formation14-terrain-percent";
        public const string Formation6TerrainPercent = "simulation.battle-attributes.formation6-terrain-percent";
        public const string Formation7ForceFlagBonus = "simulation.battle-attributes.formation7-force-flag-bonus";
        public const string FormationEffect12Scale = "simulation.battle-attributes.formation-effect12-scale";
        public const string FormationEffect1Scale = "simulation.battle-attributes.formation-effect1-scale";
        public const string FormationEffect2Scale = "simulation.battle-attributes.formation-effect2-scale";
        public const string FormationEffect3Scale = "simulation.battle-attributes.formation-effect3-scale";
        public const string FormationEffect4Scale = "simulation.battle-attributes.formation-effect4-scale";
        public const string FormationEffect5Scale = "simulation.battle-attributes.formation-effect5-scale";
        public const string FormationEffect6Scale = "simulation.battle-attributes.formation-effect6-scale";
        public const string FormationEffect7Scale = "simulation.battle-attributes.formation-effect7-scale";
        public const string FormationEffect8Scale = "simulation.battle-attributes.formation-effect8-scale";
        public const string FormationEffect9Scale = "simulation.battle-attributes.formation-effect9-scale";
        public const string MoraleDivisor = "simulation.battle-attributes.morale-divisor";
        public const string MoraleOffset = "simulation.battle-attributes.morale-offset";
        public const string NormalFormationPercentScale = "simulation.battle-attributes.normal-formation-percent-scale";
        public const string ProvokedAttackPercent = "simulation.battle-attributes.provoked-attack-percent";
        public const string ProvokedOtherTargetDefensePercent = "simulation.battle-attributes.provoked-other-target-defense-percent";
        public const string ReservedAgeThreshold = "simulation.battle-attributes.reserved-age-threshold";
        public const string ReservedAgeTier1 = "simulation.battle-attributes.reserved-age-tier1";
        public const string ReservedAgeTier2 = "simulation.battle-attributes.reserved-age-tier2";
        public const string ReservedAgeTier3 = "simulation.battle-attributes.reserved-age-tier3";
        public const string ReservedAgeTier4 = "simulation.battle-attributes.reserved-age-tier4";
        public const string ReservedAgeTier5 = "simulation.battle-attributes.reserved-age-tier5";
        public const string ReservedBaseAttackSecondaryCoefficient = "simulation.battle-attributes.reserved-base-attack-secondary-coefficient";
        public const string ReservedBaseDefenseSecondaryCoefficient = "simulation.battle-attributes.reserved-base-defense-secondary-coefficient";
        public const string SiegeAbilityBonus = "simulation.battle-attributes.siege-ability-bonus";
        public const string SiegeAbilityLimit = "simulation.battle-attributes.siege-ability-limit";
        public const string SiegeAbilityStep = "simulation.battle-attributes.siege-ability-step";
        public const string WallAbilityBonus = "simulation.battle-attributes.wall-ability-bonus";
        public const string WallAbilityLimit = "simulation.battle-attributes.wall-ability-limit";
        public const string WallAbilityStep = "simulation.battle-attributes.wall-ability-step";
        public const string WallLeadershipDenominator = "simulation.battle-attributes.wall-leadership-denominator";
        public const string WallTroopDenominator = "simulation.battle-attributes.wall-troop-denominator";
        public const string WallTroopOffset = "simulation.battle-attributes.wall-troop-offset";
    }

    public static class BattleDamage
    {
        public const string AttackOffset = "simulation.battle-damage.attack-offset";
        public const string AttackSquareDivisor = "simulation.battle-damage.attack-square-divisor";
        public const string CityPersonMultiplier = "simulation.battle-damage.city-person-multiplier";
        public const string CityPolicyMultiplier = "simulation.battle-damage.city-policy-multiplier";
        public const string CityQueryMultiplier = "simulation.battle-damage.city-query-multiplier";
        public const string DamageCap = "simulation.battle-damage.damage-cap";
        public const string DamageDivisor = "simulation.battle-damage.damage-divisor";
        public const string HalfDamageDivisor = "simulation.battle-damage.half-damage-divisor";
        public const string TransportTargetMultiplier = "simulation.battle-damage.transport-target-multiplier";
        public const string TroopSumDivisor = "simulation.battle-damage.troop-sum-divisor";
        public const string TroopSumOffset = "simulation.battle-damage.troop-sum-offset";
        public const string WallPersonMultiplier = "simulation.battle-damage.wall-person-multiplier";
    }

    public static class BattleRegion
    {
        public const string BaselineRegionCoefficient = "simulation.battle-region.baseline-region-coefficient";
        public const string DifficultyThreeNonplayerCoefficient = "simulation.battle-region.difficulty-three-nonplayer-coefficient";
        public const string HigherDifficultyNonplayerCoefficient = "simulation.battle-region.higher-difficulty-nonplayer-coefficient";
        public const string RegionMultiplierModeOnePercent = "simulation.battle-region.region-multiplier-mode-one-percent";
        public const string RegionMultiplierModeTwoPercent = "simulation.battle-region.region-multiplier-mode-two-percent";
    }

    public static class CityCapture
    {
        public const string LeaderCharmRadiusThreshold = "simulation.city-capture.leader-charm-radius-threshold";
        public const string MaximumCaptureAreaRadius = "simulation.city-capture.maximum-capture-area-radius";
        public const string RankRadiusDivisor = "simulation.city-capture.rank-radius-divisor";
        public const string SquaredCharmRadiusDivisor = "simulation.city-capture.squared-charm-radius-divisor";
    }

    public static class CityFoodExpense
    {
        public const string GarrisonExpenseDivisor = "simulation.city-food-expense.garrison-expense-divisor";
        public const string GarrisonExpenseNumerator = "simulation.city-food-expense.garrison-expense-numerator";
        public const string GovernorExpenseEffectScale = "simulation.city-food-expense.governor-expense-effect-scale";
        public const string NegativeCorrection = "simulation.city-food-expense.negative-correction";
        public const string PositiveCorrection = "simulation.city-food-expense.positive-correction";
        public const string RepairGoldFee = "simulation.city-food-expense.repair-gold-fee";
    }

    public static class ForceCombatResult
    {
        public const string KilledAddend = "simulation.force-combat-result.killed-addend";
        public const string TroopLossScale = "simulation.force-combat-result.troop-loss-scale";
    }

    public static class Morale
    {
        public const string LogisticsGain = "simulation.morale.logistics-gain";
        public const string Maximum = "simulation.morale.maximum";
        public const string NegativeAdjustmentScale = "simulation.morale.negative-adjustment-scale";
        public const string FieldFatigueBase = "simulation.morale.field-fatigue-base";
        public const string PolicyScale = "simulation.morale.policy-scale";
    }

    public static class NormalAftermath
    {
        public const string AreaDevelopmentLevelChangeCoefficient = "simulation.normal-aftermath.area-development-level-change-coefficient";
        public const string AreaDevelopmentReduction = "simulation.normal-aftermath.area-development-reduction";
        public const string AreaQuantityFixedReduction = "simulation.normal-aftermath.area-quantity-fixed-reduction";
        public const string AreaQuantityRandomReductionBound = "simulation.normal-aftermath.area-quantity-random-reduction-bound";
        public const string HostileRelationAdjustment = "simulation.normal-aftermath.hostile-relation-adjustment";
        public const string HostileRelationCeiling = "simulation.normal-aftermath.hostile-relation-ceiling";
        public const string RelationBandFiveMinimum = "simulation.normal-aftermath.relation-band-five-minimum";
        public const string RelationBandFourMinimum = "simulation.normal-aftermath.relation-band-four-minimum";
        public const string RelationBandOneMinimum = "simulation.normal-aftermath.relation-band-one-minimum";
        public const string RelationBandThreeMinimum = "simulation.normal-aftermath.relation-band-three-minimum";
        public const string RelationBandTwoMinimum = "simulation.normal-aftermath.relation-band-two-minimum";
        public const string SecurityLossBaseMultiplier = "simulation.normal-aftermath.security-loss-base-multiplier";
        public const string SecurityLossDenominatorOffset = "simulation.normal-aftermath.security-loss-denominator-offset";
        public const string SecurityLossProbabilityDivisor = "simulation.normal-aftermath.security-loss-probability-divisor";
        public const string SecurityLossProbabilityNumerator = "simulation.normal-aftermath.security-loss-probability-numerator";
    }

    public static class NormalCombatApplication
    {
        public const string AdjacentAddend = "simulation.normal-combat-application.adjacent-addend";
        public const string CharmDenominator = "simulation.normal-combat-application.charm-denominator";
        public const string CharmScale = "simulation.normal-combat-application.charm-scale";
        public const string DifficultyPlayerAddend = "simulation.normal-combat-application.difficulty-player-addend";
        public const string MoraleDenominator = "simulation.normal-combat-application.morale-denominator";
        public const string MoraleLossSamplerBound = "simulation.normal-combat-application.morale-loss-sampler-bound";
        public const string MoraleProbabilityScale = "simulation.normal-combat-application.morale-probability-scale";
        public const string MoraleScale = "simulation.normal-combat-application.morale-scale";
        public const string SpecialForceAddend = "simulation.normal-combat-application.special-force-addend";
        public const string Terrain10Addend = "simulation.normal-combat-application.terrain10-addend";
        public const string Terrain2Addend = "simulation.normal-combat-application.terrain2-addend";
        public const string Terrain3Addend = "simulation.normal-combat-application.terrain3-addend";
        public const string Terrain4Addend = "simulation.normal-combat-application.terrain4-addend";
        public const string Terrain5Addend = "simulation.normal-combat-application.terrain5-addend";
        public const string Terrain6Addend = "simulation.normal-combat-application.terrain6-addend";
        public const string Terrain7Addend = "simulation.normal-combat-application.terrain7-addend";
        public const string Terrain8Addend = "simulation.normal-combat-application.terrain8-addend";
        public const string Terrain9Addend = "simulation.normal-combat-application.terrain9-addend";
        public const string TerrainOneAddend = "simulation.normal-combat-application.terrain-one-addend";
        public const string WoundedSubtractionPercent = "simulation.normal-combat-application.wounded-subtraction-percent";
    }

    public static class PersonCombatRewards
    {
        public const string KindOneLeadershipPercent = "simulation.person-combat-rewards.kind-one-leadership-percent";
        public const string KindOnePendingMeritPercent = "simulation.person-combat-rewards.kind-one-pending-merit-percent";
        public const string KindThreeLeadershipGrowth = "simulation.person-combat-rewards.kind-three-leadership-growth";
        public const string KindTwoLeadershipGrowth = "simulation.person-combat-rewards.kind-two-leadership-growth";
        public const string KindTwoPendingMerit = "simulation.person-combat-rewards.kind-two-pending-merit";
        public const string KindZeroLeadershipPercent = "simulation.person-combat-rewards.kind-zero-leadership-percent";
        public const string KindZeroPendingMeritPercent = "simulation.person-combat-rewards.kind-zero-pending-merit-percent";
    }

    public static class PersonGrowth
    {
        public const string DoctrineGrowthScale = "simulation.person-growth.doctrine-growth-scale";
        public const string PolicyGrowthScale = "simulation.person-growth.policy-growth-scale";
        public const string ProvinceAdvantageGrowthScale = "simulation.person-growth.province-advantage-growth-scale";
        public const string TraitGrowthScale = "simulation.person-growth.trait-growth-scale";
    }

    public static class PersonMerit
    {
        public const string CreditModeOnePendingMeritPercent = "simulation.person-merit.credit-mode-one-pending-merit-percent";
        public const string CreditModeZeroPendingMeritPercent = "simulation.person-merit.credit-mode-zero-pending-merit-percent";
        public const string DoctrinePendingMeritBonusPercent = "simulation.person-merit.doctrine-pending-merit-bonus-percent";
        public const string StageOnePendingMeritBonusPercent = "simulation.person-merit.stage-one-pending-merit-bonus-percent";
        public const string TraitPendingMeritScale = "simulation.person-merit.trait-pending-merit-scale";
    }

    public static class PersonReturn
    {
        public const string JourneyCoefficient = "simulation.person-return.journey-coefficient";
        public const string LandAdvantageDivisor = "simulation.person-return.land-advantage-divisor";
        public const string PersonalityJourneyMultiplier = "simulation.person-return.personality-journey-multiplier";
    }

    public static class Shared
    {
        public const string One = "simulation.shared.one";
        public const string PercentDenominator = "simulation.shared.percent-denominator";
    }

    public static class Tactics
    {
        public const string AbilitySecondaryAdjustment = "simulation.tactics.ability-secondary-adjustment";
        public const string AbilitySecondaryBaseline = "simulation.tactics.ability-secondary-baseline";
        public const string AbnormalCounterUpperBound = "simulation.tactics.abnormal-counter-upper-bound";
        public const string AbnormalGateBaseline = "simulation.tactics.abnormal-gate-baseline";
        public const string AbnormalGateHigherAbilityScale = "simulation.tactics.abnormal-gate-higher-ability-scale";
        public const string AbnormalGateLowerAbilityScale = "simulation.tactics.abnormal-gate-lower-ability-scale";
        public const string AbnormalGateTraitRate = "simulation.tactics.abnormal-gate-trait-rate";
        public const string AbnormalGeneralResistanceRate = "simulation.tactics.abnormal-general-resistance-rate";
        public const string AbnormalSourceTraitRate = "simulation.tactics.abnormal-source-trait-rate";
        public const string AbnormalType0ResistanceRate = "simulation.tactics.abnormal-type0-resistance-rate";
        public const string AbnormalType1ResistanceRate = "simulation.tactics.abnormal-type1-resistance-rate";
        public const string AbnormalType2ResistanceRate = "simulation.tactics.abnormal-type2-resistance-rate";
        public const string ActivationContribution = "simulation.tactics.activation-contribution";
        public const string EffectFactorFloorPercent = "simulation.tactics.effect-factor-floor-percent";
        public const string EffectStageScale = "simulation.tactics.effect-stage-scale";
        public const string ForceFlagWaitingReduction = "simulation.tactics.force-flag-waiting-reduction";
        public const string ManualInterventionMinimumPeriods = "simulation.tactics.manual-intervention-minimum-periods";
        public const string ChainSearchRadius = "simulation.tactics.chain-search-radius";
        public const string Formation1ManualWaitingAdjustment = "simulation.tactics.formation1-manual-waiting-adjustment";
        public const string Formation2ManualWaitingAdjustment = "simulation.tactics.formation2-manual-waiting-adjustment";
        public const string Formation14ManualWaitingAdjustment = "simulation.tactics.formation14-manual-waiting-adjustment";
        public const string Formation14AutomaticWaitingAdjustment = "simulation.tactics.formation14-automatic-waiting-adjustment";
        public const string Formation14CreationWaitingAdjustment = "simulation.tactics.formation14-creation-waiting-adjustment";
        public const string Formation1AutomaticWaitingAdjustment = "simulation.tactics.formation1-automatic-waiting-adjustment";
        public const string Formation1CreationWaitingAdjustment = "simulation.tactics.formation1-creation-waiting-adjustment";
        public const string Formation2AutomaticWaitingAdjustment = "simulation.tactics.formation2-automatic-waiting-adjustment";
        public const string Formation2CreationWaitingAdjustment = "simulation.tactics.formation2-creation-waiting-adjustment";
        public const string HealingCapacityCoefficient = "simulation.tactics.healing-capacity-coefficient";
        public const string HealingMinimum = "simulation.tactics.healing-minimum";
        public const string HealthyAbilityPercent = "simulation.tactics.healthy-ability-percent";
        public const string HighDifficultyNonplayerTrialRate = "simulation.tactics.high-difficulty-nonplayer-trial-rate";
        public const string InitialWaiting = "simulation.tactics.initial-waiting";
        public const string InjuryLevel1AbilityPercent = "simulation.tactics.injury-level1-ability-percent";
        public const string InjuryLevel2AbilityPercent = "simulation.tactics.injury-level2-ability-percent";
        public const string InjuryLevel3AbilityPercent = "simulation.tactics.injury-level3-ability-percent";
        public const string IntelligenceDifferenceDivisor = "simulation.tactics.intelligence-difference-divisor";
        public const string IntelligenceEffectDivisor = "simulation.tactics.intelligence-effect-divisor";
        public const string NonplayerAutomaticTrialRate = "simulation.tactics.nonplayer-automatic-trial-rate";
        public const string OrdinaryWaitingRate = "simulation.tactics.ordinary-waiting-rate";
        public const string PlayerDifficulty0TrialRate = "simulation.tactics.player-difficulty0-trial-rate";
        public const string PlayerDifficulty1TrialRate = "simulation.tactics.player-difficulty1-trial-rate";
        public const string PlayerDifficulty2TrialRate = "simulation.tactics.player-difficulty2-trial-rate";
        public const string PrimaryWaitingCap = "simulation.tactics.primary-waiting-cap";
        public const string ReservedTacticScale = "simulation.tactics.reserved-tactic-scale";
        public const string SecondaryWaitingCap = "simulation.tactics.secondary-waiting-cap";
        public const string SpecialForceEffectPercent = "simulation.tactics.special-force-effect-percent";
        public const string StrengthDifferenceDivisor = "simulation.tactics.strength-difference-divisor";
        public const string StrengthEffectDivisor = "simulation.tactics.strength-effect-divisor";
        public const string TertiaryWaitingCap = "simulation.tactics.tertiary-waiting-cap";
        public const string Waiting0SharedFactorPercent = "simulation.tactics.waiting0-shared-factor-percent";
        public const string Waiting1SharedFactorPercent = "simulation.tactics.waiting1-shared-factor-percent";
    }

    public static class TransportRoute
    {
        public const string BlockingAbnormalThreshold = "simulation.transport-route.blocking-abnormal-threshold";
        public const string BlockingOccupantPenalty = "simulation.transport-route.blocking-occupant-penalty";
        public const string FriendlyOccupantPenalty = "simulation.transport-route.friendly-occupant-penalty";
        public const string NonblockingOccupantPenalty = "simulation.transport-route.nonblocking-occupant-penalty";
        public const string WallPenaltyPercent = "simulation.transport-route.wall-penalty-percent";
    }

}
