using System.Globalization;
using Godot;
using San14.Client.Gameplay;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

public partial class Main
{
    private Control? _personnelAdministrationPage;
    private CommandSelectionView? _personnelAdministrationPeople;
    private DeploymentTargetConfirmation? _personnelAdministrationQuestion;
    private GameplaySession? _personnelAdministrationSession;
    private GameState? _personnelAdministrationWorld;
    private MapSceneView? _personnelAdministrationMap;
    private Action? _personnelAdministrationBack, _personnelAdministrationAbandon;
    private bool HasPersonnelAdministrationPage => _personnelAdministrationPage is not null;

    private bool IsPersonnelAdministrationCurrent(Control page) => !_failed && !_shuttingDown &&
        _personnelAdministrationPage == page && ReferenceEquals(_session, _personnelAdministrationSession) &&
        ReferenceEquals(_world, _personnelAdministrationWorld) && ReferenceEquals(_session?.World, _personnelAdministrationWorld) &&
        _residentMap == _personnelAdministrationMap;

    private void OpenPersonnelAdministration(MapMenuCommand command, int? retainedOfficer = null)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasAdministrationPage ||
            HasMilitaryOrderPage || HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || HasActionReport ||
            HasPersonnelConfirmation || _detailsView is not null || _repository is null ||
            _campaignComposition is not { } composition || _residentMap is not { IsPlayableMapReady: true } map ||
            _session is not { PlayerForceId: int player } session || session.HasPendingOperation ||
            session.World is not { Phase: TurnPhase.Strategy } world ||
            PlayableOutcome.Evaluate(world, player) != PlayableResult.Ongoing) return;
        var rules = composition.PrepareArmyQueries(world);
        if (command == MapMenuCommand.Reward) OpenReward();
        else if (command == MapMenuCommand.Expel) OpenBanish();
        else if (command is MapMenuCommand.Merit or MapMenuCommand.Award) OpenCredit(command == MapMenuCommand.Merit);
        else if (command is MapMenuCommand.Employ or MapMenuCommand.Learn or MapMenuCommand.Introduce)
            OpenPersonnelDevelopment(command, rules, world, player, session, map, retainedOfficer);

        void OpenReward()
        {
            var draft = rules.OpenPersonnelRewardDraft(world, player);
            if (draft.UnresolvedOwner is { } missing) { Unavailable(missing); return; }
            var view = new PersonnelRewardView();
            var sprites = new LocalizedUiSpriteSource(_repository);
            var portraits = new Dictionary<int, LoadedSprite?>();
            foreach (var row in draft.Rows)
                portraits[row.OfficerId] = row.Portrait is { Kind: OfficerPortraitRouteKind.StockNamed } route
                    ? sprites.LoadTexturePackSprite(route.ResourceName!, route.SpriteName!) : null;
            Begin(view, () =>
            {
                view.Configure(_repository, _ui, () => IsPersonnelAdministrationCurrent(view),
                    ChoosePeople, RequestPersonnelAdministrationBack, () => RunPlayAction(() =>
                    {
                        if (!IsPersonnelAdministrationCurrent(view) || draft.BuildOrder() is not { } order) return;
                        if (!ApplyPlayableOrder(order, ClosePersonnelAdministration)) Refresh();
                    }));
                Refresh();
            }, view.Abandon, ClosePersonnelAdministration);

            void Refresh()
            {
                var budget = draft.Budget;
                var people = draft.CardTargets.Select(row => new PersonnelRewardCardValues(row.OfficerId, row.Name,
                    portraits.GetValueOrDefault(row.OfficerId), Number(row.LoyaltyBefore),
                    row.LoyaltyBefore is int before && row.LoyaltyAfter is int after
                        ? $"{after}(^02{(after - before).ToString("+0;-0;+0", CultureInfo.InvariantCulture)}^00)" : _ui.Get("detail_unknown"), true)).ToArray();
                var fields = new Dictionary<int, string>
                {
                    [69] = Number(budget.GoldBefore), [71] = Number(budget.GoldAfter),
                    [72] = Cost(budget.GoldCost), [73] = "", [79] = Number(budget.CommandBooksBefore),
                    [81] = Number(budget.CommandBooksAfter), [82] = Cost(budget.CommandBookCost), [83] = ""
                };
                //Data03AB is the original resource format for the fixed per-person price.
                fields[73] = PersonnelPrice(100);
                view.Apply(new(people, fields, draft.CanConfirm, draft.Rows.Count > 0,
                    budget.UnresolvedOwner is null ? new AdministrationMessages(_repository, _ui.SourceLocale).Format(0x57E6)
                        : _ui.Get("information_partial")));
            }

            void ChoosePeople()
            {
                if (!IsPersonnelAdministrationCurrent(view) || _personnelAdministrationPeople is not null) return;
                var rows = draft.Rows.Select(row => new CommandSelectionRow(row.OfficerId,
                    [new(row.Name, SortKey: world.Officers.GetValueOrDefault(row.OfficerId)?.CurrentNameSortKey),
                     new(Number(row.LoyaltyBefore), row.LoyaltyBefore), new(Number(row.LoyaltyAfter), row.LoyaltyAfter)],
                    row.Eligible == true, row.UnresolvedOwner is null ? null : _ui.Get("information_partial"))).ToArray();
                var selector = new CommandSelectionView(); _personnelAdministrationPeople = selector; AddChild(selector);
                view.SetBusy(true);
                try
                {
                    selector.Configure(_repository, _ui, 0x245,
                        [new("name", PersonnelDataText(0x211), 500), new("loyalty", _ui.Get("detail_loyalty"), 458, true),
                         new("loyalty_after", _ui.Get("detail_loyalty"), 458, true)],
                        rows, draft.SelectedOfficerIds, draft.Budget.MaximumSelection ?? 0,
                        () => IsPersonnelAdministrationCurrent(view) && _personnelAdministrationPeople == selector,
                        selected => RunUiAction(() =>
                        {
                            if (!IsPersonnelAdministrationCurrent(view) || _personnelAdministrationPeople != selector) return;
                            ClosePersonnelAdministrationPeople(); view.SetBusy(false);
                            if (selected is not null) draft.SelectTargets(selected);
                            Refresh();
                        }), CurrentListScrollSpeed);
                }
                catch { ClosePersonnelAdministrationPeople(); view.SetBusy(false); throw; }
            }
        }

        void OpenBanish()
        {
            var draft = rules.OpenPersonnelBanishDraft(world, player);
            if (draft.UnresolvedOwner is { } missing) { Unavailable(missing); return; }
            var view = new CommandSelectionView();
            var rows = draft.Rows.Select(row => new CommandSelectionRow(row.OfficerId,
                [new(row.Name, SortKey: world.Officers.GetValueOrDefault(row.OfficerId)?.CurrentNameSortKey),
                 new(world.Officers.GetValueOrDefault(row.OfficerId)?.ForceId is int force ? world.Forces.GetValueOrDefault(force)?.Name ?? "" : "")],
                row.Enabled == true, row.DisabledReason)).ToArray();
            Begin(view, () => view.Configure(_repository, _ui, 0x273,
                [new("name", PersonnelDataText(0x211), 708), new("force", _ui.Get("force"), 708)],
                rows, draft.SelectedOfficerIds, rows.Length,
                () => IsPersonnelAdministrationCurrent(view) && _personnelAdministrationQuestion is null,
                chosen => RunUiAction(() =>
                {
                    if (!IsPersonnelAdministrationCurrent(view)) return;
                    if (chosen is null) { ClosePersonnelAdministration(); return; }
                    if (!draft.SelectTargets(chosen) || !draft.CanConfirm) { view.Show(); return; }
                    var prompt = draft.QueryConfirmation();
                    if (!prompt.Available) { view.ApplyNotice(_ui.Get("information_partial")); view.Show(); return; }
                    var question = new DeploymentTargetConfirmation(); _personnelAdministrationQuestion = question; AddChild(question);
                    try
                    {
                        question.ConfigureWrappedMessage(_repository, _ui, prompt.Value!, accepted => RunPlayAction(() =>
                        {
                            if (!IsPersonnelAdministrationCurrent(view) || _personnelAdministrationQuestion != question) return;
                            _personnelAdministrationQuestion = null; question.Abandon(); question.QueueFree();
                            if (accepted && draft.BuildOrder() is { } order && ApplyPlayableOrder(order, ClosePersonnelAdministration)) return;
                            view.Show();
                        }));
                    }
                    catch { _personnelAdministrationQuestion = null; question.Abandon(); question.QueueFree(); view.Show(); throw; }
                }), CurrentListScrollSpeed, guide: new AdministrationMessages(_repository, _ui.SourceLocale).Format(0x5884),
                retainOnAccept: true, requireSelection: true), view.Abandon, view.RequestCancel);
        }

        void OpenCredit(bool rankup)
        {
            var draft = rankup ? rules.OpenPersonnelCreditRankupDraft(world, player) : rules.OpenPersonnelCreditGiveDraft(world, player);
            if (draft.UnresolvedOwner is { } missing) { Unavailable(missing); return; }
            var view = new PersonnelCreditView();
            var content = new AreaAdministrationOfficerContent(_repository);
            var portraits = new Dictionary<int, LoadedSprite?>();
            var sprites = new LocalizedUiSpriteSource(_repository);
            foreach (var row in draft.Rows)
                portraits[row.OfficerId] = row.Portrait is { Kind: OfficerPortraitRouteKind.StockNamed } route
                    ? sprites.LoadTexturePackSprite(route.ResourceName!, route.SpriteName!) : null;
            Begin(view, () =>
            {
                view.Configure(_repository, _ui, rankup, () => IsPersonnelAdministrationCurrent(view),
                    RequestPersonnelAdministrationBack, () => RunPlayAction(() =>
                    {
                        if (!IsPersonnelAdministrationCurrent(view) || draft.BuildOrder() is not { } order) return;
                        if (!ApplyPlayableOrder(order, ClosePersonnelAdministration)) Refresh();
                    }), ChoosePeople, (person, choice) => RunUiAction(() =>
                    {
                        if (IsPersonnelAdministrationCurrent(view)) { draft.ToggleChoice(person, choice); Refresh(); }
                    }), mode => RunUiAction(() =>
                    {
                        if (IsPersonnelAdministrationCurrent(view)) { draft.ApplyAutomatic(mode); Refresh(); }
                    }));
                Refresh();
            }, view.Abandon, ClosePersonnelAdministration);

            void Refresh()
            {
                var budget = draft.Budget;
                var people = draft.CardTargets.Select(row =>
                {
                    var card = content.Create(row.OfficerCard);
                    var values = new Dictionary<int, string> { [10] = row.Name, [25] = PersonnelDataText(0x897) };
                    int[] names = [12, 14, 16, 18, 20];
                    for (int index = 0; index < names.Length; index++)
                        values[names[index]] = index < row.StageNames.Count ? row.StageNames[index] ?? _ui.Get("detail_unknown") : _ui.Get("detail_unknown");
                    bool showDoctrine = row.DoctrinePointCount > 0;
                    values[28] = showDoctrine ? row.DoctrineName ?? _ui.Get("detail_unknown") : "";
                    values[29] = showDoctrine ? "+" + Number(row.DoctrinePointCount) : "";
                    return new PersonnelCreditOfficerValues(row.OfficerId, portraits.GetValueOrDefault(row.OfficerId), card.Force,
                        values, row.Choices.Select(choice => new PersonnelCreditChoiceValues(choice.Index,
                            choice.Title, choice.Selected, choice.Enabled, choice.Locked)).ToArray(), showDoctrine);
                }).ToArray();
                int goldBefore = rankup ? 82 : 53, goldAfter = rankup ? 84 : 55, goldCost = rankup ? 85 : 56;
                int booksBefore = rankup ? 92 : 63, booksAfter = rankup ? 94 : 65, booksCost = rankup ? 95 : 66;
                var fields = new Dictionary<int, string>
                {
                    [goldBefore] = Number(budget.GoldBefore), [goldAfter] = Number(budget.GoldAfter), [goldCost] = Cost(budget.GoldCost),
                    [booksBefore] = Number(budget.CommandBooksBefore), [booksAfter] = Number(budget.CommandBooksAfter), [booksCost] = Cost(budget.CommandBookCost),
                    [rankup ? 86 : 57] = draft.GoldPerOfficer is int price ? PersonnelPrice(price) : _ui.Get("detail_unknown")
                };
                if (rankup)
                {
                    for (int index = 0; index < 6; index++)
                    {
                        var reserve = draft.DoctrineReserves.FirstOrDefault(item => item.DoctrineId == index + 1);
                        int node = 47 + index * 5;
                        fields[node] = reserve?.Name ?? _ui.Get("detail_unknown"); fields[node + 1] = Number(reserve?.Before);
                        fields[node + 3] = Number(reserve?.Addition);
                    }
                }
                var first = draft.CardTargets.FirstOrDefault();
                if (!rankup) { fields[44] = Number(first?.MeritBefore); fields[46] = Number(first?.MeritAfter); }
                view.Apply(new(people, first is null ? null : content.Create(first.OfficerCard), fields, draft.SelectedOfficerIds.Count,
                    draft.Rows.Count > 0, draft.CanConfirm, draft.LastError is null && budget.UnresolvedOwner is null
                        ? new AdministrationMessages(_repository, _ui.SourceLocale).Format(rankup ? 0x5C4E : 0x5C55)
                        : _ui.Get("information_partial")));
            }

            void ChoosePeople()
            {
                if (!IsPersonnelAdministrationCurrent(view) || _personnelAdministrationPeople is not null) return;
                var rows = draft.Rows.Select(row => new CommandSelectionRow(row.OfficerId,
                    [new(row.Name, SortKey: world.Officers.GetValueOrDefault(row.OfficerId)?.CurrentNameSortKey),
                     new(Number(row.MeritBefore), row.MeritBefore), new(row.CurrentStageName), new(row.NextStageName)], true)).ToArray();
                var selector = new CommandSelectionView(); _personnelAdministrationPeople = selector; AddChild(selector);
                view.SetBusy(true);
                try
                {
                    selector.Configure(_repository, _ui, rankup ? 0x81E : 0x820,
                        [new("name", PersonnelDataText(0x211), 450), new("merit", _ui.Get("detail_merit"), 322, true),
                         new("rank", PersonnelDataText(0x821), 322), new("next_rank", PersonnelDataText(0x821), 322)],
                        rows, draft.SelectedOfficerIds, draft.Budget.MaximumSelection ?? 0,
                        () => IsPersonnelAdministrationCurrent(view) && _personnelAdministrationPeople == selector,
                        selected => RunUiAction(() =>
                        {
                            if (!IsPersonnelAdministrationCurrent(view) || _personnelAdministrationPeople != selector) return;
                            ClosePersonnelAdministrationPeople(); view.SetBusy(false);
                            if (selected is not null) draft.SelectTargets(selected);
                            Refresh();
                        }), CurrentListScrollSpeed);
                }
                catch { ClosePersonnelAdministrationPeople(); view.SetBusy(false); throw; }
            }
        }

        void Begin(Control view, Action configure, Action abandon, Action back)
            => BeginPersonnelAdministration(view, session, world, map, configure, abandon, back);
        void Unavailable(string missing) { _playMessage.Text = _ui.Get("information_partial"); GD.PushWarning(missing); }
    }

    private void BeginPersonnelAdministration(Control view, GameplaySession session, GameState world,
        MapSceneView map, Action configure, Action abandon, Action back)
    {
        _personnelAdministrationPage = view; _personnelAdministrationSession = session;
        _personnelAdministrationWorld = world; _personnelAdministrationMap = map;
        _personnelAdministrationAbandon = abandon; _personnelAdministrationBack = back;
        AddChild(view);
        try
        {
            configure(); map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true);
            SetMapFocus(true); HideDeploymentHud();
        }
        catch { ClosePersonnelAdministration(); throw; }
    }

    private string PersonnelDataText(int id) => _repository!.LoadMessages(_ui.SourceLocale, MessageKind.Data).Messages
        .Sections[1].Records[id].Text ?? throw new InvalidDataException("Missing personnel command caption.");
    private string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? _ui.Get("detail_unknown");
    private string Cost(int? value) => value is int amount ? amount == 0 ? "(-0)"
        : $"(^03-{amount.ToString(CultureInfo.InvariantCulture)}^00)" : _ui.Get("detail_unknown");
    private string PersonnelPrice(int amount)
    {
        string[] parts = PersonnelDataText(0x3AB).Split("%d", StringSplitOptions.None);
        if (parts.Length != 3) throw new InvalidDataException("Unexpected personnel price format.");
        return parts[0] + "1" + parts[1] + amount.ToString(CultureInfo.InvariantCulture) + parts[2];
    }

    private void RequestPersonnelAdministrationBack()
    {
        if (_personnelAdministrationQuestion is { } question) question.RequestCancel();
        else if (_personnelAdministrationPeople is { } people) people.RequestCancel();
        else _personnelAdministrationBack?.Invoke();
    }
    private void ClosePersonnelAdministrationPeople()
    {
        var people = _personnelAdministrationPeople; _personnelAdministrationPeople = null;
        if (people is not null) { people.Abandon(); people.GetParent()?.RemoveChild(people); people.QueueFree(); }
    }
    private void ClosePersonnelAdministration()
    {
        ClosePersonnelAdministrationPeople();
        var question = _personnelAdministrationQuestion; _personnelAdministrationQuestion = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        var view = _personnelAdministrationPage; var map = _personnelAdministrationMap;
        _personnelAdministrationAbandon?.Invoke();
        _personnelAdministrationPage = null; _personnelAdministrationSession = null; _personnelAdministrationWorld = null;
        _personnelAdministrationMap = null; _personnelAdministrationBack = _personnelAdministrationAbandon = null;
        if (view is not null) { view.GetParent()?.RemoveChild(view); view.QueueFree(); }
        if (!_failed && !_shuttingDown && map is not null && map == _residentMap && GodotObject.IsInstanceValid(map))
        { map.SetDetailsOpen(false); SetMapFocus(true); }
    }
}
