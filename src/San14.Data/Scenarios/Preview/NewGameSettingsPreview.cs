using San14.Core;

namespace San14.Data;

public sealed record NewGameSettingChoice<T>(T Value, int RadioIndex, int MessageId,
    string? Text, bool? CanSelect, string? UnresolvedInput) where T : struct;

/// <summary>Group and Row are zero-based source positions. SourceDefault is
/// the raw normal-source choice; EffectiveValue includes the real mode lock.</summary>
public sealed record NewGameSettingPreview<T>(int Group, int Row, int TitleMessageId,
    string? Title, T? DraftValue, T? SourceDefault, T? EffectiveValue,
    IReadOnlyList<NewGameSettingChoice<T>> Choices, string? UnresolvedInput) where T : struct;

/// <summary>Consequences of the selected difficulty at publication time.</summary>
public sealed record NewGameDifficultyEffects(bool? ProposalsEnabled,
    NewGameEnemyArmyInformation? EnemyArmyInformation, bool Locked);

/// <summary>One normal-source settings publication. Each property is typed;
/// no reflection/dictionary is needed to read or apply a player's choice.</summary>
public sealed record NewGameSettingsPreview(NewGameOptions Draft)
{
    public required NewGameSettingPreview<NewGameDifficulty> Difficulty { get; init; }
    public required NewGameSettingPreview<NewGameAmount> WoundedRecovery { get; init; }
    public required NewGameSettingPreview<NewGameLifespan> Lifespan { get; init; }
    public required NewGameSettingPreview<NewGameBattleDeathFrequency> BattleDeathFrequency { get; init; }
    public required NewGameSettingPreview<bool> DuelsEnabled { get; init; }
    public required NewGameSettingPreview<bool> EncirclementEnabled { get; init; }
    public required NewGameSettingPreview<NewGameAdjacentRegionEffect> AdjacentRegionEffects { get; init; }
    public required NewGameSettingPreview<bool> WoundedSoldiersEnabled { get; init; }
    public required NewGameSettingPreview<bool> OfficerInjuryEnabled { get; init; }
    public required NewGameSettingPreview<NewGameCooperationEffect> CooperationEffects { get; init; }
    public required NewGameSettingPreview<NewGameMagnitude> AutomaticOccupationRange { get; init; }
    public required NewGameSettingPreview<NewGameMagnitude> FireDamage { get; init; }
    public required NewGameSettingPreview<NewGameMagnitude> MoraleEffects { get; init; }
    public required NewGameSettingPreview<NewGameTransportMobility> TransportMobility { get; init; }
    public required NewGameSettingPreview<NewGameCaptureFrequency> CaptureFrequency { get; init; }
    public required NewGameSettingPreview<NewGameAmount> LandmarkEffects { get; init; }
    public required NewGameSettingPreview<bool> InitialCreditEnabled { get; init; }
    public required NewGameSettingPreview<NewGameMeritGain> MeritGain { get; init; }
    public required NewGameSettingPreview<bool> CapturedOfficersEnabled { get; init; }
    public required NewGameSettingPreview<NewGameDisasterFrequency> DisasterFrequency { get; init; }
    public required NewGameSettingPreview<NewGameTacticDamage> TacticDamage { get; init; }
    public required NewGameSettingPreview<NewGameAmount> DurabilityRecovery { get; init; }
    public required NewGameSettingPreview<NewGameEnemyArmyInformation> EnemyArmyInformation { get; init; }
    public required NewGameSettingPreview<NewGameAmount> SupplyInterdictionEffects { get; init; }
    public required NewGameSettingPreview<bool> ForceMergingEnabled { get; init; }
    public required NewGameSettingPreview<NewGameDuration> ForeignTradeTravelDuration { get; init; }
    public required NewGameSettingPreview<bool> SpecialForceAttacksEnabled { get; init; }
    public required NewGameSettingPreview<NewGameAmount> LandmarkInvestmentCost { get; init; }
    public required NewGameSettingPreview<NewGameDuration> LandmarkInvestmentDuration { get; init; }
    public required NewGameSettingPreview<NewGameAmount> IntelligenceGain { get; init; }
    public required NewGameSettingPreview<bool> HiredOfficersEnabled { get; init; }
    public required NewGameSettingPreview<bool> HistoricalEventsEnabled { get; init; }
    public required NewGameSettingPreview<bool> ProposalsEnabled { get; init; }
    public required NewGameSettingPreview<NewGameHistoricalMode> WanderingOfficerCities { get; init; }
    public required NewGameSettingPreview<NewGameHistoricalMode> OfficerBloodRelationships { get; init; }
    public required NewGameSettingPreview<NewGameFemaleOfficerMode> FemaleOfficers { get; init; }
    public required IReadOnlyList<CurrentScenarioSettingRow> ReadOnlyRows { get; init; }
    public required NewGameDifficultyEffects DifficultyEffects { get; init; }
    public required IReadOnlyList<string> UnresolvedInputs { get; init; }

    /// <summary>Reprojects only the captured information row against the live
    /// local draft. Entering Top locks Partial without overwriting the saved
    /// user choice; leaving it immediately restores that choice or source.
    /// This performs no repository/world query or settings installation.</summary>
    public NewGameSettingPreview<NewGameEnemyArmyInformation> EnemyArmyInformationFor(NewGameOptions draft)
    {
        bool locked = NewGameOrdinarySettingsSource.DecodeDifficulty(draft.DifficultyId) == NewGameDifficulty.Top;
        return EnemyArmyInformation with
        {
            DraftValue = draft.EnemyArmyInformation,
            EffectiveValue = locked ? NewGameEnemyArmyInformation.Partial : draft.EnemyArmyInformation ?? EnemyArmyInformation.SourceDefault,
            Choices = Array.AsReadOnly(EnemyArmyInformation.Choices.Select(choice => choice with { CanSelect = !locked }).ToArray())
        };
    }

    /// <summary>Top locks the actual proposal entry off, retaining the user's
    /// draft for a later ordinary difficulty. Reuses the captured publication.</summary>
    public NewGameSettingPreview<bool> ProposalsEnabledFor(NewGameOptions draft)
    {
        bool locked = NewGameOrdinarySettingsSource.DecodeDifficulty(draft.DifficultyId) == NewGameDifficulty.Top;
        return ProposalsEnabled with
        {
            DraftValue = draft.ProposalsEnabled,
            EffectiveValue = locked ? false : draft.ProposalsEnabled ?? ProposalsEnabled.SourceDefault,
            Choices = Array.AsReadOnly(ProposalsEnabled.Choices.Select(choice => choice with { CanSelect = !locked }).ToArray())
        };
    }

    /// <summary>The remaining two source rows are immutable in this draft.</summary>
    public IReadOnlyList<CurrentScenarioSettingRow> ReadOnlyRowsFor(NewGameOptions draft) => ReadOnlyRows;
}
