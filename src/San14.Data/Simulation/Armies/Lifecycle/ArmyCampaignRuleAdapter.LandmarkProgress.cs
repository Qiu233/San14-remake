using San14.Core;
namespace San14.Data;

internal sealed record LandmarkProgressStep(GameState State, LandmarkStoryCheckpoint? Checkpoint,
    CampaignStoryRequest? Story, CampaignServiceChanges ServiceChanges, long LastSequence);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal void RequireStorySource(CampaignStoryRequest story)
    {
        var source = _campaignEvents.Value.Source;
        if (story.Source.ResourceUri != source.ResourceUri || story.Source.ResourceSha256 != source.Sha256)
            throw OccupationMissing("story_source_revision_changed");
    }
    internal LandmarkProgressStep ContinueLandmarkProgress(GameState state, NativeArmyProgressStageMoment moment,
        CampaignStoryBindings bindings, CampaignServiceChanges changes, long nextSequence,
        LandmarkStoryCheckpoint? resume = null)
    {
        if (moment.SchedulerStageIndex != 2 || moment.Boundary != NativeArmyProgressStageBoundary.AfterArmyWaiting ||
            !NativeArmyProgressAtomicRuntime.IsArmyWaitingDue(moment.PreWriterThirdDate))
            throw new InvalidDataException("Story continuation requires the true post-waiting stage2 boundary.");
        var registry = resume?.Registry ?? (state.ReadArmyLifecycle()?.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Landmark)
            ?? throw OccupationMissing("landmark_registry")).Select(entry =>
                entry.Kind == WorldEntityKind.Landmark && entry.Id is int id ? id : throw OccupationMissing("landmark_registry_identity")).ToArray();
        var working = state;
        for (int index = resume?.NextIndex ?? 0; index < registry.Length; index++)
        {
            int id = registry[index];
            var current = LandmarkCurrent(working);
            var landmark = current.ReadSceneryTableReference(id) ?? throw OccupationMissing("landmark_pointer");
            if (!OccupationKnown(current.ReadSceneryNativeValidity(landmark), "landmark_validity") ||
                (OccupationKnown(current.ReadSceneryFlags47(landmark), "landmark_flags") & 1) == 0) continue;
            var investment = working.LandmarkInvestments?.GetValueOrDefault(id) ?? throw OccupationMissing("landmark_investment");
            if (investment.Phase is not (2 or 3) || investment.CompletionDay <= 0 || investment.AssignedOfficerId == 0) continue;
            var date = moment.PreWriterThirdDate;
            int day = date.Day - 31 + (date.Month + date.Year * 12) * 30;
            if (day < investment.CompletionDay)
            {
                if (investment.Phase == 2) working = working with { LandmarkInvestments = new(working.LandmarkInvestments!) { [id] = investment with { Phase = 3 } } };
                continue;
            }
            var development = _landmarkDevelopment.Advance(working, investment.Development ?? throw OccupationMissing("landmark_saved_development"));
            working = working with { LandmarkInvestments = new(working.LandmarkInvestments!) { [id] = investment with
                { Phase = development.Level < 6 ? (byte)1 : (byte)4, AssignedOfficerId = 0, CompletionDay = 0, Development = development } } };
            working = AddLandmarkCompletionReport(working, current, landmark, development.Level, moment);
            if (development.Level != 6) continue;
            current = LandmarkCurrent(working);
            int owner = LandmarkOwner(current, landmark);
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current) ?? throw OccupationMissing("landmark_maximum_player");
            if (!OccupationKnown(current.ReadForceNativeValidity(player), "landmark_maximum_player_validity") ||
                OccupationKnown(current.ReadForcePhysicalSlot(player), "landmark_maximum_player_identity") != owner)
            { working = CompleteMaximumLandmark(working, current, landmark, id, moment); continue; }
            var mode = bindings.ExternalMode ?? throw OccupationMissing("story_external_mode_pointer");
            bool enterStoryCaller = !mode.Present || OccupationKnown(mode.Value, "story_external_mode_value") == 0;
            var originalCamera = enterStoryCaller ? bindings.CaptureCamera() : null;
            var prepared = enterStoryCaller ? PrepareMaximumLandmarkStory(working, id, bindings.Audio, bindings.SkipCamera,
                bindings.ExternalMode, bindings.ApplicationPresent, bindings.ApplicationUserIndex, moment) : new(working, null, null, null);
            working = prepared.ConditionState;
            CampaignStoryRequest? story = prepared.Story is null ? null : CampaignStorySemantics.Bind(prepared.Story, nextSequence);
            int eventSteps = story?.Steps.Length ?? 0;
            var notice = MaximumLandmarkNotice(working, id, owner);
            var steps = story?.Steps.ToList() ?? new List<CampaignStoryStep>();
            if (originalCamera is not null) steps.Add(new StoryRestoreCampaignCamera(originalCamera));
            if (notice is not null) steps.Add(notice);
            if (steps.Count == 0)
            { working = FinishPlayerLandmark(working, id, bindings, ref changes, moment); continue; }
            if (story is null)
            {
                var source = _campaignEvents.Value;
                story = new(nextSequence, new(source.Source.ResourceUri, source.Source.Sha256, id+338-source.Document.NativeBaseId, id+338),
                    "landmark-completion-notice:" + source.Source.Sha256, steps.ToArray(), 0);
            }
            else story = story with { Steps = steps.ToArray() };
            var occurrenceDate = prepared.Continuation?.Date ?? new CampaignEventOccurrenceDate(
                (ushort)Math.Clamp((int)date.Year, 0, 350), (byte)Math.Clamp((int)date.Month, 1, 12), (byte)Math.Clamp((int)date.Day, 1, 30));
            return new(working, new(registry, index+1, id, id+338, occurrenceDate, eventSteps,
                eventSteps != 0 ? LandmarkStoryPhase.Event : originalCamera is not null ? LandmarkStoryPhase.CameraReturn : LandmarkStoryPhase.RewardNotice), story, changes, nextSequence);
        }
        return new(working, null, null, changes, nextSequence-1);
    }
    internal LandmarkProgressStep ResumeLandmarkProgress(CampaignProgressOperation pending, CampaignStoryBindings bindings)
    {
        var checkpoint = pending.Landmark; var story = pending.Story;
        var working = pending.Candidate; var changes = pending.ServiceChanges;
        if (checkpoint.Phase == LandmarkStoryPhase.Event && story.NextStep == checkpoint.EventStepCount)
        {
            var history = working.CampaignEvents ?? throw OccupationMissing("story_event_history");
            var occurred = history.Occurred.ToArray(); var dates = history.OccurrenceDates.ToArray();
            occurred[checkpoint.EventId] = true; dates[checkpoint.EventId] = checkpoint.Date;
            working = working with { CampaignEvents = history with { Occurred = occurred, OccurrenceDates = dates } };
            changes = PrepareStoryService(working, bindings, changes, checkpoint.EventId, pending.Context.Moment, CampaignEventServicePart.Event);
            checkpoint = checkpoint with { Phase = LandmarkStoryPhase.CameraReturn };
        }
        if (checkpoint.Phase == LandmarkStoryPhase.CameraReturn && story.NextStep > checkpoint.EventStepCount)
            checkpoint = checkpoint with { Phase = LandmarkStoryPhase.RewardNotice };
        if (story.NextStep < story.Steps.Length)
            return new(working, checkpoint, story, changes, pending.LastStorySequence);
        // Return the acknowledged presentation before service/reward can fail.
        return new(working, checkpoint, null, changes, pending.LastStorySequence);
    }
    internal CampaignServiceChanges PrepareCurrentLandmarkService(CampaignProgressOperation pending, CampaignStoryBindings bindings) =>
        PrepareStoryService(pending.Candidate, bindings, pending.ServiceChanges, null, pending.Context.Moment, CampaignEventServicePart.Landmark);
    internal GameState ApplyCurrentLandmarkRewards(CampaignProgressOperation pending)
    {
        var current = LandmarkCurrent(pending.Candidate); int id = pending.Landmark.CurrentLandmarkId;
        var landmark = current.ReadSceneryTableReference(id) ?? throw OccupationMissing("landmark_pointer");
        return ApplyMaximumLandmarkRewards(pending.Candidate, current, landmark, id, LandmarkOwner(current, landmark));
    }
    private CampaignServiceChanges PrepareStoryService(GameState state, CampaignStoryBindings bindings, CampaignServiceChanges changes,
        int? eventId, NativeArmyProgressStageMoment moment, CampaignEventServicePart part)
    {
        var prepared = PrepareMaximumLandmarkService(state, bindings.Materialize(changes), eventId,
            bindings.ApplicationPresent, bindings.ApplicationUserIndex, bindings.ExternalMode, moment, part);
        return prepared.Service is { } service ? bindings.Capture(service) : throw OccupationMissing(prepared.UnresolvedInput ?? "story_service");
    }
    private GameState FinishPlayerLandmark(GameState state, int id, CampaignStoryBindings bindings,
        ref CampaignServiceChanges changes, NativeArmyProgressStageMoment moment)
    {
        changes = PrepareStoryService(state, bindings, changes, null, moment, CampaignEventServicePart.Landmark);
        var current = LandmarkCurrent(state); var landmark = current.ReadSceneryTableReference(id) ?? throw OccupationMissing("landmark_pointer");
        return ApplyMaximumLandmarkRewards(state, current, landmark, id, LandmarkOwner(current, landmark));
    }
    private StoryLandmarkRewardNotice? MaximumLandmarkNotice(GameState state, int id, int force)
    {
        byte kind = state.LandmarkInvestments![id].Development!.EffectKind;
        string? key = kind switch { 1 => SimulationParameterKeys.Landmark.CompletionSecurity,
            2 => SimulationParameterKeys.Landmark.CompletionDurability, 3 => SimulationParameterKeys.Landmark.CompletionMorale, _ => null };
        return key is null ? null : new(force, kind, CounterParameter(state, key), 0x3499 + kind);
    }
    private SourceWorldContext LandmarkCurrent(GameState state)
    {
        var current = CaptureWritableTransportRouteCurrent(state);
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current); _personAssignmentPrecallInputs.BindCurrent(state, current);
        current.MaterializeFixedScenery(_landmarkDefinitions); return current;
    }
    private static int LandmarkOwner(SourceWorldContext current, SceneryReference landmark)
    {
        int raw = OccupationKnown(current.ReadSceneryHexIndex(landmark), "landmark_maximum_hex");
        var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw OccupationMissing("landmark_maximum_hex_pointer");
        return hex.PhysicalSlot is null ? 0 : OccupationKnown(current.ReadHexForceSlot(hex), "landmark_maximum_owner");
    }
}
