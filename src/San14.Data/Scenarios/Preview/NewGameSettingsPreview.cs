using San14.Core;

namespace San14.Data;

public sealed record NewGameSettingChoice<T>(T Value, int RadioIndex, int MessageId,
    string? Text, bool? CanSelect, string? UnresolvedInput) where T : struct;

/// <summary>Group and Row are zero-based source positions. SourceDefault is
/// the raw normal-source choice; EffectiveValue includes the real mode lock.</summary>
public sealed record NewGameSettingPreview<T>(int Group, int Row, int TitleMessageId,
    string? Title, T? DraftValue, T? SourceDefault, T? EffectiveValue,
    IReadOnlyList<NewGameSettingChoice<T>> Choices, string? UnresolvedInput) where T : struct;

/// <summary>Read-only consequences of the selected difficulty. These two
/// consumers are not included in the fifteen newly editable settings.</summary>
public enum NewGameEnemyArmyInformation { All = 1, Partial = 2 }
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
    public required NewGameDifficultyEffects DifficultyEffects { get; init; }
    public required IReadOnlyList<string> UnresolvedInputs { get; init; }
}
