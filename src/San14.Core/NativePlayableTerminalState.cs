namespace San14.Core;

public enum NativePlayableTerminalKind { PlayerDefeated, OrdinaryUnified }

/// <summary>A source-decided session result. It does not unregister a native
/// Force or encode the original presentation object's exit request.</summary>
public sealed record NativePlayableTerminalState(NativePlayableTerminalKind Kind,
    int PlayerForcePhysical, int? TriggerVictimPhysical, string RuleId);

public sealed partial record GameState
{
    public NativePlayableTerminalState? NativePlayableTerminal { get; init; }
}

public static class NativePlayableTerminalStateValidation
{
    public static void Validate(GameState state)
    {
        if (state.NativePlayableTerminal is not { } result) return;
        if (!Enum.IsDefined(result.Kind) || state.Phase is not (TurnPhase.Strategy or TurnPhase.Reporting) ||
            result.PlayerForcePhysical is < 1 or > 45 || !state.Forces.ContainsKey(result.PlayerForcePhysical) ||
            string.IsNullOrWhiteSpace(result.RuleId) ||
            result.TriggerVictimPhysical is int victim && (victim is < 1 or > 45 || !state.Forces.ContainsKey(victim)) ||
            result.Kind == NativePlayableTerminalKind.PlayerDefeated && result.TriggerVictimPhysical != result.PlayerForcePhysical)
            throw new InvalidDataException("Saved playable terminal result has an invalid kind, player, trigger or phase.");
    }
}
