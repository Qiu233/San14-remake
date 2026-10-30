using San14.Core;

namespace San14.Data;

/// <summary>The installed lesson's finite target qualifications. Fixed map
/// links are read directly; pointer queries never build or scan a Hex list.</summary>
internal static class TutorialTargetQualification
{
    internal static bool AllowsHex(GameState state, SourceWorldContext current, int hexId,
        int? activeWaypointIndex = null)
    {
        if (!Active(state)) return true;
        var progress = Progress(state);
        if (progress.CourseIndex == 0) return true; //No target callback in this course.
        if (progress.CourseIndex == 2) return TutorialThirdLessonTargets.AllowsHex(state, current, hexId, activeWaypointIndex);
        if (progress.CourseIndex == 3) return TutorialFourthLessonTargets.AllowsHex(state, current, hexId);
        if (progress.CourseIndex == 4) return TutorialFifthLessonTargets.AllowsHex(state, current, hexId, activeWaypointIndex);
        if (progress.CourseIndex == 5) return TutorialSixthLessonTargets.AllowsHex(state, current, hexId, activeWaypointIndex);
        if (progress.CourseIndex == 6) return TutorialSeventhLessonTargets.AllowsHex(state, current, hexId, activeWaypointIndex);
        if (progress.CourseIndex == 7) return TutorialEighthLessonTargets.AllowsHex(state, current, hexId, activeWaypointIndex);
        if (progress.CourseIndex != 1) throw Missing("course_target_provider");
        if (progress.ActiveParagraphIndex == 9 && progress.ParagraphPhases[9] <= 2)
        {
            var area = current.ReadAreaTableReference(TutorialSecondLessonPlan.CapitalAreaId) ?? throw Missing("capital_pointer");
            return hexId == (current.ReadAreaHexIndex(area) ?? throw Missing("capital_representative"));
        }
        if (progress.ActiveParagraphIndex == 10 && progress.ParagraphPhases[10] == 1)
        {
            var hex = current.ReadHexTableReference(hexId) ?? throw Missing("candidate_Hex");
            int raw = current.ReadHexAreaSlot(hex) ?? throw Missing("candidate_Area");
            var area = current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw Missing("candidate_Area_pointer");
            return area == current.ReadAreaTableReference(TutorialSecondLessonPlan.CapitalAreaId);
        }
        return true; //545120's actual absent callback branch.
    }

    internal static bool AllowsArmy(GameState state, int physical, byte? action)
    {
        if (!Active(state)) return true;
        var progress = Progress(state);
        if (progress.CourseIndex == 0) return true;
        if (progress.CourseIndex == 2) return TutorialThirdLessonTargets.AllowsArmy(state, physical, action);
        if (progress.CourseIndex == 3) return TutorialFourthLessonTargets.AllowsArmy(state, physical, action);
        if (progress.CourseIndex == 4) return TutorialFifthLessonTargets.AllowsArmy(state, physical, action);
        if (progress.CourseIndex == 5) return TutorialSixthLessonTargets.AllowsArmy(state, physical, action);
        if (progress.CourseIndex == 6) return TutorialSeventhLessonTargets.AllowsArmy(state, physical, action);
        if (progress.CourseIndex == 7) return TutorialEighthLessonTargets.AllowsArmy(state, physical, action);
        if (progress.CourseIndex != 1) throw Missing("course_Army_provider");
        if (progress.ActiveParagraphIndex != 10 || progress.ParagraphPhases[10] != 1) return true;
        int tracked = progress.RestrictedArmyPhysicalSlot ?? throw Missing("tracked_Army_snapshot");
        return physical != tracked || (action ?? throw Missing("tracked_Army_action37")) != 14;
    }

    private static bool Active(GameState state) => (TacticRemakeCurrentBattleControl.ReadHeader(state)
        ?? throw Missing("controller_header")) != 0;
    private static TutorialLessonProgress Progress(GameState state) => state.TutorialLessonProgress
        ?? throw Missing("lesson_progress");
    private static InvalidDataException Missing(string field) => new("tutorial.target_qualification." + field);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    //The actual lesson controller is independent of an open command draft's
    //business baseline. Qualification reads its live small owner, while all
    //ordinary map and Army data remain on that draft's current capture.
    private Func<GameState>? tutorialControlStateReader;
    internal void BindTutorialControlState(Func<GameState> reader) => tutorialControlStateReader = reader;
    internal GameState ReadTutorialControlState(GameState business) => tutorialControlStateReader?.Invoke() ?? business;
}
