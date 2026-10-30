using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private sealed record TutorialMenuUiSnapshot(int? ActionTop,
        bool AllianceForcePresent, bool AllianceOfficerPresent,
        bool ConstructionOfficerActive, bool ConstructionSummarySelected, bool FacilityActive, bool ConstructionDeploymentReady,
        int? Order0, int? Order1, int? Order2, int? CityInformationAction,
        int? MapDisplayMode, long DiplomacyAccepted, long StrategicAccepted,
        long CountryAccepted, long PersonAccepted, long ForeignAccepted);

    private TutorialMenuUiSnapshot? CaptureTutorialMenus(TutorialWorkspace owner)
    {
        int course = owner.Lesson.Progress.CourseIndex;
        if (course is not (4 or 5 or 6)) return null;
        int? top = null;
        bool force = _diplomacyTargets is { } targetList && targetList.IsVisibleInTree() &&
            _diplomacySelectionPurpose == DiplomacySelectionPurpose.Force;
        bool officer = _diplomacyExecutors?.IsVisibleInTree() == true ||
            _diplomacyPersons is { } personList && personList.IsVisibleInTree();
        if (IsDiplomacyOrdersCurrent)
        {
            top = _diplomacySelectionPurpose switch
            {
                DiplomacySelectionPurpose.Force or DiplomacySelectionPurpose.City when force => 24,
                DiplomacySelectionPurpose.Executor when officer => 25,
                DiplomacySelectionPurpose.Months when _diplomacyMonths?.IsVisibleInTree() == true => 26,
                DiplomacySelectionPurpose.GiftArea when _diplomacyTargets?.IsVisibleInTree() == true => 27,
                DiplomacySelectionPurpose.Gold when _diplomacyAmounts is not null => 28,
                DiplomacySelectionPurpose.GiftItem when _diplomacyTargets?.IsVisibleInTree() == true => 29,
                DiplomacySelectionPurpose.Supplies when _diplomacyAmounts is not null => 30,
                _ => _diplomacyDraft!.Kind == DiplomaticMissionKind.Alliance ? 23 :
                    _diplomacyDraft.Kind == DiplomaticMissionKind.Goodwill ? 37 : 0
            };
        }
        if (_strategicOrders is { } strategic && strategic.Draft.Kind is 14 or 20 && IsStrategicOrdersCurrent(strategic))
            top = strategic.Selection is { } selection && selection.IsVisibleInTree()
                ? strategic.SelectionPurpose == StrategicSelectionPurpose.Executor ? 34 : 33
                : strategic.Draft.Kind == 14 ? 49 : 32;
        else if (_strategicOrders is { } other && IsStrategicOrdersCurrent(other)) top = 0;
        if (ForeignTradeCurrentPage is { } foreignPage) top = (int)foreignPage;
        if (_detailsView is CityDetailsView city && city.IsVisibleInTree()) top = 31;
        // A reached ordinary result/decision is not the strategy parent. Wait
        // for its real retirement rather than treating an unrelated modal as
        // top1 merely because none of this course's editors is open.
        if (HasPersonnelPresentation || HasPersonnelConfirmation || HasForcePresentation || HasForeignTradeReport) top = 0;
        if (top is null && owner.Session.World.Phase != TurnPhase.Strategy &&
            !HasActionReport && !HasIncomeReport) top = 0;
        var make = _deploymentSetupPresenter;
        var target = _deploymentTargetPresenter;
        var rows = target?.Rows ?? make?.Rows ?? [];
        int? slot = owner.Lesson.CurrentRequest?.Guidance?.ConstructionSummaryIndex;
        var selectedSummary = slot is >= 0 && slot < rows.Count ? rows[slot.Value] : null;
        return new(top, force, officer,
            make?.OfficerSelectionActive == true,
            selectedSummary is not null && target?.SelectedOfficerIds.Contains(selectedSummary.Request.OfficerId) == true,
            target?.FacilitySelectionActive == true || _unitFacilitySelection?.FacilitySelectionActive == true,
            rows.Count == 3 && rows.All(row => row.Request.OfficerId > 0),
            TutorialFourthLessonPlan.ReadDraftOrderKind(rows.ElementAtOrDefault(0)),
            TutorialFourthLessonPlan.ReadDraftOrderKind(rows.ElementAtOrDefault(1)),
            TutorialFourthLessonPlan.ReadDraftOrderKind(rows.ElementAtOrDefault(2)),
            owner.LastCityInformationActionId, _residentMap?.UserDisplayMode,
            _diplomacySelectionAcceptedSequence, _strategicOrders?.SelectionAcceptedSequence ?? 0,
            ForeignTradeCountryAcceptedSequence, ForeignTradePersonAcceptedSequence, ForeignTradeAcceptedSequence);
    }

    private TutorialLessonObservation CaptureTutorialMenuObservation(TutorialWorkspace owner,
        TutorialLessonObservation common, TutorialMenuUiSnapshot? ui)
    {
        if (ui is null) return common;
        return owner.Lesson.Progress.CourseIndex == 4
            ? common with { FifthLesson = new(common)
            {
                AllianceForceSelectionPresent = ui.AllianceForcePresent,
                AllianceOfficerSelectionPresent = ui.AllianceOfficerPresent,
                ConstructionOfficerSelectionActive = ui.ConstructionOfficerActive,
                SelectedConstructionSummaryActive = ui.ConstructionSummarySelected,
                FacilitySelectionActive = ui.FacilityActive,
                ConstructionDeploymentReady = ui.ConstructionDeploymentReady,
                ConstructionSummaryOrderKinds = new int?[] { ui.Order0, ui.Order1, ui.Order2 }
            } }
            : owner.Lesson.Progress.CourseIndex == 5 ? common with { SixthLesson = new(common)
            {
                LastCityInformationActionId = ui.CityInformationAction,
                MapDisplayMode = ui.MapDisplayMode
            } } : common;
    }

    private void ObserveTutorialMenuSelections(TutorialWorkspace owner)
    {
        if (owner.Lesson.Progress.CourseIndex is not (4 or 5 or 6)) return;
        if (owner.DiplomacyAccepted != _diplomacySelectionAcceptedSequence)
        {
            owner.DiplomacyAccepted = _diplomacySelectionAcceptedSequence;
            TutorialLessonAction? action = _lastDiplomacyAcceptedPurpose switch
            {
                DiplomacySelectionPurpose.Executor when _lastDiplomacyAcceptedMissionKind is
                    DiplomaticMissionKind.Alliance or DiplomaticMissionKind.Goodwill => TutorialLessonAction.DiplomacyOfficerSelected,
                DiplomacySelectionPurpose.Months when _lastDiplomacyAcceptedMissionKind == DiplomaticMissionKind.Alliance
                    => TutorialLessonAction.AllianceTermSelected,
                DiplomacySelectionPurpose.Gold => TutorialLessonAction.DiplomacyGoldSelected,
                DiplomacySelectionPurpose.GiftItem => TutorialLessonAction.DiplomacyItemSelected,
                DiplomacySelectionPurpose.Supplies => TutorialLessonAction.DiplomacySuppliesSelected,
                DiplomacySelectionPurpose.GiftArea when _lastDiplomacyAcceptedMissionKind == DiplomaticMissionKind.Alliance
                    => TutorialLessonAction.AllianceAreaSelected,
                _ => null
            };
            if (action is { } accepted) owner.Lesson.ObserveAction(accepted);
            owner.LastUi = null;
        }
        if (!ReferenceEquals(owner.StrategicPage, _strategicOrders))
        {
            if (owner.StrategicPage is { } previous) ReadStrategic(previous);
            owner.StrategicPage = _strategicOrders;
            owner.StrategicAccepted = 0;
        }
        if (_strategicOrders is { } strategic) ReadStrategic(strategic);
        if (owner.ForeignPersonAccepted != ForeignTradePersonAcceptedSequence)
        {
            owner.ForeignPersonAccepted = ForeignTradePersonAcceptedSequence;
            owner.Lesson.ObserveAction(TutorialLessonAction.ForeignTradeOfficerSelected);
            owner.LastUi = null;
        }
        if (owner.ForeignCountryAccepted != ForeignTradeCountryAcceptedSequence)
        {
            // The actual page transition is observed independently. No event
            // number is inferred for the country choice.
            owner.ForeignCountryAccepted = ForeignTradeCountryAcceptedSequence;
            owner.LastUi = null;
        }
        if (owner.ForeignAccepted != ForeignTradeAcceptedSequence)
        {
            owner.ForeignAccepted = ForeignTradeAcceptedSequence;
            owner.Lesson.ObserveAction(TutorialLessonAction.ForeignTradeConfirmed);
            owner.LastUi = null;
        }
        void ReadStrategic(StrategicOrdersWorkspace page)
        {
            if (page.Draft.Kind is not (14 or 20) || owner.StrategicAccepted == page.SelectionAcceptedSequence) return;
            owner.StrategicAccepted = page.SelectionAcceptedSequence;
            if (page.LastAcceptedPurpose is StrategicSelectionPurpose.Target or StrategicSelectionPurpose.Executor)
                owner.Lesson.ObserveAction(page.Draft.Kind == 14
                    ? page.LastAcceptedPurpose == StrategicSelectionPurpose.Target
                        ? TutorialLessonAction.PlacateAreaSelected : TutorialLessonAction.PlacateOfficerSelected
                    : page.LastAcceptedPurpose == StrategicSelectionPurpose.Target
                        ? TutorialLessonAction.SabotageTargetSelected : TutorialLessonAction.SabotageOfficerSelected);
            owner.LastUi = null;
        }
    }

    private void ObserveTutorialMenuOrderAccepted(TutorialWorkspace owner, GameOrder order)
    {
        if (owner.Lesson.Progress.CourseIndex is not (4 or 5 or 6)) return;
        if (order is PrepareDiplomaticOrder diplomacy)
        {
            if (diplomacy.Kind == DiplomaticMissionKind.Alliance)
                owner.Lesson.ObserveAction(TutorialLessonAction.AllianceConfirmed);
            else if (diplomacy.Kind == DiplomaticMissionKind.Goodwill)
                owner.Lesson.ObserveAction(TutorialLessonAction.GoodwillConfirmed);
        }
        else if (order is IssueStrategicCommandOrder { Kind: 20 })
            owner.Lesson.ObserveAction(TutorialLessonAction.SabotageConfirmed);
        else if (order is IssueStrategicCommandOrder { Kind: 14 })
            owner.Lesson.ObserveAction(TutorialLessonAction.PlacateConfirmed);
    }

    private bool TutorialCityInformationAllowed(int cityId) => _tutorialLesson is not { } owner ||
        owner.Lesson.CurrentRequest?.Guidance is not { RequiredCityInformationActionId: 2, CityId: int required } ||
        cityId == required;

    private void ObserveTutorialCityInformationAction(int actionId)
    {
        if (_tutorialLesson is not { } owner) return;
        owner.LastCityInformationActionId = actionId;
        owner.LastUi = null;
    }

    private void ObserveTutorialAdvantageEntered()
    {
        if (_tutorialLesson is not { } owner) return;
        owner.Lesson.ObserveAdvantageEntered();
        owner.LastUi = null;
    }

    private void ObserveTutorialAdvantageExited()
    {
        if (_tutorialLesson is not { } owner) return;
        owner.Lesson.ObserveAction(TutorialLessonAction.GeographicAdvantageExited);
        owner.LastUi = null;
    }

    private void BindCurrentTutorialForeignTrade()
    {
        if (_tutorialLesson is not { } owner)
        {
            ForeignTradeCountryAdmission = ForeignTradePersonAdmission = null;
            ForeignTradeCountryReturnAllowed = ForeignTradePersonReturnAllowed = true;
            RefreshForeignTradeAdmission();
            return;
        }
        var guidance = owner.Lesson.CurrentRequest?.Guidance;
        if (ReferenceEquals(owner.ForeignAdmissionGuidance, guidance) &&
            ReferenceEquals(owner.ForeignAdmissionSelector, _foreignTradeSelector) &&
            ReferenceEquals(owner.ForeignAdmissionView, _foreignTradeView)) return;
        owner.ForeignAdmissionGuidance = guidance;
        owner.ForeignAdmissionSelector = _foreignTradeSelector;
        owner.ForeignAdmissionView = _foreignTradeView;
        ForeignTradeCountryAdmission = ids => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            ids.Count == 1 && owner.Lesson.CanAcceptForeignTradeCountrySelection(ids[0]);
        ForeignTradePersonAdmission = ids => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            owner.Lesson.CanAcceptForeignTradePersonSelection(ids);
        ForeignTradeCountryReturnAllowed = ForeignTradePersonReturnAllowed = guidance?.AllowsSelectionReturn ?? true;
        RefreshForeignTradeAdmission();
    }

    private void BindCurrentTutorialDiplomacy()
    {
        if (_tutorialLesson is not { } owner)
        {
            DiplomacyForceAdmission = DiplomacyExecutorAdmission = null;
            DiplomacySelectionReturnAllowed = true;
            RefreshDiplomacySelectionAdmission();
            return;
        }
        var guidance = owner.Lesson.CurrentRequest?.Guidance;
        Control? selector = (Control?)_diplomacyTargets ?? (Control?)_diplomacyExecutors ?? _diplomacyPersons;
        if (ReferenceEquals(owner.DiplomacyAdmissionGuidance, guidance) &&
            ReferenceEquals(owner.DiplomacyAdmissionView, _diplomacyOrdersView) &&
            ReferenceEquals(owner.DiplomacyAdmissionSelector, selector)) return;
        owner.DiplomacyAdmissionGuidance = guidance;
        owner.DiplomacyAdmissionView = _diplomacyOrdersView;
        owner.DiplomacyAdmissionSelector = selector;
        DiplomacyForceAdmission = id => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            _diplomacyDraft is { } draft && owner.Lesson.CanAcceptDiplomacyForceSelection(draft.Kind, id);
        DiplomacyExecutorAdmission = id => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            _diplomacyDraft is { } draft && owner.Lesson.CanAcceptDiplomacyOfficerSelection(draft.Kind, new[] { id });
        DiplomacySelectionReturnAllowed = guidance?.AllowsSelectionReturn ?? true;
        RefreshDiplomacySelectionAdmission();
    }
}
