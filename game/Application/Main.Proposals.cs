using Godot;
using San14.Client.Gameplay;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

public partial class Main
{
    private ProposalView? _proposalsView;
    private ProposalDraft? _proposalDraft;
    private GameplaySession? _proposalSession;
    private GameState? _proposalWorld;
    private MapSceneView? _proposalMap;
    private MapInformationBrowserView? _proposalInformation;
    private bool _proposalConfirming;
    private Action<GameState>? _proposalConfirmAdoptSpeechWorld;
    private Action<GameState>? _proposalInformationAdoptSpeechWorld;
    private Action<GameState, GameState>? _proposalEffectsAdoptSpeechWorld;
    private Func<bool>? _proposalEffectsCurrent;
    private bool IsProposalWorkspaceCurrent => _proposalsView is not null ? IsProposalsCurrent :
        _proposalEffectsCurrent?.Invoke() == true;

    private void AdoptProposalsSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        if (_proposalsView is not null && _proposalSession == session && _proposalMap == _residentMap &&
            ReferenceEquals(_proposalWorld, previous))
        {
            // The source board, selected slots and prepared confirmation remain
            // the same publication across this presentation-only transaction.
            _proposalWorld = replacement;
            _proposalConfirmAdoptSpeechWorld?.Invoke(replacement);
            _proposalInformationAdoptSpeechWorld?.Invoke(replacement);
        }
        _proposalEffectsAdoptSpeechWorld?.Invoke(previous, replacement);
    }
    private bool HasProposalsPage => _proposalsView is not null || HasProposalResults || HasProposalOfficerDetail;
    private bool IsProposalsCurrent => !_failed && !_shuttingDown && !IsReturningToTitle &&
        _proposalsView is not null && _proposalDraft is not null &&
        ReferenceEquals(_session, _proposalSession) && ReferenceEquals(_world, _proposalWorld) &&
        ReferenceEquals(_session?.World, _proposalWorld) && _residentMap == _proposalMap && _proposalMap is not null;
    private bool CanEditProposals => IsProposalsCurrent && !_advancingPeriod &&
        _proposalInformation is null && _proposalTargetMap is null && !_proposalConfirming && !HasPersonnelPresentation;

    private void OpenProposals()
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasAdministrationPage ||
            HasMilitaryOrderPage || HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace ||
            HasActionReport || HasPersonnelConfirmation || _detailsView is not null || _repository is null ||
            _campaignComposition is not { } composition || _session is not { PlayerForceId: int player } session ||
            session.HasPendingOperation || session.World is not { Phase: TurnPhase.Strategy } world ||
            !ReferenceEquals(world, _world) || _residentMap is not { IsPlayableMapReady: true } map) return;
        var draft = composition.PrepareArmyQueries(world).OpenProposalDraft(world, player);
        var admission = draft.Admission;
        if (!admission.Available || admission.Value is not { } entry)
        { ProposalUnavailable(admission.UnresolvedInput ?? "proposal.entry"); return; }
        string entryHint = new AdministrationMessages(_repository, _ui.SourceLocale).Format(entry.GameMessageId);
        if (!entry.Allowed) { _playMessage.Text = entryHint; return; }
        if (draft.UnresolvedOwner is { } missing) { ProposalUnavailable(missing); return; }
        var view = new ProposalView(); _proposalsView = view; _proposalDraft = draft;
        _proposalSession = session; _proposalWorld = world; _proposalMap = map;
        AddChild(view);
        try
        {
            view.Configure(_repository, _ui, draft.Page, () => IsProposalsCurrent && _proposalInformation is null,
                CloseProposals, SelectProposal, ConfirmProposals, RunUiAction);
            view.ApplyHint(entryHint);
            view.DetailRequested += InspectProposalTarget;
            view.InformationRequested += OpenProposalInformation;
            view.SelectAllRequested += () =>
            {
                if (!CanEditProposals) return;
                draft.SelectAll(); RefreshProposalView();
            };
            map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true);
            SetMapFocus(true); HideDeploymentHud();
            ObserveTutorialInformation(46);
        }
        catch { CloseProposals(); throw; }
    }

    private void SelectProposal(int id)
    {
        if (!CanEditProposals) return;
        var draft = _proposalDraft!; var view = _proposalsView!;
        bool officerPage = view.IsOfficerPage;
        var row = (officerPage ? draft.Page.OfficerRows : draft.Page.FirstRows).FirstOrDefault(row => row.Id == id);
        if (row is null) return;
        if (!draft.Select(id, officerPage)) { RefreshProposalView(); return; }
        RefreshProposalView();
        if (row.OpensOfficerPage) view.ShowOfficer();
        else if (!officerPage) ConfirmProposals();
    }

    private void RefreshProposalView()
    {
        if (!IsProposalsCurrent) return;
        _proposalsView!.Apply(_proposalDraft!.Page);
        if (_proposalDraft.UnresolvedOwner is { } missing) ProposalUnavailable(missing);
    }

    private void ConfirmProposals()
    {
        if (!CanEditProposals || _proposalDraft!.BuildOrder() is not { } order) return;
        var draft = _proposalDraft; var view = _proposalsView!;
        var world = _proposalWorld; var session = _proposalSession;
        int[] selected = order.SlotIds.ToArray();
        var query = draft.QueryConfirmation();
        if (!query.Available || query.Value is null)
        { ProposalUnavailable(query.UnresolvedInput ?? "proposal.confirmation"); return; }
        // The UI owns only this question. The immutable order is submitted once
        // after checking that its actual owner and selected source slots survive.
        _proposalConfirmAdoptSpeechWorld = next => world = next;
        view.ShowConfirmation(query.Value, answer =>
        {
            _proposalConfirming = false; _proposalConfirmAdoptSpeechWorld = null;
            if (!answer || !CanEditProposals || !ReferenceEquals(_proposalDraft, draft) ||
                !ReferenceEquals(_proposalWorld, world) || !ReferenceEquals(_proposalSession, session) ||
                draft.BuildOrder() is not { } current || current.ForceId != order.ForceId ||
                !current.SlotIds.SequenceEqual(selected)) return;
            RunPlayAction(() =>
            {
                if (!CanEditProposals || !ReferenceEquals(_proposalDraft, draft)) return;
                bool officerSelection = view.IsOfficerPage;
                if (ApplyPlayableOrder(order, CloseProposals))
                {
                    if (officerSelection) ObserveTutorialProposalOfficerAccepted();
                }
                else
                { RefreshProposalView(); view.ApplyHint(_playMessage.Text); }
            });
        });
        _proposalConfirming = true;
    }

    private void InspectProposalTarget(int id)
    {
        if (!CanEditProposals) return;
        var row = _proposalDraft!.Page.FirstRows.Concat(_proposalDraft.Page.OfficerRows)
            .FirstOrDefault(row => row.Id == id);
        if (row?.DetailId is int hex) OpenProposalTargetMap(hex);
        else ProposalUnavailable($"proposal.detail.target.{id}");
    }

    private void ProposalUnavailable(string missing)
    {
        _playMessage.Text = _ui.Get("detail_unknown");
        _proposalsView?.ApplyHint(_playMessage.Text); GD.PushWarning(missing);
    }

    private void RequestProposalsBack()
    {
        if (_proposalTargetMap is { } targetMap) { targetMap.RequestBack(); return; }
        if (_proposalOfficerDetail is { } detail) { detail.RequestClose(); return; }
        if (_proposalResultsView is { } results) { results.RequestBack(); return; }
        if (_proposalInformation is { } information) { information.RequestClose(); return; }
        _proposalsView?.RequestBack();
    }

    private void CloseProposals()
    {
        CloseProposalTargetMap(resume: false); CloseProposalOfficerDetail(); CloseProposalResults(); CloseProposalInformation(resume: false);
        var view = _proposalsView; var map = _proposalMap;
        _proposalsView = null; _proposalDraft = null; _proposalSession = null;
        _proposalWorld = null; _proposalMap = null; _proposalConfirming = false;
        _proposalConfirmAdoptSpeechWorld = null;
        if (view is not null) { view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree(); }
        if (map is not null && GodotObject.IsInstanceValid(map) && _residentMap == map)
        {
            map.SetDetailsOpen(false);
            if (!_failed && !_shuttingDown) SetMapFocus(_mapFocused);
        }
    }

    private void CloseProposalInformation(bool resume = true)
    {
        _proposalInformationAdoptSpeechWorld = null;
        var child = _proposalInformation; _proposalInformation = null;
        if (child is not null) { child.Hide(); child.GetParent()?.RemoveChild(child); child.QueueFree(); }
        if (resume && IsProposalsCurrent && _proposalsView is { } view)
        { view.ProcessMode = ProcessModeEnum.Inherit; view.Show(); RefreshProposalView(); }
    }

    private void OpenProposalInformation()
    {
        if (!CanEditProposals || _repository is null || _campaignComposition is not { } composition ||
            _proposalSession is not { PlayerForceId: int player } session || _proposalWorld is not { } world ||
            _proposalsView is not { } parent) return;
        var rules = composition.PrepareArmyQueries(world);
        var page = rules.PrepareInformationBrowserPage(world, player);
        var initial = page.Menu(InformationTab.OwnForce);
        if (initial.InitialTab is not { } initialTab || initial.TabEnabled.Length != 3 ||
            initial.TabEnabled[(int)initialTab] != true)
        { ProposalUnavailable(initial.UnresolvedOwner ?? "proposal.information.initial_tab"); return; }
        var browser = new MapInformationBrowserView(); _proposalInformation = browser;
        AddChild(browser); parent.Hide(); parent.ProcessMode = ProcessModeEnum.Disabled;
        browser.BindRetainedSpeechHost(Current);
        _proposalInformationAdoptSpeechWorld = next =>
        {
            world = next;
            rules = composition.PrepareArmyQueries(next);
            page = rules.PrepareInformationBrowserPage(next, player);
            browser.AdoptRetainedSpeechWorld(next);
        };
        bool Current() => IsProposalsCurrent && _proposalInformation == browser &&
            ReferenceEquals(_proposalSession, session) && ReferenceEquals(_proposalWorld, world);
        void AdoptWorld(GameState next)
        {
            int[] selected = _proposalDraft!.Page.SelectedProposalIds.ToArray();
            bool officer = parent.IsOfficerPage;
            world = next; _proposalWorld = next;
            rules = composition.PrepareArmyQueries(next);
            page = rules.PrepareInformationBrowserPage(next, player);
            _proposalDraft.Refresh(next);
            foreach (int id in selected) _proposalDraft.Select(id, officer);
        }
        try
        {
            browser.Configure(_repository, _ui, _sourceHeaders, Current, ReadMenu,
                (tab, node) => RunUiAction(() => OpenList(tab, node)),
                () => RunUiAction(OpenHistory), () => CloseProposalInformation(), CurrentListScrollSpeed,
                Literal(0x5F00), Literal(0x2733), true, (int)initialTab,
                initial.TabEnabled.Select(value => value == true).ToArray(), TryBorrowApplicationUiResources(browser));
            browser.RegisterHeader(_sourceHeaders);
        }
        catch { CloseProposalInformation(); throw; }

        string Literal(int id) => _repository.LoadMessages(_ui.SourceLocale, MessageKind.Game)
            .Messages.Sections[id / 10000].Records[id % 10000].Literals.Single().Text;
        IReadOnlyList<MapInformationMenuEntry> ReadMenu(int tab) => page.Menu((InformationTab)tab).Entries
            .Select(entry => new MapInformationMenuEntry(entry.Node, entry.Caption, entry.Visible,
                entry.Enabled == true, entry.UnresolvedOwner is null ? null : _ui.Get("information_partial"))).ToArray();
        void Inspect(InformationList publication, InformationRow row)
        {
            if (!Current()) return;
            if (row.Detail is { Kind: InformationListKind.Officers } person && world.Officers.ContainsKey(person.Id))
            {
                var detail = new OfficerDetailsView();
                browser.ShowDetail(detail, close => ConfigureCurrentOfficerDetail(detail, world, person.Id,
                    close, Current, AdoptWorld));
            }
            else OpenInformationRecord(browser, rules, world, publication, row, AdoptWorld);
        }
        void OpenHistory()
        {
            if (!Current()) return;
            var publication = page.QueryHistory();
            if (publication.UnresolvedOwner is { } missing) GD.PushWarning(missing);
            var detail = new MapInformationHistoryView();
            browser.ShowDetail(detail, close => detail.Configure(_repository, _ui, publication,
                Current, close, CurrentListScrollSpeed, TryBorrowApplicationUiResources(detail)));
        }
        void OpenList(int tab, int node)
        {
            if (!Current()) return;
            if ((InformationTab)tab == InformationTab.OwnForce && node == 48) { OpenBelongings(); return; }
            var entry=page.Menu((InformationTab)tab).Entries.FirstOrDefault(value=>value.Node==node);
            if(entry is {Enabled:true} && TryOpenTypedInformationPages(browser,rules,world,player,
                (InformationTab)tab,entry.Kind,AdoptWorld))return;
            var publication = page.OpenList((InformationTab)tab, node);
            if (publication.UnresolvedOwner is { } missing) GD.PushWarning(missing);
            if (publication.DestinySummary is { } popularity)
            {
                var detail = new MapInformationDestinyView();
                browser.ShowDetail(detail, close => detail.Configure(_repository, _ui, popularity,
                    Current, close, CurrentListScrollSpeed, TryBorrowApplicationUiResources(detail)));
                return;
            }
            if ((InformationTab)tab == InformationTab.OwnForce && node == 20)
            {
                var ownForce = publication.Rows.FirstOrDefault(row =>
                    row.Detail is { Kind: InformationListKind.Forces } force && force.Id == player);
                if (ownForce is null) browser.ApplyNotice(_ui.Get("information_partial"));
                else Inspect(publication, ownForce);
                return;
            }
            if (publication.Rows.IsEmpty && publication.BodyText is { } body)
            {
                var detail = new MapInformationRecordView();
                browser.ShowDetail(detail, close => detail.Configure(_repository, _ui, publication.Title,
                    [new(publication.Title, body)], close, CurrentListScrollSpeed, TryBorrowApplicationUiResources(detail)));
                return;
            }
            if (publication.Columns.IsEmpty) { browser.ApplyNotice(_ui.Get("information_partial")); return; }
            var columns = publication.Columns.Select(column => new MapInformationListColumn(column.Key,
                InformationColumnCaption(column), column.Key == "description" ? 640 : column.Key == "name" ? 260 :
                column.Key is "force" or "location" or "ruler" or "governor" or "owner" ? 200 : 148)).ToArray();
            var indices = new Dictionary<InformationRow, int>(ReferenceEqualityComparer.Instance);
            for (int i = 0; i < publication.Rows.Length; i++) indices.Add(publication.Rows[i], i);
            IReadOnlyList<MapInformationListRow> Rows(InformationList ordered) => ordered.Rows.Select(row =>
                new MapInformationListRow(indices[row], publication.Columns.Select(column =>
                    InformationCellText(row.Cells.GetValueOrDefault(column.Key))).ToArray(), true)).ToArray();
            browser.ShowList(publication.Title, columns, Rows(publication), index => RunUiAction(() =>
            {
                if (Current() && (uint)index < publication.Rows.Length) Inspect(publication, publication.Rows[index]);
            }), (key, ascending) => Rows(publication.Sort(key, ascending)),
                publication.UnresolvedOwner is null ? null : _ui.Get("information_partial"));
        }
        void OpenBelongings()
        {
            var prepared=rules.PrepareBelongingsInformationPages(world,player);
            if(!prepared.Available||prepared.Value is not { } publication)
            {GD.PushWarning(prepared.UnresolvedInput??"belongings.current");browser.ApplyNotice(_ui.Get("information_partial"));return;}
            var detail=new MapInformationBelongingsView();
            browser.ShowDetail(detail,close=>detail.Configure(_repository!,_ui,publication,Current,close,
                item=>RunUiAction(()=>
                {
                    if(!Current()||!browser.IsCurrentDetail(detail))return;
                    OpenInformationItem(browser,rules,world,publication.Items.CurrentDefinitionIds,item,
                        detail.ApplyItemDetailReturn,publication.Items.NameSortAscending);
                }),CurrentListScrollSpeed,TryBorrowApplicationUiResources(detail)));
        }
    }
}
