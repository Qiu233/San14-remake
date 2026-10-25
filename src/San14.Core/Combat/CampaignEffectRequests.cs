using System.Collections.Immutable;

namespace San14.Core;

public enum CampaignEffectSource { NormalCombatRefresh, ArmyFalseReport, ArmyProgressNotifications, ArmyCommandMaintenance,
    ArmyDemolition, MapObjectRetirement, ForceAssociationRemoval, FacilityEffects, ArmyFacilityRepair, ArmyFacilityConstruction,
    ArmyTraitMaintenance, FacilityTraps, ArmyBattleDefeat, FireDamage, TerrainDamage, OfficerGrowthNotifications, PersonAppearance, PersonDeath,
    LandmarkInvestment, PersonLifespanNotifications, ForceUnregister, GovernanceRoster, ArmyStructureAttack, ArmyMovement, NormalCombatApplication, CityMerchant, TalentCommands, DiplomacyCommands, ForcePolicyLevelUp, OfficerOfficeCommands, PersonnelAdministrationCommands, MonthlyMaintenance, DoctrineResearchCommands, StrategicCommands }
public enum CampaignReportEvent { FirstContact, FalseReportSucceeded, FalseReportFailed, FalseReportBackfired,
    ArmyAutomaticTargetSelected, PlayerDemolitionVisible, PlayerDemolitionAcknowledged, OtherArmyDemolitionObserved,
    PlayerFacilityConstructionCompleted, PlayerFacilityRepairCompleted,
    PlayerFacilityConstructionStarted, PlayerFacilityRepairStarted }

/// <summary>Source-time semantics awaiting the application's real report owner.</summary>
public sealed record CampaignReportRequest(int DistrictId, int HexId, int CampaignDay,
    int OfficerId, string ArmyName, string TargetName, CombatParticipantKind TargetKind,
    int TargetId, int? ForceSlot, byte? ForceColor, byte? KingdomNameIndex, int? RulerId)
{
    public CampaignReportEvent Event { get; init; }
    public int? AreaId { get; init; }
    public byte? CommandChoiceId { get; init; }
    public string? RulerNameSnapshot { get; init; }
}
public enum ArmyFalseReportVisualEvent { TargetResisted, InitiatorFailed }
/// <summary>Unconsumed source-time65/66 visual intention, not a created effect or playback receipt.
/// The application owns the Army view transform and resource effect admission.</summary>
public sealed record ArmyFalseReportVisualRequest(ArmyFalseReportVisualEvent Event, ArmyReceiverId ReceiverId, int HexId);
public enum MapObjectPresentationEvent { DestructionVisual, DestructionSound, RetirementVisualDetach,
    RelationVisibilityAttach, RelationVisibilityDetach, BaseOwnershipLostVisual, BaseOwnershipLostSound }
/// <summary>Unconsumed semantic presentation work. Retirement asks the view
/// owner to detach any existing model and paired view links; it is not a receipt.</summary>
public sealed record MapObjectPresentationRequest(MapObjectPresentationEvent Event,
    MapObjectReceiverId ReceiverId, int? HexId, string? DefinitionKey);
/// <summary>Source-time body arguments for an association removal report.
/// Display strings must come from the complete message formatter.</summary>
public sealed record ForceAssociationRemovalReportRequest(int OldForceId, int OfficerId,
    int AssociationSlot, int TargetForceId, int DistrictId, int LocationId, int HexId,
    int CampaignDay, string OfficerDisplayName, string TargetForceDisplayName);
/// <summary>Stop all prior facility-manager persistent view effects. This is pending work, not playback.</summary>
public sealed record FacilityAuraResetRequest;
/// <summary>Source identities and logical selected Hex only. The application still
/// owns the actual Army view pose, facility attachment and effect playback.</summary>
public sealed record MilitaryMusicPulseRequest(MapObjectReceiverId FacilityReceiverId, int ArmyId, int HexId);
/// <summary>An unconsumed kind5 projectile attempt. The original Object/Army
/// Hex operands are retained even outside the current map:188880 computes X/Z
/// from them unchanged, while its height query returns0 for operands>=48400.
/// Geometry, effect admission, created handles and playback belong to App.</summary>
public sealed record FacilityProjectilePresentationRequest(MapObjectReceiverId FacilityReceiverId,
    ArmyReceiverId TargetReceiverId, ushort SourceHexOperand, ushort TargetHexOperand);
public enum FacilityRepairPresentationOperation { EnsureRepairLoop, FinishRepairLoop,
    RefreshFacilityCompletionModel, FacilityCompletionVisual, FacilityCompletionSound, EnsureFacilityAmbientLoop }
/// <summary>Unconsumed semantic work. Hex identifies the logical source cell;
/// real view existence, transform, attachment, handles and playback stay with App.
/// Only the completion visual consumes the source commander's identity.</summary>
public sealed record FacilityRepairPresentationRequest(FacilityRepairPresentationOperation Operation,
    MapObjectReceiverId ReceiverId, int HexId, string DefinitionKey, int? CommanderId);
public enum FacilityConstructionPresentationOperation { EnsureInitialFacilityModel, NotifyFacilityCreated,
    ShowSiteOwnershipChanged, PlaySiteOwnershipChangedSound }
/// <summary>Creation and ordinary site-ownership presentation intentions.
/// Hex is logical; the application owns view creation, pose and actual playback.
/// Only ownership intentions carry the source-time new Force identity.</summary>
public sealed record FacilityConstructionPresentationRequest(FacilityConstructionPresentationOperation Operation,
    MapObjectReceiverId ReceiverId, int HexId, string DefinitionKey, int? ForceSlot);
/// <summary>Refresh the shared Army trait view if the application has one.
/// ArmyId records the source caller; this is no view-existence or playback receipt.</summary>
public sealed record ArmyTraitViewRefreshRequest(int ArmyId);
/// <summary>Source-time trait feedback awaiting application admission. SpeakerHex
/// is the actual Person position; ContextHex is the independent site argument.</summary>
public sealed record ArmyTraitFeedbackRequest(int ArmyId, int OfficerId, int SpeakerHexId,
    int TraitId, int? ContextHexId)
{
    public static bool HasContext(int trait) => trait is >= 10 and <= 19 or >= 21 and <= 24 or
        >= 26 and <= 29 or 32 or 65 or 66 or 77 or 86 or 87 or 94 or 105 or 106 or 108 or
        150 or 152 or 153 or 216;
}
public enum FacilityTrapPresentationOperation { FireRangeVisual, ActivatedVisual,
    FireSmallMapNotice, StrongFireSmallMapNotice, ActivatedSmallMapNotice }
public sealed record FacilityTrapPresentationRequest(FacilityTrapPresentationOperation Operation,
    MapObjectReceiverId ReceiverId, int DefinitionId, int HexId, float? Range);
public enum ArmyTrapPresentationOperation { TargetPreparationVisual, TargetPreparationSmallMapNotice,
    AbnormalConfusionStarted, AbnormalImmobilizationStarted, TargetViewRefresh }
public sealed record ArmyTrapPresentationRequest(ArmyTrapPresentationOperation Operation,
    int ArmyId, int HexId, MapObjectReceiverId ReceiverId, int DefinitionId);
public enum ArmyFacilityDamagePresentationOperation { ConditionalFloatingNumber, SmallMapNotice, LargeMapNotice }
public sealed record ArmyFacilityDamagePresentationRequest(ArmyFacilityDamagePresentationOperation Operation,
    int ArmyId, int HexId, int GrossAmount, sbyte? DisplayModifier);
public enum FacilityTrapReportOperation { Triggered, Countered, AbnormalApplied, AbnormalAttribution, AbnormalImmunity }
public enum TrapCounterForceNameForm { Empty, CustomKingdom, PersonWithArmySuffix, PersonWithoutArmySuffix }
public sealed record TrapCounterForceNameOperand(byte ForceArgument0, TrapCounterForceNameForm Form,
    byte? CustomKingdomIndex, string? PersonNameOperandAtSource)
{
    internal void Validate()
    {
        bool valid = Form switch
        {
            TrapCounterForceNameForm.Empty => CustomKingdomIndex is null && PersonNameOperandAtSource is null,
            TrapCounterForceNameForm.CustomKingdom => ForceArgument0 is >= 1 and <= 51 &&
                CustomKingdomIndex is >= 1 and <= 100 && PersonNameOperandAtSource is null,
            TrapCounterForceNameForm.PersonWithArmySuffix or TrapCounterForceNameForm.PersonWithoutArmySuffix =>
                ForceArgument0 is >= 1 and <= 51 && CustomKingdomIndex is null && PersonNameOperandAtSource is not null,
            _ => false
        };
        if (!valid) throw new InvalidDataException("Invalid counter trap Force name operand.");
    }
}
/// <summary>Source-time trap report union. Each operation admits only its
/// actual fields; it is neither App report admission nor playback.</summary>
public sealed record FacilityTrapReportRequest(FacilityTrapReportOperation Operation,
    MapObjectReceiverId ReceiverId, int DefinitionId, int HexId, int CampaignDay, int DistrictId,
    string TrapName, int? ForceId, TrapCounterForceNameOperand? CounterForceName, int? ArmyId, int? OfficerId,
    string? ArmyName, int? TraitId, string? TraitName, byte? AbnormalIndex, bool? PositiveContribution)
{
    public ushort? AreaArgument0 { get; init; }
    //Complete source-time field33 Area operand, including its source color
    //controls. Valid empty names differ from invalid/absent Area text.
    public string? AreaNameAtSource { get; init; }
}
/// <summary>Exact morale-defeat report semantics. ReportHex is the source's
/// preserved ushort location argument, including invalid/unplaced values.</summary>
public sealed record ArmyMoraleDefeatReportRequest(int ArmyId, ushort OfficerId,
    string ArmyNameSnapshot, byte DistrictId, ushort ReportHex, int CampaignDay);

