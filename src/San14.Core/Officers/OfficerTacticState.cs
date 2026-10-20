namespace San14.Core;

public sealed partial record OfficerState
{
    /// <summary>Current ten tactic slots after fresh post-load and subsequent
    /// edits. Zero, repeated entries and order are retained. Null is unknown,
    /// independently of the static BaseTacticIds projection.</summary>
    public byte[]? CurrentTacticSlots { get; init; }
}
