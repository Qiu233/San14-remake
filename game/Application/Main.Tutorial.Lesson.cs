using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class TutorialWorkspace(TutorialLessonSession lesson,
        GameplaySession session, CampaignComposition composition)
    {
        internal readonly TutorialLessonSession Lesson = lesson;
        internal GameplaySession Session = session;
        internal CampaignComposition Composition = composition;
        internal TutorialLessonHost Observer = null!;
        internal StoryPlaybackHost? Story;
        internal DeploymentNarrationView? Error;
        internal DeploymentNarrationView? Notice;
        internal TutorialLessonRequest? NoticeRequest;
        internal readonly CancellationTokenSource Lifetime = new();
        internal TutorialLessonRequest? AppliedSetup;
        internal CampaignServiceChanges? PublishedServices;
        internal GameState? LastBusinessWorld;
        internal TutorialLessonProgress? LastProgress;
        internal TutorialUiSnapshot? LastUi;
        internal ApplicationGameplayContext? ApplicationContext;
        internal AreaAdministrationPresenter? AreaPresenter;
        internal CityDomesticPresenter? CityPresenter;
        internal long OfficerAccepted, AreaAccepted, CityOfficerAccepted;
        internal long FocusChanged;
        internal DeploymentSetupPresenter? DeploymentSetup;
        internal DeploymentTargetPresenter? DeploymentTarget;
        internal long DeploymentOfficersAccepted, DeploymentSetupAccepted, DeploymentTargetAccepted;
        internal long DeploymentSelectionChanged;
        internal IReadOnlyList<int> DeploymentSelectedOfficers = [];
        internal int? CapitalHexId;
        internal int? LastCommandId = 13, LastInformationId = 51;
        internal bool Retired, Failed, ExitCleaned;
        internal bool MapBindingsReady;
        internal bool PeriodBorrowed;
        internal uint SourceMilliseconds;
        internal GovernanceRosterPage? GovernancePage;
        internal long GovernanceAdvisorAccepted, GovernanceAutoAccepted;
        internal int? LastCityInformationActionId = 5;
        internal long DiplomacyAccepted, ForeignCountryAccepted, ForeignPersonAccepted, ForeignAccepted;
        internal StrategicOrdersWorkspace? StrategicPage;
        internal long StrategicAccepted;
        internal TutorialLessonGuidance? ForeignAdmissionGuidance;
        internal Control? ForeignAdmissionSelector;
        internal Control? ForeignAdmissionView;
        internal TutorialLessonGuidance? DiplomacyAdmissionGuidance;
        internal Control? DiplomacyAdmissionView, DiplomacyAdmissionSelector;
        internal long IntelligencePersonAccepted, IntelligenceEditorAccepted, IntelligenceAccepted, TalentSearchAccepted;
        internal int? LastSceneryActionId = 3;
        internal DoctrineResearchPageValues? DoctrineObservedPublication;
        internal IReadOnlySet<int> DoctrineResearchedSlots = new HashSet<int>();
        internal int CreditAdmissionIndex = -2, CreditAdmissionPhase = -1;
        internal PersonnelCreditView? CreditAdmissionView;
        internal Control? CreditAdmissionSelector;
        internal DoctrineResearchView? DoctrineAdmissionView;
        internal TutorialLessonGuidance? IntelligenceAdmissionGuidance, StrategicAdmissionGuidance, SearchAdmissionGuidance;
        internal Control? IntelligenceAdmissionEditor, IntelligenceAdmissionOverview, IntelligenceAdmissionSelector;
        internal StrategicOrdersWorkspace? StrategicAdmissionPage;
        internal Control? StrategicAdmissionSelector, SearchAdmissionView, SearchAdmissionCity, SearchAdmissionPeople;
        internal TutorialLessonGuidance? SceneryAdmissionGuidance;
        internal Control? SceneryAdmissionMenu, SceneryAdmissionView, SceneryAdmissionInformation;
    }

    private sealed record TutorialUiSnapshot(int? ActionTop, bool DomesticSelectionOpen,
        bool CityFirstSelectorVisible, bool CitySecondSelectorVisible,
        bool ProposalListVisible, bool ProposalDetailVisible, int? LastCommandId,
        int? LastInformationId, long OfficerAccepted, long AreaAccepted, long CityOfficerAccepted, long FocusChanged,
        TutorialDeploymentUiSnapshot? Deployment, TutorialAdvancedUiSnapshot? Advanced,
        TutorialMenuUiSnapshot? Menus, TutorialFinalUiSnapshot? FinalCourses);

    private TutorialWorkspace? _tutorialLesson;
    private MapSceneView? _tutorialBoundMap;
    private bool HasTutorialLesson => _tutorialLesson is not null;
    private bool TutorialEntryAllowed => _tutorialLesson is not { } lesson ||
        lesson.MapBindingsReady && lesson.Lesson.CurrentRequest is not null && !lesson.Failed;
    private bool TutorialOverlayOwnsInput => _tutorialLesson is { Story: not null } or { Error: not null } or
        { NoticeRequest: not null } or { ExitCleaned: true } || _tutorialLesson?.Lesson.CurrentRequest?.Kind is
            TutorialLessonRequestKind.Narration or TutorialLessonRequestKind.InstructionCue or
            TutorialLessonRequestKind.Correction or TutorialLessonRequestKind.CompletionNotice;

    private void BeginTutorialLesson(TutorialSelectionPreparation selection, TutorialLessonPreparation prepared)
    {
        if (!IsTutorialPreparationCurrent(selection) || HasTutorialLesson || _repository is null) return;
        var repository = selection.Repository ?? throw new InvalidOperationException("The prepared tutorial resource reader is absent.");
        var composition = CampaignComposition.PrepareTutorial(repository, prepared,
            TutorialLessonSession.ReadApplicationContext(prepared.World!), _personnelGroupMoveDestinations);
        composition.ConfigurePersonnelNarrationVoice(_preferences.Voice.Capture());
        composition.ConfigureApplicationProfile(GetApplicationProfile());
        var rules = composition.Army ?? throw new InvalidDataException("tutorial.main.rule_owner");
        var audio = new CampaignNormalEventAudioInputs(_ui.SourceLocale,
            readApplicationField1AC: () => 0, readApplicationPointerPresent: () => true);
        var lesson = TutorialLessonSession.Begin(prepared with { Rules = rules }, new(repository, audio));
        var entered = rules.EnterStrategy(lesson.World, CampaignEntryKind.NewCampaign);
        lesson.AdoptWorld(lesson.BusinessWorld, entered);
        // The real map/info controllers must exist before Opening can create
        // Armies. Its first Advance occurs only after the resident map has
        // published those bindings; an absent request admits no commands.
        var campaign = composition.AttachPreparedTutorial(lesson.World, lesson.PlayerForceId);
        // Restore owns its actual copied world. The tutorial must borrow that
        // identity, not retain its pre-attachment image as a command baseline.
        lesson.AdoptWorld(lesson.BusinessWorld, campaign.Session.World);
        ResetMilitaryDeploymentComposition(); LeaveSceneAudioContext(); CloseResidentMap();
        _campaignComposition = composition; _session = campaign.Session;
        _loadedMap = prepared.Map; _map = prepared.Map!.Map; _mapOverview = composition.Overview;
        _selectedForceId = lesson.PlayerForceId; _selectedCell = null;
        AttachCampaignApplication(playable: true); PublishCampaignPresentation(campaign.Presentation);
        AttachTutorialLessonHost(lesson, campaign.Session, composition);
        selection.View.Hide(); _titleMenu?.Hide();
        _mapHost.Show();
        _browserContent.ProcessMode = ProcessModeEnum.Inherit; _browserContent.Show();
        AdoptStoryRandom(lesson.World); RefreshCurrentPlayWorld(); RefreshSceneAudioContext();
    }

    private TutorialWorkspace AttachTutorialLessonHost(TutorialLessonSession lesson,
        GameplaySession session, CampaignComposition composition)
    {
        var owner = new TutorialWorkspace(lesson, session, composition);
        owner.MapBindingsReady = _residentMap is not null && ReferenceEquals(_tutorialBoundMap, _residentMap);
        owner.DiplomacyAccepted = _diplomacySelectionAcceptedSequence;
        owner.ForeignCountryAccepted = ForeignTradeCountryAcceptedSequence;
        owner.ForeignPersonAccepted = ForeignTradePersonAcceptedSequence;
        owner.ForeignAccepted = ForeignTradeAcceptedSequence;
        owner.IntelligencePersonAccepted = IntelligencePersonAcceptedSequence;
        owner.IntelligenceEditorAccepted = IntelligenceEditorAcceptedSequence;
        owner.IntelligenceAccepted = IntelligenceAcceptedSequence;
        owner.TalentSearchAccepted = TalentSearchAcceptedSequence;
        _tutorialLesson = owner;
        BindTutorialSession(owner);
        owner.Observer = new TutorialLessonHost(); AddChild(owner.Observer);
        owner.Observer.Configure(() => IsTutorialLessonCurrent(owner) && !_advancingPeriod && !owner.Failed,
            () => ObserveTutorialLesson(owner), error => TutorialLessonFailed(owner, error));
        return owner;
    }

    private bool IsTutorialLessonCurrent(TutorialWorkspace owner) => !_failed && !_shuttingDown &&
        !owner.Retired && ReferenceEquals(_tutorialLesson, owner) && _session is not null &&
        ReferenceEquals(_campaignComposition?.ApplicationGameplay, owner.Composition.ApplicationGameplay);

    private void NotifyTutorialMapBindingsReady(MapSceneView map)
    {
        if (!ReferenceEquals(_residentMap, map)) return;
        // Loading may finish the real map before the restored lesson host is
        // attached. Retain that exact publication, never infer it from ready.
        _tutorialBoundMap = map;
        if (_tutorialLesson is not { } owner || !IsTutorialLessonCurrent(owner)) return;
        owner.MapBindingsReady = true;
        owner.LastUi = null;
    }

    private void BindTutorialSession(TutorialWorkspace owner)
    {
        owner.Lesson.BindCommandRules(owner.Composition.Army
            ?? throw new InvalidDataException("tutorial.main.command_rules"));
        owner.Session.ConfigureTutorialWorld(owner.Lesson.PrepareCommandWorld,
            (before, after) => owner.Lesson.AdoptWorld(before, after),
            _ => IsTutorialLessonCurrent(owner) && TutorialEntryAllowed);
    }

    private void ObserveTutorialLesson(TutorialWorkspace owner, bool force = false,
        bool periodBorrow = false, bool actualTutorialUpdate = false)
    {
        if (!IsTutorialLessonCurrent(owner) || owner.Failed || _advancingPeriod && !periodBorrow) return;
        var map = _residentMap;
        if (!owner.ExitCleaned && (!owner.MapBindingsReady || map is not { IsPlayableMapReady: true })) return;
        if (!ReferenceEquals(owner.Session, _session))
        {
            owner.Session.ConfigureTutorialWorld(null, null);
            owner.Session = _session!; owner.Composition = _campaignComposition!;
            BindTutorialSession(owner);
        }
        if (!periodBorrow && !ReferenceEquals(owner.Lesson.BusinessWorld, owner.Session.World))
            owner.Lesson.AdoptWorld(owner.Lesson.BusinessWorld, owner.Session.World);
        if (!periodBorrow) owner.Lesson.AdoptApplicationRandom(new(_presentationRandom.State));
        ObserveTutorialSelections(owner);
        ObserveTutorialDeploymentSelections(owner);
        ObserveTutorialAdvancedSelections(owner);
        ObserveTutorialMenuSelections(owner);
        ObserveTutorialFinalSelections(owner);
        var currentUi = CaptureTutorialUi(owner);
        if (!force && ReferenceEquals(owner.LastBusinessWorld, owner.Lesson.BusinessWorld) &&
            ReferenceEquals(owner.LastProgress, owner.Lesson.Progress) && currentUi == owner.LastUi) return;
        owner.LastBusinessWorld = owner.Lesson.BusinessWorld;
        owner.LastProgress = owner.Lesson.Progress; owner.LastUi = currentUi;
        owner.Lesson.Advance(CaptureTutorialObservation(owner, currentUi), actualTutorialUpdate);
        if (!periodBorrow) AdoptStoryRandom(owner.Lesson.World);
        if (!periodBorrow) AdoptTutorialBusinessUpdate(owner);
        BindTutorialDeployment(owner);
        BindTutorialAdministration(owner);
        BindCurrentTutorialDiplomacy();
        BindCurrentTutorialForeignTrade();
        BindCurrentTutorialCreditAndDoctrine();
        BindCurrentTutorialIntelligence(); BindCurrentTutorialStrategic(); BindCurrentTutorialTalentSearch();
        BindCurrentTutorialSceneryInvestment();
        if (owner.Lesson.UnresolvedOwner is { } missing) throw new InvalidDataException(missing);
        var context = owner.Lesson.ReadApplicationContext();
        if (context != owner.ApplicationContext)
        {
            owner.Composition.ApplicationGameplay.UpdateContext(context);
            owner.ApplicationContext = context;
        }
        PublishTutorialServices(owner);
        if (owner.Lesson.Completed) { CloseTutorialLesson(); return; }
        if (owner.Lesson.CurrentRequest is not { } request) return;
        if (request.Kind == TutorialLessonRequestKind.Narration)
        {
            if (owner.Story is null) OpenTutorialStory(owner, map ?? throw new InvalidDataException("tutorial.main.story_map"));
            return;
        }
        CloseTutorialStory(owner);
        if (request.Kind == TutorialLessonRequestKind.InstructionCue)
        {
            var cue = request.InstructionCue ?? throw new InvalidDataException("tutorial.main.instruction_cue");
            if (cue.CameraFocusHexId is int focus)
            {
                if (map?.FocusCommanderHex(focus, cue.DurationMilliseconds) != true)
                    throw new InvalidDataException("tutorial.main.camera_cue_owner");
            }
            else if (_deploymentSetupPresenter?.TryApplyTutorialTacticCue(cue.TacticMarkerId, cue.EnableTacticButtons) != true)
                throw new InvalidDataException("tutorial.main.tactic_cue_owner");
            if (!owner.Lesson.CompleteInstructionCue(request))
                throw new InvalidDataException("tutorial.main.instruction_cue_receipt");
            owner.LastProgress = null;
            return;
        }
        if (request.Kind is TutorialLessonRequestKind.CompletionNotice or TutorialLessonRequestKind.Correction)
        {
            if (!ReferenceEquals(owner.NoticeRequest, request))
            { owner.NoticeRequest = request; _ = PresentTutorialNotice(owner, request); }
            return;
        }
        if (request.Kind == TutorialLessonRequestKind.Exit)
        {
            //4C65F0 restores the real ordinary windows before32BDD0 returns
            //to the directory. Completion awards follow that return, not the
            //last teaching Story. Keep the actual audio/data owner until its
            //result notices have really retired.
            CloseDeploymentSetup(); CloseAreaAdministration(); CloseCityDomestic(); CloseProposals();
            CloseGovernanceRoster(abandon: true); CloseUnitRetarget();
            CloseDiplomacyOrders(); CloseStrategicOrders(); CloseForeignTradeOrders();
            ClosePersonnelAdministration(); CloseDoctrineResearch(); CloseTalentOrders();
            CloseIntelligenceOrders(); CloseSceneryInvestment();
            CloseActionReport(); CloseIncomeReport();
            map?.SetDetailsOpen(false); SetMapFocus(false); _mapHost.Hide();
            _tutorialSelection?.Show(); owner.ExitCleaned = true;
            if (!owner.Lesson.CompleteExit(request)) throw new InvalidDataException("tutorial.main.exit_receipt");
            owner.LastProgress = null;
            return;
        }
        if (request.Guidance is not { } guidance) throw new InvalidDataException("tutorial.main.guidance");
        if (guidance.UnresolvedOwner is { } unavailable) throw new InvalidDataException(unavailable);
        var priorSetup = owner.AppliedSetup;
        if (priorSetup is null || priorSetup.Sequence != request.Sequence ||
            priorSetup.ParagraphIndex != request.ParagraphIndex || priorSetup.Phase != request.Phase ||
            priorSetup.Guidance?.InstructionCallbackPending != guidance.InstructionCallbackPending)
        {
            // Reset the actual host registers before acknowledging this source
            // setup. These sentinels are not command or information selections.
            if (guidance.ResetLastCommandId is int command) owner.LastCommandId = command;
            if (guidance.ResetLastInformationId is int information) owner.LastInformationId = information;
            if (guidance.ResetCityInformationActionId is int cityAction) owner.LastCityInformationActionId = cityAction;
            if (guidance.ResetSceneryActionId is int sceneryAction) owner.LastSceneryActionId = sceneryAction;
            owner.AppliedSetup = request;
            if (guidance.InstructionCallbackPending)
            {
                // The borrowed event view has actually retired. Restore the
                // real command/map parent's input before returning its source
                // callback; a Story receipt alone does not finish the lesson.
                map?.SetDetailsOpen(HasAdministrationPage || HasMilitaryOrderPage || HasActionReport || HasIncomeReport || HasProposalsPage || HasGovernanceRosterPage,
                    keepMapVisible: true);
                if (!owner.Lesson.CompleteInstructionCallback(request))
                    throw new InvalidDataException("tutorial.main.instruction_callback_owner");
                owner.LastProgress = null;
                return;
            }
            if (owner.Lesson.CompleteParagraphSetup(request))
                owner.LastProgress = null;
        }
    }

    private TutorialUiSnapshot CaptureTutorialUi(TutorialWorkspace owner)
    {
        var menus = CaptureTutorialMenus(owner);
        var finalCourses = CaptureTutorialFinalUi(owner);
        int? top = _advancingPeriod ? 2 : _governanceRosterPage is { } government && government.IsVisibleInTree()
            ? government.AdvisorSelectorVisible ? 19 : government.EffectDialogVisible ? 21 : 18 :
            _deploymentTargetPresenter?.IsFacilityPlacementActive == true || _unitFacilitySelection?.IsFacilityPlacementActive == true ? 22 :
            _deploymentSetupPresenter?.IsOfficerSelectionOpen == true ? 5 :
            _deploymentSetup is not null ? 4 : _deploymentTarget is not null || _deploymentNarration is not null ? 6 :
            _unitOrderTarget is not null ? 6 :
            HasCityDomesticPage ? 16 : HasIncomeReport ? 11 :
            HasProposalsPage ? 12 : _administrationParent is not null ? 1 : HasActionReport ? 10 : 1;
        if (!_advancingPeriod && menus?.ActionTop is int menuTop) top = menuTop;
        if (!_advancingPeriod && finalCourses?.ActionTop is int finalTop) top = finalTop;
        return new(top, _administrationPresenter?.HasChild == true,
            _cityDomesticPresenter is { OfficerSelectionOpen: true, ActiveSelectionDuty: CityDomesticDuty.Recruiter },
            _cityDomesticPresenter is { OfficerSelectionOpen: true, ActiveSelectionDuty: CityDomesticDuty.Trainer },
            _proposalsView is { IsOfficerPage: false } first && first.IsVisibleInTree(),
            _proposalsView is { IsOfficerPage: true } officer && officer.IsVisibleInTree(),
            owner.LastCommandId, owner.LastInformationId, owner.OfficerAccepted, owner.AreaAccepted, owner.CityOfficerAccepted, owner.FocusChanged,
            CaptureTutorialDeploymentUi(owner), CaptureTutorialAdvancedUi(owner), menus, finalCourses);
    }

    private void ObserveTutorialSelections(TutorialWorkspace owner)
    {
        if (owner.Lesson.Progress.CourseIndex is not (0 or 7)) return;
        if (!ReferenceEquals(owner.AreaPresenter, _administrationPresenter))
        {
            if (owner.AreaPresenter is { } previous) ReadAreaActions(previous);
            owner.AreaPresenter = _administrationPresenter;
            owner.OfficerAccepted = owner.AreaAccepted = owner.FocusChanged = 0;
            BindTutorialAdministration(owner);
        }
        if (_administrationPresenter is { } area) ReadAreaActions(area);
        if (!ReferenceEquals(owner.CityPresenter, _cityDomesticPresenter))
        {
            if (owner.CityPresenter is { } previous) ReadCityActions(previous);
            owner.CityPresenter = _cityDomesticPresenter; owner.CityOfficerAccepted = 0;
        }
        if (_cityDomesticPresenter is { } city) ReadCityActions(city);

        void ReadAreaActions(AreaAdministrationPresenter area)
        {
            if (area.OfficerSelectionAcceptedSequence != owner.OfficerAccepted)
            {
                owner.OfficerAccepted = area.OfficerSelectionAcceptedSequence;
                if (area.OfficerSelectionAcceptedPersonId is int person)
                    owner.Lesson.ObserveOfficerSelection(person);
            }
            if (area.FocusChangedSequence != owner.FocusChanged)
            {
                owner.FocusChanged = area.FocusChangedSequence;
                owner.Lesson.ObserveAction(TutorialLessonAction.PriorityChanged);
            }
            if (area.AreaAcceptedSequence != owner.AreaAccepted)
            {
                owner.AreaAccepted = area.AreaAcceptedSequence;
                if (area.AreaAcceptedFocus is { } focus) owner.Lesson.ObservePriorityConfirmation(focus);
            }
        }
        void ReadCityActions(CityDomesticPresenter city)
        {
            if (city.OfficerSelectionAcceptedSequence == owner.CityOfficerAccepted) return;
            owner.CityOfficerAccepted = city.OfficerSelectionAcceptedSequence;
            if (city.OfficerSelectionAcceptedDuty is { } duty)
                owner.Lesson.ObserveAction(duty == CityDomesticDuty.Recruiter
                    ? TutorialLessonAction.CityFirstSelectionAccepted : TutorialLessonAction.CitySecondSelectionAccepted);
        }
    }

    private void BindTutorialAdministration(TutorialWorkspace owner)
    {
        if (_administrationPresenter is not { } area) return;
        area.AreaSelectionAdmission = id => !IsTutorialLessonCurrent(owner) ||
            !TutorialOverlayOwnsInput && (owner.Lesson.CurrentRequest?.Guidance?.AreaId is not int required || id == required);
        area.OfficerSelectionAdmission = person => !IsTutorialLessonCurrent(owner) ||
            !TutorialOverlayOwnsInput && (owner.Lesson.CurrentRequest?.Guidance?.OfficerId is null ||
                owner.Lesson.CanAcceptOfficerSelection(person));
        area.AreaConfirmationAdmission = focus =>
        {
            if (!IsTutorialLessonCurrent(owner)) return true;
            if (TutorialOverlayOwnsInput) return false;
            if (owner.Lesson.CurrentRequest?.Guidance?.RequiredDevelopmentFocus is null ||
                owner.Lesson.CanAcceptPriority(focus)) return true;
            owner.Lesson.ObservePriorityConfirmation(focus);
            AdoptStoryRandom(owner.Lesson.World); owner.LastProgress = null;
            return false;
        };
    }

    private void OpenTutorialStory(TutorialWorkspace owner, MapSceneView map)
    {
        var audio = _sceneAudioBinding ?? throw new InvalidDataException("tutorial.main.audio_owner");
        var host = new StoryPlaybackHost(); owner.Story = host; AddChild(host);
        host.Configure(_repository!, _ui, new(owner.Lesson.Rules, () => owner.Lesson.World,
            () => owner.Lesson.Progress.Story,
            () =>
            {
                var result = owner.Lesson.PrepareCurrentStoryStep();
                if (result.UnresolvedOwner is { } missing) throw new InvalidDataException(missing);
                AdoptStoryRandom(owner.Lesson.World);
            }, receipt => owner.Lesson.Progress.Story is { } story &&
                owner.Lesson.CompleteStoryStep(story, receipt)), map, audio, _preferences.MusicSelection,
            () => TutorialStoryAdmitted(owner), () => _preferences.AutomaticMessageAdvance,
            CurrentListScrollSpeed, HandleNarrationKeyboard, AdoptStoryRandom,
            () =>
            {
                CloseTutorialStory(owner); owner.LastProgress = null;
                Callable.From(() => ObserveTutorialLesson(owner, true)).CallDeferred();
            }, error => TutorialLessonFailed(owner, error), inputWaitKeyboard: HandleNarrationKeyboard);
    }

    private bool TutorialStoryAdmitted(TutorialWorkspace owner) => IsTutorialLessonCurrent(owner) &&
        !owner.Failed && (!_advancingPeriod || owner.PeriodBorrowed) &&
        !HasEnvironmentSettings && !HasCampaignSavePage && !HasMapSystemMenu;

    private void CloseTutorialStory(TutorialWorkspace owner)
    {
        var story = owner.Story; owner.Story = null;
        if (story is not null) { story.Abandon(); story.QueueFree(); }
    }

    private void PublishTutorialServices(TutorialWorkspace owner)
    {
        var services = owner.Lesson.World.PendingCampaignServices;
        if (ReferenceEquals(owner.PublishedServices, services)) return;
        owner.Composition.PublishApplicationServices(owner.Lesson.World);
        owner.PublishedServices = services;
    }

    private async Task PresentTutorialNotice(TutorialWorkspace owner, TutorialLessonRequest request)
    {
        try
        {
            var audio = _sceneAudioBinding ?? throw new InvalidDataException("tutorial.main.notice_audio");
            if (request.CompletionNotice?.SoundId is int sound)
                await audio.PlayNormalSoundAsync(sound, owner.Lifetime.Token);
            if (!IsTutorialLessonCurrent(owner) || !ReferenceEquals(owner.NoticeRequest, request)) return;
            var text = request.CorrectionText?.Text ?? request.CompletionNotice?.TextAtSource
                ?? throw new InvalidDataException("tutorial.main.notice_text");
            if (request.CorrectionText is { } correction)
                foreach (var voice in correction.Voices) audio.PlayPersonnelNarrationVoice(voice);
            var view = new DeploymentNarrationView { Name = "TutorialNotice" }; owner.Notice = view; AddChild(view);
            view.ConfigureAnnouncement(_repository!, _ui, text, () => RunUiAction(() =>
            {
                if (!IsTutorialLessonCurrent(owner) || !ReferenceEquals(owner.NoticeRequest, request)) return;
                bool accepted = request.Kind == TutorialLessonRequestKind.Correction
                    ? owner.Lesson.CompleteCorrection(request) : owner.Lesson.CompleteCompletionNotice(request);
                if (!accepted) throw new InvalidDataException("tutorial.main.notice_receipt");
                owner.Notice = null; owner.NoticeRequest = null; view.Abandon(); view.QueueFree();
                audio.StopNarrationVoice(); owner.LastProgress = null;
            }), () => _preferences.AutomaticMessageAdvance,
                () => IsTutorialLessonCurrent(owner) && !HasEnvironmentSettings && !HasCampaignSavePage && !HasMapSystemMenu);
        }
        catch (OperationCanceledException) when (owner.Lifetime.IsCancellationRequested) { }
        catch (Exception error) { TutorialLessonFailed(owner, error); }
    }

    private bool HandleTutorialLessonInput(InputEvent input)
    {
        if (_tutorialLesson is not { } owner) return false;
        if (owner.Error is not null)
        {
            if (input is InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false } or
                InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false })
            { RequestTutorialLessonExit(); return true; }
            return false;
        }
        if (owner.NoticeRequest is not null)
        {
            if (owner.Notice is { } notice && HandleNarrationKeyboard(input, notice)) return true;
            // The announcement owns these inputs while its real view/audio is
            // entering. Do not let cancel close the command/report underneath.
            return input is InputEventKey or InputEventMouseButton { ButtonIndex: MouseButton.Right };
        }
        if (owner.Story is not { } story)
            return TutorialOverlayOwnsInput && input is InputEventKey or InputEventMouseButton;
        if (HandleNarrationKeyboard(input, story.Message)) return true;
        if (input is InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false } or
            InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false })
        { story.RequestSkip(); return true; }
        return false;
    }

    private bool TutorialCommandAllowed(int commandId) => _tutorialLesson is not { } owner ||
        !owner.Failed && owner.Story is null && owner.Lesson.CurrentRequest is
            { Kind: TutorialLessonRequestKind.Interaction, Guidance: { BlocksOrdinaryCommands: false } guidance } &&
        (uint)commandId < 13 && TutorialFirstLessonRules.IsCommandAllowed(commandId, guidance.EnabledCommandMask);

    private bool TutorialProgressAllowed => _tutorialLesson is not { } owner || !owner.Failed && owner.Story is null &&
        owner.Lesson.CurrentRequest is { Kind: TutorialLessonRequestKind.Interaction,
            Guidance: { AllowsPeriodAdvance: true, InstructionCallbackPending: false } };

    private void ObserveTutorialCommand(int commandId)
    {
        if (_tutorialLesson is { } owner)
        { owner.LastCommandId = commandId; owner.LastUi = null; BindTutorialAdministration(owner); BindTutorialDeployment(owner); }
    }
    private void ObserveTutorialInformation(int informationId)
    { if (_tutorialLesson is { } owner) { owner.LastInformationId = informationId; owner.LastUi = null; } }
    private void ObserveTutorialOrderAccepted(GameOrder order)
    {
        if (_tutorialLesson is not { } owner) return;
        if (order is SetAreaAdministrationOrder) owner.Lesson.ObserveAction(TutorialLessonAction.DomesticOrderAccepted);
        if (order is CompleteCityDomesticOrder { Terminal: CityDomesticTerminal.Confirm })
            owner.Lesson.ObserveAction(TutorialLessonAction.CityDomesticOrderAccepted);
        ObserveTutorialMenuOrderAccepted(owner, order);
        owner.LastUi = null;
    }
    private void ObserveTutorialProgressRequested()
    {
        if (_tutorialLesson is not { } owner) return;
        owner.Lesson.ObserveAction(TutorialLessonAction.ProgressRequested); owner.LastUi = null;
        // Main has accepted the actual Progress input and set its busy owner,
        // but has not captured the worker yet. Observe this real top2 boundary
        // once; do not open a new UI inside the caller's page-close callback.
        if (_advancingPeriod)
        {
            var ui = CaptureTutorialUi(owner);
            owner.Lesson.Advance(CaptureTutorialObservation(owner, ui));
            owner.LastProgress = null;
        }
    }
    private void ObserveTutorialActionReportAcknowledged()
    { if (_tutorialLesson is { } owner) { owner.Lesson.ObserveAction(TutorialLessonAction.ActionReportAcknowledged); owner.LastUi = null; } }
    private void ObserveTutorialIncomeReportAcknowledged()
    { if (_tutorialLesson is { } owner) { owner.Lesson.ObserveAction(TutorialLessonAction.IncomeReportAcknowledged); owner.LastUi = null; } }
    private void ObserveTutorialProposalOfficerAccepted()
    { if (_tutorialLesson is { } owner) { owner.Lesson.ObserveAction(TutorialLessonAction.ProposalOfficerAccepted); owner.LastUi = null; } }
    private void ObserveTutorialReportReturned()
    { if (_tutorialLesson is { } owner) owner.LastUi = null; }

    private void TutorialLessonFailed(TutorialWorkspace owner, Exception error)
    {
        if (!IsTutorialLessonCurrent(owner)) return;
        owner.Failed = true; CloseTutorialStory(owner); ReportPlayFailure(error);
        var view = new DeploymentNarrationView { Name = "TutorialUnavailable" }; owner.Error = view; AddChild(view);
        view.ConfigureAnnouncement(_repository!, _ui, error.Message, RequestTutorialLessonExit,
            () => _preferences.AutomaticMessageAdvance, () => IsTutorialLessonCurrent(owner));
    }

    private void RequestTutorialLessonExit()
    { if (!_advancingPeriod) CloseTutorialLesson(); }

    // The ordinary return-to-title coordinator retains the session/application
    // until its real map retirement completes. This only retires lesson UI.
    private void RetireTutorialLessonForReturn()
    {
        _tutorialLoadPreparation = null;
        var owner = _tutorialLesson; _tutorialLesson = null;
        if (owner is null) return;
        owner.Retired = true; owner.Observer.Abandon(); owner.Observer.QueueFree();
        owner.Lifetime.Cancel();
        CloseTutorialStory(owner);
        if (owner.Error is { } error) { error.Abandon(); error.QueueFree(); }
        if (owner.Notice is { } notice) { notice.Abandon(); notice.QueueFree(); }
        owner.Session.ConfigureTutorialWorld(null, null);
        _mapHost.Show();
        CloseTitleMenu();
    }

    private void CloseTutorialLesson(bool resumeDirectory = true)
    {
        var owner = _tutorialLesson; _tutorialLesson = null;
        if (owner is null || owner.Retired) return;
        owner.Retired = true; owner.Observer.Abandon(); owner.Observer.QueueFree();
        owner.Lifetime.Cancel();
        CloseTutorialStory(owner);
        if (owner.Error is { } error) { error.Abandon(); error.QueueFree(); }
        if (owner.Notice is { } notice) { notice.Abandon(); notice.QueueFree(); }
        owner.Session.ConfigureTutorialWorld(null, null);
        if (!ReferenceEquals(_session, owner.Session)) return;
        ResetMilitaryDeploymentComposition(); LeaveSceneAudioContext(); ClosePlayablePeriodReport(); CloseResidentMap();
        _campaignApplication?.Detach(); _campaignApplication = null;
        _session = null; _campaignComposition = null; _selectedForceId = null; _selectedCell = null;
        _browserContent.Hide(); _browserContent.ProcessMode = ProcessModeEnum.Disabled;
        _mapHost.Show();
        if (resumeDirectory && !_failed && !_shuttingDown) _ = RestoreDirectory();
        async Task RestoreDirectory()
        {
            await Task.WhenAll(_mapRetirements.Select(row => row.Completion));
            if (_failed || _shuttingDown || _session is not null) return;
            if (_titleMenu is null) OpenTitleMenu();
            CloseTutorialSelection(resumeTitle: false); OpenTutorialSelection();
        }
    }
}
