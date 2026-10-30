using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

public partial class Main
{
    private GovernanceRosterPage? _governanceRosterPage;
    private DeploymentTargetConfirmation? _governanceRosterConfirmation;
    private GameplaySession? _governanceRosterSession;
    private GameState? _governanceRosterWorld;
    private MapSceneView? _governanceRosterMap;
    private Action<GameState>? _governanceRosterAdoptSpeechWorld;
    private Action? _governanceRosterRequestReturn;

    private void AdoptGovernanceRosterSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        // Pending-period editors stay with their candidate through the shared
        // admission gate; ordinary policy editors may follow Session.World.
        if (_governanceRosterPage is null ||
            _governanceRosterSession != session || _governanceRosterMap != _residentMap ||
            !ReferenceEquals(_governanceRosterWorld, previous)) return;
        _governanceRosterWorld = replacement;
        _governanceRosterAdoptSpeechWorld?.Invoke(replacement);
    }

    private Button _playGovernanceOpen = null!;
    private bool HasGovernanceRosterPage => _governanceRosterPage is not null;

    private void OpenGovernanceRoster() => OpenGovernanceRoster(null);

    private bool IsGovernanceRosterCurrentOwner => _governanceRosterSession == _session &&
        _session is { } session && _governanceRosterMap == _residentMap &&
        ReferenceEquals(_governanceRosterWorld, _policyRosterEditor is null ? session.World : PersonnelPresentationOwner(session));

    private void OpenGovernanceRoster(PolicyRosterEditorContext? policyEditor)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle ||
            (policyEditor is null ? HasAdministrationPage : HasAdministrationPageExceptStory ||
                (HasStoryPresentation && !IsStoryPresentationCurrent)) || HasMilitaryOrderPage ||
            HasCampaignSavePage || HasMapSystemMenu || HasActionReport || HasPersonnelConfirmation || _detailsView is not null ||
            _repository is null || _residentMap is not { IsPlayableMapReady: true } map ||
            _session is not { PlayerForceId: int player } session || session.Rules.GovernanceRosterRules is not { } rules) return;
        var world = policyEditor is null ? session.World : PersonnelPresentationOwner(session);
        if ((policyEditor is null && (world.Phase != TurnPhase.Strategy || !ReferenceEquals(world, _world))) ||
            !world.Forces.TryGetValue(player, out var force) || PlayableOutcome.Evaluate(world, player) != PlayableResult.Ongoing) return;
        var policyEntry = policyEditor is null ? null : FindPolicyEditorEntry(world, policyEditor.Sequence, player);
        if (policyEditor is not null && policyEntry is null) return;
        var current = rules.QueryGovernanceRoster(world, player);
        var automaticRules = rules as ArmyCampaignRuleAdapter;
        var messages = _repository.LoadMessages(_ui.SourceLocale, MessageKind.Data).Messages.Sections[0];
        var model = BuildModel();
        CloseCommandWorkspace();
        var view = new GovernanceRosterPage(); _governanceRosterPage = view;
        _governanceRosterSession = session; _governanceRosterWorld = world; _governanceRosterMap = map;
        _governanceRosterAdoptSpeechWorld = next => world = next;
        _governanceRosterRequestReturn = RequestReturn;
        GovernanceRoster? draft = null;
        GovernanceRosterEvaluation? evaluation = null;
        AddChild(view);
        try
        {
            view.Configure(_repository, _ui, _playCommandDeck.Theme, model, RequestReturn, RequestConfirm, Evaluate,
                automaticRules is null ? null : () => RunPlayAction(ApplyAutomatic),
                automaticRules is null ? null : () => RunPlayAction(ShowEffects),
                automaticRules is null ? null : group => RunPlayAction(()=>OpenSelection(group)),
                group => RunPlayAction(()=>RequestExpansion(group)));
            SetMapFocus(true); map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
            ObserveTutorialInformation(12);
        }
        catch { CloseGovernanceRoster(abandon: true); throw; }

        bool Current() => !_failed && !_shuttingDown && _governanceRosterPage == view &&
            ReferenceEquals(_session, session) && ReferenceEquals(policyEditor is null ? session.World : PersonnelPresentationOwner(session), world) &&
            (policyEditor is not null || ReferenceEquals(_world, world)) && _residentMap == map;
        GovernanceRosterOfficer Person(int id, int? cost, int? pending, bool selectable = true)
        {
            var person = world.Officers.GetValueOrDefault(id);
            string? policy = person?.PolicyId is int policyId ? world.Policies.GetValueOrDefault(policyId)?.Name : null;
            return new(id, person?.Name ?? _ui.Get("detail_unknown"), policy, cost, pending, selectable);
        }
        GovernanceRosterPageModel BuildModel()
        {
            var value = new GovernanceRosterPageModel(force.Name,
            current.Advisor is { } advisor ? Person(advisor.OfficerId, 0, advisor.PendingValue) : null,
            current.Candidates.Select(person => Person(person.OfficerId, 0, null, person.AdvisorEligible == true)).ToArray(),
            current.Groups.Select(group => new GovernanceRosterDepartment(group.Group,
                messages.Records[group.NameMessageId].Text ?? throw new InvalidDataException("Missing governance department name."),
                group.Capacity, group.Expansion,
                group.Members.Select(member => Person(member.OfficerId,
                    current.Candidates.FirstOrDefault(person => person.OfficerId == member.OfficerId)?.PolicyCost, member.PendingValue)).ToArray(),
                current.Candidates.Where(person => person.Group == group.Group).Select(person =>
                    Person(person.OfficerId, person.PolicyCost, null, person.DepartmentEligible == true)).ToArray())).ToArray(),
            true); // This route owns editing; each command retains its independent source quote.
            var expanded = GovernanceRosterPresentation.Enrich(world,player,value,automaticRules,
                _campaignComposition?.DetailForces(world,[player]).GetValueOrDefault(player),
                doctrines:_campaignComposition?.Scenario.Snapshot.DoctrineParameters);
            var quotes = current.Groups.ToDictionary(group=>group.Group,group=>ExpansionQuote(group.Group));
            return expanded with { ShowCommandBooks=true, RemainingExpansions=quotes.Values.FirstOrDefault()?.RemainingPoints,
                CanExpand=quotes.ToDictionary(pair=>pair.Key,pair=>pair.Value.Allowed==true) };
        }
        GovernanceExpansionEvaluation ExpansionQuote(int group) => policyEntry is int entry
            ? session.EvaluateGovernanceExpansionForPolicyLevelUp(world,policyEditor!.Sequence,entry,group)
            : session.EvaluateGovernanceExpansion(group);
        void OpenSelection(int? group)
        {
            if(!Current() || automaticRules is null || _governanceRosterConfirmation is not null)return;
            var selected=view.Selection;
            var roster=new GovernanceRoster(selected.AdvisorId,selected.Departments.Select(row=>
                new GovernanceDepartmentConfiguration(row.Group,row.OfficerIds)).ToArray());
            var department=group is int id?model.Departments.Single(row=>row.Group==id):null;
            if(department is not null&&department.Capacity is null)
            {view.ApplyHint(_ui.Get("play_governance_roster_unavailable"));return;}
            var candidates=department is null?model.AdvisorCandidates:department.Candidates;
            int[] ids=candidates.Where(row=>row.Selectable || (department is null?selected.AdvisorId==row.Id:
                selected.Departments.Single(item=>item.Group==department.Group).OfficerIds.Contains(row.Id))).Select(row=>row.Id).ToArray();
            var pages=automaticRules.PrepareGovernanceOfficerInformationPages(world,player,ids,group is null,roster);
            IReadOnlyList<int> initial=group is int selectedGroup?selected.Departments.Single(row=>row.Group==selectedGroup).OfficerIds:
                selected.AdvisorId is int adviser?[adviser]:[];
            var dialog=new GovernanceRosterSelectorPage();
            GovernanceDoctrineSmallPanel? panel=null;
            view.ShowSelector(dialog,group is null);
            try
            {
                string title=_repository!.LoadMessages(_ui.SourceLocale,MessageKind.Data).Messages.Sections[1].Records[0x32E].Text
                    ??throw new InvalidDataException("Missing government selector title.");
                string guide=GovernanceRosterMessages.Format(_repository,_ui.SourceLocale,group is null?0x57EA:0x57F1,department?.Name);
                dialog.Configure(_repository,_ui,_playCommandDeck.Theme,pages,initial,department?.Capacity??1,group is null,title,
                    ()=>{dialog.Abandon();view.TryCloseSelector();},
                    values=>{if(!Current())return;dialog.Abandon();view.TryCloseSelector();view.ApplyPickedSelection(group,values);},
                    CurrentListScrollSpeed,guide,
                    mountSourcePanel:chrome=>
                    {
                        panel=new();panel.Configure(_repository,chrome,model.DoctrineId??0,
                            model.DoctrineProgress??new(null,null,null,null));
                        panel.ApplyRuler(model.Ruler?.Name??_ui.Get("detail_unknown"),view.RulerFace);
                    },
                    selectionChanged:values=>
                    {
                        if(!Current()||panel is null)return;
                        var picked=selected.WithPick(group,values,model.Departments);
                        var candidate=new GovernanceRoster(picked.AdvisorId,picked.Departments.Select(row=>
                            new GovernanceDepartmentConfiguration(row.Group,row.OfficerIds)).ToArray());
                        var quote=policyEntry is int entry
                            ?session.EvaluateGovernanceRosterForPolicyLevelUp(world,policyEditor!.Sequence,entry,candidate)
                            :session.EvaluateGovernanceRoster(candidate);
                        panel.Apply(quote.PolicyCost,quote.CostLimit,values.Count,department?.Capacity??1);
                    });
            }
            catch{dialog.Abandon();view.TryCloseSelector();throw;}
        }
        void RequestReturn()
        {
            if(view.TryCloseSelector() || view.TryCloseEffectDialog())return;
            if(!Current() || _governanceRosterConfirmation is not null)return;
            if(!view.HasChanges){CloseGovernanceRoster(abandon:false);return;}
            Ask(GovernanceRosterMessages.Format(_repository!,_ui.SourceLocale,0x57FE),accepted=>
            {if(accepted)CloseGovernanceRoster(abandon:false);});
        }
        void RequestExpansion(int group)
        {
            if(!Current() || _governanceRosterConfirmation is not null || ExpansionQuote(group).Allowed!=true)return;
            Ask(GovernanceRosterMessages.Format(_repository!,_ui.SourceLocale,0x5A62),accepted=>
            {
                if(!accepted || !Current())return;
                var order=new ExpandGovernanceDepartmentOrder(player,group);
                bool issued;
                if(policyEditor is not null && policyEntry is int entry)
                {
                    var result=session.IssuePolicyLevelUpOrder(world,policyEditor.Sequence,entry,order,new(_presentationRandom.State));
                    issued=result.Accepted;
                    if(!issued)view.ApplyHint(result.Error??_ui.Get("play_governance_roster_denied"));
                    else {world=PersonnelPresentationOwner(session);AdoptExpansionOwner();SynchronizeForcePresentationOwner();}
                }
                else issued=ApplyPlayableOrder(order,()=>{world=session.World;AdoptExpansionOwner();});
                if(issued)
                {
                    current=rules.QueryGovernanceRoster(world,player);model=BuildModel();view.ApplyModel(model);
                    view.ApplyHint(GovernanceRosterMessages.Format(_repository!,_ui.SourceLocale,0x5A63));
                }
                else view.ApplyHint(_playMessage.Text);
            });
        }
        void AdoptExpansionOwner()
        {_governanceRosterWorld=world;_governanceRosterAdoptSpeechWorld?.Invoke(world);}
        void Ask(string prompt,Action<bool> completed)
        {
            var question=new DeploymentTargetConfirmation();_governanceRosterConfirmation=question;AddChild(question);view.SetBusy(true);
            try
            {
                question.ConfigureWrappedMessage(_repository!,_ui,prompt,accepted=>RunPlayAction(()=>
                {
                    if(_governanceRosterConfirmation!=question)return;
                    if(!Current()){CloseGovernanceRoster(abandon:true);return;}
                    CloseGovernanceRosterConfirmation();completed(accepted);
                }));
                question.Name="GovernanceRosterConfirmation";
            }
            catch{CloseGovernanceRosterConfirmation();throw;}
        }
        void ApplyAutomatic()
        {
            if (!Current() || _governanceRosterConfirmation is not null || automaticRules is null) return;
            var selected = view.Selection;
            var input = new GovernanceRoster(selected.AdvisorId, selected.Departments.Select(group =>
                new GovernanceDepartmentConfiguration(group.Group, group.OfficerIds.ToArray())).ToArray());
            var result = automaticRules.PrepareGovernanceAutoPolicy(world, player, input, _tutorialLesson?.Lesson.World ?? world);
            if (result.Roster is not { } roster)
            {
                view.ApplyHint(_ui.Get("play_governance_roster_unavailable"));
                if (result.UnresolvedOwner is { } missing) GD.PushWarning(missing);
                return;
            }
            //The normal producer only changes this page's temporary roster.
            //Its later actual confirm still issues SetGovernanceRosterOrder.
            view.ApplyAutomaticSelection(new(roster.AdvisorId, roster.Groups.Select(group =>
                new GovernanceDepartmentSelection(group.Group, group.OfficerIds.ToArray())).ToArray()));
        }
        void ShowEffects()
        {
            if (!Current() || _governanceRosterConfirmation is not null || automaticRules is null) return;
            var selected = view.Selection;
            var roster = new GovernanceRoster(selected.AdvisorId, selected.Departments.Select(group =>
                new GovernanceDepartmentConfiguration(group.Group, group.OfficerIds.ToArray())).ToArray());
            var publication = automaticRules.ReadGovernanceRosterEffects(world, player, roster, _tutorialLesson?.Lesson.World ?? world);
            if (publication.Columns.Count == 0)
            {
                view.ApplyHint(_ui.Get("play_governance_roster_unavailable"));
                if (publication.UnresolvedOwner is { } missing) GD.PushWarning(missing);
                return;
            }
            var columns = publication.Columns.Select(column => new MapInformationListColumn(
                column.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), column.Caption, column.Width,
                column.CanSort, !column.InitialDescending)).ToArray();
            MapInformationListRow Row(GovernanceRosterEffectRow row) => new(row.EffectId,
                publication.Columns.Select(column => row.Cells.GetValueOrDefault(column.Id) is { } value &&
                    !(column.Id == 2 && row.UnresolvedOwner is not null && value.Length == 0)
                        ? value : _ui.Get("detail_unknown")).ToArray(), false);
            view.ShowEffectDialog((dialog, close) =>
            {
                dialog.Configure(_repository!, _ui, publication.Title, columns, publication.Rows.Select(Row).ToArray(),
                    () => Current() && view.EffectDialogVisible && !TutorialOverlayOwnsInput, close, _ => { }, CurrentListScrollSpeed,
                    sort: (key, ascending) => key switch
                    {
                        "0" => (ascending ? publication.Rows.OrderBy(row => row.EffectId) : publication.Rows.OrderByDescending(row => row.EffectId))
                            .Select(Row).ToArray(),
                        "1" => (ascending ? publication.Rows.OrderBy(row => Math.Min(row.Level, 10)) : publication.Rows.OrderByDescending(row => Math.Min(row.Level, 10)))
                            .Select(Row).ToArray(),
                        _ => throw new InvalidOperationException("This governance effect column has no comparator.")
                    },
                    tabCaptions: [publication.BasicTabCaption], normalColumnLayout: true,
                    initialSortColumn: "0", initialSortAscending: true, retainSortDirections: true,
                    notice: publication.UnresolvedOwner is null ? null : _ui.Get("detail_unknown"));
                dialog.RegisterHeader(_sourceHeaders);
            });
        }
        void Evaluate(GovernanceRosterSelection selected)
        {
            if (!Current() || _governanceRosterConfirmation is not null) return;
            draft = new(selected.AdvisorId, selected.Departments.Select(group =>
                new GovernanceDepartmentConfiguration(group.Group, group.OfficerIds.ToArray())).ToArray());
            evaluation = current.Available ? policyEntry is { } policy
                ? session.EvaluateGovernanceRosterForPolicyLevelUp(world, policyEditor!.Sequence, policy, draft)
                : session.EvaluateGovernanceRoster(draft)
                : new(null, null, null, current.PolicyCost, current.CostLimit, null, null, current.UnresolvedInput);
            string status = _ui.Get(evaluation.Allowed switch
            {
                true => evaluation.ExceedsLevelLimit == true ? "play_governance_roster_overflow" : "play_governance_roster_ready",
                false => "play_governance_roster_denied",
                _ => "play_governance_roster_unavailable"
            });
            view.ApplyEvaluation(new(evaluation.Allowed, evaluation.CommandBooks,
                evaluation.PolicyCost ?? (!view.HasChanges ? current.PolicyCost : null),
                evaluation.CostLimit ?? (!view.HasChanges ? current.CostLimit : null), evaluation.ExceedsLevelLimit, status));
        }
        void RequestConfirm()
        {
            if (!Current() || _governanceRosterConfirmation is not null || !view.HasChanges || draft is null ||
                evaluation is not { Allowed: true, CommandBooks: int books, ExceedsLevelLimit: bool overflow }) return;
            var order = new SetGovernanceRosterOrder(player, draft, overflow);
            string prompt = _ui.Format("play_governance_roster_confirm", books);
            if (overflow) prompt += "\n" + _ui.Get("play_governance_roster_overflow_confirm");
            var question = new DeploymentTargetConfirmation(); _governanceRosterConfirmation = question; AddChild(question);
            view.SetBusy(true);
            try
            {
                question.ConfigureWrappedMessage(_repository!, _ui, prompt, accepted => RunPlayAction(() =>
                {
                    if (_governanceRosterConfirmation != question) return;
                    if (!Current()) { CloseGovernanceRoster(abandon: true); return; }
                    CloseGovernanceRosterConfirmation();
                    if (accepted)
                    {
                        if (policyEditor is not null && policyEntry is int entryIndex)
                        {
                            var result = session.IssuePolicyLevelUpOrder(world, policyEditor.Sequence, entryIndex, order,
                                new(_presentationRandom.State));
                            if (result.Accepted)
                            {
                                CloseGovernanceRoster(abandon: true);
                                SynchronizeForcePresentationOwner();
                                ShowPolicyGovernanceNarrations(policyEditor);
                            }
                            else view.ApplyHint(result.Error ?? _ui.Get("play_governance_roster_denied"));
                        }
                        else if (ApplyPlayableOrder(order, () => CloseGovernanceRoster(abandon:true))) TryOpenGovernanceNarration();
                        else view.ApplyHint(_playMessage.Text);
                    }
                }));
                question.Name = "GovernanceRosterConfirmation";
            }
            catch { CloseGovernanceRosterConfirmation(); throw; }
        }
    }

    private void CloseGovernanceRosterConfirmation(bool restorePage = true)
    {
        var question = _governanceRosterConfirmation; _governanceRosterConfirmation = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        if (restorePage) _governanceRosterPage?.SetBusy(false);
    }
    private void CloseGovernanceRoster()
    {
        if (_governanceRosterRequestReturn is { } request) { request(); return; }
        CloseGovernanceRoster(abandon: false);
    }

    private void CloseGovernanceRoster(bool abandon)
    {
        var map = _governanceRosterMap; var view = _governanceRosterPage;
        CloseGovernanceRosterConfirmation(restorePage: false);
        _governanceRosterAdoptSpeechWorld = null; _governanceRosterRequestReturn = null;
        _governanceRosterPage = null; _governanceRosterSession = null; _governanceRosterWorld = null; _governanceRosterMap = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (map is not null && map == _residentMap && GodotObject.IsInstanceValid(map)) map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown && map is not null && map == _residentMap) SetMapFocus(true);
        if (!abandon && view is not null && _policyRosterEditor is { } policy) CompletePolicyRosterEditor(policy);
    }
}
