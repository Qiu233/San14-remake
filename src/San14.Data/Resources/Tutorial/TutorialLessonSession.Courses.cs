using San14.Core;

namespace San14.Data;

public sealed partial class TutorialLessonSession
{
    private bool borrowingPeriod;
    private int?[]? preparedBitmapBaseline;
    private int?[]? borrowBitmapBaseline;
    private static int?[] ReadTutorialBitmap(GameState world)
    {
        var values = world.NativeArmySimulation?.Parameters.Int32Values ?? throw Missing("control_parameters");
        return Enumerable.Range(0, 13).Select(index => values.TryGetValue(TacticRemakeCurrentBattleControl.BitmapKey(index),
            out int value) ? value : (int?)null).ToArray();
    }
    private TutorialLessonObservation? lastUiObservation;
    private CampaignAppRandomState? borrowPublishedApp;
    private uint? areaSupportLastMilliseconds;
    private uint? homeCommandLastMilliseconds;

    private static int InitialStage(int course) => course switch
        { 0 => 1, 1 => 24, 2 => 38, 3 => 51, 4 => 67, 5 => 100, 6 => 126, 7 => 179, _ => throw Missing("supported_lesson") };
    private static int ExitStage(int course) => course switch
        { 0 => 23, 1 => 37, 2 => 50, 3 => 66, 4 => 99, 5 => 125, 6 => 178, 7 => 197, _ => throw Missing("supported_lesson") };

    /// <summary>The stopped period worker lends its actual candidate exclusively
    /// to the host. The committed business world retains its identity.</summary>
    public void BeginPeriodBorrow(GameState actualCandidate)
    {
        ArgumentNullException.ThrowIfNull(actualCandidate);
        if (borrowingPeriod || businessUpdatePending) throw Missing("period_borrow_lifecycle");
        borrowingPeriod = true;
        World = OverlayControl(actualCandidate, World, preparedBitmapBaseline ?? ReadTutorialBitmap(BusinessWorld))
            with { CampaignAppRandom32 = actualCandidate.CampaignAppRandom32 };
        borrowBitmapBaseline = ReadTutorialBitmap(actualCandidate);
        preparedBitmapBaseline = null;
        borrowPublishedApp = World.CampaignAppRandom32;
    }

    public void RefreshPeriodBorrowWorld(GameState latestCandidate)
    {
        ArgumentNullException.ThrowIfNull(latestCandidate);
        if (!borrowingPeriod) throw Missing("period_borrow_lifecycle");
        var app = World.CampaignAppRandom32 != borrowPublishedApp
            ? World.CampaignAppRandom32 : latestCandidate.CampaignAppRandom32;
        World = OverlayControl(latestCandidate, World, borrowBitmapBaseline) with { CampaignAppRandom32 = app };
    }

    public GameState ReadPeriodBorrowWorld()
    {
        if (!borrowingPeriod) throw Missing("period_borrow_lifecycle");
        borrowPublishedApp = World.CampaignAppRandom32;
        borrowBitmapBaseline = ReadTutorialBitmap(World);
        //The stopped worker receives this owned business image through its
        //candidate, rather than through the committed session adoption path.
        businessUpdatePending = false;
        return World;
    }

    public GameState EndPeriodBorrow(GameState latestCandidate)
    {
        RefreshPeriodBorrowWorld(latestCandidate);
        var result = World;
        businessUpdatePending = false;
        borrowingPeriod = false;
        World = OverlayControl(BusinessWorld, result);
        borrowPublishedApp = null;
        borrowBitmapBaseline = null;
        return result;
    }

