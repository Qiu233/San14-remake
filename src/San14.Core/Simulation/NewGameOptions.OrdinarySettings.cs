namespace San14.Core;

// Semantic values are independent of original radio indices. Zero is not a
// default choice. A null option preserves the loaded source setting.
public enum NewGameDifficulty { Beginner = 1, Intermediate = 2, Advanced = 3, Super = 4, Top = 5 }
public enum NewGameAmount { Low = 1, Standard = 2, High = 3 }
public enum NewGameLifespan { Historical = 1, Long = 2, Unlimited = 3 }
public enum NewGameAdjacentRegionEffect { Standard = 1, Medium = 2, Large = 3 }
public enum NewGameCooperationEffect { Small = 1, Standard = 2 }
public enum NewGameMagnitude { Standard = 1, Large = 2 }
public enum NewGameTransportMobility { Standard = 1, Fast = 2 }
public enum NewGameCaptureFrequency { Standard = 1, More = 2, None = 3 }
public enum NewGameMeritGain { Standard = 1, More = 2 }
public enum NewGameBattleDeathFrequency { Low = 1, Standard = 2, High = 3, None = 4 }
public enum NewGameDisasterFrequency { Low = 1, Standard = 2, High = 3, None = 4 }
public enum NewGameTacticDamage { Small = 1, Standard = 2 }
public enum NewGameEnemyArmyInformation { All = 1, Partial = 2 }
public enum NewGameDuration { Short = 1, Standard = 2 }
public enum NewGameHistoricalMode { Historical = 1, Fictional = 2 }
public enum NewGameFemaleOfficerMode { Appear = 1, EventsOnly = 2 }

/// <summary>Optional ordinary settings. Old three-argument calls and old saved
/// records leave these null; loading never reapplies defaults to the saved bank.</summary>
public sealed partial record NewGameOptions
{
    public NewGameAmount? WoundedRecovery { get; init; }
    public NewGameLifespan? Lifespan { get; init; }
    public NewGameBattleDeathFrequency? BattleDeathFrequency { get; init; }
    public bool? DuelsEnabled { get; init; }
    public bool? EncirclementEnabled { get; init; }
    public NewGameAdjacentRegionEffect? AdjacentRegionEffects { get; init; }
    public bool? WoundedSoldiersEnabled { get; init; }
    public bool? OfficerInjuryEnabled { get; init; }
    public NewGameCooperationEffect? CooperationEffects { get; init; }
    public NewGameMagnitude? AutomaticOccupationRange { get; init; }
    public NewGameMagnitude? FireDamage { get; init; }
    public NewGameMagnitude? MoraleEffects { get; init; }
    public NewGameTransportMobility? TransportMobility { get; init; }
    public NewGameCaptureFrequency? CaptureFrequency { get; init; }
    public NewGameAmount? LandmarkEffects { get; init; }
    public NewGameMeritGain? MeritGain { get; init; }

    public NewGameDisasterFrequency? DisasterFrequency { get; init; }
    public NewGameTacticDamage? TacticDamage { get; init; }
    public NewGameAmount? DurabilityRecovery { get; init; }
    public NewGameEnemyArmyInformation? EnemyArmyInformation { get; init; }
    public NewGameAmount? SupplyInterdictionEffects { get; init; }
    public bool? ForceMergingEnabled { get; init; }
    public NewGameDuration? ForeignTradeTravelDuration { get; init; }
    public bool? SpecialForceAttacksEnabled { get; init; }
    public NewGameAmount? LandmarkInvestmentCost { get; init; }
    public NewGameDuration? LandmarkInvestmentDuration { get; init; }
    public NewGameAmount? IntelligenceGain { get; init; }
    public bool? HiredOfficersEnabled { get; init; }
    public bool? HistoricalEventsEnabled { get; init; }
    public bool? ProposalsEnabled { get; init; }
    public NewGameHistoricalMode? WanderingOfficerCities { get; init; }
    public NewGameHistoricalMode? OfficerBloodRelationships { get; init; }
    public NewGameFemaleOfficerMode? FemaleOfficers { get; init; }

    public void ValidateOrdinarySettings()
    {
        if ((WoundedRecovery is { } woundedRecovery && !Enum.IsDefined(woundedRecovery))
            || (Lifespan is { } lifespan && !Enum.IsDefined(lifespan))
            || (BattleDeathFrequency is { } battleDeathFrequency && !Enum.IsDefined(battleDeathFrequency))
            || (AdjacentRegionEffects is { } adjacentRegionEffects && !Enum.IsDefined(adjacentRegionEffects))
            || (CooperationEffects is { } cooperationEffects && !Enum.IsDefined(cooperationEffects))
            || (AutomaticOccupationRange is { } automaticOccupationRange && !Enum.IsDefined(automaticOccupationRange))
            || (FireDamage is { } fireDamage && !Enum.IsDefined(fireDamage))
            || (MoraleEffects is { } moraleEffects && !Enum.IsDefined(moraleEffects))
            || (TransportMobility is { } transportMobility && !Enum.IsDefined(transportMobility))
            || (CaptureFrequency is { } captureFrequency && !Enum.IsDefined(captureFrequency))
            || (LandmarkEffects is { } landmarkEffects && !Enum.IsDefined(landmarkEffects))
            || (MeritGain is { } meritGain && !Enum.IsDefined(meritGain))
            || (DisasterFrequency is { } disasterFrequency && !Enum.IsDefined(disasterFrequency))
            || (TacticDamage is { } tacticDamage && !Enum.IsDefined(tacticDamage))
            || (DurabilityRecovery is { } durabilityRecovery && !Enum.IsDefined(durabilityRecovery))
            || (EnemyArmyInformation is { } enemyArmyInformation && !Enum.IsDefined(enemyArmyInformation))
            || (SupplyInterdictionEffects is { } supplyInterdictionEffects && !Enum.IsDefined(supplyInterdictionEffects))
            || (ForeignTradeTravelDuration is { } foreignTradeTravelDuration && !Enum.IsDefined(foreignTradeTravelDuration))
            || (LandmarkInvestmentCost is { } landmarkInvestmentCost && !Enum.IsDefined(landmarkInvestmentCost))
            || (LandmarkInvestmentDuration is { } landmarkInvestmentDuration && !Enum.IsDefined(landmarkInvestmentDuration))
            || (IntelligenceGain is { } intelligenceGain && !Enum.IsDefined(intelligenceGain))
            || (WanderingOfficerCities is { } appearance && !Enum.IsDefined(appearance))
            || (OfficerBloodRelationships is { } bloodline && !Enum.IsDefined(bloodline))
            || (FemaleOfficers is { } female && !Enum.IsDefined(female)))
            throw new InvalidDataException("An ordinary new-game setting choice is invalid.");
    }
}
