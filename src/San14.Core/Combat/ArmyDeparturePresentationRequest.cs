namespace San14.Core;

/// <summary>The reached departure view setup, before later route/position
/// writers. The host must use this captured Army and appearance, not a later
/// world lookup. Null layout presence remains unresolved source knowledge.</summary>
public sealed record ArmyDeparturePresentationRequest(UnitState ArmyAtSource,
    int OfficerAppearanceAtSource, bool ReconcileModel, bool? RebuildInfoLayout)
{
    public ArmyDepartureSourceSnapshot? SourceSnapshot { get; init; }
    internal ArmyDeparturePresentationRequest Copy() => this with
        { ArmyAtSource = GameStateCopy.Copy(ArmyAtSource)!, SourceSnapshot = SourceSnapshot?.Copy() };
    internal void Validate()
    {
        if (ArmyAtSource is null || ArmyAtSource.Id <= 0 || ArmyAtSource.ForceId is < 0 or > 51 ||
            ArmyAtSource.OfficerId is < 0 or > 6000 || ArmyAtSource.HexId is < 0 or > 48400 ||
            OfficerAppearanceAtSource is < 0 or > ushort.MaxValue)
            throw new InvalidDataException("Army departure presentation is incomplete or invalid.");
        SourceSnapshot?.Validate();
    }
}
