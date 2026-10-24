namespace San14.Core;

/// <summary>Unplayed source-time discovery event. It is not an event receipt.</summary>
public sealed record TalentSceneryDiscoveryRequest(int SceneryId, int ForceId, int EventId,
    NativeMapSeasonDate Date, int HexId);

public sealed record TalentSceneryRewardRequest(int SceneryId, int ExecutorOfficerId,
    int RewardKind, int Amount)
{
    public string? DiscoveryTextAtSource { get; init; }
    public string? ExecutorNameAtSource { get; init; }
    public ushort? ExecutorPortraitAppearanceIndex { get; init; }
    public string? SceneryNameAtSource { get; init; }
    public TalentNarrationVoiceRequest? Voice { get; init; }
}

public enum TalentForceExperiencePresentationOperation { ExperienceGain, LevelTenReached }
public sealed record TalentForceExperiencePresentationRequest(TalentForceExperiencePresentationOperation Operation,
    int ForceId, int Amount)
{
    public int? PreviousLevel { get; init; }
}

public enum ForcePolicyLevelUpStage { Announcement, PolicyDecision, PolicyEditor }
public sealed record ForcePolicyLevelUpRequest(int ForceId, int PreviousLevel, int CurrentLevel,
    int AdditionalPolicySlots, int AdditionalCostLimit, string AnnouncementTextAtSource,
    string ConfirmationTextAtSource, ForcePolicyLevelUpStage Stage = ForcePolicyLevelUpStage.Announcement);
public enum ForcePolicyLevelUpInputKind { AnnouncementCompleted, OpenPolicy, DeclinePolicy, PolicyEditorReturned }
/// <summary>PolicyEditorAvailable is the actual application's strategy host
/// presence, supplied only when the announcement has really completed.</summary>
public sealed record ForcePolicyLevelUpInput(ForcePolicyLevelUpInputKind Kind, bool? PolicyEditorAvailable = null);
public sealed record ForcePolicyLevelUpResult(GameState State, ForcePolicyLevelUpRequest? Pending);
public interface IForceExperiencePresentationRules
{
    GameState CompleteExperiencePresentation(GameState state, TalentForceExperiencePresentationRequest request,
        int previousDisplayLevel);
    ForcePolicyLevelUpResult AdvancePolicyLevelUp(GameState state, ForcePolicyLevelUpRequest request,
        ForcePolicyLevelUpInput input);
}
