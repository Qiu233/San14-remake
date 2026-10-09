namespace San14.Core;

/// <summary>Optional current occupancy rule for choosing a Base before a
/// field Army sharing the same Hex. Null is a reached unresolved query, not
/// permission to fall back to another receiver.</summary>
public readonly record struct PlayableBaseContactPriority(int? LocationId);

public interface IPlayableBaseContactPriorityRules
{
    Func<int, PlayableBaseContactPriority?> CaptureCurrentBasePriority(GameState state);
}
