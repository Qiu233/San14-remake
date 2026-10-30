namespace San14.Core;

public sealed record ArmyCommandSpeechPreparationResult(GameState State, bool Handled,
    string? UnresolvedOwner = null);

/// <summary>Attempts actual admission into the shared Army speech queue.
/// Known source refusals are handled; missing inputs retain the pending intent.
/// Admission does not receipt balloon or voice playback.</summary>
public interface IArmyCommandSpeechRules
{
    ArmyCommandSpeechPreparationResult PrepareArmyCommandSpeech(GameState state, CombatSpeechRequest request);
}