public enum FireDamagePresentationOperation { ArmyTargetViewRefresh, ArmyGrossDamage, BaseGrossDamage }
/// <summary>Source-time conditional work, not App existence, playback or ACK.
/// Kind3/Kind2 and null-related/byte0 are fixed by the gross operation.
/// ArmyId and BaseTarget are canonical campaign identities, not pointer keys.</summary>
public sealed record FireDamagePresentationRequest(FireDamagePresentationOperation Operation,
    int? ArmyId, SettlementBase? BaseTarget, int? GrossAmount);

public enum TerrainDamagePresentationOperation { ArmyTargetViewRefresh, ArmyGrossDamage }
/// <summary>Ordered source-time conditional work. Gross is one item's signed
/// request, including zero or negative, and is not an aggregate or App ACK.</summary>
public sealed record TerrainDamagePresentationRequest(TerrainDamagePresentationOperation Operation,
    int ArmyId, int? RequestedAmount);

public enum OfficerGrowthFeedbackOperation { SoundCue, Narration, History }
/// <summary>Already formatted source-time text. Future App work must not
/// select another person, recompute a delta or read a later mutable name.</summary>
public sealed record OfficerGrowthTextSnapshot(int OfficerId, string Text);
/// <summary>Category0, empty attachment and marks0/1/0/0 belong to this
/// history operation. Day is the complete signed current campaign operand.</summary>
public sealed record OfficerGrowthHistorySnapshot(int OfficerId, int RawDistrictArgument,
    int HexArgument, int CampaignDay, string Text);
/// <summary>Conditional semantic work, not App presence, admission or ACK.
/// SoundCue fixes sound4/float0 and has neither optional member.</summary>
public sealed record OfficerGrowthFeedbackRequest(OfficerGrowthFeedbackOperation Operation,
    OfficerGrowthTextSnapshot? Narration, OfficerGrowthHistorySnapshot? History);

public enum PersonAppearanceOperation { SoundCue, Announcement, EventEnter, SpeakerText, EventExit, History }
public sealed record PersonAppearanceText(ushort PersonId, string Text);
/// <summary>Category0, empty attachment and marks0/1/0/0 are fixed source semantics.</summary>
public sealed record PersonAppearanceHistory(ushort PersonId, byte DistrictArgument,
    int HexArgument, int CampaignDay, string Text);
/// <summary>Ordered source-time conditional work; never App presence or an ACK.</summary>
public sealed record PersonAppearanceRequest(PersonAppearanceOperation Operation,
    int? SoundId, int? EventKind, string? Announcement,
    PersonAppearanceText? Speaker, PersonAppearanceHistory? History);

public enum PersonDeathReportOperation { EventEnter, SpeakerText, EventExit, ControllerQueueEvent, NarrationText }
/// <summary>Ordered source-time conditional work; not App admission, playback or ACK.</summary>
public sealed record PersonDeathReportRequest(PersonDeathReportOperation Operation,
    int? EventKind, ushort? SpeakerId, string? TextAtSource);

public enum PersonLifespanNotificationOperation { ControllerQueueEvent, Narration }
/// <summary>Source-time conditional work. The Person operand belongs to the
/// narration packet; this is not a serif-speaker, App admission or receipt.</summary>
public sealed record PersonLifespanNotificationRequest(PersonLifespanNotificationOperation Operation,
    int? EventKind, ushort? PersonId, string? TextAtSource);

/// <summary>Formatted response to an ordered governance change. This is pending
/// application narration, not a history record, playback or acknowledgment.</summary>
public sealed record GovernanceNarrationRequest(int ForceId, int SpeakerId, int MessageId, string TextAtSource);
/// <summary>2ECE50 target reaction awaiting the real Army view. The view owns
/// existence, presentation148 and playback; this request acknowledges none.</summary>
public sealed record NormalCombatTargetReactionRequest(int ArmyId, int Variant);

/// <summary>Unconsumed source-time wall-damage cutscene intent. Tier2/3 map
/// to controller events0x12/0x13. Model66 is already committed by simulation;
/// playback must not repeat its setter or pretend this request is an ACK.</summary>
public sealed record BaseDamageTierCutsceneRequest(int BaseLocationId, byte DamageTier, int HexId);

/// <summary>Unconsumed source-time capture announcement. Text formatting,
/// controller admission and sound playback belong to the application.</summary>
public sealed record DuelCaptureAnnouncementRequest(int OfficerId, string OfficerNameAtSource,
    int MessageRoot, int SoundId);

public enum TalentCommandPresentationOperation { SoundCue, Announcement, OfficerText, RecruitConversation, ConditionalOfficerText }
public enum PersonnelVoiceLanguage { Japanese, Chinese }
/// <summary>The actual source-time voice selection, never a playback receipt.</summary>
public sealed record TalentNarrationVoiceRequest(int SpeakerId, byte VoiceType, int VoiceId,
    PersonnelVoiceLanguage Language, int VariantIndex, uint SoundId);
/// <summary>Both source-time actors remain attached to each line after recruitment
/// changes their service and Force. Unknown old portrait knowledge stays null.</summary>
public sealed record TalentRecruitConversationContext(int ExecutorOfficerId, int TargetOfficerId,
    string ExecutorNameAtSource, string TargetNameAtSource,
    ushort? ExecutorPortraitAppearanceIndex, ushort? TargetPortraitAppearanceIndex);
public sealed record TalentOfficerPresentationContext(int OfficerId, string NameAtSource,
    ushort? PortraitAppearanceIndex);
/// <summary>Original talent presentation calls awaiting application playback.</summary>
public sealed record TalentCommandPresentationRequest(TalentCommandPresentationOperation Operation,
    int? SoundId, int? OfficerId, int? TargetOfficerId, bool? RecruitSucceeded, string? TextAtSource)
{
    public TalentRecruitConversationContext? RecruitConversation { get; init; }
    public TalentNarrationVoiceRequest? Voice { get; init; }
    public TalentOfficerPresentationContext? Speaker { get; init; }
}

public sealed record DiplomacyArrivalAudioRequest(int Variant);

public enum MonthlyMaintenanceOperation
{
    ControllerQueueEvent, NarrationText, ConditionalSpeakerText, TerritoryOccupation, SceneryControlFeedback,
    DisasterSceneEnter, DisasterSceneExit, PersonnelSceneEnter, PersonnelSceneExit,
    EmperorSceneEnter, EmperorSceneExit, FinalOrderSceneEnter, FinalOrderSceneExit, ResetPersonnelGroupMoveDestinations
}
/// <summary>Source-time monthly controller and territory presentation intent.
/// Numerical completion never acknowledges any display, sound or animation.</summary>
public sealed record MonthlyMaintenanceRequest(MonthlyMaintenanceOperation Operation,
    int? EventKind, ushort? SpeakerId, string? TextAtSource)
{
    public int? ForceId { get; init; }
    public int? HexId { get; init; }
    public int? SceneryId { get; init; }
    public int? ArmyId { get; init; }
}

public enum ForceCoalitionVisualOperation { CameraFocus, CameraRestore, NarrationEnter, NarrationExit, CityRelationIndicatorsRefresh }
/// <summary>The source coalition scene's unconsumed camera/narration scope.
/// Camera focus uses the actual source Hex, transition1000 and zoom band4.
/// Camera state is captured by the presentation host only when it really plays.</summary>
public sealed record ForceCoalitionVisualRequest(ForceCoalitionVisualOperation Operation, int? TargetHexId)
{
    public int? SceneKind { get; init; }
}
/// <summary>The actual independence cutscene queued before force creation.
/// Keeping the request does not imply that its scene has played.</summary>
public sealed record ForceIndependenceSceneRequest(int OriginalForceId, int RulerOfficerId,
    int HomeLocationId, int SourceDay);
/// <summary>The emergence scene queued after the new force's followers have
/// migrated. It is distinct from the pre-creation independence scene.</summary>
public sealed record ForceEmergenceSceneRequest(int ForceId, int RulerOfficerId,
    int HomeLocationId, int SourceDay);
public enum MonthlyDisasterConsequenceOperation { CameraFocus, AreaOrBaseEffect, ArmyLossNumber, Summary, CameraRestore }
/// <summary>Ordered, unconsumed epidemic or locust feedback. Closing a real
/// message can suppress later display while numerical settlement still runs.</summary>
public sealed record MonthlyDisasterConsequenceRequest(MonthlyDisasterConsequenceOperation Operation,
    int DisasterKind, int CityId, int HexId, string? TextAtSource = null, int? Amount = null, int? ArmyReceiverId = null)
{
    public int? BaseLocationId { get; init; }
}
public sealed record NormalTurnFoodWarningPresentationRequest(ImmutableArray<int> WarningBaseLocationIds,
    string TextAtSource);