    private TutorialParagraphPlan ReadPlan(int index, int phase) => Progress.CourseIndex switch
    {
        0 => TutorialFirstLessonPlan.Read(index, phase),
        1 => TutorialSecondLessonPlan.Read(index, phase),
        2 => TutorialThirdLessonPlan.Read(index, phase),
        3 => TutorialFourthLessonPlan.Read(index, phase),
        4 => TutorialFifthLessonPlan.Read(index, phase),
        5 => TutorialSixthLessonPlan.Read(index, phase),
        6 => TutorialSeventhLessonPlan.Read(index, phase),
        7 => TutorialEighthLessonPlan.Read(index, phase),
        _ => throw Missing("supported_lesson")
    };
    private static TutorialSecondLessonObservation SecondObservation(TutorialLessonObservation facts)
        => (facts.SecondLesson ?? new TutorialSecondLessonObservation(facts)) with { Common = facts };
    private static TutorialThirdLessonObservation ThirdObservation(TutorialLessonObservation facts)
        => (facts.ThirdLesson ?? new TutorialThirdLessonObservation(facts)) with { Common = facts };
    private static TutorialFourthLessonObservation FourthObservation(TutorialLessonObservation facts)
        => (facts.FourthLesson ?? new TutorialFourthLessonObservation(facts)) with { Common = facts };
    private static TutorialFifthLessonObservation FifthObservation(TutorialLessonObservation facts)
        => (facts.FifthLesson ?? new TutorialFifthLessonObservation(facts)) with { Common = facts };
    private static TutorialSixthLessonObservation SixthObservation(TutorialLessonObservation facts)
        => (facts.SixthLesson ?? new TutorialSixthLessonObservation(facts)) with { Common = facts };
    private static TutorialSeventhLessonObservation SeventhObservation(TutorialLessonObservation facts)
        => (facts.SeventhLesson ?? new TutorialSeventhLessonObservation(facts)) with { Common = facts };
    private static TutorialEighthLessonObservation EighthObservation(TutorialLessonObservation facts)
        => (facts.EighthLesson ?? new TutorialEighthLessonObservation(facts)) with { Common = facts };
    private bool CanStart(int index, TutorialLessonObservation facts) => Progress.CourseIndex switch
    {
        0 => TutorialFirstLessonRules.CanStart(index, facts),
        1 => TutorialSecondLessonRules.CanStart(index, SecondObservation(facts)),
        2 => TutorialThirdLessonRules.CanStart(index, ThirdObservation(facts)),
        3 => (index != 11 || facts.ActionStackTop == 1) &&
            TutorialFourthLessonRules.CanStart(index, FourthObservation(facts)),
        4 => TutorialFifthLessonRules.CanStart(index, FifthObservation(facts)),
        5 => TutorialSixthLessonRules.CanStart(index, SixthObservation(facts)),
        6 => TutorialSeventhLessonRules.CanStart(index, SeventhObservation(facts)),
        7 => TutorialEighthLessonRules.CanStart(index, EighthObservation(facts)),
        _ => false
    };
    private int NextPhase(int index, int phase, TutorialLessonObservation facts) => Progress.CourseIndex switch
    {
        0 => TutorialFirstLessonRules.NextPhase(index, phase, facts),
        1 => TutorialSecondLessonRules.NextPhase(index, phase, SecondObservation(facts)),
        2 => TutorialThirdLessonRules.NextPhase(index, phase, ThirdObservation(facts)),
        3 => TutorialFourthLessonRules.NextPhase(index, phase, FourthObservation(facts)),
        4 => TutorialFifthLessonRules.NextPhase(index, phase, FifthObservation(facts)),
        5 => TutorialSixthLessonRules.NextPhase(index, phase, SixthObservation(facts)),
        6 => TutorialSeventhLessonRules.NextPhase(index, phase, SeventhObservation(facts)),
        7 => TutorialEighthLessonRules.NextPhase(index, phase, EighthObservation(facts)),
        _ => throw Missing("supported_lesson")
    };
    private bool CanComplete(int index, int phase, TutorialLessonObservation facts) => Progress.CourseIndex switch
    {
        0 => TutorialFirstLessonRules.CanComplete(index, phase, facts),
        1 => TutorialSecondLessonRules.CanComplete(index, phase, SecondObservation(facts)),
        2 => TutorialThirdLessonRules.CanComplete(index, phase, ThirdObservation(facts)),
        3 => TutorialFourthLessonRules.CanComplete(index, phase, FourthObservation(facts)),
        4 => TutorialFifthLessonRules.CanComplete(index, phase, FifthObservation(facts)),
        5 => TutorialSixthLessonRules.CanComplete(index, phase, SixthObservation(facts)),
        6 => TutorialSeventhLessonRules.CanComplete(index, phase, SeventhObservation(facts)),
        7 => TutorialEighthLessonRules.CanComplete(index, phase, EighthObservation(facts)),
        _ => false
    };
    private int NextStage(int index, TutorialLessonObservation facts) => Progress.CourseIndex switch
    {
        0 => TutorialFirstLessonRules.NextStage(index),
        1 => TutorialSecondLessonRules.NextStage(index),
        2 => TutorialThirdLessonRules.NextStage(index, ThirdObservation(facts)),
        3 => TutorialFourthLessonRules.NextStage(index),
        4 => TutorialFifthLessonRules.NextStage(index, FifthObservation(facts)),
        5 => TutorialSixthLessonRules.NextStage(index),
        6 => TutorialSeventhLessonRules.NextStage(index),
        7 => TutorialEighthLessonRules.NextStage(index),
        _ => throw Missing("supported_lesson")
    };
    private bool ShouldRunStories(int index, int phase, TutorialLessonObservation facts) => Progress.CourseIndex switch
    {
        0 => true,
        1 => TutorialSecondLessonPlan.ShouldRunStories(index, phase, SecondObservation(facts)),
        2 => TutorialThirdLessonPlan.ShouldRunStories(index, phase, ThirdObservation(facts)),
        3 => TutorialFourthLessonPlan.ShouldRunStories(index, phase, FourthObservation(facts)),
        4 => TutorialFifthLessonPlan.ShouldRunStories(index, phase, FifthObservation(facts)),
        5 => TutorialSixthLessonPlan.ShouldRunStories(index, phase, SixthObservation(facts)),
        6 => TutorialSeventhLessonPlan.ShouldRunStories(index, phase, SeventhObservation(facts)),
        7 => TutorialEighthLessonPlan.ShouldRunStories(index, phase, EighthObservation(facts)),
        _ => false
    };

