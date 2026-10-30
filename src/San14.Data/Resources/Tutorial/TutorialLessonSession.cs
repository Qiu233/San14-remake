using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A reached lesson's execution, borrowing the real command
/// world. Presentation changes never replace the business world's identity.</summary>
public sealed partial class TutorialLessonSession
{
    private readonly TutorialLessonInputs inputs;
    private readonly LoadedEvents events;
    private GameState? secondObservationOwner;
    private TutorialSecondLessonObservation? secondWorldFacts;
    public GameState BusinessWorld { get; private set; }
    public GameState World { get; private set; }
    public ArmyCampaignRuleAdapter Rules { get; }
    public TutorialLessonProgress Progress => World.TutorialLessonProgress
        ?? throw Missing("progress");
    public TutorialLessonRequest? CurrentRequest { get; private set; }
    private bool businessUpdatePending;
    public GameState? PendingBusinessWorldUpdate => businessUpdatePending ? World : null;
    public string? UnresolvedOwner { get; private set; }
    public bool Completed => Progress.Stage == 0 && Progress.CompletionNotices is { } notices &&
        Progress.CompletionNoticeIndex == notices.Length;
    private int ParagraphCount => TutorialLessonProgress.ParagraphCount(Progress.CourseIndex);
    private int ExitIndex => ParagraphCount - 1;
    public bool IsExitCleanupCompleted => Progress.Stage == 0;
    public int PlayerForceId => OwnedCurrentPlayerForce.ReadRawForce3A(World)
        ?? throw Missing("player_force");

    /// <summary>This entry constructs the lesson controller, without the
    /// optional user-selection or special-mode controllers. Its three mutable
    /// restrictions are independent reads of the actual lesson bitmap.</summary>
    public static ApplicationGameplayContext ReadApplicationContext(GameState controlWorld)
    {
        int header = TacticRemakeCurrentBattleControl.ReadHeader(controlWorld)
            ?? throw Missing("controller_header");
        bool active = header != 0;
        bool Flag(int index) => TacticRemakeCurrentBattleControl.Read(controlWorld, index)
            ?? throw Missing("event_" + index);
        return new(ApplicationGameplayMode.Tutorial, false, true, active,
            false, active && Flag(78), active && Flag(51), false, false)
            { TutorialBlocksAssociationReports = active && Flag(79), TutorialAllowsForeignTrade = Flag(55), TutorialAllowsSpecialGoodwill = Flag(54) };
    }

    public ApplicationGameplayContext ReadApplicationContext() => ReadApplicationContext(World);
    public TutorialLessonGuidance? QueryInteraction() => CurrentRequest?.Guidance;

    /// <summary>Binds a replacement command adapter to this actual lesson
    /// controller without replacing the open command's business world.</summary>
    public void BindCommandRules(ArmyCampaignRuleAdapter rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        rules.BindTutorialControlState(() => World);
    }


    public bool IsCommandAllowed(int commandId) => !Completed &&
        TutorialFirstLessonRules.IsCommandAllowed(commandId, Progress.EnabledCommandMask);

    private TutorialLessonSession(GameState world, ArmyCampaignRuleAdapter rules,
        TutorialLessonInputs inputs, bool initializeFresh, int courseIndex = 0)
    {
        this.inputs = inputs;
        Rules = rules;
        BusinessWorld = world;
        World = initializeFresh
            ? world with { TutorialLessonProgress = new(courseIndex, InitialStage(courseIndex),
                ImmutableArray.CreateRange(Enumerable.Repeat(0, TutorialLessonProgress.ParagraphCount(courseIndex))), 0, 0, null) { TerritoryTransferCount = 0 } }
            : world;
        if (Progress.CourseIndex is < 0 or > 7) throw Missing("supported_lesson");
        if (initializeFresh)
        {
            World = Rules.InitializeSelectedTutorialBusinessCurrent(World);
            businessUpdatePending = true;
        }
        events = inputs.Repository.LoadEvents("0004/EventTutorialPKCE.s14");
        Rules.BindTutorialControlState(() => World);
    }

