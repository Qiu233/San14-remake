using Godot;
using San14.Client.Gameplay;
using San14.Core;
using San14.Data;

public partial class Main
{
    private sealed record PersonnelDevelopmentContinuation(GameplaySession Session, MapSceneView Map,
        MapMenuCommand Command, int? OfficerId);
    private PersonnelDevelopmentContinuation? _personnelDevelopmentContinuation;
    private CampaignCrtRandomState? _applicationCrtRandom;

    //The original application seeds its independent CRT stream once from its
    //elapsed monotonic milliseconds. Loading never invents missing saved state.
    private CampaignCrtRandomState CaptureApplicationCrtRandom() =>
        _applicationCrtRandom ??= new(unchecked((uint)Time.GetTicksMsec()));
    private void SynchronizeApplicationCrtRandom(GameState world)
    { if (world.CampaignCrtRandom is { } random) _applicationCrtRandom = random; }

    private void OpenPersonnelDevelopment(MapMenuCommand command, ArmyCampaignRuleAdapter rules, GameState world,
        int player, GameplaySession session, MapSceneView map, int? retainedOfficer)
    {
        if (_repository is null) return;
        var content = new AreaAdministrationOfficerContent(_repository);
        if (command == MapMenuCommand.Employ) OpenHire();
        else if (command == MapMenuCommand.Learn) OpenLearn();
        else if (command == MapMenuCommand.Introduce) OpenMediation();

        void Submit(Control page, GameOrder? order, bool keepOpen, int? officer = null)
        {
            if (!IsPersonnelAdministrationCurrent(page) || order is null) return;
            ApplyPlayableOrder(order, () =>
            {
                ClosePersonnelAdministration();
                if (keepOpen) _personnelDevelopmentContinuation = new(session, map, command, officer);
            });
        }

        void OpenHire()
        {
            var draft = rules.OpenPersonnelHireDraft(world, player);
            if (draft.UnresolvedOwner is { } missing) { Unavailable(missing); return; }
            var view = new PersonnelHirePageView();
            BeginPersonnelAdministration(view, session, world, map, () =>
            {
                view.Configure(_repository, _ui, () => IsPersonnelAdministrationCurrent(view), RequestPersonnelAdministrationBack,
                    keepOpen => RunPlayAction(() => Submit(view, draft.BuildOrder(), keepOpen)), ChooseCity, ChoosePeople);
                Refresh();
            }, view.Abandon, ClosePersonnelAdministration);

            void Refresh()
            {
                var budget = draft.Budget;
                var fields = new Dictionary<int, string>
                {
                    [96] = Number(draft.CurrentHired), [98] = Number(draft.CurrentHiredAfter), [99] = draft.StatusText,
                    [105] = Number(draft.Capacity), [114] = Number(budget.GoldBefore), [116] = Number(budget.GoldAfter),
                    [117] = Cost(budget.GoldCost), [123] = Number(budget.CommandBooksBefore),
                    [125] = Number(budget.CommandBooksAfter), [126] = Cost(budget.CommandBookCost)
                };
                view.Apply(new(draft.Cities.FirstOrDefault(city => city.CityId == draft.CityId)?.Name ?? PersonnelDataText(0x3FE),
                    draft.CardTargets.FirstOrDefault() is { } first ? content.Create(first.OfficerCard) : null,
                    draft.Count, fields, draft.Cities.Count > 0, draft.People.Count > 0,
                    draft.CanConfirm, draft.CanContinue, new AdministrationMessages(_repository, _ui.SourceLocale).Format(draft.CityId is null ? 0x5CDE : 0x5CDF)));
            }
            void ChooseCity() => OpenPersonnelDevelopmentSelection(view, 0x39A,
                [new("city", PersonnelDataText(0x478), 1416)],
                draft.Cities.Select(city => new CommandSelectionRow(city.CityId, [new(city.Name)], true)).ToArray(),
                draft.CityId is int id ? [id] : [], 1, selected =>
                { if (selected is { Count: > 0 }) draft.SelectCity(selected[0]); Refresh(); }, view.SetBusy, single: true);
            void ChoosePeople() => OpenPersonnelDevelopmentSelection(view, 0x32E,
                [new("name", PersonnelDataText(0x211), 1416)], OfficerRows(draft.People),
                draft.SelectedOfficerIds, draft.MaximumCount ?? 0, selected =>
                { if (selected is not null) draft.SelectTargets(selected); Refresh(); }, view.SetBusy);
        }

        void OpenLearn()
        {
            var draft = rules.OpenPersonnelLearnDraft(world, player);
            if (draft.UnresolvedOwner is { } missing) { Unavailable(missing); return; }
            if (retainedOfficer is int retained) draft.SelectOfficer(retained);
            var view = new PersonnelLearningView();
            BeginPersonnelAdministration(view, session, world, map, () =>
            {
                view.Configure(_repository, _ui, () => IsPersonnelAdministrationCurrent(view), RequestPersonnelAdministrationBack,
                    keepOpen => RunUiAction(() =>
                    {
                        if (!IsPersonnelAdministrationCurrent(view) || (keepOpen ? !draft.CanContinue : !draft.CanConfirm)) return;
                        RequestPersonnelDevelopmentConfirmation(view, draft.QueryConfirmation(), view.SetBusy,
                            () => Submit(view, draft.BuildOrder(), keepOpen, draft.OfficerId));
                    }), ChooseOfficer, ChooseTactic, InspectTactic);
                Refresh();
            }, view.Abandon, ClosePersonnelAdministration);

            void Refresh()
            {
                var fields = new Dictionary<int, string>
                {
                    [181] = Number(draft.CommandBooksBefore), [183] = Number(draft.CommandBooksAfter),
                    [184] = Cost(draft.CommandBookCost)
                };
                if (draft.SelectedBook is { } book)
                {
                    fields[81] = book.TacticName; fields[172] = Number(draft.SelectedBookCountBefore);
                    fields[174] = Number(draft.SelectedBookCountAfter); fields[175] = Cost(draft.SelectedBookCountCost);
                }
                if (draft.ReplacedTacticId is int old) fields[87] = draft.Tactics.FirstOrDefault(item => item.TacticId == old)?.Name ?? _ui.Get("detail_unknown");
                for (int index = 0; index < 2; index++) fields[163 + index] = index < draft.Effects.Count ? draft.Effects[index] : "";
                view.Apply(new(draft.OfficerCard is { } card ? content.Create(card.OfficerCard) : null,
                    draft.TacticSlots.Select(slot => new PersonnelLearningTacticValues(slot.Index, slot.Text,
                        slot.ButtonVisible, slot.MarkerVisible, slot.Enabled, slot.Selected)).ToArray(), fields,
                    draft.People.Count > 0, draft.CanConfirm, draft.CanContinue,
                    draft.UnresolvedOwner is null && draft.EffectUnresolvedOwner is null
                        ? draft.SelectedBook is null ? new AdministrationMessages(_repository, _ui.SourceLocale).Format(
                            draft.OfficerId is null ? 0x5980 : draft.RequiresReplacement == true ? 0x5981 : 0x597F) : ""
                        : _ui.Get("information_partial")));
            }
            void ChooseOfficer() => OpenPersonnelDevelopmentSelection(view, 0x32E,
                [new("name", PersonnelDataText(0x211), 1416)], OfficerRows(draft.People),
                draft.OfficerId is int id ? [id] : [], 1, selected =>
                { if (selected is { Count: > 0 }) draft.SelectOfficer(selected[0]); Refresh(); }, view.SetBusy, single: true);
            void ChooseTactic(int index)
            {
                var slot = draft.TacticSlots.FirstOrDefault(item => item.Index == index);
                if (slot?.Enabled != true) return;
                int? replacement = draft.RequiresReplacement == true ? slot.TacticId : null;
                var rows = draft.Books.Select(book => new CommandSelectionRow(book.GemId,
                    [new(book.Name), new(Number(book.Count), book.Count), new(book.TacticName)],
                    book.Eligible == true && book.Count > 0, book.UnresolvedOwner is null ? null : _ui.Get("information_partial"))).ToArray();
                OpenPersonnelDevelopmentSelection(view, 0x699,
                    [new("book", PersonnelDataText(0x41E), 560), new("count", PersonnelDataText(0x41F), 296, true),
                     new("tactic", PersonnelDataText(0x234), 560)], rows,
                    draft.GemId is int gem ? [gem] : [], 1, selected =>
                    {
                        if (selected is { Count: > 0 } && draft.SelectGem(selected[0])) draft.SelectReplacement(replacement);
                        Refresh();
                    }, view.SetBusy, single: true);
            }
            void InspectTactic(int? index)
            {
                if (!IsPersonnelAdministrationCurrent(view)) return;
                if (index is null) { view.ApplyEffectLines(draft.Effects); return; }
                var slot = draft.TacticSlots.FirstOrDefault(item => item.Index == index.Value);
                int? tactic = slot?.Selected == true ? draft.SelectedBook?.TacticId : slot?.TacticId;
                if (tactic is null) { view.ApplyEffectLines(draft.Effects); return; }
                var result = draft.QueryTacticEffects(tactic.Value);
                view.ApplyEffectLines(result.Available ? result.Value! : [_ui.Get("detail_unknown")]);
            }
        }

        void OpenMediation()
        {
            var draft = rules.OpenPersonnelMediationDraft(world, player);
            if (draft.UnresolvedOwner is { } missing) { Unavailable(missing); return; }
            var view = new PersonnelMediationPageView();
            BeginPersonnelAdministration(view, session, world, map, () =>
            {
                view.Configure(_repository, _ui, () => IsPersonnelAdministrationCurrent(view), RequestPersonnelAdministrationBack,
                    marriage => RunUiAction(() =>
                    {
                        if (!IsPersonnelAdministrationCurrent(view) || (marriage ? !draft.CanMarry : !draft.CanSwear)) return;
                        RequestPersonnelDevelopmentConfirmation(view, draft.QueryConfirmation(), view.SetBusy,
                            () => Submit(view, draft.BuildOrder(), false));
                    }), ChoosePerson, () => RunUiAction(() => { if (IsPersonnelAdministrationCurrent(view)) { draft.Clear(); Refresh(); } }));
                Refresh();
            }, view.Abandon, ClosePersonnelAdministration);

            void Refresh()
            {
                var quote = draft.View;
                var people = draft.TargetSlots.Select((person, index) => new PersonnelMediationPersonValues(
                    person?.Name ?? PersonnelDataText(0x3FE), person is null ? null : content.Create(person.OfficerCard).Portrait,
                    index < draft.CandidatesBySlot.Count && draft.CandidatesBySlot[index].Any(row => row.Enabled == true), person is not null)).ToArray();
                view.Apply(new(people, new Dictionary<int, string>
                {
                    [47] = Number(quote?.GoldBefore), [49] = Number(quote?.GoldAfter), [50] = Cost(quote?.GoldCost),
                    [55] = Number(quote?.BooksBefore), [57] = Number(quote?.BooksAfter), [58] = Cost(quote?.BookCost)
                }, draft.CanMarry, draft.CanSwear, draft.SelectedOfficerIds.Any(id => id > 0), draft.IsMarriage != true,
                    quote?.UnresolvedOwner is null ? new AdministrationMessages(_repository, _ui.SourceLocale).Format(0x5835) : _ui.Get("information_partial")));
            }
            void ChoosePerson(int slot)
            {
                if ((uint)slot >= draft.CandidatesBySlot.Count) return;
                OpenPersonnelDevelopmentSelection(view, 0x58F,
                    [new("name", PersonnelDataText(0x211), 1416)], OfficerRows(draft.CandidatesBySlot[slot]),
                    draft.SelectedOfficerIds[slot] > 0 ? [draft.SelectedOfficerIds[slot]] : [], 1, selected =>
                    { if (selected is { Count: > 0 }) draft.SelectSlot(slot, selected[0]); Refresh(); }, view.SetBusy, single: true);
            }
        }

        CommandSelectionRow[] OfficerRows(IReadOnlyList<PersonnelDevelopmentOfficer> people) => people.Select(person =>
            new CommandSelectionRow(person.OfficerId, [new(person.Name, SortKey: person.NameSortKey)],
                person.Enabled == true, person.DisabledReason ?? (person.UnresolvedOwner is null ? null : _ui.Get("information_partial")))).ToArray();
        void Unavailable(string reason) { _playMessage.Text = _ui.Get("information_partial"); GD.PushWarning(reason); }
    }