    private void ApplyParagraphWorld(int index, int phase)
    {
        var before = World;
        World = Progress.CourseIndex switch
        {
            0 => Rules.ApplyFirstTutorialParagraphWorldCurrent(World, index, phase),
            1 => Rules.ApplySecondTutorialParagraphWorldCurrent(World, index, phase),
            2 => Rules.ApplyThirdTutorialParagraphWorldCurrent(World, index, phase),
            3 => Rules.ApplyFourthTutorialParagraphWorldCurrent(World, index, phase),
            4 => Rules.ApplyFifthTutorialParagraphWorldCurrent(World, index, phase),
            5 => Rules.ApplySixthTutorialParagraphWorldCurrent(World, index, phase),
            6 => Rules.ApplySeventhTutorialParagraphWorldCurrent(World, index, phase),
            7 => Rules.ApplyEighthTutorialParagraphWorldCurrent(World, index, phase),
            _ => throw Missing("supported_lesson")
        };
        // Only source business entry bodies replace the command baseline.
        // Bitmap/filter/callback writes remain small controller overlays, so
        // open deployment drafts retain their real business identity.
        if (phase == 0 && (Progress.CourseIndex == 2 && index is 0 or 1 ||
            Progress.CourseIndex == 3 && index is 0 or 11) && !ReferenceEquals(before, World))
            businessUpdatePending = true;
        if (Progress.CourseIndex is >= 4 and <= 7 && TutorialBusinessChanged(before, World)) businessUpdatePending = true;
    }

    private static bool TutorialBusinessChanged(GameState before, GameState after) =>
        !ReferenceEquals(before.Units, after.Units) || !ReferenceEquals(before.Officers, after.Officers) ||
        !ReferenceEquals(before.Cities, after.Cities) || !ReferenceEquals(before.Gates, after.Gates) ||
        !ReferenceEquals(before.Forces, after.Forces) || !ReferenceEquals(before.Districts, after.Districts) ||
        !ReferenceEquals(before.Areas, after.Areas) || !ReferenceEquals(before.MapObjects, after.MapObjects) ||
        !ReferenceEquals(before.Structures, after.Structures) || !ReferenceEquals(before.ArmyLifecycle, after.ArmyLifecycle) ||
        !ReferenceEquals(before.ResourceState, after.ResourceState) || !ReferenceEquals(before.PersonnelCommands, after.PersonnelCommands) ||
        !ReferenceEquals(before.NativeMapSeason, after.NativeMapSeason) || !ReferenceEquals(before.CampaignEventWorld, after.CampaignEventWorld) ||
        !ReferenceEquals(before.ForceCreditReserves, after.ForceCreditReserves) || !ReferenceEquals(before.LandmarkInvestments, after.LandmarkInvestments) ||
        !ReferenceEquals(before.NativeForceUnregisterCurrent, after.NativeForceUnregisterCurrent) ||
        !ReferenceEquals(before.NativeArmySimulation?.World, after.NativeArmySimulation?.World) ||
        !ReferenceEquals(before.NativeArmySimulation?.Forces, after.NativeArmySimulation?.Forces);

