namespace San14.Core;

/// <summary>An unconsumed fire balloon request. A null personality requests
/// the burning reaction; a personality requests its immunity reaction.
/// Display admission, position resolution and playback remain presentation work.</summary>
public sealed record ArmyFireAnnouncement(int UnitId, int OfficerId, int? PersonalityId)
{
    public bool FromFireCommand { get; init; }
}

/// <summary>Unplayed fire-casting effect. The renderer decides whether the
/// actor has a visible model; simulation does not fabricate a graphics object.</summary>
public sealed record ArmyFireCastPresentation(int UnitId, int OfficerId, int HexId);

public sealed partial record GameState
{
    // Earlier versions could not complete occupied fire branches and therefore
    // could not produce these requests. New saves require the explicit array.
    public ArmyFireAnnouncement[] PendingArmyFireAnnouncements { get; init; } = [];
    public ArmyFireCastPresentation[] PendingArmyFireCasts { get; init; } = [];
}