public sealed record NormalTurnForceResourceNoticeRequest(int ForceId, int BaseLocationId, int ResourceKind,
    int MessageRoot, int Amount, int CurrentAmount)
{
    public string? TextAtSource { get; init; }
}

public sealed record CampaignEffectRequest(CombatSpeechRequest? Speech, CampaignReportRequest? Report)
{
    public ForceCoalitionVisualRequest? ForceCoalitionVisual { get; init; }
    public ForceIndependenceSceneRequest? ForceIndependenceScene { get; init; }
    public ForceEmergenceSceneRequest? ForceEmergenceScene { get; init; }
    public MonthlyDisasterConsequenceRequest? MonthlyDisasterConsequence { get; init; }
    public NormalTurnFoodWarningPresentationRequest? NormalTurnFoodWarningPresentation { get; init; }
    public NormalTurnForceResourceNoticeRequest? NormalTurnForceResourceNotice { get; init; }
    public MonthlyMaintenanceRequest? MonthlyMaintenance { get; init; }
    public ArmyFalseReportVisualRequest? ArmyFalseReportVisual { get; init; }
    public DuelCaptureAnnouncementRequest? DuelCaptureAnnouncement { get; init; }
    public GovernanceNarrationRequest? GovernanceNarration { get; init; }
    public NormalCombatTargetReactionRequest? NormalCombatTargetReaction { get; init; }
    public BaseDamageTierCutsceneRequest? BaseDamageTierCutscene { get; init; }
    public PersonLifespanNotificationRequest? PersonLifespanNotification { get; init; }
    public CampaignHistoryNotification? HistoryNotification { get; init; }
    public PersonDeathReportRequest? PersonDeathReport { get; init; }
    public PersonAppearanceRequest? PersonAppearance { get; init; }
    public TalentCommandPresentationRequest? TalentCommandPresentation { get; init; }
    public DiplomacyArrivalAudioRequest? DiplomacyArrivalAudio { get; init; }
    public TalentSceneryDiscoveryRequest? TalentSceneryDiscovery { get; init; }
    public TalentSceneryRewardRequest? TalentSceneryReward { get; init; }
    public TalentForceExperiencePresentationRequest? TalentForceExperiencePresentation { get; init; }
    public ForcePolicyLevelUpRequest? ForcePolicyLevelUp { get; init; }
    public OfficerGrowthFeedbackRequest? OfficerGrowthFeedback { get; init; }
    public TerrainDamagePresentationRequest? TerrainDamagePresentation { get; init; }
    public FireDamagePresentationRequest? FireDamagePresentation { get; init; }
    public FacilityTrapPresentationRequest? FacilityTrapPresentation { get; init; }
    public ArmyTrapPresentationRequest? ArmyTrapPresentation { get; init; }
    public ArmyFacilityDamagePresentationRequest? ArmyFacilityDamagePresentation { get; init; }
    public FacilityTrapReportRequest? FacilityTrapReport { get; init; }
    public ArmyMoraleDefeatReportRequest? ArmyMoraleDefeatReport { get; init; }
    public ArmyTraitViewRefreshRequest? ArmyTraitViewRefresh { get; init; }
    public ArmyTraitFeedbackRequest? ArmyTraitFeedback { get; init; }
    public FacilityConstructionPresentationRequest? FacilityConstructionPresentation { get; init; }
    public FacilityRepairPresentationRequest? FacilityRepairPresentation { get; init; }
    public FacilityAuraResetRequest? FacilityAuraReset { get; init; }
    public MilitaryMusicPulseRequest? MilitaryMusicPulse { get; init; }
    public FacilityProjectilePresentationRequest? FacilityProjectile { get; init; }
    public MapObjectPresentationRequest? MapObjectPresentation { get; init; }
    public ForceAssociationRemovalReportRequest? ForceAssociationRemovalReport { get; init; }
}
public sealed record CampaignEffectBatch(long Sequence, CampaignEffectSource Source, long SourceRevision,
    CampaignEffectRequest[] Requests);

