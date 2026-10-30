namespace San14.Core;

public sealed record FacilityArmySpeechPreparationResult(GameState State, bool Handled,
    string? UnresolvedOwner = null);

/// <summary>Attempts the actual shared Army speech admission. Known native
/// refusals are handled; missing source inputs leave the outbox intact.</summary>
public interface IFacilityArmySpeechRules
{
    FacilityArmySpeechPreparationResult PrepareFacilityArmySpeech(GameState state, CombatSpeechRequest request);
}
