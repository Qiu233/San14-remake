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
    private Button _playGovernanceOpen = null!;
    private bool HasGovernanceRosterPage => _governanceRosterPage is not null;

    private void OpenGovernanceRoster() => OpenGovernanceRoster(null);

    private bool IsGovernanceRosterCurrentOwner => _governanceRosterSession == _session &&
        _session is { } session && _governanceRosterMap == _residentMap &&
        ReferenceEquals(_governanceRosterWorld, _policyRosterEditor is null ? session.World : PersonnelPresentationOwner(session));

    private void OpenGovernanceRoster(PolicyRosterEditorContext? policyEditor)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasAdministrationPage || HasMilitaryOrderPage ||
            HasCampaignSavePage || HasMapSystemMenu || HasActionReport || HasPersonnelConfirmation || _detailsView is not null ||
            _repository is null || _residentMap is not { IsPlayableMapReady: true } map ||
            _session is not { PlayerForceId: int player } session || session.Rules.GovernanceRosterRules is not { } rules) return;
        var world = policyEditor is null ? session.World : PersonnelPresentationOwner(session);
        if ((policyEditor is null && (world.Phase != TurnPhase.Strategy || !ReferenceEquals(world, _world))) ||
            !world.Forces.TryGetValue(player, out var force) || PlayableOutcome.Evaluate(world, player) != PlayableResult.Ongoing) return;
        var policyEntry = policyEditor is null ? null : FindPolicyEditorEntry(world, policyEditor.Sequence, player);
        if (policyEditor is not null && policyEntry is null) return;
        var current = rules.QueryGovernanceRoster(world, player);
        var messages = _repository.LoadMessages(_ui.SourceLocale, MessageKind.Data).Messages.Sections[0];
        var model = new GovernanceRosterPageModel(force.Name,
            current.Advisor is { } advisor ? Person(advisor.OfficerId, 0, advisor.PendingValue) : null,
            current.Candidates.Select(person => Person(person.OfficerId, 0, null, person.AdvisorEligible == true)).ToArray(),
            current.Groups.Select(group => new GovernanceRosterDepartment(group.Group,
                messages.Records[group.NameMessageId].Text ?? throw new InvalidDataException("Missing governance department name."),
                group.Capacity, group.Expansion,
                group.Members.Select(member => Person(member.OfficerId,
                    current.Candidates.FirstOrDefault(person => person.OfficerId == member.OfficerId)?.PolicyCost, member.PendingValue)).ToArray(),
                current.Candidates.Where(person => person.Group == group.Group).Select(person =>
                    Person(person.OfficerId, person.PolicyCost, null, person.DepartmentEligible == true)).ToArray())).ToArray(),
            current.Available);
        CloseCommandWorkspace();
        var view = new GovernanceRosterPage(); _governanceRosterPage = view;
        _governanceRosterSession = session; _governanceRosterWorld = world; _governanceRosterMap = map;
        GovernanceRoster? draft = null;
        GovernanceRosterEvaluation? evaluation = null;
        AddChild(view);
        try
        {
            view.Configure(_repository, _ui, _playCommandDeck.Theme, model, () => CloseGovernanceRoster(), RequestConfirm, Evaluate);
            SetMapFocus(true); map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
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
                        else if (ApplyPlayableOrder(order, () => CloseGovernanceRoster())) TryOpenGovernanceNarration();
                        else view.ApplyHint(_playMessage.Text);
                    }
                }));
                question.Name = "GovernanceRosterConfirmation";
            }
            catch { CloseGovernanceRosterConfirmation(); throw; }
        }
    }

    private void CloseGovernanceRosterConfirmation()
    {
        var question = _governanceRosterConfirmation; _governanceRosterConfirmation = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        _governanceRosterPage?.SetBusy(false);
    }
    private void CloseGovernanceRoster() => CloseGovernanceRoster(abandon: false);

    private void CloseGovernanceRoster(bool abandon)
    {
        var map = _governanceRosterMap; var view = _governanceRosterPage;
        CloseGovernanceRosterConfirmation();
        _governanceRosterPage = null; _governanceRosterSession = null; _governanceRosterWorld = null; _governanceRosterMap = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (map is not null && map == _residentMap && GodotObject.IsInstanceValid(map)) map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown && map is not null && map == _residentMap) SetMapFocus(true);
        if (!abandon && view is not null && _policyRosterEditor is { } policy) CompletePolicyRosterEditor(policy);
    }
}
