namespace San14.Core;

public sealed partial record OfficerState
{
    /// <summary>Current appearance notification association. Null is unknown;
    /// zero is a real cleared value, never inferred from unrelated relations.</summary>
    public int? AppearanceNotificationPersonId { get; init; }
}