    private void ApplyFourthAttackUpdate(int? actionStackTop)
    {
        var before = World;
        World = Rules.AdvanceFourthTutorialAttackCurrent(World, actionStackTop);
        if (!ReferenceEquals(before.Units, World.Units) || !ReferenceEquals(before.Cities, World.Cities) ||
            !ReferenceEquals(before.Officers, World.Officers) || !ReferenceEquals(before.ResourceState, World.ResourceState) ||
            !ReferenceEquals(before.ArmyLifecycle, World.ArmyLifecycle) ||
            !ReferenceEquals(before.NativeArmySimulation?.World, World.NativeArmySimulation?.World))
            businessUpdatePending = true;
    }

    private TutorialLessonObservation ReadAdditionalObservation(TutorialLessonObservation actual)
    {
        if (Progress.CourseIndex == 2)
        {
            var third = Rules.ReadThirdTutorialObservationCurrent(World, ThirdObservation(actual));
            return actual with { ThirdLesson = third with
                { Common = actual, AreaSupportElapsedMilliseconds = Progress.AreaSupportElapsedMilliseconds } };
        }
        if (Progress.CourseIndex == 3)
        {
            var fourth = FourthObservation(actual) with
            {
                ConstructionExplanationShown = Progress.ActiveParagraphIndex == 9 &&
                    Progress.ParagraphPhases[9] == 4 && Progress.InstructionIndex > 0
            };
            return actual with { FourthLesson = Rules.ReadFourthTutorialObservationCurrent(World, fourth) };
        }
        if (Progress.CourseIndex == 4) return actual with
            { FifthLesson = Rules.ReadFifthTutorialObservationCurrent(World, FifthObservation(actual)) };
        if (Progress.CourseIndex == 5) return actual with
            { SixthLesson = Rules.ReadSixthTutorialObservationCurrent(World, SixthObservation(actual)) };
        if (Progress.CourseIndex == 6) return actual with
            { SeventhLesson = Rules.ReadSeventhTutorialObservationCurrent(World, SeventhObservation(actual)) with
                { HomeCommandElapsedMilliseconds = Progress.HomeCommandElapsedMilliseconds } };
        if (Progress.CourseIndex == 7) return actual with
            { EighthLesson = Rules.ReadEighthTutorialObservationCurrent(World, EighthObservation(actual)) };
        return actual;
    }

    private void UpdateCourseObservation(TutorialLessonObservation observed)
    {
        UpdateHomeCommandObservation(observed);
        if (Progress.CourseIndex != 2 || Progress.ActiveParagraphIndex != 6 || Progress.ParagraphPhases[6] != 1)
        { areaSupportLastMilliseconds = null; return; }
        var third = ThirdObservation(ActualObservation(observed));
        if (third.AreaSupportFocusMatchesArmyHex is not bool matches) throw Missing("area_support_focus");
        uint now = third.CurrentMilliseconds ?? throw Missing("active_source_milliseconds");
        uint? elapsed = matches ? Progress.AreaSupportElapsedMilliseconds ?? 0 : null;
        if (matches && areaSupportLastMilliseconds is uint last && Progress.AreaSupportElapsedMilliseconds is uint prior)
            elapsed = (uint)Math.Min(uint.MaxValue, (ulong)prior + unchecked(now - last));
        areaSupportLastMilliseconds = now;
        if (elapsed != Progress.AreaSupportElapsedMilliseconds)
            Store(Progress with { AreaSupportElapsedMilliseconds = elapsed });
    }

    private void UpdateHomeCommandObservation(TutorialLessonObservation observed)
    {
        if (Progress.CourseIndex != 6 || Progress.ActiveParagraphIndex != 1 || Progress.ParagraphPhases[1] != 1)
        { homeCommandLastMilliseconds = null; return; }
        //54FC40's accepted information check precedes the optional timer path.
        if (observed.LastInformationId == 29) return;
        var seventh = SeventhObservation(ActualObservation(observed));
        uint? elapsed = Progress.HomeCommandElapsedMilliseconds;
        if (elapsed is null && seventh.StrategyControlReady != true)
        { homeCommandLastMilliseconds = null; return; }
        uint now = seventh.CurrentMilliseconds ?? throw Missing("active_source_milliseconds");
        elapsed ??= 0;
        if (homeCommandLastMilliseconds is uint last && Progress.HomeCommandElapsedMilliseconds is uint prior)
            elapsed = (uint)Math.Min(uint.MaxValue, (ulong)prior + unchecked(now - last));
        homeCommandLastMilliseconds = now;
        if (elapsed != Progress.HomeCommandElapsedMilliseconds)
            Store(Progress with { HomeCommandElapsedMilliseconds = elapsed });
    }