    public static TutorialLessonSession Begin(TutorialLessonPreparation preparation, TutorialLessonInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(preparation); ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.Repository); ArgumentNullException.ThrowIfNull(inputs.Audio);
        if (preparation.UnresolvedOwner is not null || preparation.Course.Index is < 0 or > 7 ||
            preparation.World is null || preparation.Rules is null)
            throw Missing(preparation.UnresolvedOwner ?? "prepared_first_lesson");
        return new(preparation.World, preparation.Rules, inputs,
            initializeFresh: preparation.World.TutorialLessonProgress is null, courseIndex: preparation.Course.Index);
    }

    /// <summary>Rebinds an actual loaded lesson. Restoring never runs fresh
    /// merchant/hire initialization, chooses its player, or resets a paragraph.</summary>
    public static TutorialLessonSession Restore(GameState world, ArmyCampaignRuleAdapter rules,
        TutorialLessonInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(inputs); ArgumentNullException.ThrowIfNull(inputs.Repository);
        ArgumentNullException.ThrowIfNull(inputs.Audio);
        if (world.TutorialLessonProgress is null) throw Missing("saved_progress");
        return new(world, rules, inputs, initializeFresh: false);
    }

    /// <summary>Only accepted source actions call this, never a preselected row
    /// or a text-continue button. These are the original mutable event flags.</summary>
    public void ObserveAction(TutorialLessonAction action)
    {
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
        if (Completed) return;
        World = Rules.SetTutorialLessonEventCurrent(World, (int)action, true);
    }

    /// <summary>The first selector's tutorial predicate accepts only its
    /// actual designated Person receiver. A different selection remains an
    /// interaction and must not raise the completed-selection event.</summary>
    public bool CanAcceptOfficerSelection(int officerId) => Progress.CourseIndex switch
    {
        0 => officerId == Rules.ReadFirstTutorialSelectedOfficerCurrent(World),
        //4C1400 compares Person.Id185, rather than physical table slot185.
        7 => Progress.ActiveParagraphIndex is not (5 or 8) || officerId == 185,
        _ => true
    };

    public bool ObserveOfficerSelection(int officerId)
    {
        if (Completed || Progress.CourseIndex != 0 &&
            !(Progress.CourseIndex == 7 && Progress.ActiveParagraphIndex is 5 or 8)) return false;
        if (!CanAcceptOfficerSelection(officerId))
        { if (Progress.CourseIndex == 0) PrepareCorrection(0x9CD7); return false; }
        ObserveAction(TutorialLessonAction.OfficerSelectionAccepted);
        return true;
    }

    public bool CanAcceptDeploymentOfficerSelection(IReadOnlyList<int> officerIds)
    {
        ArgumentNullException.ThrowIfNull(officerIds);
        return Progress.CourseIndex switch
        {
            1 => officerIds.Count == 1 && officerIds[0] == Rules.ReadSecondTutorialSelectedOfficerCurrent(World),
            2 => Progress.ActiveParagraphIndex != 3 || Progress.ParagraphPhases[3] is not (4 or 5) ||
                officerIds.Count == 2 && officerIds.Distinct().Count() == 2 &&
                officerIds.Contains(Rules.ReadTutorialPhysicalOfficerIdentityCurrent(World, 147)) &&
                officerIds.Contains(Rules.ReadTutorialPhysicalOfficerIdentityCurrent(World, 656)),
            3 => Progress.ActiveParagraphIndex != 9 || officerIds.Count == 1,
            4 => Progress.ActiveParagraphIndex != 20 || Progress.ParagraphPhases[20] != 2 || officerIds.Count == 3,
            5 or 6 or 7 => true,
            _ => false
        };
    }

    /// <summary>The actual checkbox-change notification. A new wrong
    /// person produces9C62; changing selection never raises accepted event1.</summary>
    public void ObserveDeploymentOfficerSelectionChanged(IReadOnlyList<int> previous,
        IReadOnlyList<int> selected)
    {
        ArgumentNullException.ThrowIfNull(previous); ArgumentNullException.ThrowIfNull(selected);
        if (Completed || Progress.CourseIndex != 1 || Progress.ActiveParagraphIndex != 4) return;
        int required = Rules.ReadSecondTutorialSelectedOfficerCurrent(World);
        foreach (int id in selected)
            if (id != required && !previous.Contains(id)) { PrepareCorrection(0x9C62, id); break; }
    }

    public bool ObserveDeploymentOfficerSelection(IReadOnlyList<int> officerIds)
    {
        if (Completed || Progress.CourseIndex is < 1 or > 7) return false;
        if (!CanAcceptDeploymentOfficerSelection(officerIds))
        {
            if (Progress.CourseIndex != 1) return false;
            int required = Rules.ReadSecondTutorialSelectedOfficerCurrent(World);
            int? wrong = officerIds.Where(id => id != required).Cast<int?>().FirstOrDefault();
            if (wrong is int id) PrepareCorrection(0x9C62, id);
            return false;
        }
        if (Progress.CourseIndex == 1) ObserveAction(TutorialLessonAction.DeploymentOfficerSelectionAccepted);
        return true;
    }

    /// <summary>The actual target-page decision receipt. Phase1 already
    /// checked its current Move instruction against the real capital Hex;
    /// a successful phase2 Yes produces the original event3, not a new order.</summary>
    public bool ObserveDeploymentTargetDecision(IReadOnlyList<DeploymentDraftRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (Completed || Progress.ActiveParagraphIndex is not int index) return false;
        bool expected = Progress.CourseIndex switch
        {
            1 => index == 9 && Progress.ParagraphPhases[index] == 2,
            2 => index == 5 && Progress.ParagraphPhases[index] == 3,
            3 => index == 9 && Progress.ParagraphPhases[index] == 6,
            4 => index == 23 && Progress.ParagraphPhases[index] == 7,
            _ => false
        };
        if (!expected) return false;
        ObserveAction(TutorialLessonAction.DeploymentTargetAccepted);
        return true;
    }

    public bool CanAcceptPriority(AreaDevelopmentFocus focus) => Progress.CourseIndex != 0 || focus == AreaDevelopmentFocus.Barracks;

    public bool ObservePriorityConfirmation(AreaDevelopmentFocus focus)
    {
        if (Completed || Progress.CourseIndex != 0 &&
            !(Progress.CourseIndex == 7 && Progress.ActiveParagraphIndex is 6 or 9)) return false;
        if (!CanAcceptPriority(focus)) { PrepareCorrection(0x9C5E); return false; }
        ObserveAction(TutorialLessonAction.PriorityConfirmed);
        return true;
    }

    /// <summary>Actual command entry overlays the latest small controller and
    /// App state once. Existing draft/world identity checks keep their baseline.</summary>
    public GameState PrepareCommandWorld(GameState expectedBusinessWorld)
    {
        if (!ReferenceEquals(BusinessWorld, expectedBusinessWorld)) throw Missing("stale_command_world");
        preparedBitmapBaseline = ReadTutorialBitmap(World);
        return OverlayControl(expectedBusinessWorld, World);
    }

    public void AdoptWorld(GameState expectedBusinessWorld, GameState actualWorld)
    {
        ArgumentNullException.ThrowIfNull(actualWorld);
        if (!ReferenceEquals(BusinessWorld, expectedBusinessWorld)) throw Missing("stale_business_world");
        var control = World;
        //A command which draws App consumes the supplied current snapshot;
        //an unchanged business App must not rewind intervening story draws.
        bool commandDrew = actualWorld.CampaignAppRandom32 != expectedBusinessWorld.CampaignAppRandom32;
        World = OverlayControl(actualWorld, control, preparedBitmapBaseline ?? ReadTutorialBitmap(expectedBusinessWorld));
        preparedBitmapBaseline = null;
        if (commandDrew) World = World with { CampaignAppRandom32 = actualWorld.CampaignAppRandom32 };
        BusinessWorld = actualWorld;
        businessUpdatePending = false;
    }

    public GameState ExportWorld() => World;

    public void Advance(TutorialLessonObservation observation, bool actualTutorialUpdate = false)
    {
        ArgumentNullException.ThrowIfNull(observation);
        lastUiObservation = observation;
        if (Completed)
        {
            if (Progress.ActiveParagraphIndex == ExitIndex) Store(Progress with { ActiveParagraphIndex = null });
            CurrentRequest = null; return;
        }
        try
        {
            UnresolvedOwner = null;
            if (Progress.CorrectionText is not null) { PublishCorrection(); return; }
            if (Progress.ActiveParagraphIndex is not int index)
            {
                var actual = ActualObservation(observation);
                int selected = -1;
                for (int candidate = 0; candidate < ParagraphCount; candidate++)
                    if ((Progress.CompletedParagraphMask & (1UL << candidate)) == 0 &&
                        CanStart(candidate, actual))
                    { selected = candidate; break; }
                if (selected < 0) return;
                index = selected;
                Store(Progress with { ActiveParagraphIndex = index, InstructionIndex = 0,
                    Sequence = checked(Progress.Sequence + 1), Story = null });
                EnterPhase(index, Progress.ParagraphPhases[index]);
            }

            int phase = Progress.ParagraphPhases[index];
            UpdateCourseObservation(observation);
            if (Progress.CourseIndex == 3 && index == 11 && phase == 1)
                ApplyFourthAttackUpdate(observation.ActionStackTop);
            var plan = ReadPlan(index, phase);
            if (index == ExitIndex)
            {
                if (phase == 0) { PublishExit(); return; }
                //548020 consumes the course recorded by the Ending body.
                //Publish the actual515E50 outbox and text exactly once; neither
                //opening a notification nor continuing a story is completion.
                if (Progress.CompletionNotices is null)
                {
                    int course = Progress.CompletionCourseIndex ?? throw Missing("completion_course");
                    var completed = Rules.CompleteTutorialLessonCurrent(World, course);
                    World = completed.State;
                    Store(Progress with { CompletionNotices = completed.Notices, CompletionNoticeIndex = 0 });
                }
                if (Progress.CompletionNotices is { } notices && Progress.CompletionNoticeIndex < notices.Length)
                { PublishCompletionNotice(notices[Progress.CompletionNoticeIndex]); return; }
                Store(Progress with { ActiveParagraphIndex = null });
                CurrentRequest = null;
                return;
            }
            //The source checks these already-accepted actions before entering
            //their explanation/setup bodies. Do not replay an earlier prompt.
            if (Progress.CourseIndex == 1 && index is 2 or 4 or 7 &&
                CanComplete(index, phase, ActualObservation(observation)))
            { CompleteParagraph(index, plan, observation); return; }
            if (Progress.CourseIndex == 5 && index == 12 &&
                TutorialSixthLessonRules.CanComplete(index, phase, SixthObservation(ActualObservation(observation))))
            { CompleteParagraph(index, plan, observation); return; }
            if (Progress.CourseIndex == 6 && index == 1 && phase >= 1 &&
                CanComplete(index, phase, ActualObservation(observation)))
            { CompleteParagraph(index, plan, observation); return; }
            if (Progress.CourseIndex == 4 && index == 27 &&
                FifthObservation(ActualObservation(observation)).EnemyArmyValid == false)
            { CompleteParagraph(index, plan, observation); return; }
            if (Progress.CourseIndex == 2 && index is 7 or 8 or 9 &&
                ThirdObservation(ActualObservation(observation)).EnemyArmyValid == false)
            { CompleteParagraph(index, plan, observation); return; }
            if (Progress.Story is { } story)
            {
                if (story.NextStep < story.Steps.Length) { PublishNarration(index, phase); return; }
                Store(Progress with { Story = null, InstructionIndex = checked(Progress.InstructionIndex + 1) });
                CurrentRequest = null;
            }
            var availableFacts = ActualObservation(observation);
            bool storiesAvailable = ShouldRunStories(index, phase, availableFacts);
            if (Progress.CourseIndex == 2 && index == 9 && Progress.Story is null && Progress.InstructionIndex == 0)
            {
                if (!actualTutorialUpdate) { PublishInteraction(index, phase, plan); return; }
                var update = TutorialThirdLessonRules.ReadTacticsUpdate(phase, ThirdObservation(availableFacts));
                if (update.Counter != phase)
                { SetPhase(index, update.Counter); phase = update.Counter; plan = ReadPlan(index, phase); }
                storiesAvailable = update.RequestInstruction;
            }
            int instructionCount = InstructionCount(index, phase, plan);
            if (storiesAvailable && IsInstructionCue(index, phase, Progress.InstructionIndex))
            { PublishInstructionCue(index, phase); return; }
            if (storiesAvailable && Progress.InstructionIndex < instructionCount)
            {
                int eventId = StoryEventId(index, phase, plan, Progress.InstructionIndex);
                var prepared = Rules.PrepareTutorialLessonStoryCurrent(World, events, eventId,
                    checked(Progress.Sequence + 1), inputs.Audio, Progress.MessageContext);
                Store(Progress with { Sequence = checked(Progress.Sequence + 1), Story = prepared.Story,
                    MessageContext = prepared.MessageContext });
                World = Rules.SetTutorialLessonEventCurrent(World, 63, true);
                PublishNarration(index, phase);
                return;
            }
            if (plan.StoryEventIds.Count != 0 && Progress.InstructionIndex == instructionCount)
            {
                PublishInteraction(index, phase, plan);
                return;
            }

            var facts = ActualObservation(observation) with
            {
                InstructionSequenceCompleted = plan.StoryEventIds.Count != 0 && Progress.InstructionIndex > instructionCount,
                RequiredCommandId = plan.RequiredCommandId,
                RequiredInformationId = plan.RequiredInformationId,
                //These two phases record a completed source calendar writer,
                //not a UI setup. Restoring that reached phase never repeats it.
                ParagraphSetupCompleted = Progress.CourseIndex == 5 && index is 20 or 21 && phase == 1
                    ? true : observation.ParagraphSetupCompleted
            };
            //These paragraphs initialize a real command/selection UI instead
            //of waiting for an instruction callback. Publish its setup once;
            //the host returns CompleteParagraphSetup only after applying it.
            if ((Progress.CourseIndex < 2 ? phase == 0 : IsSynchronousSetup(index, phase)) && plan.StoryEventIds.Count == 0)
            { PublishInteraction(index, phase, plan); return; }
            int nextPhase = NextPhase(index, phase, facts);
            if (nextPhase != phase)
            {
                if (Progress.CourseIndex == 3 && index == 9 && phase == 5 && nextPhase == 6)
                    Store(Progress with { FourthConstructionCommanderId = FourthObservation(facts).ConstructionCommanderId
                        ?? throw Missing("construction_commander") });
                SetPhase(index, nextPhase);
                EnterPhase(index, nextPhase);
                phase = nextPhase;
                plan = ReadPlan(index, phase);
                if (plan.StoryEventIds.Count != 0) { PublishInteraction(index, phase, plan); return; }
            }
            if (CanComplete(index, phase, facts))
            {
                CompleteParagraph(index, plan, observation);
                return;
            }
            PublishInteraction(index, phase, plan);
        }
        catch (InvalidDataException error) { UnresolvedOwner = error.Message; }
    }

    /// <summary>Receives only the completed synchronous paragraph UI setup,
    /// such as resetting the last-command sentinel and applying its arrows.</summary>
    public bool CompleteParagraphSetup(TutorialLessonRequest expectedRequest)
    {
        if (!ReferenceEquals(CurrentRequest, expectedRequest) || expectedRequest.Kind != TutorialLessonRequestKind.Interaction ||
            Progress.ActiveParagraphIndex != expectedRequest.ParagraphIndex ||
            Progress.ParagraphPhases[expectedRequest.ParagraphIndex] != expectedRequest.Phase)
            return false;
        int phase = expectedRequest.Phase;
        var plan = ReadPlan(expectedRequest.ParagraphIndex, phase);
        if (plan.StoryEventIds.Count != 0 || !IsSynchronousSetup(expectedRequest.ParagraphIndex, phase)) return false;
        SetPhase(expectedRequest.ParagraphIndex, phase + 1);
        EnterPhase(expectedRequest.ParagraphIndex, phase + 1);
        CurrentRequest = null;
        return true;
    }

    /// <summary>The final story receipt only completes playback. The reached
    /// host callback must also restore its actual windows/audio/arrow state
    /// before this source instruction body is allowed to return.</summary>
    public bool CompleteInstructionCallback(TutorialLessonRequest expectedRequest)
    {
        if (!ReferenceEquals(CurrentRequest, expectedRequest) || expectedRequest.Kind != TutorialLessonRequestKind.Interaction ||
            Progress.ActiveParagraphIndex != expectedRequest.ParagraphIndex ||
            Progress.ParagraphPhases[expectedRequest.ParagraphIndex] != expectedRequest.Phase) return false;
        var plan = ReadPlan(expectedRequest.ParagraphIndex, expectedRequest.Phase);
        if (plan.StoryEventIds.Count == 0 || Progress.InstructionIndex != InstructionCount(expectedRequest.ParagraphIndex, expectedRequest.Phase, plan) || Progress.Story is not null)
            return false;
        World = Rules.SetTutorialLessonEventCurrent(World, 63, false);
        foreach (int flag in plan.AfterStoryClearEventFlags) World = Rules.SetTutorialLessonEventCurrent(World, flag, false);
        Store(Progress with { InstructionIndex = checked(Progress.InstructionIndex + 1) });
        CurrentRequest = null;
        return true;
    }

    public bool CompleteCompletionNotice(TutorialLessonRequest expectedRequest)
    {
        if (!ReferenceEquals(CurrentRequest, expectedRequest) || expectedRequest.Kind != TutorialLessonRequestKind.CompletionNotice ||
            Progress.ActiveParagraphIndex != ExitIndex || Progress.CompletionNotices is not { } notices ||
            Progress.CompletionNoticeIndex >= notices.Length) return false;
        Store(Progress with { CompletionNoticeIndex = Progress.CompletionNoticeIndex + 1,
            Sequence = checked(Progress.Sequence + 1) });
        CurrentRequest = null;
        return true;
    }

    public bool CompleteCorrection(TutorialLessonRequest expectedRequest)
    {
        if (!ReferenceEquals(CurrentRequest, expectedRequest) || expectedRequest.Kind != TutorialLessonRequestKind.Correction ||
            Progress.CorrectionText is null) return false;
        Store(Progress with { CorrectionText = null });
        CurrentRequest = null;
        return true;
    }

    private void PrepareCorrection(int root, int? incorrectOfficerId = null)
    {
        if (Progress.CorrectionText is not null) return;
        var prepared = Rules.PrepareTutorialLessonCorrectionCurrent(World, root, incorrectOfficerId);
        World = prepared.State;
        Store(Progress with { CorrectionText = prepared.Text, Sequence = checked(Progress.Sequence + 1) });
        PublishCorrection();
    }

    /// <summary>Only the host which restored the real tutorial windows,
    /// arrows and map may acknowledge4C65F0. It does not replay515E50.</summary>
    public bool CompleteExit(TutorialLessonRequest expectedRequest)
    {
        if (!ReferenceEquals(CurrentRequest, expectedRequest) || expectedRequest.Kind != TutorialLessonRequestKind.Exit ||
            Progress.ActiveParagraphIndex != ExitIndex || Progress.Stage != ExitStage(Progress.CourseIndex) || Progress.ParagraphPhases[ExitIndex] != 0)
            return false;
        World = Rules.SetTutorialLessonEventCurrent(World, 62, true);
        Store(Progress with { Stage = 0,
            ParagraphPhases = Progress.ParagraphPhases.SetItem(ExitIndex, 1),
            CompletedParagraphMask = Progress.CompletedParagraphMask | (1UL << ExitIndex), Story = null });
        CurrentRequest = null;
        return true;
    }

    public CampaignStoryPreparationResult PrepareCurrentStoryStep()
    {
        if (Progress.Story is not { } story || story.NextStep >= story.Steps.Length)
            return new(null, null, "tutorial.lesson.story_step");
        var result = Rules.PrepareNormalTurnStoryStep(World, story);
        if (result.UnresolvedOwner is not null || result.Prepared is null) return result;
        World = World with { CampaignAppRandom32 = result.AppRandomState ?? World.CampaignAppRandom32 };
        Store(Progress with { Story = story with { PreparedStep = result.Prepared } });
        PublishNarration(Progress.ActiveParagraphIndex ?? throw Missing("story_paragraph"),
            Progress.ParagraphPhases[Progress.ActiveParagraphIndex!.Value]);
        return result;
    }

    public bool CompleteStoryStep(CampaignStoryRequest expectedStory, CampaignStoryStepReceipt receipt)
    {
        if (!ReferenceEquals(Progress.Story, expectedStory) || receipt.RequestSequence != expectedStory.Sequence ||
            receipt.Step != expectedStory.NextStep || expectedStory.NextStep >= expectedStory.Steps.Length ||
            expectedStory.Steps[expectedStory.NextStep] is StoryMessage or StoryNarration or StoryFormatOnlyMessage && expectedStory.PreparedStep?.Text is null)
            return false;
        Store(Progress with { Story = expectedStory.CompleteStep(receipt) });
        PublishNarration(Progress.ActiveParagraphIndex ?? throw Missing("story_paragraph"),
            Progress.ParagraphPhases[Progress.ActiveParagraphIndex!.Value]);
        return true;
    }

    private void EnterPhase(int index, int phase)
    {
        var plan = ReadPlan(index, phase);
        ApplyParagraphWorld(index, phase);
        if (index == 0 && phase == 0 || Progress.CourseIndex == 1 && index == 11 && phase == 1) businessUpdatePending = true;
        foreach (int flag in plan.BeforeStoryClearEventFlags)
            World = Rules.SetTutorialLessonEventCurrent(World, flag, false);
        if (plan.ResetProgressRequested) World = Rules.SetTutorialLessonEventCurrent(World, 4, false);
        if (plan.EnabledCommandMask is uint mask)
            Store(Progress with { EnabledCommandMask = checked((ushort)mask) });
        Store(Progress with { InstructionIndex = 0 });
        CurrentRequest = null;
        if (plan.StoryEventIds.Count == 0) PublishInteraction(index, phase, plan);
    }

    private void CompleteParagraph(int index, TutorialParagraphPlan plan, TutorialLessonObservation observation)
    {
        ApplyParagraphWorld(index, -1);
        foreach (int flag in plan.CompletionClearEventFlags)
            World = Rules.SetTutorialLessonEventCurrent(World, flag, false);
        foreach (int flag in plan.CompletionSetEventFlags)
            World = Rules.SetTutorialLessonEventCurrent(World, flag, true);
        Store(Progress with { Stage = NextStage(index, ActualObservation(observation)),
            CompletedParagraphMask = Progress.CompletedParagraphMask | (1UL << index),
            ActiveParagraphIndex = null, InstructionIndex = 0, Story = null });
        CurrentRequest = null;
    }

    private TutorialLessonObservation ActualObservation(TutorialLessonObservation observed)
    {
        var actual = observed with
        {
            TutorialActive = TacticRemakeCurrentBattleControl.ReadHeader(World) is int header
                ? header != 0 : throw Missing("controller_header"),
            Stage = Progress.Stage,
            ReadTutorialEventFlag = flag => TacticRemakeCurrentBattleControl.Read(World, flag)
                ?? throw Missing("event_" + flag)
        };
        if (Progress.CourseIndex == 1)
        {
            var second = (observed.SecondLesson ?? new TutorialSecondLessonObservation(actual)) with
            {
                Common = actual,
                TargetExplanationShown = Progress.ActiveParagraphIndex == 9 &&
                    Progress.ParagraphPhases[9] == 1 && Progress.InstructionIndex > 0
            };
            if (!ReferenceEquals(secondObservationOwner, World))
            {
                secondWorldFacts = Rules.ReadSecondTutorialObservationCurrent(World, second);
                secondObservationOwner = World;
            }
            actual = actual with { SecondLesson = second with
            {
                CapitalHexBelongsToPlayer = secondWorldFacts!.CapitalHexBelongsToPlayer,
                CapitalOccupation = secondWorldFacts.CapitalOccupation,
                CapitalMaximumOccupation = secondWorldFacts.CapitalMaximumOccupation,
                ConquerAreasComplete = secondWorldFacts.ConquerAreasComplete
            } };
        }
        return ReadAdditionalObservation(actual);
    }
    private void SetPhase(int index, int phase) => Store(Progress with
        { ParagraphPhases = Progress.ParagraphPhases.SetItem(index, phase) });
    private void Store(TutorialLessonProgress progress) => World = World with { TutorialLessonProgress = progress };
    private void PublishNarration(int index, int phase) => CurrentRequest = new(Progress.Sequence,
        index, phase, TutorialLessonRequestKind.Narration)
        { Story = Progress.Story, StoryEventId = Progress.Story?.Source.EventId };
    private void PublishInteraction(int index, int phase, TutorialParagraphPlan plan)
    {
        var guidance = new TutorialLessonGuidance(index, TutorialDefinitions.Paragraphs[Progress.CourseIndex][index], Progress.EnabledCommandMask,
            plan.RequiredCommandId, plan.RequiredInformationId)
            { CityId = 7, OfficerId = Progress.CourseIndex == 0
                    ? index is 4 or 5 ? Rules.ReadFirstTutorialSelectedOfficerCurrent(World) : null
                    : Progress.CourseIndex == 1 && index is 3 or 4 ? Rules.ReadSecondTutorialSelectedOfficerCurrent(World) : null,
                AreaId = Progress.CourseIndex == 1 && index is >= 8 and <= 10 ? 51 : null,
                RequiredFormationId = Progress.CourseIndex == 1 && index == 5 ? 5 : null,
                RequiredDevelopmentFocus = Progress.CourseIndex == 0 && index == 5 ? AreaDevelopmentFocus.Barracks : null,
                AllowsPeriodAdvance = Progress.CourseIndex == 0 ? phase > 0 && (index is 7 or 13 or 18)
                    : index == 9 && phase == 4 || index == 10 && phase == 1 || index == 11 && phase == 2,
                AllowsDeploymentSetupConfirmation = Progress.CourseIndex == 1 && (index == 7 && phase == 1 || index == 11 && phase == 2),
                AllowsDeploymentTargetConfirmation = Progress.CourseIndex == 1 && (index == 9 && phase == 2 || index == 11 && phase == 2),
                InstructionCallbackPending = plan.StoryEventIds.Count != 0 && Progress.InstructionIndex == InstructionCount(index, phase, plan),
                ResetLastCommandId = plan.ResetLastCommandId,
                ResetLastInformationId = plan.ResetLastInformationId,
                BlocksOrdinaryCommands = Progress.CourseIndex == 0 ? index is 0 or 1 or 10 or 20 : index is 0 or 8 or 12 };
        guidance = AdaptGuidance(guidance, index, phase);
        if (CurrentRequest is { Kind: TutorialLessonRequestKind.Interaction } existing &&
            existing.Sequence == Progress.Sequence && existing.ParagraphIndex == index && existing.Phase == phase &&
            existing.Guidance == guidance) return;
        CurrentRequest = new(Progress.Sequence, index, phase, TutorialLessonRequestKind.Interaction) { Guidance = guidance };
    }
    private void PublishExit()
    {
        if (CurrentRequest is { Kind: TutorialLessonRequestKind.Exit } existing && existing.Sequence == Progress.Sequence) return;
        CurrentRequest = new(Progress.Sequence, ExitIndex, Progress.ParagraphPhases[ExitIndex], TutorialLessonRequestKind.Exit);
    }
    private void PublishCompletionNotice(TutorialLessonCompletionNotice notice)
    {
        if (CurrentRequest is { Kind: TutorialLessonRequestKind.CompletionNotice } existing && existing.Sequence == Progress.Sequence) return;
        CurrentRequest = new(Progress.Sequence, ExitIndex, Progress.ParagraphPhases[ExitIndex], TutorialLessonRequestKind.CompletionNotice)
            { CompletionNotice = notice };
    }
    private void PublishCorrection()
    {
        if (CurrentRequest is { Kind: TutorialLessonRequestKind.Correction } existing && existing.Sequence == Progress.Sequence) return;
        int index = Progress.ActiveParagraphIndex ?? throw Missing("correction_paragraph");
        CurrentRequest = new(Progress.Sequence, index, Progress.ParagraphPhases[index], TutorialLessonRequestKind.Correction)
            { CorrectionText = Progress.CorrectionText };
    }

    private static GameState OverlayControl(GameState business, GameState control, int?[]? eventBaseline = null)
    {
        var destination = business.NativeArmySimulation ?? throw Missing("business_parameters");
        var source = control.NativeArmySimulation ?? throw Missing("control_parameters");
        Dictionary<string, int>? values = null;
        int keyIndex = 0;
        foreach (string key in new[] { TacticRemakeCurrentBattleControl.VersionKey,
            TacticRemakeCurrentBattleControl.HeaderKey, TacticRemakeCurrentBattleControl.LessonIndexKey,
            TacticRemakeCurrentBattleControl.BitmapPresenceKey }.Concat(Enumerable.Range(0, 13).Select(TacticRemakeCurrentBattleControl.BitmapKey)))
        {
            bool hasSource = source.Parameters.Int32Values.TryGetValue(key, out int value);
            bool hasDestination = destination.Parameters.Int32Values.TryGetValue(key, out int previous);
            int eventIndex = keyIndex++ - 4;
            if (eventIndex >= 0 && eventBaseline is not null)
            {
                int? baseline = eventBaseline[eventIndex];
                if (hasDestination && baseline is int before && hasSource)
                {
                    // Only bits written by the actual business candidate win
                    // over local lesson callbacks. Other bits retain their
                    // latest local clear/set instead of replaying an old bank.
                    int changed = previous ^ before;
                    value = (value & ~changed) | (previous & changed);
                }
                else if ((hasDestination ? previous : (int?)null) != baseline)
                { hasSource = hasDestination; value = previous; }
            }
            if (hasSource == hasDestination && (!hasSource || value == previous)) continue;
            values ??= new(destination.Parameters.Int32Values);
            if (hasSource) values[key] = value;
            else values.Remove(key);
        }
        return business with { TutorialLessonProgress = control.TutorialLessonProgress,
            CampaignAppRandom32 = control.CampaignAppRandom32,
            NativeArmySimulation = values is null ? destination : destination with
                { Parameters = destination.Parameters with { Int32Values = values } } };
    }
    private static InvalidDataException Missing(string owner) => new("tutorial.lesson." + owner);
}

