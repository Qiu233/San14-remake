using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    // These are observations of the current, bounded command drafts. They do
    // not allocate units or infer acceptance from a dialog being open.
    private sealed record TutorialAdvancedUiSnapshot(
        bool AutomaticMenuVisible, int SummaryCount, int TargetSelectionCount,
        bool OfficerSelectorVisible, int? FocusedOfficer, bool ConfigurationPresent,
        bool ConfigurationActive, int? FormationId, bool KanuSelected, bool ChouhiSelected,
        DeploymentDraftRow? KanuRow, DeploymentDraftRow? ChouhiRow,
        int? AdvisorId, bool AdvisorSelectorVisible, bool GovernmentEffectVisible,
        long AdvisorAccepted, long AutoPolicyAccepted,
        bool FacilityVisible, int? FacilityDefinitionId, bool PlacementActive,
        DeploymentDraftRow? ConstructionRow, int? ConstructionHex,
        uint? SourceMilliseconds, int? CameraFocusHex);

    private TutorialAdvancedUiSnapshot? CaptureTutorialAdvancedUi(TutorialWorkspace owner)
    {
        int course = owner.Lesson.Progress.CourseIndex;
        if (course is not (2 or 3)) return null;
        var make = _deploymentSetupPresenter;
        var target = _deploymentTargetPresenter;
        var rows = target?.Rows ?? make?.Rows ?? [];
        var government = _governanceRosterPage;
        bool governmentVisible = government?.IsVisibleInTree() == true;
        bool facilityVisible = target?.IsFacilitySelectionOpen == true ||
            _unitFacilitySelection?.IsFacilitySelectionOpen == true;
        int? definition = target?.CurrentChoiceDefinitionKey ?? _unitFacilitySelection?.CurrentChoiceDefinitionKey;
        var construction = target?.Rows.FirstOrDefault();
        int? constructionHex = construction?.Request.Actions.FirstOrDefault()?.ClickedHexId;
        uint? milliseconds = null; int? focusHex = null;
        if (course == 2 && owner.Lesson.Progress.ActiveParagraphIndex == 6 &&
            owner.Lesson.Progress.ParagraphPhases[6] == 1)
        {
            milliseconds = owner.PeriodBorrowed ? owner.SourceMilliseconds : _residentMap?.SourceActiveMilliseconds;
            if (_residentMap?.TryReadSourceFocusHex(out int actualFocus) == true) focusHex = actualFocus;
        }
        return new(make?.IsAutomaticMenuOpen == true, rows.Count,
            target?.SelectedOfficerIds.Count ?? 0, make?.IsOfficerSelectionOpen == true,
            make?.FocusedOfficerId, make?.UnitConfigurationPresent == true,
            make?.UnitConfigurationActive == true, make?.SelectedFormationId,
            target?.SelectedOfficerIds.Contains(147) == true, target?.SelectedOfficerIds.Contains(656) == true,
            rows.FirstOrDefault(row => row.Request.OfficerId == 147),
            rows.FirstOrDefault(row => row.Request.OfficerId == 656),
            governmentVisible ? government!.SelectedAdvisorId : null,
            governmentVisible && government!.AdvisorSelectorVisible,
            governmentVisible && government!.EffectDialogVisible,
            government?.AdvisorSelectionSequence ?? 0, government?.AutoPolicySelectionSequence ?? 0,
            facilityVisible, definition, target?.IsFacilityPlacementActive == true ||
                _unitFacilitySelection?.IsFacilityPlacementActive == true,
            construction, constructionHex, milliseconds, focusHex);
    }

    private TutorialLessonObservation CaptureTutorialAdvancedObservation(TutorialWorkspace owner,
        TutorialLessonObservation common, TutorialAdvancedUiSnapshot? ui)
    {
        if (ui is null) return common;
        if (owner.Lesson.Progress.CourseIndex == 2)
            return common with { ThirdLesson = new(common)
            {
                UnitActionDropdownVisible = ui.AutomaticMenuVisible,
                SummaryCount = ui.SummaryCount, TargetSelectionCount = ui.TargetSelectionCount,
                OfficerSelectionVisible = ui.OfficerSelectorVisible,
                SelectedSummaryOfficerId = ui.FocusedOfficer,
                SelectedSummaryPresent = ui.FocusedOfficer is not null,
                UnitConfigurationPresent = ui.ConfigurationPresent,
                UnitConfigurationActive = ui.ConfigurationActive,
                SelectedLandFormationId = ui.FormationId,
                KanuSummaryActive = ui.KanuSelected, ChouhiSummaryActive = ui.ChouhiSelected,
                KanuSummaryOrderKind = TutorialThirdLessonPlan.ReadDraftOrderKind(ui.KanuRow),
                ChouhiSummaryOrderKind = TutorialThirdLessonPlan.ReadDraftOrderKind(ui.ChouhiRow),
                CurrentMilliseconds = ui.SourceMilliseconds,
                CameraFocusHexId = ui.CameraFocusHex,
                ProgressStateExists = owner.PeriodBorrowed && _advancingPeriod
            } };
        return common with { FourthLesson = new(common)
        {
            SelectedStrategistId = ui.AdvisorId,
            GovernmentEffectDialogVisible = ui.GovernmentEffectVisible,
            ConstructionFacilityDialogVisible = ui.FacilityVisible,
            ConstructionFacilityId = ui.FacilityDefinitionId is int definition
                ? owner.Lesson.Rules.ReadFourthTutorialFacilityKind(owner.Lesson.World, definition) : null,
            ConstructionInstructionKind = TutorialFourthLessonPlan.ReadDraftOrderKind(ui.ConstructionRow),
            ConstructionCommanderId = ui.ConstructionRow?.Request.OfficerId,
            ConstructionTargetHex = ui.ConstructionHex,
            DeploymentRowCount = ui.SummaryCount,
            DeploymentSelectorMemberCount = _deploymentSetupPresenter?.OfficerSelectionSelectedOfficerIds.Count ?? 0
        } };
    }

    private void ObserveTutorialAdvancedSelections(TutorialWorkspace owner)
    {
        if (owner.Lesson.Progress.CourseIndex != 3) return;
        if (!ReferenceEquals(owner.GovernancePage, _governanceRosterPage))
        {
            if (owner.GovernancePage is { } old) ReadGovernment(old);
            owner.GovernancePage = _governanceRosterPage;
            owner.GovernanceAdvisorAccepted = owner.GovernanceAutoAccepted = 0;
        }
        if (_governanceRosterPage is { } government) ReadGovernment(government);
        void ReadGovernment(GovernanceRosterPage page)
        {
            if (owner.GovernanceAdvisorAccepted != page.AdvisorSelectionSequence)
            {
                owner.GovernanceAdvisorAccepted = page.AdvisorSelectionSequence;
                owner.LastUi = null;
            }
            if (owner.GovernanceAutoAccepted != page.AutoPolicySelectionSequence)
            {
                owner.GovernanceAutoAccepted = page.AutoPolicySelectionSequence;
                owner.Lesson.ObserveAction(TutorialLessonAction.GovernmentAutoPolicySelected);
                owner.LastUi = null;
            }
        }
    }
}