    private void OpenPersonnelDevelopmentSelection(Control parent, int title, IReadOnlyList<CommandSelectionColumn> columns,
        IReadOnlyList<CommandSelectionRow> rows, IReadOnlyList<int> initial, int maximum,
        Action<IReadOnlyList<int>?> selected, Action<bool> busy, bool single = false)
    {
        if (_repository is null || !IsPersonnelAdministrationCurrent(parent) || _personnelAdministrationPeople is not null ||
            _personnelAdministrationQuestion is not null) return;
        var selector = new CommandSelectionView(); _personnelAdministrationPeople = selector; AddChild(selector); busy(true);
        try
        {
            selector.Configure(_repository, _ui, title, columns, rows, initial, maximum,
                () => IsPersonnelAdministrationCurrent(parent) && _personnelAdministrationPeople == selector,
                result => RunUiAction(() =>
                {
                    if (!IsPersonnelAdministrationCurrent(parent) || _personnelAdministrationPeople != selector) return;
                    ClosePersonnelAdministrationPeople(); busy(false); selected(result);
                }), CurrentListScrollSpeed, requireSelection: single, singleSelection: single);
        }
        catch { ClosePersonnelAdministrationPeople(); busy(false); throw; }
    }

    private void RequestPersonnelDevelopmentConfirmation(Control parent, AreaAdministrationQuery<string> prompt,
        Action<bool> busy, Action submit)
    {
        if (_repository is null || !IsPersonnelAdministrationCurrent(parent) || _personnelAdministrationQuestion is not null) return;
        if (!prompt.Available)
        { _playMessage.Text = _ui.Get("information_partial"); GD.PushWarning(prompt.UnresolvedInput ?? "Personnel confirmation unavailable."); return; }
        var question = new DeploymentTargetConfirmation(); _personnelAdministrationQuestion = question; AddChild(question); busy(true);
        try
        {
            question.ConfigureWrappedMessage(_repository, _ui, prompt.Value!, accepted => RunPlayAction(() =>
            {
                if (!IsPersonnelAdministrationCurrent(parent) || _personnelAdministrationQuestion != question) return;
                _personnelAdministrationQuestion = null; question.Abandon(); question.QueueFree(); busy(false);
                if (accepted) submit();
            }));
        }
        catch { _personnelAdministrationQuestion = null; question.Abandon(); question.QueueFree(); busy(false); throw; }
    }

    private bool TryResumePersonnelDevelopment()
    {
        if (_personnelDevelopmentContinuation is not { } next) return false;
        if (_failed || _shuttingDown || IsReturningToTitle || _session != next.Session || _residentMap != next.Map ||
            next.Session.World.Phase != TurnPhase.Strategy)
        { _personnelDevelopmentContinuation = null; return false; }
        if (HasAdministrationPage || HasMilitaryOrderPage || HasEnvironmentSettings || HasCampaignSavePage || HasMapSystemMenu ||
            HasCommandWorkspace || HasPersonnelConfirmation || _detailsView is not null || _diplomacyNegotiation is not null) return false;
        _personnelDevelopmentContinuation = null;
        OpenPersonnelAdministration(next.Command, next.OfficerId);
        return HasPersonnelAdministrationPage;
    }
}
