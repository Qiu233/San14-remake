namespace San14.Core;

public enum CampaignEffectSource { NormalCombatRefresh, ArmyFalseReport, ArmyProgressNotifications, ArmyCommandMaintenance,
    ArmyDemolition, MapObjectRetirement, ForceAssociationRemoval, FacilityEffects, ArmyFacilityRepair, ArmyFacilityConstruction,
    ArmyTraitMaintenance, FacilityTraps, ArmyBattleDefeat, FireDamage, TerrainDamage, OfficerGrowthNotifications, PersonAppearance, PersonDeath,
    LandmarkInvestment }
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
public enum MapObjectPresentationEvent { DestructionVisual, DestructionSound, RetirementVisualDetach }
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

public sealed record CampaignEffectRequest(CombatSpeechRequest? Speech, CampaignReportRequest? Report)
{
    public CampaignHistoryNotification? HistoryNotification { get; init; }
    public PersonDeathReportRequest? PersonDeathReport { get; init; }
    public PersonAppearanceRequest? PersonAppearance { get; init; }
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
                if (request is null || (request.Speech is null ? 0 : 1) + (request.Report is null ? 0 : 1) +
                    (request.MapObjectPresentation is null ? 0 : 1) + (request.ForceAssociationRemovalReport is null ? 0 : 1) +
                    (request.FacilityAuraReset is null ? 0 : 1) + (request.MilitaryMusicPulse is null ? 0 : 1) +
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
                    (request.PersonDeathReport is null ? 0 : 1) +
                    (request.HistoryNotification is null ? 0 : 1) != 1)
                    throw new InvalidDataException("A campaign effect requires one semantic payload.");
                if (request.HistoryNotification is { } notification)
                {
                    if (batch.Source is not (CampaignEffectSource.NormalCombatRefresh or
                        CampaignEffectSource.ArmyFalseReport or CampaignEffectSource.ArmyProgressNotifications or
                        CampaignEffectSource.ArmyCommandMaintenance or CampaignEffectSource.ArmyDemolition or
                        CampaignEffectSource.ForceAssociationRemoval or CampaignEffectSource.ArmyFacilityRepair or
                        CampaignEffectSource.ArmyFacilityConstruction or CampaignEffectSource.FacilityTraps or
                        CampaignEffectSource.ArmyBattleDefeat or CampaignEffectSource.FireDamage or
                        CampaignEffectSource.OfficerGrowthNotifications or CampaignEffectSource.PersonAppearance or
                        CampaignEffectSource.PersonDeath or CampaignEffectSource.LandmarkInvestment))
                        throw new InvalidDataException("Invalid campaign history notification origin.");
                    CampaignHistoryValidation.ValidateNotification(notification);
                    continue;
                }
                if (batch.Source == CampaignEffectSource.LandmarkInvestment)
                    throw new InvalidDataException("A landmark history batch requires a post-write notification.");
                ValidateTrap(batch.Source, request);
                ValidateFireDamage(batch.Source, request);
                ValidateTerrainDamage(batch.Source, request);
                ValidateOfficerGrowth(batch.Source, request);
                ValidatePersonAppearance(batch.Source, request);
                ValidatePersonDeath(batch.Source, request);
                if (request.ArmyTraitViewRefresh is { } traitRefresh &&
                    (batch.Source is not (CampaignEffectSource.ArmyTraitMaintenance or CampaignEffectSource.FacilityTraps or CampaignEffectSource.FireDamage or CampaignEffectSource.TerrainDamage) || traitRefresh.ArmyId <= 0))
                    throw new InvalidDataException("Invalid Army trait view refresh request.");
                if (request.ArmyTraitFeedback is { } traitFeedback &&
                    (batch.Source is not (CampaignEffectSource.ArmyTraitMaintenance or CampaignEffectSource.FacilityTraps or CampaignEffectSource.FireDamage or CampaignEffectSource.TerrainDamage) || traitFeedback.ArmyId <= 0 ||
                     traitFeedback.OfficerId < 0 || traitFeedback.SpeakerHexId is < 0 or > 48400 ||
                     (ArmyTraitFeedbackRequest.HasContext(traitFeedback.TraitId)
                        ? traitFeedback.ContextHexId is null or < 0 or >= 48400
                        : traitFeedback.TraitId != 20 || traitFeedback.ContextHexId is not null)))
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
                if ((request.FacilityAuraReset is not null || request.MilitaryMusicPulse is not null) &&
                    batch.Source != CampaignEffectSource.FacilityEffects)
                    throw new InvalidDataException("Facility presentation requires the facility effects origin.");
                if (request.MilitaryMusicPulse is { } pulse &&
                    (pulse.FacilityReceiverId.Value <= 0 || pulse.ArmyId <= 0 || pulse.HexId is < 0 or >= 48400))
                    throw new InvalidDataException("Invalid military music pulse request.");
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
                if (request.Report is { } report && (!Enum.IsDefined(report.Event) || !Enum.IsDefined(report.TargetKind) ||
                    report.ArmyName is null || report.TargetName is null || report.AreaId is < 0))
                    throw new InvalidDataException("Invalid campaign report request.");
                if (request.MapObjectPresentation is { } map && (!Enum.IsDefined(map.Event) || map.ReceiverId.Value <= 0 ||
                    map.HexId is < 0 || map.Event != MapObjectPresentationEvent.RetirementVisualDetach &&
                    (map.HexId is null || string.IsNullOrWhiteSpace(map.DefinitionKey))))
                    throw new InvalidDataException("Invalid map object presentation request.");
                if (request.ForceAssociationRemovalReport is { } association &&
                    (association.OldForceId < 0 || association.OfficerId < 0 || association.AssociationSlot < 0 ||
                     association.TargetForceId < 0 || association.DistrictId < 0 || association.LocationId < 0 ||
                     association.HexId < 0 || association.OfficerDisplayName is null || association.TargetForceDisplayName is null))
                    throw new InvalidDataException("Invalid force association removal report.");
            }
        }
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
        if (request.FacilityTrapPresentation is { } visual &&
            (!trap || !Enum.IsDefined(visual.Operation) || visual.ReceiverId.Value <= 0 ||
             visual.DefinitionId is < 0 or > 30 || visual.HexId is < 0 or >= 48400 ||
             (visual.Operation == FacilityTrapPresentationOperation.FireRangeVisual
                ? visual.Range is not float range || !float.IsFinite(range) : visual.Range is not null)))
            throw new InvalidDataException("Invalid trap presentation request.");
        if (request.ArmyTrapPresentation is { } target &&
            (!trap || !Enum.IsDefined(target.Operation) || target.ArmyId <= 0 || target.ReceiverId.Value <= 0 ||
             target.DefinitionId is < 0 or > 30 || target.HexId is < 0 or >= 48400))
            throw new InvalidDataException("Invalid trap target presentation request.");
        if (request.ArmyFacilityDamagePresentation is { } damage &&
            (!trap || !Enum.IsDefined(damage.Operation) || damage.ArmyId <= 0 || damage.HexId is < 0 or >= 48400 ||
             (damage.Operation == ArmyFacilityDamagePresentationOperation.ConditionalFloatingNumber
                ? damage.DisplayModifier is null : damage.DisplayModifier is not null)))
            throw new InvalidDataException("Invalid facility damage presentation request.");
        if (request.ArmyMoraleDefeatReport is { } defeat &&
            (source is not (CampaignEffectSource.FacilityTraps or CampaignEffectSource.ArmyBattleDefeat or CampaignEffectSource.FireDamage or CampaignEffectSource.TerrainDamage) ||
             defeat.ArmyId <= 0 || defeat.ArmyNameSnapshot is null || defeat.CampaignDay < 0))
            throw new InvalidDataException("Invalid Army morale defeat report request.");
        if (request.Speech is { Event: >= CombatSpeechEvent.TrapPitfall } speech &&
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