    private bool IsSynchronousSetup(int index, int phase) => Progress.CourseIndex switch
    {
        0 => phase == 0 && index is 2 or 6 or 11 or 13 or 16 or 18,
        1 => phase == 0 && index is 1 or 4 or 7 or 9,
        2 => phase == 0 && index is 2 or 4 or 5 or 8 or 10 or 12,
        3 => phase == 0 && index is 1 or 6 or 11 or 13 or 15 || index == 9 && phase == 2,
        4 => phase == 0 && index is 2 or 4 or 6 or 13 or 14 or 15 or 20 or 31 ||
            index is 21 or 22 or 23 && phase is 0 or 1 or 3 || index == 27 && phase == 0,
        5 => TutorialSixthLessonPlan.IsSynchronousSetup(index, phase) && !(index is 20 or 21 && phase == 1),
        6 => TutorialSeventhLessonPlan.IsSynchronousSetup(index, phase),
        7 => TutorialEighthLessonPlan.IsSynchronousSetup(index, phase),
        _ => false
    };

    private bool HasTacticCue(int index, int phase) => Progress.CourseIndex == 2 && index == 3 && phase == 10;
    private bool HasEnemyFocusCue(int index, int phase) => Progress.CourseIndex == 2 && index == 1 && phase == 0;
    private bool HasSixthOpeningCue(int index, int phase) => Progress.CourseIndex == 5 && index == 0 && phase == 0;
    private bool HasGoodwillFocusCue(int index, int phase) => Progress.CourseIndex == 5 && index == 3 && phase == 0;
    private bool HasSeventhFocusCue(int index, int phase) => Progress.CourseIndex == 6 &&
        (index == 23 && phase == 1 || index == 31 && phase == 0);
    private int InstructionCount(int index, int phase, TutorialParagraphPlan plan)
        => HasTacticCue(index, phase) ? 4 : HasEnemyFocusCue(index, phase) || HasSixthOpeningCue(index, phase) ? 3 :
            HasGoodwillFocusCue(index, phase) || HasSeventhFocusCue(index, phase) ? 2 : plan.StoryEventIds.Count;
    private bool IsInstructionCue(int index, int phase, int instruction)
        => HasTacticCue(index, phase) && instruction is 1 or 3 ||
            (HasEnemyFocusCue(index, phase) || HasSixthOpeningCue(index, phase)) && instruction == 1 ||
            HasGoodwillFocusCue(index, phase) && instruction == 0 ||
            Progress.CourseIndex == 6 && TutorialSeventhLessonPlan.IsInstructionCue(index, phase, instruction);
    private int StoryEventId(int index, int phase, TutorialParagraphPlan plan, int instruction)
        => plan.StoryEventIds[HasGoodwillFocusCue(index, phase) || Progress.CourseIndex == 6 && index == 23 && phase == 1 ? instruction - 1 :
            HasTacticCue(index, phase) || HasEnemyFocusCue(index, phase) || HasSixthOpeningCue(index, phase) ? instruction / 2 : instruction];
    private void PublishInstructionCue(int index, int phase)
    {
        if (CurrentRequest is { Kind: TutorialLessonRequestKind.InstructionCue } active &&
            active.Sequence == Progress.Sequence && active.ParagraphIndex == index && active.Phase == phase) return;
        var cue = HasSeventhFocusCue(index, phase) ? Rules.QuerySeventhTutorialInstructionCueCurrent(World, index, phase) :
            HasSixthOpeningCue(index, phase) || HasGoodwillFocusCue(index, phase)
            ? new TutorialLessonInstructionCue(null, false)
                { CameraFocusHexId = Rules.QuerySixthTutorialCityHex(World, HasSixthOpeningCue(index, phase) ? 47 : 7), DurationMilliseconds = 1000 }
            : HasEnemyFocusCue(index, phase)
            ? new TutorialLessonInstructionCue(null, false)
                { CameraFocusHexId = Rules.QueryThirdTutorialAreaHex(World, 44), DurationMilliseconds = 1000 }
            : new TutorialLessonInstructionCue(Progress.InstructionIndex == 1 ? 92 : null, Progress.InstructionIndex == 1);
        if (CurrentRequest is { Kind: TutorialLessonRequestKind.InstructionCue } prior &&
            prior.Sequence == Progress.Sequence && prior.InstructionCue == cue) return;
        CurrentRequest = new(Progress.Sequence, index, phase, TutorialLessonRequestKind.InstructionCue)
            { InstructionCue = cue };
    }
    public bool CompleteInstructionCue(TutorialLessonRequest expectedRequest)
    {
        if (!ReferenceEquals(CurrentRequest, expectedRequest) || expectedRequest.Kind != TutorialLessonRequestKind.InstructionCue ||
            Progress.ActiveParagraphIndex != expectedRequest.ParagraphIndex || Progress.Story is not null ||
            Progress.ParagraphPhases[expectedRequest.ParagraphIndex] != expectedRequest.Phase ||
            !IsInstructionCue(expectedRequest.ParagraphIndex, expectedRequest.Phase, Progress.InstructionIndex)) return false;
        Store(Progress with { InstructionIndex = Progress.InstructionIndex + 1 });
        CurrentRequest = null;
        return true;
    }

