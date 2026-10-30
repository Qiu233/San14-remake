using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record TutorialLessonInputs(GameResourceRepository Repository,
    IS14AudioEventExecutionState Audio);

public enum TutorialLessonRequestKind { Narration, Interaction, Exit, CompletionNotice, Correction, InstructionCue }
public enum TutorialLessonAction
{
    DeploymentAccepted = 0, DeploymentOfficerSelectionAccepted = 1,
    DeploymentTargetAccepted = 3, ProgressRequested = 4, OfficerSelectionAccepted = 11,
    PriorityChanged = 12, PriorityConfirmed = 13, DomesticOrderAccepted = 14,
    ActionReportAcknowledged = 15, IncomeReportAcknowledged = 16,
    ProposalOfficerAccepted = 18, CityFirstSelectionAccepted = 23,
    CitySecondSelectionAccepted = 24, CityDomesticOrderAccepted = 25,
    AllianceConfirmed = 26, DiplomacyOfficerSelected = 28, AllianceTermSelected = 29,
    DiplomacyGoldSelected = 30, DiplomacyItemSelected = 31, DiplomacySuppliesSelected = 32,
    AllianceAreaSelected = 33, SabotageTargetSelected = 35, SabotageOfficerSelected = 36,
    SabotageConfirmed = 37, GoodwillConfirmed = 40, ForeignTradeConfirmed = 43, ForeignTradeOfficerSelected = 45, GeographicAdvantageExited = 67, GovernmentAutoPolicySelected = 76,
    PlacateAreaSelected = 35, PlacateOfficerSelected = 36, PlacateConfirmed = 93,
    SpyHomeEditorAccepted = 82, SpyHomeOfficerSelected = 84, SpyAwayAccepted = 85, SpyAwayOfficerSelected = 87,
    ScenerySearchAccepted = 95
}

/// <summary>A reached lesson interaction. Only Narration owns Story receipts;
/// Interaction is completed by the original command/selection predicates.</summary>
public sealed record TutorialLessonInstructionCue(int? TacticMarkerId, bool EnableTacticButtons)
{
    public int? CameraFocusHexId { get; init; }
    public int DurationMilliseconds { get; init; }
}

public sealed record TutorialLessonRequest(long Sequence, int ParagraphIndex,
    int Phase, TutorialLessonRequestKind Kind)
{
    public TutorialLessonInstructionCue? InstructionCue { get; init; }
    public int? StoryEventId { get; init; }
    public CampaignStoryRequest? Story { get; init; }
    public TutorialLessonGuidance? Guidance { get; init; }
    public TutorialLessonCompletionNotice? CompletionNotice { get; init; }
    public CampaignStoryPreparedText? CorrectionText { get; init; }
}

public sealed record TutorialLessonGuidance(int ParagraphIndex, string ParagraphName,
    ushort EnabledCommandMask, int? RequiredCommandId, int? RequiredInformationId)
{
    public int? CityId { get; init; }
    public int? RequiredCityInformationActionId { get; init; }
    public int? ResetCityInformationActionId { get; init; }
    public int? ConstructionSummaryIndex { get; init; }
    public int? RequiredDiplomacyForceId { get; init; }
    public int? RequiredDiplomacyOfficerId { get; init; }
    public int? RequiredForeignTradeCountryId { get; init; }
    public int? RequiredForeignTradeOfficerId { get; init; }
    public int? RequiredIntelligenceForceId { get; init; }
    public int? RequiredIntelligenceOfficerId { get; init; }
    public int? RequiredStrategicTargetId { get; init; }
    public int? RequiredStrategicOfficerId { get; init; }
    public int? RequiredSceneryActionId { get; init; }
    public int? ResetSceneryActionId { get; init; }
    public bool AllowsSelectionReturn { get; init; } = true;
    public int? RequiredFormationId { get; init; }
    public bool AllowsPeriodAdvance { get; init; }
    public bool AllowsDeploymentSetupConfirmation { get; init; }
    public bool AllowsDeploymentTargetConfirmation { get; init; }
    public int? AreaId { get; init; }
    public int? OfficerId { get; init; }
    public AreaDevelopmentFocus? RequiredDevelopmentFocus { get; init; }
    public bool InstructionCallbackPending { get; init; }
    public bool BlocksOrdinaryCommands { get; init; }
    public int? ResetLastCommandId { get; init; }
    public int? ResetLastInformationId { get; init; }
    public string? UnresolvedOwner { get; init; }
}
