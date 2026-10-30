using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private sealed record TutorialDeploymentUiSnapshot(int? CityMenuId,
        bool FormationActive, int? FormationId, DeploymentTroopMarker? TroopMarker,
        bool TargetMenuVisible, bool TargetMatchesCapital, bool MapVisible, bool InstructionOverlay,
        long PersonSelectionChanged, long PersonSelectionAccepted, long FormationAccepted,
        long SettingsChanged, long SetupAccepted, long TargetAssigned, long TargetAccepted);

    private TutorialDeploymentUiSnapshot? CaptureTutorialDeploymentUi(TutorialWorkspace owner)
    {
        if (owner.Lesson.Progress.CourseIndex != 1) return null;
        var make = _deploymentSetupPresenter; var target = _deploymentTargetPresenter;
        bool matches = false;
        if (target?.Rows.FirstOrDefault()?.Request.Actions.FirstOrDefault() is
            { ActionKey: "move", Target.Kind: DeploymentTargetKind.Hex } action)
        {
            owner.CapitalHexId ??= owner.Lesson.Rules.QuerySecondTutorialCapitalHex(owner.Lesson.World);
            matches = action.Target.Id == owner.CapitalHexId;
        }
        return new(_residentMap?.CurrentCityCommandMenuId, make?.FormationSelectionActive == true,
            make?.SelectedFormationId, make?.TroopMarker, target?.IsTargetMenuOpen == true, matches,
            _residentMap is { IsPlayableMapReady: true } map && map.IsVisibleInTree(), owner.Story is not null,
            make?.OfficerSelectionChangedSequence ?? 0, make?.OfficerSelectionAcceptedSequence ?? 0,
            make?.LandFormationAcceptedSequence ?? 0, make?.SettingsChangedSequence ?? 0,
            make?.SetupAcceptedSequence ?? 0, target?.TargetActionAcceptedSequence ?? 0,
            target?.TargetDecisionAcceptedSequence ?? 0);
    }

    private TutorialLessonObservation CaptureTutorialObservation(TutorialWorkspace owner, TutorialUiSnapshot ui)
    {
        var common = new TutorialLessonObservation(true, owner.Lesson.Progress.Stage, ui.ActionTop, _ => false)
        {
            DomesticSelectionOpen = ui.DomesticSelectionOpen,
            CityFirstSelectorVisible = ui.CityFirstSelectorVisible,
            CitySecondSelectorVisible = ui.CitySecondSelectorVisible,
            ProposalDetailVisible = ui.ProposalDetailVisible, ProposalListVisible = ui.ProposalListVisible,
            LastCommandId = ui.LastCommandId, LastInformationId = ui.LastInformationId
        };
        if (ui.Deployment is not { } deployment) return CaptureTutorialFinalObservation(owner,
            CaptureTutorialMenuObservation(owner, CaptureTutorialAdvancedObservation(owner, common, ui.Advanced), ui.Menus), ui.FinalCourses);
        return CaptureTutorialFinalObservation(owner, CaptureTutorialMenuObservation(owner, CaptureTutorialAdvancedObservation(owner, common with { SecondLesson = new(common)
        {
            FormationSelectionActive = deployment.FormationActive,
            SelectedFormationId = deployment.FormationId,
            TroopMarker = deployment.TroopMarker,
            TargetInformationVisible = deployment.TargetMenuVisible,
            TargetOrderMatchesCapital = deployment.TargetMatchesCapital,
            LessonMapStateVisible = deployment.MapVisible,
            InstructionOverlayActive = deployment.InstructionOverlay
        } }, ui.Advanced), ui.Menus), ui.FinalCourses);
    }

    private void ObserveTutorialDeploymentSelections(TutorialWorkspace owner)
    {
        if (owner.Lesson.Progress.CourseIndex is < 1 or > 6) return;
        if (!ReferenceEquals(owner.DeploymentSetup, _deploymentSetupPresenter))
        {
            if (owner.DeploymentSetup is { } old) ReadMake(old);
            owner.DeploymentSetup = _deploymentSetupPresenter;
            owner.DeploymentOfficersAccepted = owner.DeploymentSetupAccepted = owner.DeploymentSelectionChanged = 0;
            owner.DeploymentSelectedOfficers = [];
            BindTutorialDeployment(owner);
        }
        if (_deploymentSetupPresenter is { } make) ReadMake(make);
        if (!ReferenceEquals(owner.DeploymentTarget, _deploymentTargetPresenter))
        {
            if (owner.DeploymentTarget is { } old) ReadTarget(old);
            owner.DeploymentTarget = _deploymentTargetPresenter;
            owner.DeploymentTargetAccepted = 0;
            BindTutorialDeployment(owner);
        }
        if (_deploymentTargetPresenter is { } target) ReadTarget(target);

        void ReadMake(DeploymentSetupPresenter source)
        {
            if (owner.Lesson.Progress.CourseIndex == 1 && owner.Lesson.Progress.ActiveParagraphIndex == 4 &&
                source.OfficerSelectionChangedSequence != owner.DeploymentSelectionChanged)
            {
                owner.DeploymentSelectionChanged = source.OfficerSelectionChangedSequence;
                var selected = source.OfficerSelectionSelectedOfficerIds;
                owner.Lesson.ObserveDeploymentOfficerSelectionChanged(owner.DeploymentSelectedOfficers, selected);
                owner.DeploymentSelectedOfficers = selected;
                owner.LastProgress = null;
            }
            if (source.OfficerSelectionAcceptedSequence != owner.DeploymentOfficersAccepted)
            {
                owner.DeploymentOfficersAccepted = source.OfficerSelectionAcceptedSequence;
                if (owner.Lesson.Progress.CourseIndex >= 2 || owner.Lesson.CurrentRequest?.Guidance?.OfficerId is not null)
                    owner.Lesson.ObserveDeploymentOfficerSelection(source.LastAcceptedOfficerIds);
                owner.LastProgress = null;
            }
            if (source.SetupAcceptedSequence != owner.DeploymentSetupAccepted)
            {
                owner.DeploymentSetupAccepted = source.SetupAcceptedSequence;
                owner.Lesson.ObserveAction(TutorialLessonAction.DeploymentAccepted);
                owner.LastProgress = null;
            }
        }
        void ReadTarget(DeploymentTargetPresenter source)
        {
            if (source.TargetDecisionAcceptedSequence == owner.DeploymentTargetAccepted) return;
            owner.DeploymentTargetAccepted = source.TargetDecisionAcceptedSequence;
            owner.Lesson.ObserveDeploymentTargetDecision(source.LastAcceptedRows);
            owner.LastProgress = null;
        }
    }

    private void BindTutorialDeployment(TutorialWorkspace owner)
    {
        if (owner.Lesson.Progress.CourseIndex is < 1 or > 6) return;
        if (_deploymentSetupPresenter is { } make)
        {
            make.OfficerSelectionAdmission = ids => IsTutorialLessonCurrent(owner) &&
                !TutorialOverlayOwnsInput && (owner.Lesson.Progress.CourseIndex == 1 &&
                    owner.Lesson.CurrentRequest?.Guidance?.OfficerId is null ||
                    owner.Lesson.CanAcceptDeploymentOfficerSelection(ids));
            make.DecisionAdmission = _ => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
                owner.Lesson.CurrentRequest?.Guidance?.AllowsDeploymentSetupConfirmation == true;
        }
        if (_deploymentTargetPresenter is { } target)
            target.DecisionAdmission = _ => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
                owner.Lesson.CurrentRequest?.Guidance?.AllowsDeploymentTargetConfirmation == true;
    }

    private void BindCurrentTutorialDeployment()
    {
        if (_tutorialLesson is { } owner) BindTutorialDeployment(owner);
    }

    private void AdoptTutorialBusinessUpdate(TutorialWorkspace owner)
    {
        if (owner.Lesson.PendingBusinessWorldUpdate is not { } prepared) return;
        // Only an explicit lesson writer enters here. Text, cameras and bitmap
        // updates retain the business identity used by ordinary open drafts.
        if (!owner.Session.TryAdoptTutorialBusinessWorld(owner.Lesson.BusinessWorld, prepared))
            throw new InvalidDataException("tutorial.main.business_update_owner");
        owner.LastBusinessWorld = null;
        RefreshCurrentPlayWorld();
    }
}