/// <summary>Ordered, unconsumed campaign results. Admission, App random state,
/// accepted queues, playback and acknowledgments are not owned here.</summary>
public sealed record CampaignEffectRequests(long LastBatchSequence, CampaignEffectBatch[] PendingBatches)
{
    public static CampaignEffectRequests Empty => new(0, []);
    public CampaignEffectRequests Append(CampaignEffectSource source, long sourceRevision,
        IReadOnlyList<CampaignEffectRequest> requests)
    {
        if (!Enum.IsDefined(source) || sourceRevision < 0) throw new InvalidDataException("Invalid campaign effect origin.");
        if (requests.Count == 0) return this;
        if (LastBatchSequence == long.MaxValue) throw new InvalidDataException("Campaign effect sequence is exhausted.");
        long sequence = LastBatchSequence + 1;
        return new(sequence, [.. PendingBatches, new(sequence, source, sourceRevision, requests.ToArray())]);
    }
    public CampaignEffectRequests Copy() => this with
    { PendingBatches = PendingBatches.Select(batch => batch with { Requests = batch.Requests.ToArray() }).ToArray() };
    internal static void Validate(CampaignEffectRequests? state)
    {
        if (state is null) return;
        if (state.LastBatchSequence < 0 || state.PendingBatches is null) throw new InvalidDataException("Invalid campaign effect owner.");
        long previous = 0;
        foreach (var batch in state.PendingBatches)
        {
            if (batch is null || batch.Sequence <= previous || batch.Sequence > state.LastBatchSequence ||
                !Enum.IsDefined(batch.Source) || batch.SourceRevision < 0 || batch.Requests is null)
                throw new InvalidDataException("Invalid campaign effect batch order.");
            previous = batch.Sequence;
            foreach (var request in batch.Requests)
            {
                if (request is null || (request.ArmyFalseReportVisual is null ? 0 : 1) + (request.Speech is null ? 0 : 1) + (request.Report is null ? 0 : 1) +
                    (request.MapObjectPresentation is null ? 0 : 1) + (request.ForceAssociationRemovalReport is null ? 0 : 1) +
                    (request.FacilityAuraReset is null ? 0 : 1) + (request.MilitaryMusicPulse is null ? 0 : 1) +
                    (request.FacilityProjectile is null ? 0 : 1) +
                    (request.FacilityRepairPresentation is null ? 0 : 1) +
                    (request.FacilityConstructionPresentation is null ? 0 : 1) +
                    (request.ArmyTraitViewRefresh is null ? 0 : 1) +
                    (request.ArmyTraitFeedback is null ? 0 : 1) +
                    (request.FacilityTrapPresentation is null ? 0 : 1) +
                    (request.ArmyTrapPresentation is null ? 0 : 1) +
                    (request.ArmyFacilityDamagePresentation is null ? 0 : 1) +
                    (request.FacilityTrapReport is null ? 0 : 1) +
                    (request.ArmyMoraleDefeatReport is null ? 0 : 1) +
                    (request.FireDamagePresentation is null ? 0 : 1) +
                    (request.TerrainDamagePresentation is null ? 0 : 1) +
                    (request.OfficerGrowthFeedback is null ? 0 : 1) +
                    (request.PersonAppearance is null ? 0 : 1) +
                    (request.TalentCommandPresentation is null ? 0 : 1) +
                    (request.DiplomacyArrivalAudio is null ? 0 : 1) +
                    (request.TalentSceneryDiscovery is null ? 0 : 1) +
                    (request.TalentSceneryReward is null ? 0 : 1) +
                    (request.TalentForceExperiencePresentation is null ? 0 : 1) +
                    (request.ForcePolicyLevelUp is null ? 0 : 1) +
                    (request.PersonDeathReport is null ? 0 : 1) +
                    (request.HistoryNotification is null ? 0 : 1) +
                    (request.PersonLifespanNotification is null ? 0 : 1) +
                    (request.GovernanceNarration is null ? 0 : 1) +
                    (request.NormalCombatTargetReaction is null ? 0 : 1) +
                    (request.BaseDamageTierCutscene is null ? 0 : 1) +
                    (request.DuelCaptureAnnouncement is null ? 0 : 1) +
                    (request.MonthlyMaintenance is null ? 0 : 1) +
                    (request.ForceCoalitionVisual is null ? 0 : 1) +
                    (request.ForceIndependenceScene is null ? 0 : 1) +
                    (request.ForceEmergenceScene is null ? 0 : 1) +
                    (request.MonthlyDisasterConsequence is null ? 0 : 1) +
                    (request.NormalTurnFoodWarningPresentation is null ? 0 : 1) +
                    (request.NormalTurnForceResourceNotice is null ? 0 : 1) != 1)
                    throw new InvalidDataException("A campaign effect requires one semantic payload.");
                if (request.DuelCaptureAnnouncement is { } captured &&
                    (batch.Source != CampaignEffectSource.ArmyBattleDefeat || captured.OfficerId is < 1 or > 6000 ||
                     captured.OfficerNameAtSource is null || captured.MessageRoot is not (0x2A8C or 0x34AA) ||
                     captured.SoundId is not (6 or 8)))
                    throw new InvalidDataException("Invalid duel capture announcement.");
                if (request.NormalCombatTargetReaction is { } reaction &&
                    (batch.Source != CampaignEffectSource.NormalCombatApplication || reaction.ArmyId <= 0 ||
                     reaction.Variant is not (1 or 2)))
                    throw new InvalidDataException("Invalid normal combat target reaction.");
                if (request.BaseDamageTierCutscene is { } tier &&
                    (batch.Source != CampaignEffectSource.NormalCombatApplication || tier.BaseLocationId is < 1 or > 51 ||
                     tier.DamageTier is not (2 or 3) || tier.HexId is < 0 or > 48400))
                    throw new InvalidDataException("Invalid Base damage-tier cutscene request.");
                if (batch.Source == CampaignEffectSource.NormalCombatApplication &&
                    request.NormalCombatTargetReaction is null && request.BaseDamageTierCutscene is null &&
                    request.ArmyTraitViewRefresh is null && request.ArmyTraitFeedback is null &&
                    request.HistoryNotification is null)
                    throw new InvalidDataException("Normal combat presentation requires a supported pending payload.");
                if (request.GovernanceNarration is { } governance &&
                    (batch.Source != CampaignEffectSource.GovernanceRoster || governance.ForceId is < 1 or > 51 || governance.SpeakerId is < 1 or > 6000 ||
                     governance.MessageId is not (0x57F4 or 0x57F9) || governance.TextAtSource is null))
                    throw new InvalidDataException("Invalid governance narration request.");
                if (batch.Source == CampaignEffectSource.GovernanceRoster && request.GovernanceNarration is null)
                    throw new InvalidDataException("A governance response requires its own narration payload.");
                if (request.HistoryNotification is { } notification)
                {
                    if (batch.Source is not (CampaignEffectSource.NormalCombatRefresh or
                        CampaignEffectSource.ArmyFalseReport or CampaignEffectSource.ArmyProgressNotifications or
                        CampaignEffectSource.ArmyCommandMaintenance or CampaignEffectSource.ArmyDemolition or CampaignEffectSource.ArmyStructureAttack or
                        CampaignEffectSource.ForceAssociationRemoval or CampaignEffectSource.ArmyFacilityRepair or
                        CampaignEffectSource.ArmyFacilityConstruction or CampaignEffectSource.FacilityTraps or
                        CampaignEffectSource.ArmyBattleDefeat or CampaignEffectSource.FireDamage or
                        CampaignEffectSource.NormalCombatApplication or CampaignEffectSource.FacilityEffects or
                        CampaignEffectSource.OfficerGrowthNotifications or CampaignEffectSource.PersonAppearance or
                        CampaignEffectSource.PersonDeath or CampaignEffectSource.LandmarkInvestment or CampaignEffectSource.CityMerchant or CampaignEffectSource.TalentCommands or CampaignEffectSource.DiplomacyCommands or CampaignEffectSource.ForcePolicyLevelUp or CampaignEffectSource.MonthlyMaintenance))
                        throw new InvalidDataException("Invalid campaign history notification origin.");
                    CampaignHistoryValidation.ValidateNotification(notification);
                    continue;
                }
                if (batch.Source == CampaignEffectSource.LandmarkInvestment)
                    throw new InvalidDataException("A landmark history batch requires a post-write notification.");
                if (request.MonthlyMaintenance is { } monthly)
                {
                    bool noSite = monthly.ForceId is null && monthly.HexId is null && monthly.SceneryId is null && monthly.ArmyId is null;
                    bool valid = monthly.Operation switch
                    {
                        MonthlyMaintenanceOperation.ControllerQueueEvent => noSite && monthly.EventKind is 6 or 8 &&
                            monthly.SpeakerId is null && monthly.TextAtSource is null,
                        MonthlyMaintenanceOperation.NarrationText => noSite && monthly.EventKind is null &&
                            monthly.SpeakerId is null && monthly.TextAtSource is not null,
                        MonthlyMaintenanceOperation.ConditionalSpeakerText => noSite && monthly.EventKind is null &&
                            monthly.SpeakerId is > 0 and <= 6000 && monthly.TextAtSource is not null,
                        MonthlyMaintenanceOperation.TerritoryOccupation => monthly.EventKind is 4 or 5 &&
                            monthly.ForceId is >= 0 and <= 51 && monthly.HexId is >= 0 and < 48400 &&
                            monthly.SpeakerId is null && monthly.TextAtSource is null && monthly.SceneryId is null && monthly.ArmyId is null,
                        MonthlyMaintenanceOperation.SceneryControlFeedback => monthly.SceneryId is >= 0 and <= 100 &&
                            monthly.ArmyId is > 0 && monthly.EventKind is null && monthly.ForceId is null &&
                            monthly.HexId is >= 0 and <= 48400 && monthly.SpeakerId is >= 0 and <= 6000 && monthly.TextAtSource is null,
                        MonthlyMaintenanceOperation.DisasterSceneEnter => noSite && monthly.EventKind is 14 or 15 or 16 &&
                            monthly.SpeakerId is null && monthly.TextAtSource is null,
                        MonthlyMaintenanceOperation.DisasterSceneExit => noSite && monthly.EventKind is null &&
                            monthly.SpeakerId is null && monthly.TextAtSource is null,
                        MonthlyMaintenanceOperation.PersonnelSceneEnter => noSite && monthly.EventKind == 9 &&
                            monthly.SpeakerId is null && monthly.TextAtSource is null,
                        MonthlyMaintenanceOperation.PersonnelSceneExit => noSite && monthly.EventKind is null &&
                            monthly.SpeakerId is null && monthly.TextAtSource is null,
                        MonthlyMaintenanceOperation.EmperorSceneEnter => noSite && monthly.EventKind is 6 or 45 or 55 &&
                            monthly.SpeakerId is null && monthly.TextAtSource is null,
                        MonthlyMaintenanceOperation.EmperorSceneExit => noSite && monthly.EventKind is null &&
                            monthly.SpeakerId is null && monthly.TextAtSource is null,
                        MonthlyMaintenanceOperation.FinalOrderSceneEnter => noSite && monthly.EventKind is 10 or 26 or 31 &&
                            monthly.SpeakerId is null && monthly.TextAtSource is null,
                        MonthlyMaintenanceOperation.FinalOrderSceneExit or MonthlyMaintenanceOperation.ResetPersonnelGroupMoveDestinations =>
                            noSite && monthly.EventKind is null && monthly.SpeakerId is null && monthly.TextAtSource is null,
                        _ => false
                    };
                    if ((batch.Source != CampaignEffectSource.MonthlyMaintenance &&
                         !(batch.Source == CampaignEffectSource.ArmyBattleDefeat && monthly.Operation is
                             (MonthlyMaintenanceOperation.TerritoryOccupation or MonthlyMaintenanceOperation.SceneryControlFeedback))) || !valid)
                        throw new InvalidDataException("Invalid monthly maintenance presentation intent.");
                }
                ValidateTrap(batch.Source, request);
                ValidateFireDamage(batch.Source, request);
                ValidateTerrainDamage(batch.Source, request);
                ValidateOfficerGrowth(batch.Source, request);
                ValidatePersonAppearance(batch.Source, request);
                ValidatePersonLifespanNotification(batch.Source, request);
                ValidatePersonDeath(batch.Source, request);
                if (request.ForceCoalitionVisual is { } coalitionVisual &&
                    (batch.Source is not (CampaignEffectSource.MonthlyMaintenance or CampaignEffectSource.StrategicCommands or CampaignEffectSource.ArmyBattleDefeat) || !Enum.IsDefined(coalitionVisual.Operation) ||
                     batch.Source == CampaignEffectSource.ArmyBattleDefeat && coalitionVisual.Operation != ForceCoalitionVisualOperation.CityRelationIndicatorsRefresh ||
                     (coalitionVisual.Operation == ForceCoalitionVisualOperation.CameraFocus
                        ? coalitionVisual.TargetHexId is null or < 0 or > 48400 : coalitionVisual.TargetHexId is not null) ||
                     (coalitionVisual.Operation == ForceCoalitionVisualOperation.NarrationEnter
                        ? coalitionVisual.SceneKind is not (null or 24 or 40 or 53) : coalitionVisual.SceneKind is not null) ||
                     coalitionVisual.Operation == ForceCoalitionVisualOperation.CityRelationIndicatorsRefresh &&
                     batch.Source is not (CampaignEffectSource.MonthlyMaintenance or CampaignEffectSource.ArmyBattleDefeat)))
                    throw new InvalidDataException("Invalid force coalition visual intent.");
                if (request.ForceIndependenceScene is { } independenceScene &&
                    (batch.Source is not (CampaignEffectSource.StrategicCommands or CampaignEffectSource.MonthlyMaintenance) ||
                     independenceScene.OriginalForceId is < 0 or > 51 ||
                     independenceScene.RulerOfficerId is < 0 or > 6000 ||
                     independenceScene.HomeLocationId is < 0 or > 61))
                    throw new InvalidDataException("Invalid force independence scene intent.");
                if (request.ForceEmergenceScene is { } emergence &&
                    (batch.Source != CampaignEffectSource.MonthlyMaintenance || emergence.ForceId is < 0 or > 51 ||
                     emergence.RulerOfficerId is < 0 or > 6000 || emergence.HomeLocationId is < 0 or > 61))
                    throw new InvalidDataException("Invalid force emergence scene intent.");
                if (request.MonthlyDisasterConsequence is { } consequence)
                {
                    bool valid = consequence.Operation switch
                    {
                        MonthlyDisasterConsequenceOperation.CameraFocus or MonthlyDisasterConsequenceOperation.CameraRestore =>
                            consequence.TextAtSource is null && consequence.Amount is null && consequence.ArmyReceiverId is null && consequence.BaseLocationId is null,
                        MonthlyDisasterConsequenceOperation.AreaOrBaseEffect => consequence.TextAtSource is null &&
                            (consequence.BaseLocationId is not null
                                ? consequence.ArmyReceiverId is null
                                : consequence.Amount is null),
                        MonthlyDisasterConsequenceOperation.ArmyLossNumber => consequence.TextAtSource is null && consequence.Amount is not null &&
                            consequence.ArmyReceiverId is not null && consequence.BaseLocationId is null,
                        MonthlyDisasterConsequenceOperation.Summary => consequence.TextAtSource is not null && consequence.Amount is null &&
                            consequence.ArmyReceiverId is null && consequence.BaseLocationId is null,
                        _ => false
                    };
                    if (batch.Source != CampaignEffectSource.MonthlyMaintenance || !valid || consequence.DisasterKind is not (0 or 1) ||
                        consequence.CityId is < 1 or > 51 || consequence.HexId is < 0 or >= 48400 ||
                        consequence.ArmyReceiverId is < 1 or > 500 || consequence.BaseLocationId is < 1 or > 61)
                        throw new InvalidDataException("Invalid monthly disaster consequence intent.");
                }
                if (request.NormalTurnFoodWarningPresentation is { } warning &&
                    (batch.Source != CampaignEffectSource.MonthlyMaintenance || warning.TextAtSource is null ||
                     warning.WarningBaseLocationIds.IsDefault || warning.WarningBaseLocationIds.Any(id => id is < 1 or > 61)))
                    throw new InvalidDataException("Invalid normal Turn food warning intent.");
                if (request.NormalTurnForceResourceNotice is { } notice &&
                    (batch.Source != CampaignEffectSource.MonthlyMaintenance || notice.ForceId is < 1 or > 45 ||
                     notice.BaseLocationId is < 1 or > 61 || notice.Amount <= 0 ||
                     (notice.ResourceKind, notice.MessageRoot) is not ((0x17, 0x5EE3) or (0x2A, 0x5EE4) or (0x21, 0x5EE5))))
                    throw new InvalidDataException("Invalid normal Turn force resource notice.");
                if (request.DiplomacyArrivalAudio is { } audio &&
                    (batch.Source != CampaignEffectSource.DiplomacyCommands || audio.Variant is < 0 or > 2))
                    throw new InvalidDataException("Invalid diplomatic arrival audio variant.");
                if (request.TalentCommandPresentation is { } talent)
                {
                    bool valid = talent.Operation switch
                    {
                        TalentCommandPresentationOperation.SoundCue =>
                            (talent.SoundId is 5 or 6 or 8 ||
                             batch.Source == CampaignEffectSource.ArmyBattleDefeat && talent.SoundId == 7 ||
                             batch.Source == CampaignEffectSource.MonthlyMaintenance && (talent.SoundId is 7 or 16 or 18 or 20 or 21 or 22 or 23 or 129) ||
                             batch.Source == CampaignEffectSource.StrategicCommands && talent.SoundId is (7 or 21) ||
                             (batch.Source is CampaignEffectSource.DiplomacyCommands or CampaignEffectSource.PersonnelAdministrationCommands or CampaignEffectSource.MonthlyMaintenance) && talent.SoundId == 4) &&
                            talent.TextAtSource is null,
                        TalentCommandPresentationOperation.Announcement => talent.SoundId is null && talent.TextAtSource is not null,
                        TalentCommandPresentationOperation.OfficerText or TalentCommandPresentationOperation.ConditionalOfficerText => talent.OfficerId is > 0 and <= 6000 && talent.TextAtSource is not null,
                        TalentCommandPresentationOperation.RecruitConversation => talent.OfficerId is > 0 and <= 6000 &&
                            talent.TargetOfficerId is > 0 and <= 6000 && talent.RecruitSucceeded.HasValue,
                        _ => false
                    };
                    if (talent.Operation == TalentCommandPresentationOperation.ConditionalOfficerText && batch.Source is not (CampaignEffectSource.StrategicCommands or CampaignEffectSource.MonthlyMaintenance or CampaignEffectSource.ArmyBattleDefeat) ||
                        batch.Source is not (CampaignEffectSource.TalentCommands or CampaignEffectSource.DiplomacyCommands or CampaignEffectSource.OfficerOfficeCommands or CampaignEffectSource.PersonnelAdministrationCommands or CampaignEffectSource.MonthlyMaintenance or CampaignEffectSource.DoctrineResearchCommands or CampaignEffectSource.StrategicCommands or CampaignEffectSource.ArmyBattleDefeat) || !valid ||
                        batch.Source == CampaignEffectSource.ArmyBattleDefeat &&
                        (talent.Operation is not (TalentCommandPresentationOperation.SoundCue or TalentCommandPresentationOperation.Announcement or TalentCommandPresentationOperation.ConditionalOfficerText) ||
                         talent.Operation == TalentCommandPresentationOperation.SoundCue && talent.SoundId is not (5 or 6 or 7 or 8) ||
                         talent.Operation == TalentCommandPresentationOperation.ConditionalOfficerText && talent.Speaker is null) ||
                        batch.Source == CampaignEffectSource.DoctrineResearchCommands &&
                        (talent.Operation != TalentCommandPresentationOperation.OfficerText || talent.Speaker is null) ||
                        batch.Source == CampaignEffectSource.StrategicCommands &&
                        (talent.Operation is not (TalentCommandPresentationOperation.SoundCue or TalentCommandPresentationOperation.Announcement or TalentCommandPresentationOperation.OfficerText or TalentCommandPresentationOperation.ConditionalOfficerText) ||
                         talent.Operation == TalentCommandPresentationOperation.SoundCue && talent.SoundId is not (6 or 7 or 8 or 21) ||
                         talent.Operation is (TalentCommandPresentationOperation.OfficerText or TalentCommandPresentationOperation.ConditionalOfficerText) && talent.Speaker is null) ||
                        batch.Source == CampaignEffectSource.OfficerOfficeCommands &&
                        talent.Operation is not (TalentCommandPresentationOperation.SoundCue or TalentCommandPresentationOperation.Announcement))
                        throw new InvalidDataException("Invalid talent presentation payload.");
                    if (talent.Speaker is { } speaker &&
                        (batch.Source is not (CampaignEffectSource.DoctrineResearchCommands or CampaignEffectSource.StrategicCommands or CampaignEffectSource.MonthlyMaintenance or CampaignEffectSource.ArmyBattleDefeat) ||
                         talent.Operation is not (TalentCommandPresentationOperation.OfficerText or TalentCommandPresentationOperation.ConditionalOfficerText) ||
                         speaker.OfficerId != talent.OfficerId || speaker.OfficerId is < 1 or > 6000 ||
                         speaker.NameAtSource is null))
                        throw new InvalidDataException("Invalid source-time officer presentation context.");
                    if (talent.RecruitConversation is { } conversation &&
                        (batch.Source is not (CampaignEffectSource.TalentCommands or CampaignEffectSource.PersonnelAdministrationCommands) ||
                         talent.Operation != TalentCommandPresentationOperation.OfficerText ||
                         conversation.ExecutorOfficerId is < 1 or > 6000 || conversation.TargetOfficerId is < 1 or > 6000 ||
                         talent.OfficerId != conversation.ExecutorOfficerId && talent.OfficerId != conversation.TargetOfficerId ||
                         talent.TargetOfficerId != conversation.TargetOfficerId ||
                         batch.Source == CampaignEffectSource.TalentCommands && !talent.RecruitSucceeded.HasValue ||
                         conversation.ExecutorNameAtSource is null || conversation.TargetNameAtSource is null))
                        throw new InvalidDataException("Invalid recruitment conversation context.");
                    if (talent.Voice is { } voice &&
                        (talent.Operation is not (TalentCommandPresentationOperation.OfficerText or TalentCommandPresentationOperation.ConditionalOfficerText) ||
                         batch.Source == CampaignEffectSource.MonthlyMaintenance && talent.Speaker is null ||
                         voice.SpeakerId != talent.OfficerId || voice.SpeakerId is < 1 or > 6000 ||
                         (batch.Source == CampaignEffectSource.MonthlyMaintenance ? voice.VoiceId is not (33 or 40 or 42 or 43) :
                          batch.Source == CampaignEffectSource.StrategicCommands ? voice.VoiceId is not (33 or 42 or 43) :
                          batch.Source == CampaignEffectSource.DoctrineResearchCommands ? voice.VoiceId != 42 :
                          batch.Source == CampaignEffectSource.PersonnelAdministrationCommands
                             ? voice.VoiceId is not (33 or 34 or 35 or 36) : voice.VoiceId != 40) ||
                         voice.VoiceType >= 26 || !Enum.IsDefined(voice.Language) ||
                         voice.VariantIndex is < 0 or > 4 || voice.SoundId == 0 ||
                         batch.Source is not (CampaignEffectSource.DoctrineResearchCommands or CampaignEffectSource.StrategicCommands or CampaignEffectSource.MonthlyMaintenance) && talent.RecruitConversation is null))
                        throw new InvalidDataException("Invalid recruitment voice selection.");
                }
                if (request.TalentSceneryDiscovery is { } discovery &&
                    (batch.Source != CampaignEffectSource.TalentCommands || discovery.SceneryId is < 0 or > 100 ||
                     discovery.ForceId is < 1 or > 51 || discovery.EventId != 0x14F ||
                     discovery.Date is null || discovery.HexId is < 0 or > 48400))
                    throw new InvalidDataException("Invalid talent scenery discovery intent.");
                if (request.TalentSceneryReward is { } reward &&
                    (batch.Source != CampaignEffectSource.TalentCommands || reward.SceneryId is < 0 or > 100 ||
                     reward.ExecutorOfficerId is < 1 or > 6000 || reward.RewardKind is < 1 or > 3))
                    throw new InvalidDataException("Invalid talent scenery reward intent.");
                if (request.TalentSceneryReward is { Voice: { } sceneryVoice } sceneryReward &&
                    (sceneryVoice.SpeakerId != sceneryReward.ExecutorOfficerId || sceneryVoice.VoiceId != 42 ||
                     sceneryVoice.VoiceType >= 26 || !Enum.IsDefined(sceneryVoice.Language) ||
                     sceneryVoice.VariantIndex is < 0 or > 4 || sceneryVoice.SoundId == 0))
                    throw new InvalidDataException("Invalid scenery discovery voice selection.");
                if (request.TalentForceExperiencePresentation is { } forceExperience &&
                    (batch.Source is not (CampaignEffectSource.TalentCommands or CampaignEffectSource.PersonnelAdministrationCommands or CampaignEffectSource.MonthlyMaintenance) || !Enum.IsDefined(forceExperience.Operation) ||
                     forceExperience.ForceId is < 1 or > 51 || forceExperience.PreviousLevel is < 0 or > 10))
                    throw new InvalidDataException("Invalid talent force experience intent.");
                if (request.ForcePolicyLevelUp is { } policy &&
                    (batch.Source != CampaignEffectSource.ForcePolicyLevelUp || policy.ForceId is < 1 or > 51 ||
                     policy.PreviousLevel is < 0 or > 10 || policy.CurrentLevel is < 2 or > 10 ||
                     !Enum.IsDefined(policy.Stage) || policy.AnnouncementTextAtSource is null || policy.ConfirmationTextAtSource is null))
                    throw new InvalidDataException("Invalid force policy-level scene.");
                if (batch.Source == CampaignEffectSource.ForcePolicyLevelUp && request.ForcePolicyLevelUp is null)
                    throw new InvalidDataException("A force policy-level scene requires its own pending payload.");
                if (request.ArmyTraitViewRefresh is { } traitRefresh &&
                    (batch.Source is not (CampaignEffectSource.ArmyFalseReport or CampaignEffectSource.ArmyTraitMaintenance or CampaignEffectSource.FacilityTraps or CampaignEffectSource.FireDamage or CampaignEffectSource.TerrainDamage or CampaignEffectSource.NormalCombatApplication or CampaignEffectSource.FacilityEffects or CampaignEffectSource.ArmyBattleDefeat) || traitRefresh.ArmyId <= 0))
                    throw new InvalidDataException("Invalid Army trait view refresh request.");
                if (request.ArmyFalseReportVisual is { } falseReportVisual &&
                    (batch.Source != CampaignEffectSource.ArmyFalseReport || !Enum.IsDefined(falseReportVisual.Event) ||
                     falseReportVisual.ReceiverId.Value <= 0 || falseReportVisual.HexId is < 0 or > 48400))
                    throw new InvalidDataException("Invalid false-report visual request.");
                if (request.ArmyTraitFeedback is { } traitFeedback &&
                    (batch.Source is not (CampaignEffectSource.ArmyFalseReport or CampaignEffectSource.ArmyTraitMaintenance or CampaignEffectSource.FacilityTraps or CampaignEffectSource.FireDamage or CampaignEffectSource.TerrainDamage or CampaignEffectSource.NormalCombatApplication or CampaignEffectSource.FacilityEffects or CampaignEffectSource.ArmyBattleDefeat) || traitFeedback.ArmyId <= 0 ||
                     traitFeedback.OfficerId < 0 || traitFeedback.SpeakerHexId is < 0 or > 48400 ||
                     (ArmyTraitFeedbackRequest.HasContext(traitFeedback.TraitId)
                        ? traitFeedback.ContextHexId is null or < 0 or >= 48400
                        : traitFeedback.TraitId is not (20 or 25) || traitFeedback.ContextHexId is not null)))
                    throw new InvalidDataException("Invalid Army trait feedback request.");
                if (request.FacilityConstructionPresentation is { } construction &&
                    (batch.Source != CampaignEffectSource.ArmyFacilityConstruction || !Enum.IsDefined(construction.Operation) ||
                     construction.ReceiverId.Value <= 0 || construction.HexId is < 0 or >= 48400 ||
                     !FacilityConstructionIntent.IsDefinitionKey(construction.DefinitionKey) ||
                     (construction.Operation is FacilityConstructionPresentationOperation.ShowSiteOwnershipChanged or
                        FacilityConstructionPresentationOperation.PlaySiteOwnershipChangedSound
                        ? construction.ForceSlot is null or < 1 or > 51 : construction.ForceSlot is not null)))
                    throw new InvalidDataException("Invalid facility construction presentation request.");
                if (request.Report is { Event: CampaignReportEvent.PlayerFacilityConstructionStarted or
                    CampaignReportEvent.PlayerFacilityRepairStarted } constructionReport &&
                    (batch.Source != CampaignEffectSource.ArmyFacilityConstruction || constructionReport.CommandChoiceId is not null ||
                     constructionReport.TargetKind != CombatParticipantKind.FieldObject || constructionReport.TargetId <= 0 ||
                     constructionReport.HexId is < 0 or >= 48400 || constructionReport.OfficerId < 0))
                    throw new InvalidDataException("Invalid facility construction report request.");
                if (request.Speech is { Event: CombatSpeechEvent.ArmyFacilityConstructionStarted } started &&
                    (batch.Source != CampaignEffectSource.ArmyFacilityConstruction ||
                     started.SpeakerKind != CombatParticipantKind.Army || started.SpeakerId <= 0 ||
                     started.TargetKind != CombatParticipantKind.FieldObject || started.TargetId is null or <= 0 ||
                     started.HexId is < 0 or > 48400))
                    throw new InvalidDataException("Invalid facility construction start speech request.");
                if ((request.FacilityAuraReset is not null || request.MilitaryMusicPulse is not null || request.FacilityProjectile is not null) &&
                    batch.Source != CampaignEffectSource.FacilityEffects)
                    throw new InvalidDataException("Facility presentation requires the facility effects origin.");
                if (request.MilitaryMusicPulse is { } pulse &&
                    (pulse.FacilityReceiverId.Value <= 0 || pulse.ArmyId <= 0 || pulse.HexId is < 0 or >= 48400))
                    throw new InvalidDataException("Invalid military music pulse request.");
                if (request.FacilityProjectile is { } projectile &&
                    (projectile.FacilityReceiverId.Value <= 0 || projectile.TargetReceiverId.Value <= 0))
                    throw new InvalidDataException("Invalid facility projectile request.");
                if (request.FacilityRepairPresentation is { } repair &&
                    (batch.Source != CampaignEffectSource.ArmyFacilityRepair || !Enum.IsDefined(repair.Operation) ||
                     repair.ReceiverId.Value <= 0 || repair.HexId is < 0 or >= 48400 || string.IsNullOrWhiteSpace(repair.DefinitionKey) ||
                     (repair.Operation == FacilityRepairPresentationOperation.FacilityCompletionVisual
                        ? repair.CommanderId is null or < 0 : repair.CommanderId is not null)))
                    throw new InvalidDataException("Invalid facility repair presentation request.");
                if (request.Report is { Event: CampaignReportEvent.PlayerFacilityConstructionCompleted or
                    CampaignReportEvent.PlayerFacilityRepairCompleted } facilityReport &&
                    (batch.Source != CampaignEffectSource.ArmyFacilityRepair || facilityReport.CommandChoiceId is null ||
                     facilityReport.TargetKind != CombatParticipantKind.FieldObject || facilityReport.TargetId <= 0 ||
                     facilityReport.HexId is < 0 or >= 48400 || facilityReport.OfficerId < 0))
                    throw new InvalidDataException("Invalid facility repair report request.");
                // Speech uses the Person's current position;48400 is the
                // original invalid/unplaced value, not a facility Hex.
                if (request.Speech is { Event: CombatSpeechEvent.ArmyFacilityCompleted } facilitySpeech &&
                    (batch.Source != CampaignEffectSource.ArmyFacilityRepair ||
                     facilitySpeech.SpeakerKind != CombatParticipantKind.Army || facilitySpeech.SpeakerId <= 0 ||
                     facilitySpeech.TargetKind != CombatParticipantKind.FieldObject || facilitySpeech.TargetId is null or <= 0 ||
                     facilitySpeech.HexId is < 0 or > 48400))
                    throw new InvalidDataException("Invalid facility repair completion speech request.");
                if (request.Speech is { } speech && (!Enum.IsDefined(speech.SpeakerKind) || !Enum.IsDefined(speech.Event) ||
                    speech.TargetKind is { } kind && !Enum.IsDefined(kind) || speech.SpeakerId < 0 || speech.OfficerId < 0 ||
                    (speech.Event == CombatSpeechEvent.UnresolvedGarrisonContact) != (speech.GarrisonChoice is not null)))
                    throw new InvalidDataException("Invalid campaign speech request.");
                if (request.Speech is { } facilityAttack &&
                    (facilityAttack.Event == CombatSpeechEvent.ArmyUnderFacilityAttack
                        ? batch.Source != CampaignEffectSource.FacilityEffects ||
                          facilityAttack.SpeakerKind != CombatParticipantKind.Army || facilityAttack.SpeakerId <= 0 ||
                          facilityAttack.TargetKind != CombatParticipantKind.FieldObject || facilityAttack.TargetId is null or <= 0 ||
                          facilityAttack.HexId is < 0 or > 48400 || facilityAttack.FacilityDefinitionId != 5
                        : facilityAttack.FacilityDefinitionId is not null))
                    throw new InvalidDataException("Invalid Army facility-attack speech context.");
                if (request.Speech is { } arrivalSpeech &&
                    ((arrivalSpeech.Event == CombatSpeechEvent.ArmyEnteredBase) != (arrivalSpeech.BaseArrival is not null) ||
                     arrivalSpeech.BaseArrival is { } arrival &&
                     (batch.Source != CampaignEffectSource.ArmyMovement || arrival.GoalHexId is < 0 or >= 48400 || arrival.LocationId is < 0 or > 61)))
                    throw new InvalidDataException("Invalid Army Base-arrival speech context.");
                if (request.Report is { } report && (!Enum.IsDefined(report.Event) || !Enum.IsDefined(report.TargetKind) ||
                    report.ArmyName is null || report.TargetName is null || report.AreaId is < 0))
                    throw new InvalidDataException("Invalid campaign report request.");
                if (request.Speech is { } areaSpeech &&
                    (areaSpeech.Event == CombatSpeechEvent.ArmyAreaOccupationCompleted
                        ? batch.Source != CampaignEffectSource.ArmyCommandMaintenance ||
                          areaSpeech.SpeakerKind != CombatParticipantKind.Army || areaSpeech.SpeakerId <= 0 ||
                          areaSpeech.TargetKind != CombatParticipantKind.Area || areaSpeech.TargetId is not (>= 1 and <= 500) ||
                          areaSpeech.HexId is < 0 or >= 48400 || areaSpeech.AfterActionChoice is null
                        : areaSpeech.AfterActionChoice is not null))
                    throw new InvalidDataException("Invalid Army area-completion speech context.");
                if (request.MapObjectPresentation is { } map && (!Enum.IsDefined(map.Event) || map.ReceiverId.Value <= 0 ||
                    map.HexId is < 0 || map.Event != MapObjectPresentationEvent.RetirementVisualDetach &&
                    (map.HexId is null || string.IsNullOrWhiteSpace(map.DefinitionKey))))
                    throw new InvalidDataException("Invalid map object presentation request.");
                if (request.MapObjectPresentation is { Event: MapObjectPresentationEvent.RelationVisibilityAttach or MapObjectPresentationEvent.RelationVisibilityDetach } &&
                    batch.Source != CampaignEffectSource.MonthlyMaintenance)
                    throw new InvalidDataException("Relation visibility requires its normal Turn source.");
                if (request.MapObjectPresentation is { Event: MapObjectPresentationEvent.BaseOwnershipLostVisual or MapObjectPresentationEvent.BaseOwnershipLostSound } &&
                    batch.Source != CampaignEffectSource.ArmyBattleDefeat)
                    throw new InvalidDataException("Base ownership loss requires its capture source.");
                if (batch.Source == CampaignEffectSource.ForceUnregister &&
                    request.MapObjectPresentation is not
                        { Event: MapObjectPresentationEvent.DestructionVisual or MapObjectPresentationEvent.DestructionSound })
                    throw new InvalidDataException("Force unregistration requires an object destruction presentation intent.");
                if (batch.Source == CampaignEffectSource.StrategicCommands && request.MapObjectPresentation is { } strategicObject &&
                    strategicObject.Event is not (MapObjectPresentationEvent.DestructionVisual or MapObjectPresentationEvent.DestructionSound))
                    throw new InvalidDataException("Strategic object presentation must describe actual destruction.");
                if (request.ForceAssociationRemovalReport is { } association &&
                    (association.OldForceId < 0 || association.OfficerId < 0 || association.AssociationSlot < 0 ||
                     association.TargetForceId < 0 || association.DistrictId < 0 || association.LocationId < 0 ||
                     association.HexId < 0 || association.OfficerDisplayName is null || association.TargetForceDisplayName is null))
                    throw new InvalidDataException("Invalid force association removal report.");
            }
        }
    }

