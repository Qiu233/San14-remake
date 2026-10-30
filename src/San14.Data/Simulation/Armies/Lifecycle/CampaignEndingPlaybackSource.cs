using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>Actual application inputs captured before an Ending operation.
/// Movie permission is the profile playback setting, never file existence.</summary>
public sealed record CampaignEndingBindings(CampaignStoryBindings Story,
    bool ParentStatePresent, bool? MoviePlaybackEnabled, bool? OfficerModalAllowed = null);

public sealed partial class ArmyCampaignRuleAdapter : ICampaignEndingRules
{
    private CampaignEndingBindings? endingBindings;

    public void ConfigureCampaignEndingBindings(CampaignEndingBindings? bindings) => endingBindings = bindings;

    public CampaignEndingPreparationResult PrepareCampaignEnding(GameState candidate, bool fromEvent900 = false)
    {
        try
        {
            if (candidate.EndingProgress is null && !fromEvent900 &&
                candidate.CampaignTurnStop?.Reason != CampaignTurnStopReason.OrdinaryForcesResolved)
                return new(candidate);
            if (candidate.EndingProgress is null && fromEvent900 &&
                (candidate.NativeNormalTurnContinuation?.StoryPlayback?.Story is not { Source.EventId: 900 } event900 ||
                 event900.NextStep != event900.Steps.Length))
                throw EndingPlaybackMissing("event900_completed_source_owner");
            var current = CaptureOwnedArmyReturnWorld(candidate).Context;
            if (candidate.EndingProgress is null)
            {
                var bindings = endingBindings ?? throw EndingPlaybackMissing("host_bindings");
                var admission = ReadCampaignEndingAdmissionCurrent(candidate, current,
                    fromEvent900, bindings.ParentStatePresent);
                if (admission is null)
                    return new(candidate.CampaignTurnStop is { } stop ? candidate with
                        { CampaignTurnStop = stop with { EndingRequested = false } } : candidate);
                candidate = candidate with { EndingProgress = new(admission.FromEvent900,
                    admission.FromEvent900 ? 13 : 0, 0, 1) };
                if (!fromEvent900 && candidate.CampaignTurnStop is { } admittedStop)
                    candidate = candidate with { CampaignTurnStop = admittedStop with { EndingRequested = true } };
            }
            return new(AdvanceCampaignEndingCurrent(candidate, current));
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    public CampaignEndingPreparationResult CompleteCampaignEndingPresentation(GameState candidate,
        CampaignEndingProgress progress, long sequence, bool? abdicationAccepted, int? kingdomId)
    {
        try
        {
            if (!ReferenceEquals(candidate.EndingProgress, progress) ||
                progress.Pending is not { } pending || pending.Sequence != sequence)
                throw EndingPlaybackMissing("presentation_owner");
            var next = progress with { Pending = null };
            //The application may already have detached its parent after
            //accepting Exit. Completing this small owner reads no world or
            //application service and does not recreate a retired context.
            if (pending.Kind == CampaignEndingPresentationKind.Exit)
                return new(candidate with { EndingProgress = next with { Completed = true } });
            var current = CaptureOwnedArmyReturnWorld(candidate).Context;
            switch (pending.Kind)
            {
                case CampaignEndingPresentationKind.Opening:
                    next = next with { Stage = EndingObserverCurrent(current) ? 15 : 1, Substep = 0 };
                    break;
                case CampaignEndingPresentationKind.AbdicationChoice:
                    next = next with { AbdicationAccepted = abdicationAccepted
                        ?? throw EndingPlaybackMissing("abdication_answer"), Substep = 2 };
                    break;
                case CampaignEndingPresentationKind.KingdomNameChoice:
                    //243410's explicit cancel leaves its output list empty.
                    //28B610 then writes no name; its Ending caller still runs
                    //43EC30. Only Core's separate cancel entry admits null.
                    if (kingdomId is int selected)
                    {
                        if (!pending.KingdomChoices.Any(choice => choice.Id == selected))
                            throw EndingPlaybackMissing("kingdom_candidate");
                        candidate = ApplyEndingKingdomNameCurrent(candidate, current, selected);
                    }
                    next = next with { ChosenKingdomId = kingdomId, Substep = next.Substep + 1 };
                    break;
                case CampaignEndingPresentationKind.OfficerModal:
                    next = next with { Stage = 5, Substep = 0 };
                    break;
                case CampaignEndingPresentationKind.Movie:
                    next = next with { Stage = 14, Substep = 0 };
                    break;
                case CampaignEndingPresentationKind.CompletionNotices:
                    next = next with { Stage = 15, Substep = 0 };
                    break;
                default: throw EndingPlaybackMissing("presentation_kind");
            }
            return new(AdvanceCampaignEndingCurrent(candidate with { EndingProgress = next }, current));
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    public CampaignEndingPreparationResult CompleteCampaignEndingStory(GameState candidate,
        CampaignEndingProgress progress)
    {
        try
        {
            if (!ReferenceEquals(candidate.EndingProgress, progress) || progress.Story is not { } story ||
                story.NextStep != story.Steps.Length)
                throw EndingPlaybackMissing("story_actual_completion");
            var current = CaptureOwnedArmyReturnWorld(candidate).Context;
            candidate = PrepareApplicationServiceCurrent(candidate, current, profile =>
            {
                var application = ReadApplicationGameplay() ?? throw EndingPlaybackMissing("story_application");
                var prepared = CampaignEventService.PrepareEventCompletion(new(current, current,
                    id => ReadCampaignServiceRecordKind(_campaignEvents.Value.Document, id),
                    application.UserSelectionServicePresent, candidate.CampaignEventWorld?.CurrentUserIndex),
                    profile, story.Source.EventId);
                return prepared.Service ?? throw EndingPlaybackMissing(prepared.UnresolvedInput ?? "story_profile");
            });
            var next = progress with { Story = null, Substep = progress.Substep + 1 };
            return new(AdvanceCampaignEndingCurrent(candidate with { EndingProgress = next }, current));
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private GameState AdvanceCampaignEndingCurrent(GameState state, SourceWorldContext current)
    {
        while (true)
        {
            var progress = state.EndingProgress ?? throw EndingPlaybackMissing("progress");
            if (progress.Completed || progress.Pending is not null || progress.Story is not null) return state;
            switch (progress.Stage)
            {
                case 0:
                    return PrepareEndingOpeningCurrent(state, current, progress);
                case 1:
                    if (progress.Substep == 0) return EndingStory(state, current, progress, 950);
                    state = SetStage(state, ReadEndingImperialStageCurrent(state, current) ? 5 : 2);
                    break;
                case 2:
                    state = SetStage(state, ReadEndingEmperorStageCurrent(state, current) ? 3 : 4);
                    break;
                case 3:
                    switch (progress.Substep)
                    {
                        case 0: return EndingStory(state, current, progress, 951);
                        case 1:
                            var question = FormatNormalTurnReport(state, current, 0x7CD5, new());
                            return SetPending(question.State, progress,
                                new(progress.NextSequence, CampaignEndingPresentationKind.AbdicationChoice, question.Text));
                        case 2:
                            return EndingStory(state, current, progress,
                                progress.AbdicationAccepted is true ? 960 : progress.AbdicationAccepted is false ? 961 :
                                    throw EndingPlaybackMissing("abdication_choice"));
                        case 3:
                            if (progress.AbdicationAccepted is false)
                            {
                                state = SetStage(state with { EndingProgress = progress with { DeclinedAbdication = true } }, 5);
                                break;
                            }
                            state = PrepareEndingKingdomChoiceCurrent(state, current, progress);
                            break;
                        case 4:
                            state = PrepareEndingKingdomModalCurrent(state, current, progress);
                            break;
                        default: throw EndingPlaybackMissing("abdication_substep");
                    }
                    break;
                case 4:
                    switch (progress.Substep)
                    {
                        case 0: return EndingStory(state, current, progress, 952);
                        case 1: state = PrepareEndingKingdomChoiceCurrent(state, current, progress); break;
                        case 2: state = PrepareEndingKingdomModalCurrent(state, current, progress); break;
                        default: throw EndingPlaybackMissing("imperial_substep");
                    }
                    break;
                case 5:
                    state = SetStage(state, ReadEndingFinalStageCurrent(state, current, progress.DeclinedAbdication));
                    break;
                case >= 6 and <= 12:
                    if (progress.Substep != 0) { state = SetStage(state, 13); break; }
                    state = PrepareEndingBranchProgressCurrent(state, current, progress.Stage);
                    int eventId = progress.Stage switch { 6 => 953, 7 => 959, 8 => 957, 9 => 956,
                        10 => 958, 11 => 954, 12 => 955, _ => throw EndingPlaybackMissing("branch") };
                    return EndingStory(state, current, progress, eventId);
                case 13:
                    bool enabled = (endingBindings ?? throw EndingPlaybackMissing("host_bindings")).MoviePlaybackEnabled
                        ?? throw EndingPlaybackMissing("Movie45_profile_playback_permission");
                    if (!enabled) { state = SetStage(state, 14); break; }
                    return SetPending(state, progress, new(progress.NextSequence,
                        CampaignEndingPresentationKind.Movie) { MovieId = 45 });
                case 14:
                    var completed = CompleteCampaignEndingProgressCurrent(state, current,
                        new(progress.FromEvent900), progress.FromEvent900 ? 0 : ReadEndingAlternateTrackCurrent(state) ? 1 : 0);
                    state = completed.State;
                    if (completed.Notices.IsEmpty) { state = SetStage(state, 15); break; }
                    return SetPending(state, progress, new(progress.NextSequence,
                        CampaignEndingPresentationKind.CompletionNotices) { Notices = completed.Notices });
                case 15:
                    return SetPending(state, progress, new(progress.NextSequence, CampaignEndingPresentationKind.Exit));
                default: throw EndingPlaybackMissing("stage");
            }
        }
    }

    private GameState EndingStory(GameState state, SourceWorldContext current,
        CampaignEndingProgress progress, int eventId)
    {
        var bindings = endingBindings ?? throw EndingPlaybackMissing("story_bindings");
        BindCurrentCampaignEventQueries(state, current);
        //450760's packet mode is0.833170/3D2E50 explicitly clear both
        //context1D8 and1DC: ordinary predicates, no collection override and
        //no skip-camera. World.ScenarioMode remains an independent input.
        var inputs = BindCampaignStoryInputs(state, current, false);
        var program = CampaignEventExecutionProgram.ReadLoadedEvent(_campaignEvents.Value, eventId);
        var plan = CampaignEventExecution.Stage(program, inputs.Persons,
            _ => ReadOrdinaryEventDispatchInputs(state), bindings.Story.Audio, storyInputs: inputs);
        var story = CampaignStorySemantics.Bind(plan, progress.NextSequence);
        return state with { EndingProgress = progress with { Story = story,
            NextSequence = checked(progress.NextSequence + 1) } };
    }

    private GameState PrepareEndingKingdomChoiceCurrent(GameState state, SourceWorldContext current,
        CampaignEndingProgress progress)
    {
        var choices = ReadEndingKingdomNameChoicesCurrent(state, current);
        if (choices.AutomaticSelectionId is int selected)
        {
            state = ApplyEndingKingdomNameCurrent(state, current, selected);
            return state with { EndingProgress = progress with { ChosenKingdomId = selected,
                Substep = progress.Substep + 1 } };
        }
        if (choices.Choices.Count == 0)
            return state with { EndingProgress = progress with { Substep = progress.Substep + 1 } };
        return SetPending(state, progress, new(progress.NextSequence, CampaignEndingPresentationKind.KingdomNameChoice)
            { KingdomChoices = choices.Choices.ToImmutableArray() });
    }

    private GameState PrepareEndingKingdomModalCurrent(GameState state, SourceWorldContext current,
        CampaignEndingProgress progress)
    {
        //2D2580 creates no modal under the real application suppression
        //flags or without its actual parent. A source skip is not a receipt.
        var bindings = endingBindings ?? throw EndingPlaybackMissing("host_bindings");
        if (!bindings.ParentStatePresent || !(bindings.OfficerModalAllowed
            ?? throw EndingPlaybackMissing("OfficerModal_application_flags")))
            return SetStage(state, 5);
        var modal = PrepareEndingEmperorModalCurrent(state, current);
        return SetPending(modal.State, progress, new(progress.NextSequence,
            CampaignEndingPresentationKind.OfficerModal, modal.TextAtSource)
            { Speaker = modal.Speaker, RulerOfficerId = modal.Speaker.OfficerId });
    }

    private GameState PrepareEndingOpeningCurrent(GameState state, SourceWorldContext current,
        CampaignEndingProgress progress)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw EndingPlaybackMissing("opening_player_pointer");
        int winner = current.ReadForcePhysicalSlot(player) ?? throw EndingPlaybackMissing("opening_player_ordinal");
        if (EndingObserverCurrent(current))
        {
            int count = 0, only = 0;
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force)
                ?? throw EndingPlaybackMissing("opening_ForceC0"))
            {
                if (entry is not { Kind: RegistryKind.Force, PhysicalSlot: int physical })
                    throw EndingPlaybackMissing("opening_ForceC0_payload");
                int ordinal = current.ReadForcePhysicalSlot(new(physical))
                    ?? throw EndingPlaybackMissing("opening_Force_virtual60");
                if (ordinal is >= 46 and <= 50) continue;
                only = ordinal; count++;
            }
            if (count == 1) winner = only;
        }
        var date = state.NativeMapSeason?.ThirdDate ?? throw EndingPlaybackMissing("opening_current_World_date");
        //30DE10 supplies five formatter integers, not an independent Date
        //selector.3233 consumes the source year and month from slots0 and1.
        var text = FormatNormalTurnReport(state, current, 0x3233,
            new(Force0: winner, Integers: [date.Year, date.Month, 0, 0, 0]));
        return SetPending(text.State, progress, new(progress.NextSequence,
            CampaignEndingPresentationKind.Opening, text.Text) { ForceId = winner, Date = date });
    }

    private static GameState SetStage(GameState state, int stage) => state with
        { EndingProgress = state.EndingProgress! with { Stage = stage, Substep = 0 } };
    private static GameState SetPending(GameState state, CampaignEndingProgress progress,
        CampaignEndingPresentation presentation) => state with
        { EndingProgress = progress with { Pending = presentation, NextSequence = checked(progress.NextSequence + 1) } };
    private static bool EndingObserverCurrent(SourceWorldContext current) =>
        ((current.ReadWorldTransferServiceFlags() ?? throw EndingPlaybackMissing("observer_World16A8")) & 0x100) != 0;
    private static InvalidDataException EndingPlaybackMissing(string owner) => new("campaign.ending." + owner);
}
