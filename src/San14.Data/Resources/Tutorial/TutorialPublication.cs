using San14.Core;

namespace San14.Data;

/// <summary>Application progress readers, independent of a scenario or its
/// local tutorial control bitmap. Omitted readers retain unknown progress.</summary>
public sealed record TutorialProgressInputs(
    Func<int, bool?>? ReadCourseCompleted = null,
    Func<int, bool?>? ReadRewardAvailable = null,
    Func<int, bool?>? ReadRewardUnlocked = null);

public sealed record TutorialParagraph(int Index, string Name);
public sealed record TutorialReward(int Slot, int OfficerId, string Name,
    bool? Available, bool? Unlocked, OfficerPortraitRoute? Portrait, string? UnresolvedOwner);
public sealed record TutorialCourse(int Index, string Title, string Description,
    int ScenarioNumber, bool? Completed, IReadOnlyList<TutorialReward> Rewards,
    IReadOnlyList<TutorialParagraph> Paragraphs);

public sealed record TutorialPreparationInputs(ulong RandomSeed,
    CampaignAppRandomState AppRandom, CampaignCrtRandomState CrtRandom,
    NarrationVoiceSettings VoiceSettings, NewGameOptions Options);

/// <summary>A loaded lesson and selected controller. A paragraph host must
/// still own the real camera/UI/timer/command waits before the course runs.</summary>
public sealed record TutorialLessonPreparation(TutorialCourse Course,
    LoadedScenario? Scenario, LoadedMap? Map, GameState? World,
    ArmyCampaignRuleAdapter? Rules, string? UnresolvedOwner)
{
    public bool RequiresLessonHost => true;
    public int? PlayerForceId => World is null ? null : OwnedCurrentPlayerForce.ReadRawForce3A(World);
}