    private static void ValidatePersonLifespanNotification(CampaignEffectSource source, CampaignEffectRequest request)
    {
        if (source == CampaignEffectSource.PersonLifespanNotifications && request.PersonLifespanNotification is null)
            throw new InvalidDataException("Lifespan notifications require their own payload.");
        if (request.PersonLifespanNotification is not { } notice) return;
        bool valid = notice.Operation switch
        {
            PersonLifespanNotificationOperation.ControllerQueueEvent => notice.EventKind == 7 &&
                notice.PersonId is null && notice.TextAtSource is null,
            PersonLifespanNotificationOperation.Narration => notice.EventKind is null && notice.PersonId is not null &&
                notice.TextAtSource is { Length: <= 338 } text && !text.Any(c => c == '\0' || char.IsSurrogate(c)),
            _ => false
        };
        if (source != CampaignEffectSource.PersonLifespanNotifications || !valid)
            throw new InvalidDataException("Invalid lifespan notification union.");
    }

    private static void ValidatePersonDeath(CampaignEffectSource source, CampaignEffectRequest request)
    {
        if (source == CampaignEffectSource.PersonDeath && request.PersonDeathReport is null)
            throw new InvalidDataException("Person death source requires its report payload.");
        if (request.PersonDeathReport is not { } report) return;
        bool valid = report.Operation switch
        {
            PersonDeathReportOperation.EventEnter => report.EventKind is 4 or 5 or 6 &&
                report.SpeakerId is null && report.TextAtSource is null,
            PersonDeathReportOperation.SpeakerText => report.EventKind is null && report.SpeakerId is not null &&
                report.TextAtSource is { Length: <= 338 } text && !text.Any(c => c == '\0' || char.IsSurrogate(c)),
            PersonDeathReportOperation.EventExit => report.EventKind is null &&
                report.SpeakerId is null && report.TextAtSource is null,
            PersonDeathReportOperation.ControllerQueueEvent => report.EventKind == 22 &&
                report.SpeakerId is null && report.TextAtSource is null,
            PersonDeathReportOperation.NarrationText => report.EventKind is null && report.SpeakerId is null &&
                report.TextAtSource is { Length: <= 338 } narration && !narration.Any(c => c == '\0' || char.IsSurrogate(c)),
            _ => false
        };
        if (source != CampaignEffectSource.PersonDeath || !valid)
            throw new InvalidDataException("Invalid person death report union.");
    }

