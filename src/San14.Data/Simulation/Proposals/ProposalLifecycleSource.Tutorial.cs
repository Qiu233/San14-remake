using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    /// <summary>527BD0's reached first-course override. The first lesson's
    /// complete paragraph set initializes its ProposalOfficer policy before
    /// stage1; that policy remains until the set is disposed, rather than
    /// turning on when the lesson reaches its proposal paragraph.</summary>
    internal bool TryGenerateTutorialOffer()
    {
        var lesson = State.TutorialLessonProgress ?? throw Missing("tutorial_initialized_paragraph_set");
        if (lesson.CourseIndex != 0) return false;
        if (TacticRemakeCurrentBattleControl.ReadSelectedLessonIndex(State) != lesson.CourseIndex)
            throw Missing("tutorial_selected_paragraph_set");
        if (FreeSlot() is null) return false;
        int person = (Current.ReadTransferPersonTableReference(147) ?? throw Missing("tutorial_proposer_pointer"))
            .PhysicalSlot ?? throw Missing("tutorial_proposer");
        //The source asks2A6C40 for this actual receiver's ordered assignment.
        //No valid assignment returns the null Area and falls through to the
        //ordinary generator; it does not fabricate a target from City7.
        if (!CityAssignedAreas(person).Any()) return false;
        int area = CityAssignedArea(person);
        int force = K(Current.ReadAreaCurrentForceSlot(new(area)), "tutorial_target_Force");
        return StoreOffer(ProposalKind.SecurityUp, 147,
            new(Math.Max(0, BaseCost(ProposalKind.SecurityUp)), 0, Math.Max(0, area), Math.Max(0, force), 0));
    }
}