    private TutorialLessonGuidance AdaptGuidance(TutorialLessonGuidance guidance, int index, int phase)
    {
        if (Progress.CourseIndex < 2) return guidance;
        if (Progress.CourseIndex == 2) return guidance with
        {
            OfficerId = index == 3 && phase == 6 ? Rules.ReadTutorialPhysicalOfficerIdentityCurrent(World, 656) : null,
            RequiredFormationId = index == 3 && phase == 8 ? 2 : null,
            AllowsPeriodAdvance = index == 5 && phase == 4 || index == 8 && phase == 2,
            AllowsDeploymentSetupConfirmation = index == 3 && phase == 11,
            AllowsDeploymentTargetConfirmation = index == 5 && phase == 3,
            BlocksOrdinaryCommands = index is 0 or 1 or 11
        };
        if (Progress.CourseIndex == 4) return AdaptFifthGuidance(guidance, index, phase);
        if (Progress.CourseIndex == 5) return AdaptSixthGuidance(guidance, index, phase);
        if (Progress.CourseIndex == 6) return AdaptSeventhGuidance(guidance, index, phase);
        if (Progress.CourseIndex == 7) return AdaptEighthGuidance(guidance, index, phase);
        var fourth = lastUiObservation?.FourthLesson;
        return guidance with
        {
            OfficerId = null,
            AllowsPeriodAdvance = index == 6 && phase == 1 || index == 9 && phase == 7 ||
                index == 10 && phase == 0 || index == 11 && phase == 1 || index == 12 && phase is 3 or 4 ||
                index == 13 && phase is 1 or 2,
            AllowsDeploymentSetupConfirmation = index == 9 && phase == 1 &&
                lastUiObservation?.ActionStackTop == 4 && fourth?.DeploymentRowCount == 1,
            AllowsDeploymentTargetConfirmation = index == 9 && phase == 6,
            BlocksOrdinaryCommands = index is 0 or 2 or 8 or 14
        };
    }
}


public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState? tutorialIdentityOwner;
    private SourceWorldContext? tutorialIdentityCurrent;
    internal int ReadTutorialPhysicalOfficerIdentityCurrent(GameState state, int physical)
    {
        if (tutorialIdentityOwner is null || !ReferenceEquals(tutorialIdentityOwner.Officers, state.Officers) ||
            !ReferenceEquals(tutorialIdentityOwner.NativeArmySimulation?.World, state.NativeArmySimulation?.World) ||
            !ReferenceEquals(tutorialIdentityOwner.ResourceState, state.ResourceState))
        {
            tutorialIdentityOwner = state;
            tutorialIdentityCurrent = state.TutorialLessonProgress?.CourseIndex == 2 ? ThirdCapture(state).Current :
                CaptureCurrentDefinitions(state) ?? throw new InvalidDataException("tutorial.lesson.selection_current");
        }
        var current = tutorialIdentityCurrent!;
        int slot = current.ReadTransferPersonTableReference(physical)?.PhysicalSlot
            ?? throw new InvalidDataException("tutorial.lesson.selection_person");
        if (current.ReadPersonNativeValidity(slot) != true) throw new InvalidDataException("tutorial.lesson.selection_validity");
        return current.ReadPersonIdentity(slot) ?? throw new InvalidDataException("tutorial.lesson.selection_identity");
    }
}