    private static void ValidatePersonAppearance(CampaignEffectSource source, CampaignEffectRequest request)
    {
        if (source == CampaignEffectSource.PersonAppearance && request.PersonAppearance is null)
            throw new InvalidDataException("Appearance source requires its own payload.");
        if (request.PersonAppearance is not { } appearance) return;
        bool valid = appearance.Operation switch
        {
            PersonAppearanceOperation.SoundCue => appearance.SoundId is 5 or 8 &&
                appearance.EventKind is null && appearance.Announcement is null && appearance.Speaker is null && appearance.History is null,
            PersonAppearanceOperation.Announcement => Text(appearance.Announcement) &&
                appearance.SoundId is null && appearance.EventKind is null && appearance.Speaker is null && appearance.History is null,
            PersonAppearanceOperation.EventEnter => appearance.EventKind is 7 or 8 &&
                appearance.SoundId is null && appearance.Announcement is null && appearance.Speaker is null && appearance.History is null,
            PersonAppearanceOperation.SpeakerText => appearance.Speaker is { } speaker && Text(speaker.Text) &&
                appearance.SoundId is null && appearance.EventKind is null && appearance.Announcement is null && appearance.History is null,
            PersonAppearanceOperation.EventExit => appearance.SoundId is null && appearance.EventKind is null &&
                appearance.Announcement is null && appearance.Speaker is null && appearance.History is null,
            PersonAppearanceOperation.History => appearance.History is { } history && Text(history.Text) &&
                history.HexArgument is >= 0 and < 48400 && appearance.SoundId is null && appearance.EventKind is null &&
                appearance.Announcement is null && appearance.Speaker is null,
            _ => false
        };
        if (source != CampaignEffectSource.PersonAppearance || !valid)
            throw new InvalidDataException("Invalid person appearance request union.");
        static bool Text(string? text) => text is not null && text.Length <= 338 &&
            !text.Any(c => c == '\0' || char.IsSurrogate(c));
    }