internal sealed record TutorialStoryPreparation(CampaignStoryRequest Story, CampaignStoryMessageContext MessageContext);
internal sealed record TutorialCorrectionPreparation(GameState State, CampaignStoryPreparedText Text);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState SetTutorialLessonEventCurrent(GameState state, int index, bool enabled)
        => WriteFirstTutorialControl(state, index, enabled);

    internal int ReadFirstTutorialSelectedOfficerCurrent(GameState state)
    {
        var current = CaptureCurrentDefinitions(state) ?? throw new InvalidDataException("tutorial.lesson.selection_current");
        //4C0DC0 compares against World's table pointer at+5F0, not its
        //logical lookup key or the currently highlighted row.
        int person = (current.ReadTransferPersonTableReference(147)
            ?? throw new InvalidDataException("tutorial.lesson.selection_person_pointer")).PhysicalSlot
            ?? throw new InvalidDataException("tutorial.lesson.selection_person");
        if (current.ReadPersonNativeValidity(person) != true)
            throw new InvalidDataException("tutorial.lesson.selection_person_validity");
        return current.ReadPersonIdentity(person)
            ?? throw new InvalidDataException("tutorial.lesson.selection_person_identity");
    }

    internal TutorialCorrectionPreparation PrepareTutorialLessonCorrectionCurrent(GameState state, int root, int? incorrectOfficerId = null)
    {
        if (root is not (0x9CD7 or 0x9C5E or 0x9C62)) throw new ArgumentOutOfRangeException(nameof(root));
        var current = CaptureDeploymentPersonSelectionCurrent(state);
        int speakerPhysical = (current.ReadTransferPersonTableReference(root == 0x9C62 ? 952 : 147)
            ?? throw new InvalidDataException("tutorial.lesson.correction_speaker_pointer")).PhysicalSlot
            ?? throw new InvalidDataException("tutorial.lesson.correction_speaker");
        int otherPhysical = (root == 0x9C62
            ? current.ReadPersonLookupReference(incorrectOfficerId ?? throw new InvalidDataException("tutorial.lesson.correction_incorrect_person"))
            : current.ReadTransferPersonTableReference(952))?.PhysicalSlot
            ?? throw new InvalidDataException("tutorial.lesson.correction_other");
        int speaker = current.ReadPersonIdentity(speakerPhysical)
            ?? throw new InvalidDataException("tutorial.lesson.correction_speaker_identity");
        int other = current.ReadPersonIdentity(otherPhysical)
            ?? throw new InvalidDataException("tutorial.lesson.correction_other_identity");
        var source = state.Officers.GetValueOrDefault(speaker)
            ?? throw new InvalidDataException("tutorial.lesson.correction_speaker_owner");
        string name = (source.CurrentSurname ?? throw new InvalidDataException("tutorial.lesson.correction_surname")) +
            (source.CurrentGivenName ?? throw new InvalidDataException("tutorial.lesson.correction_given_name"));
        var formatted = FormatNormalTurnReport(state, current, root, new(Person0: speaker, Person1: other));
        var text = new CampaignStoryPreparedText(name, formatted.Text, SplitStoryPages(formatted.Text),
            new(speaker, name, current.ReadPersonPortraitAppearance(speakerPhysical)),
            formatted.Voices.Select(SaveVoice).ToImmutableArray());
        return new(formatted.State, text);
    }

    internal TutorialStoryPreparation PrepareTutorialLessonStoryCurrent(GameState state, LoadedEvents events,
        int eventId, long sequence, IS14AudioEventExecutionState audio, CampaignStoryMessageContext? messageContext)
    {
        var inputs = CaptureCampaignStoryInputs(state, false) with
        {
            InitialMessagePeople = messageContext?.People,
            InitialNameParameters = messageContext?.Names,
            InitialCityParameters = messageContext?.Cities,
            InitialForceParameters = messageContext?.Forces,
            InitialAreaParameters = messageContext?.Areas,
            InitialItemParameters = messageContext?.Items, InitialNumberParameters = messageContext?.Numbers,
            InitialProvinceParameters = messageContext?.Provinces
        };
        var program = CampaignEventExecutionProgram.ReadLoadedEvent(events, eventId);
        var plan = CampaignEventExecution.Stage(program, inputs.Persons,
            _ => ReadOrdinaryEventDispatchInputs(state), audio, storyInputs: inputs);
        return new(CampaignStorySemantics.Bind(plan, sequence), new(
            plan.FinalPersonParameters?.ToArray() ?? new int?[5],
            plan.FinalNameParameters?.ToArray(), plan.FinalCityParameters?.ToArray())
            { Forces = plan.FinalForceParameters?.ToArray(), Areas = plan.FinalAreaParameters?.ToArray(),
                Items = plan.FinalItemParameters?.ToArray(), Numbers = plan.FinalNumberParameters?.ToArray(), Provinces = plan.FinalProvinceParameters?.ToArray() });
    }
}
