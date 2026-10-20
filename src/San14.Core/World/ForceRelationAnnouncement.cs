namespace San14.Core;

/// <summary>Unconsumed relation-level notification. Numeric relations already
/// changed; presentation still owns the message and sound admission.</summary>
public sealed record ForceRelationAnnouncement(int FirstForceId, int SecondForceId, int Level);

public sealed partial record GameState
{
    public ForceRelationAnnouncement[] PendingForceRelationAnnouncements { get; init; } = [];
}