    private static void ValidateOfficerGrowth(CampaignEffectSource source, CampaignEffectRequest request)
    {
        if (source == CampaignEffectSource.OfficerGrowthNotifications && request.OfficerGrowthFeedback is null)
            throw new InvalidDataException("Growth notifications require their own semantic payload.");
        if (request.OfficerGrowthFeedback is not { } growth) return;
        bool valid = growth.Operation switch
        {
            OfficerGrowthFeedbackOperation.SoundCue => growth.Narration is null && growth.History is null,
            OfficerGrowthFeedbackOperation.Narration => growth.History is null && growth.Narration is { } text &&
                text.OfficerId is >= 1 and <= 6000 && GrowthText(text.Text),
            OfficerGrowthFeedbackOperation.History => growth.Narration is null && growth.History is { } history &&
                history.OfficerId is >= 1 and <= 6000 && history.RawDistrictArgument is >= 0 and <= 255 &&
                history.HexArgument is >= 0 and < 48400 && GrowthText(history.Text),
            _ => false
        };
        if (source != CampaignEffectSource.OfficerGrowthNotifications || !valid)
            throw new InvalidDataException("Invalid officer growth feedback union.");
        //This batch's proved success profile is a complete BMP short string;
        //unsupported workspace/Unicode cases are not stored as truncations.
        static bool GrowthText(string? value) => value is not null && value.Length <= 338 &&
            !value.Any(c => c == '\0' || char.IsSurrogate(c));
    }

