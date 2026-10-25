using System.Collections.Immutable;

namespace San14.Core;

/// <summary>The actual player offer prepared by normal Turn16. Text belongs
/// to this arrival; displaying it never accepts the officer.</summary>
public sealed record SelfRecommendationRequest(int OfficerId, int ForceId, int DistrictId,
    int RulerOfficerId, string TextAtSource);

/// <summary>The actual monthly coalition invitation and the source's ordered
/// members. Displaying the question does not join or reject the coalition.</summary>
public sealed record CoalitionInvitationRequest(int TargetForceId, int PlayerForceId,
    int PlayerRulerId, int SpeakerOfficerId, ImmutableArray<int> OrderedMemberForceIds,
    string QuestionTextAtSource)
{
    public int? CameraHexId { get; init; }
}

/// <summary>A normal Turn decision after completed Army Progress. Officer
/// remainders belong only to16; coalition membership belongs to its own request.
/// The saved App value is restored only by the real Turn tail.</summary>
public sealed record NativeNormalTurnContinuation(int NextAction, uint RestoreAppRandomState,
    ImmutableArray<int> RemainingOfficerIds, SelfRecommendationRequest? PendingSelfRecommendation,
    CampaignGroupingCheckpoint Grouping)
{
    public CoalitionInvitationRequest? PendingCoalitionInvitation { get; init; }
}

public sealed partial record GameState
{
    public NativeNormalTurnContinuation? NativeNormalTurnContinuation { get; init; }
}
