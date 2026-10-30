using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private sealed record TutorialFinalUiSnapshot(int? ActionTop, bool? StrategyReady, uint? SourceMilliseconds,
        int CreditCount, bool CreditSelectorActive, bool CreditChoicesSelected,
        DoctrineResearchPageValues? DoctrinePublication, int? DoctrineSelectedSlot,
        bool SearchCityActive, bool SearchOfficerActive, bool ScenerySelectorActive,
        bool InvestmentDialogActive, bool InvestmentControlsReady, int? SceneryAction,
        long IntelligencePerson, long IntelligenceEditor, long IntelligenceAccepted, long SearchAccepted);

    private TutorialFinalUiSnapshot? CaptureTutorialFinalUi(TutorialWorkspace owner)
    {
        int course = owner.Lesson.Progress.CourseIndex;
        if (course is not (6 or 7)) return null;
        int? top = null;
        if (HasIntelligencePage) top = IntelligenceCurrentPage is { } page ? (int)page : 0;
        var credit = _personnelAdministrationPage as PersonnelCreditView;
        bool creditSelector = credit is not null && _personnelAdministrationPeople?.IsVisibleInTree() == true;
        if (credit is { IsRankup: true } && IsPersonnelAdministrationCurrent(credit)) top = creditSelector ? 54 : 53;
        var doctrine = _doctrineResearch;
        var doctrinePage = doctrine is not null && IsDoctrineResearchCurrent(doctrine)
            ? doctrine.View.CurrentPublication : null;
        if (doctrinePage is not null) top = 55;
        if (TalentSearchCurrentPage is int searchTop) top = searchTop;
        if (SceneryHexMenuPresent || SceneryInvestmentSelectorVisible || SceneryInvestmentMainVisible) top = 56;
        bool homeClock = course == 6 && owner.Lesson.Progress.ActiveParagraphIndex == 1 &&
            owner.Lesson.Progress.ParagraphPhases[1] == 1;
        return new(top, _forceHud?.StrategyControlReady,
            homeClock ? owner.PeriodBorrowed ? owner.SourceMilliseconds : _residentMap?.SourceActiveMilliseconds : null,
            _personnelCreditDraft?.SelectedOfficerIds.Count ?? 0, creditSelector,
            creditSelector && _personnelAdministrationPeople!.AreLeadingRowsSelected(3),
            doctrinePage, doctrinePage?.Selected?.LayoutSlot,
            TalentSearchCurrentPage == 51, TalentSearchCurrentPage == 52,
            SceneryInvestmentSelectorVisible,
            SceneryInvestmentMainVisible && SceneryInvestmentButtonsVisible,
            SceneryInvestmentSelectorControlsReady, owner.LastSceneryActionId,
            IntelligencePersonAcceptedSequence, IntelligenceEditorAcceptedSequence,
            IntelligenceAcceptedSequence, TalentSearchAcceptedSequence);
    }

    private TutorialLessonObservation CaptureTutorialFinalObservation(TutorialWorkspace owner,
        TutorialLessonObservation common, TutorialFinalUiSnapshot? ui)
    {
        if (ui is null) return common;
        if (owner.Lesson.Progress.CourseIndex == 6) return common with { SeventhLesson = new(common)
        { StrategyControlReady = ui.StrategyReady, CurrentMilliseconds = ui.SourceMilliseconds } };
        if (!ReferenceEquals(owner.DoctrineObservedPublication, ui.DoctrinePublication))
        {
            owner.DoctrineObservedPublication = ui.DoctrinePublication;
            // The small draft publication changes only after actual editing.
            // This is not recomputed on source-clock pulses or world overlays.
            owner.DoctrineResearchedSlots = ui.DoctrinePublication?.Rows.Where(row => row.Researched)
                .Select(row => row.LayoutSlot).ToHashSet() ?? [];
        }
        return common with { EighthLesson = new(common)
        {
            CreditSelectedOfficerCount = ui.CreditCount,
            CreditOfficerSelectorActive = ui.CreditSelectorActive,
            CreditRequiredChoicesSelected = ui.CreditChoicesSelected,
            ResearchedDoctrineLayoutSlots = owner.DoctrineResearchedSlots,
            SelectedDoctrineLayoutSlot = ui.DoctrineSelectedSlot,
            SearchCitySelectorActive = ui.SearchCityActive, SearchOfficerSelectorActive = ui.SearchOfficerActive,
            ScenerySelectorActive = ui.ScenerySelectorActive,
            SceneryInvestmentDialogActive = ui.InvestmentDialogActive,
            SceneryInvestmentControlsReady = ui.InvestmentControlsReady,
            LastSceneryActionId = ui.SceneryAction
        } };
    }

    private void ObserveTutorialFinalSelections(TutorialWorkspace owner)
    {
        if (owner.Lesson.Progress.CourseIndex == 6)
        {
            if (owner.IntelligencePersonAccepted != IntelligencePersonAcceptedSequence)
            {
                owner.IntelligencePersonAccepted = IntelligencePersonAcceptedSequence;
                if (IntelligenceLastPersonAcceptedMode is IntelligenceUiMode.Home or IntelligenceUiMode.Away)
                    owner.Lesson.ObserveAction(IntelligenceLastPersonAcceptedMode == IntelligenceUiMode.Home
                        ? TutorialLessonAction.SpyHomeOfficerSelected : TutorialLessonAction.SpyAwayOfficerSelected);
                owner.LastUi = null;
            }
            if (owner.IntelligenceEditorAccepted != IntelligenceEditorAcceptedSequence)
            {
                owner.IntelligenceEditorAccepted = IntelligenceEditorAcceptedSequence;
                if (IntelligenceLastEditorAcceptedMode == IntelligenceUiMode.Home)
                    owner.Lesson.ObserveAction(TutorialLessonAction.SpyHomeEditorAccepted);
                owner.LastUi = null;
            }
            if (owner.IntelligenceAccepted != IntelligenceAcceptedSequence)
            {
                owner.IntelligenceAccepted = IntelligenceAcceptedSequence;
                if (IntelligenceLastAcceptedMode == IntelligenceUiMode.Away)
                    owner.Lesson.ObserveAction(TutorialLessonAction.SpyAwayAccepted);
                owner.LastUi = null;
            }
        }
        if (owner.Lesson.Progress.CourseIndex == 7 && owner.TalentSearchAccepted != TalentSearchAcceptedSequence)
        {
            owner.TalentSearchAccepted = TalentSearchAcceptedSequence;
            owner.Lesson.ObserveAction(TutorialLessonAction.ScenerySearchAccepted);
            owner.LastUi = null;
        }
    }

    private void ObserveTutorialSceneryAction(int action)
    {
        if (_tutorialLesson is not { } owner) return;
        owner.LastSceneryActionId = action;
        owner.LastUi = null;
    }

    private void BindCurrentTutorialIntelligence()
    {
        if (_tutorialLesson is not { } owner || owner.Lesson.Progress.CourseIndex != 6)
        {
            IntelligenceForceAdmission = null; IntelligencePersonAdmission = null;
            IntelligenceSelectionReturnAllowed = true; RefreshIntelligenceAdmission(); return;
        }
        var guidance = owner.Lesson.CurrentRequest?.Guidance;
        if (ReferenceEquals(owner.IntelligenceAdmissionGuidance, guidance) &&
            ReferenceEquals(owner.IntelligenceAdmissionEditor, _intelligenceEditor) &&
            ReferenceEquals(owner.IntelligenceAdmissionOverview, _intelligenceOverview) &&
            ReferenceEquals(owner.IntelligenceAdmissionSelector, _intelligenceSelector)) return;
        owner.IntelligenceAdmissionGuidance = guidance;
        owner.IntelligenceAdmissionEditor = _intelligenceEditor;
        owner.IntelligenceAdmissionOverview = _intelligenceOverview;
        owner.IntelligenceAdmissionSelector = _intelligenceSelector;
        IntelligenceForceAdmission = id => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            (guidance?.RequiredIntelligenceForceId is not int required || id == required);
        IntelligencePersonAdmission = ids => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            (guidance?.RequiredIntelligenceOfficerId is not int required || ids.Count == 1 && ids[0] == required);
        IntelligenceSelectionReturnAllowed = guidance?.AllowsSelectionReturn ?? true;
        RefreshIntelligenceAdmission();
    }

    private void BindCurrentTutorialStrategic()
    {
        if (_tutorialLesson is not { } owner || owner.Lesson.Progress.CourseIndex != 6)
        {
            StrategicTargetAdmission = StrategicExecutorAdmission = null;
            StrategicSelectionReturnAllowed = true; RefreshStrategicSelectionAdmission(); return;
        }
        var guidance = owner.Lesson.CurrentRequest?.Guidance;
        if (ReferenceEquals(owner.StrategicAdmissionGuidance, guidance) &&
            ReferenceEquals(owner.StrategicAdmissionPage, _strategicOrders) &&
            ReferenceEquals(owner.StrategicAdmissionSelector, _strategicOrders?.Selection)) return;
        owner.StrategicAdmissionGuidance = guidance; owner.StrategicAdmissionPage = _strategicOrders;
        owner.StrategicAdmissionSelector = _strategicOrders?.Selection;
        StrategicTargetAdmission = id => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            (guidance?.RequiredStrategicTargetId is not int required || id == required);
        StrategicExecutorAdmission = id => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            (guidance?.RequiredStrategicOfficerId is not int required || id == required);
        StrategicSelectionReturnAllowed = guidance?.AllowsSelectionReturn ?? true;
        RefreshStrategicSelectionAdmission();
    }

    private void BindCurrentTutorialTalentSearch()
    {
        if (_tutorialLesson is not { } owner || owner.Lesson.Progress.CourseIndex != 7)
        {
            TalentSearchCityAdmission = TalentSearchPersonAdmission = null;
            TalentSearchCityReturnAllowed = TalentSearchPersonReturnAllowed = true;
            BindTalentSearchSelectionAdmission(); return;
        }
        var guidance = owner.Lesson.CurrentRequest?.Guidance;
        if (ReferenceEquals(owner.SearchAdmissionGuidance, guidance) &&
            ReferenceEquals(owner.SearchAdmissionView, _talentOrdersView) &&
            ReferenceEquals(owner.SearchAdmissionCity, _talentDestinations) &&
            ReferenceEquals(owner.SearchAdmissionPeople, _talentPersons)) return;
        owner.SearchAdmissionGuidance = guidance; owner.SearchAdmissionView = _talentOrdersView;
        owner.SearchAdmissionCity = _talentDestinations; owner.SearchAdmissionPeople = _talentPersons;
        TalentSearchCityAdmission = id => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            (guidance?.CityId is not int required || id == required);
        TalentSearchPersonAdmission = id => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            (guidance?.OfficerId is not int required || id == required);
        TalentSearchCityReturnAllowed = TalentSearchPersonReturnAllowed = guidance?.AllowsSelectionReturn ?? true;
        BindTalentSearchSelectionAdmission();
    }

    private void BindCurrentTutorialSceneryInvestment()
    {
        if (_tutorialLesson is not { } owner || owner.Lesson.Progress.CourseIndex != 7)
        {
            SceneryHexActionAdmission = null; SceneryInvestmentDecisionAdmission = null;
            SceneryInvestmentReturnAllowed = true; RefreshSceneryInvestmentAdmission(); return;
        }
        int index = owner.Lesson.Progress.ActiveParagraphIndex ?? -1;
        int phase = index >= 0 ? owner.Lesson.Progress.ParagraphPhases[index] : 0;
        var guidance = owner.Lesson.CurrentRequest?.Guidance;
        if (TutorialEighthLessonPlan.RetiresSceneryHexParent(index, phase))
            CloseSceneryHexParentAfterInvestment();
        if (ReferenceEquals(owner.SceneryAdmissionGuidance, guidance) &&
            ReferenceEquals(owner.SceneryAdmissionMenu, _sceneryHexMenu) &&
            ReferenceEquals(owner.SceneryAdmissionView, _sceneryInvestmentView) &&
            ReferenceEquals(owner.SceneryAdmissionInformation, _sceneryInformationView)) return;
        owner.SceneryAdmissionGuidance = guidance; owner.SceneryAdmissionMenu = _sceneryHexMenu;
        owner.SceneryAdmissionView = _sceneryInvestmentView; owner.SceneryAdmissionInformation = _sceneryInformationView;
        SceneryHexActionAdmission = action => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            (guidance?.RequiredSceneryActionId is not int required || action == required);
        SceneryInvestmentDecisionAdmission = () => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            TutorialEighthLessonPlan.AllowsSceneryInvestmentConfirmation(index, phase);
        SceneryInvestmentReturnAllowed = index != 15;
        RefreshSceneryInvestmentAdmission();
    }

    private void BindCurrentTutorialCreditAndDoctrine()
    {
        var owner = _tutorialLesson;
        if (owner is null || owner.Lesson.Progress.CourseIndex != 7) return;
        int index = owner.Lesson.Progress.ActiveParagraphIndex ?? -1;
        int phase = index >= 0 ? owner.Lesson.Progress.ParagraphPhases[index] : 0;
        var credit = _personnelAdministrationPage as PersonnelCreditView;
        var selector = credit is null ? null : _personnelAdministrationPeople;
        var doctrine = _doctrineResearch;
        if (owner.CreditAdmissionIndex == index && owner.CreditAdmissionPhase == phase &&
            ReferenceEquals(owner.CreditAdmissionView, credit) && ReferenceEquals(owner.CreditAdmissionSelector, selector) &&
            ReferenceEquals(owner.DoctrineAdmissionView, doctrine?.View)) return;
        owner.CreditAdmissionIndex = index; owner.CreditAdmissionPhase = phase;
        owner.CreditAdmissionView = credit; owner.CreditAdmissionSelector = selector;
        owner.DoctrineAdmissionView = doctrine?.View;
        int? count = TutorialEighthLessonPlan.RequiredCreditSelectionCount(index, phase);
        PersonnelCreditSelectionAdmission = ids => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            (count is not int required || ids.Count == required && selector?.AreLeadingRowsSelected(required) == true);
        PersonnelCreditDecisionAdmission = () => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            TutorialEighthLessonPlan.AllowsCreditConfirmation(index, phase, false, false);
        PersonnelCreditReturnAllowed = TutorialEighthLessonPlan.AllowsCreditReturn(index, phase, selector is not null);
        if (selector is not null)
        {
            selector.ApplySelectionAdmission(count is int required ? id => selector.IsLeadingRow(id, required) : null,
                TutorialEighthLessonPlan.AllowsCreditReturn(index, phase, true));
            selector.SelectionConfirmationAdmission = ids => PersonnelCreditSelectionAdmission?.Invoke(ids) ?? true;
        }
        credit?.RefreshTutorialAdmission();

        int? requiredSlot = TutorialEighthLessonPlan.RequiredDoctrineLayoutSlot(index, phase);
        DoctrinePanelAdmission = panelId => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            (requiredSlot is null || doctrine?.Draft.Rows.FirstOrDefault(row => row.PanelId == panelId)?.LayoutSlot == requiredSlot);
        DoctrineResearchAdmission = panelId => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            doctrine?.Draft.Rows.FirstOrDefault(row => row.PanelId == panelId) is { } row &&
            TutorialEighthLessonPlan.AllowsDoctrineResearch(index, phase, row.LayoutSlot);
        DoctrineDecisionAdmission = () => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            TutorialEighthLessonPlan.AllowsDoctrineConfirmation(index, phase);
        DoctrineReturnAllowed = TutorialEighthLessonPlan.AllowsDoctrineReturn(index, phase);
        DoctrineAuxiliaryAdmission = () => IsTutorialLessonCurrent(owner) && !TutorialOverlayOwnsInput &&
            TutorialEighthLessonPlan.AllowsDoctrineAuxiliary(index, phase);
        if (doctrine is not null) doctrine.View.RefreshTutorialAdmission();
    }
}