    private static void ValidateTerrainDamage(CampaignEffectSource source, CampaignEffectRequest request)
    {
        if (source == CampaignEffectSource.TerrainDamage && request.TerrainDamagePresentation is null &&
            request.ArmyTraitViewRefresh is null && request.ArmyTraitFeedback is null && request.ArmyMoraleDefeatReport is null)
            throw new InvalidDataException("Terrain damage has no supported semantic request.");
        if (request.TerrainDamagePresentation is not { } terrain) return;
        bool valid = terrain.ArmyId > 0 && (terrain.Operation switch
        {
            TerrainDamagePresentationOperation.ArmyTargetViewRefresh => terrain.RequestedAmount is null,
            TerrainDamagePresentationOperation.ArmyGrossDamage => terrain.RequestedAmount is not null,
            _ => false
        });
        if (source != CampaignEffectSource.TerrainDamage || !valid)
            throw new InvalidDataException("Invalid terrain damage presentation request.");
    }

    private static void ValidateFireDamage(CampaignEffectSource source, CampaignEffectRequest request)
    {
        if (source == CampaignEffectSource.FireDamage && request.FireDamagePresentation is null &&
            request.ArmyTraitViewRefresh is null && request.ArmyTraitFeedback is null &&
            request.ArmyMoraleDefeatReport is null && request.MapObjectPresentation is null)
            throw new InvalidDataException("Invalid fire damage shared semantic payload.");
        if (request.FireDamagePresentation is not { } fire) return;
        bool valid = fire.Operation switch
        {
            FireDamagePresentationOperation.ArmyTargetViewRefresh =>
                fire.ArmyId is > 0 && fire.BaseTarget is null && fire.GrossAmount is null,
            FireDamagePresentationOperation.ArmyGrossDamage =>
                fire.ArmyId is > 0 && fire.BaseTarget is null && fire.GrossAmount is not null,
            FireDamagePresentationOperation.BaseGrossDamage => fire.ArmyId is null && fire.GrossAmount is not null &&
                fire.BaseTarget is { } target && (target.Kind == SettlementBaseKind.City
                    ? target.Id is >= 1 and <= 51 : target.Kind == SettlementBaseKind.Gate && target.Id is >= 52 and <= 61),
            _ => false
        };
        if (source != CampaignEffectSource.FireDamage || !valid)
            throw new InvalidDataException("Invalid fire damage presentation request.");
    }

    private static void ValidateTrap(CampaignEffectSource source, CampaignEffectRequest request)
    {
        bool trap = source == CampaignEffectSource.FacilityTraps;
        bool facilityEffects = source == CampaignEffectSource.FacilityEffects;
        if (request.FacilityTrapPresentation is { } visual &&
            (!trap || !Enum.IsDefined(visual.Operation) || visual.ReceiverId.Value <= 0 ||
             visual.DefinitionId is < 0 or > 30 || visual.HexId is < 0 or >= 48400 ||
             (visual.Operation == FacilityTrapPresentationOperation.FireRangeVisual
                ? visual.Range is not float range || !float.IsFinite(range) : visual.Range is not null)))
            throw new InvalidDataException("Invalid trap presentation request.");
        if (request.ArmyTrapPresentation is { } target &&
            (!(trap || facilityEffects && target.Operation == ArmyTrapPresentationOperation.TargetViewRefresh &&
                target.DefinitionId == 5) || !Enum.IsDefined(target.Operation) || target.ArmyId <= 0 || target.ReceiverId.Value <= 0 ||
             target.DefinitionId is < 0 or > 30 || target.HexId is < 0 or >= 48400))
            throw new InvalidDataException("Invalid trap target presentation request.");
        if (request.ArmyFacilityDamagePresentation is { } damage &&
            (!(trap || facilityEffects) || !Enum.IsDefined(damage.Operation) || damage.ArmyId <= 0 || damage.HexId is < 0 or >= 48400 ||
             (damage.Operation == ArmyFacilityDamagePresentationOperation.ConditionalFloatingNumber
                ? damage.DisplayModifier is null : damage.DisplayModifier is not null)))
            throw new InvalidDataException("Invalid facility damage presentation request.");
        if (request.ArmyMoraleDefeatReport is { } defeat &&
            (source is not (CampaignEffectSource.ArmyFalseReport or CampaignEffectSource.FacilityTraps or CampaignEffectSource.ArmyBattleDefeat or CampaignEffectSource.FireDamage or CampaignEffectSource.TerrainDamage or CampaignEffectSource.FacilityEffects) ||
             defeat.ArmyId <= 0 || defeat.ArmyNameSnapshot is null || defeat.CampaignDay < 0))
            throw new InvalidDataException("Invalid Army morale defeat report request.");
        if (request.Speech is { Event: >= CombatSpeechEvent.TrapPitfall and <= CombatSpeechEvent.ImmobilizationApplied } speech &&
            (!trap || speech.SpeakerKind != CombatParticipantKind.Army || speech.SpeakerId <= 0 ||
             speech.TargetKind != CombatParticipantKind.FieldObject || speech.TargetId is null or <= 0 ||
             speech.HexId is < 0 or > 48400))
            throw new InvalidDataException("Invalid trap speech request.");
        if (request.Speech is { } definitionSpeech &&
            (definitionSpeech.Event == CombatSpeechEvent.TrapCountered
                ? definitionSpeech.TrapDefinitionId is null or < 0 or > 30
                : definitionSpeech.TrapDefinitionId is not null))
            throw new InvalidDataException("Invalid trap countermeasure definition argument.");
        if (request.FacilityTrapReport is not { } report) return;
        if (!trap || !Enum.IsDefined(report.Operation) || report.ReceiverId.Value <= 0 ||
            report.DefinitionId is < 0 or > 30 || report.HexId is < 0 or >= 48400 ||
            report.CampaignDay < 0 || report.DistrictId is < 0 or > 255 || report.TrapName is null)
            throw new InvalidDataException("Invalid trap report request.");
        bool army = report.ArmyId is > 0 && report.OfficerId is >= 0 && report.ArmyName is not null;
        bool noArmy = report.ArmyId is null && report.OfficerId is null && report.ArmyName is null;
        bool trait = report.TraitId is >= 0 and < 356 && report.TraitName is not null;
        bool noTrait = report.TraitId is null && report.TraitName is null;
        bool valid = report.Operation switch
        {
            FacilityTrapReportOperation.Triggered => noArmy && noTrait && report.ForceId is >= 0 and <= 51 &&
                report.CounterForceName is null && report.AbnormalIndex is null && report.PositiveContribution is null,
            FacilityTrapReportOperation.Countered => army && trait && report.ForceId is null &&
                report.CounterForceName is not null && report.AbnormalIndex is null && report.PositiveContribution is null,
            FacilityTrapReportOperation.AbnormalApplied => army && noTrait && report.ForceId is null &&
                report.CounterForceName is null && report.AbnormalIndex is 0 or 1 && report.PositiveContribution is null,
            FacilityTrapReportOperation.AbnormalAttribution => army && trait && report.ForceId is null &&
                report.CounterForceName is null && report.AbnormalIndex is 0 or 1 && report.PositiveContribution is not null,
            FacilityTrapReportOperation.AbnormalImmunity => army && trait && report.ForceId is null &&
                report.CounterForceName is null && report.AbnormalIndex is 0 or 1 && report.PositiveContribution is null,
            _ => false
        };
        if (!valid) throw new InvalidDataException("Invalid trap report union fields.");
        if (report.Operation == FacilityTrapReportOperation.Triggered
            ? report.AreaArgument0 is null || report.AreaNameAtSource is null
            : report.AreaArgument0 is not null || report.AreaNameAtSource is not null)
            throw new InvalidDataException("Invalid triggered trap Area operand.");
        report.CounterForceName?.Validate();
    }
}
public sealed partial record GameState
{
    public CampaignEffectRequests? CampaignEffectRequests { get; init; }
}
