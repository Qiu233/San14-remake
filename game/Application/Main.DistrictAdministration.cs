using Godot;
using San14.Client.Gameplay;
using San14.Core;
using San14.Data;

public partial class Main
{
    private sealed class DistrictAdministrationWorkspace(GameplaySession session, GameState world,
        MapSceneView map, DistrictAdministrationDraft draft)
    {
        internal GameplaySession Session { get; } = session;
        internal GameState World { get; } = world;
        internal MapSceneView Map { get; } = map;
        internal DistrictAdministrationDraft Draft { get; } = draft;
        internal DistrictSetupView Setup { get; set; } = null!;
        internal DistrictMapSelectionView MapView { get; set; } = null!;
        internal DistrictAdministrationStage? BoundMapStage { get; set; }
        internal CommandSelectionView? List { get; set; }
        internal DeploymentTargetConfirmation? Question { get; set; }
        internal MerchantAmountMenuView? Amount { get; set; }
        internal DistrictTarget? PendingTarget { get; set; }
        internal bool? MapCovered { get; set; }
    }
    private sealed record DistrictAdministrationContinuation(GameplaySession Session, MapSceneView Map, bool Editing, int? DistrictId);
    private DistrictAdministrationWorkspace? _districtAdministration;
    private DistrictAdministrationContinuation? _districtAdministrationContinuation;
    private bool HasDistrictAdministrationPage => _districtAdministration is not null;
    private bool IsDistrictAdministrationCurrent(DistrictAdministrationWorkspace owner) => !_failed && !_shuttingDown &&
        ReferenceEquals(_districtAdministration, owner) && ReferenceEquals(_session, owner.Session) &&
        ReferenceEquals(_world, owner.World) && ReferenceEquals(owner.Session.World, owner.World) && _residentMap == owner.Map;
    private bool CanEditDistrictAdministration(DistrictAdministrationWorkspace owner) => IsDistrictAdministrationCurrent(owner) &&
        owner.List is null && owner.Question is null && owner.Amount is null;

    private void OpenDistrictAdministration(bool editing, int? retainedDistrict = null)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasAdministrationPage || HasMilitaryOrderPage ||
            HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || HasActionReport || HasPersonnelConfirmation ||
            _detailsView is not null || _repository is null || _campaignComposition is not { } composition ||
            _residentMap is not { IsPlayableMapReady: true } map ||
            _session is not { PlayerForceId: int player } session || session.HasPendingOperation ||
            session.World is not { Phase: TurnPhase.Strategy } world || PlayableOutcome.Evaluate(world, player) != PlayableResult.Ongoing) return;
        var draft = composition.PrepareArmyQueries(world).OpenDistrictAdministrationDraft(world, player, editing);
        if (draft.UnresolvedOwner is { } missing) { DistrictAdministrationUnavailable(missing); return; }
        if (editing && retainedDistrict is int retained) draft.SelectDistrict(retained);
        var owner = new DistrictAdministrationWorkspace(session, world, map, draft);
        _districtAdministration = owner; _districtAdministrationContinuation = null;
        owner.Setup = new(); owner.MapView = new(); AddChild(owner.MapView); AddChild(owner.Setup);
        try
        {
            owner.MapView.Configure(_repository, _ui, () => CanEditDistrictAdministration(owner),
                () => RunUiAction(RequestDistrictAdministrationBack), keepOpen => RunPlayAction(() => ConfirmDistrictCitySelection(owner, keepOpen)),
                () => RunUiAction(() => OpenDistrictMapList(owner)), () => RunUiAction(() => ToggleAllDistrictCities(owner)));
            owner.MapView.RegisterHeader(_sourceHeaders);
            owner.Setup.Configure(_repository, _ui, editing, () => CanEditDistrictAdministration(owner),
                () => RunUiAction(RequestDistrictAdministrationBack), keepOpen => RunPlayAction(() => SubmitDistrictAdministration(owner, keepOpen)),
                () => RunUiAction(() => { if (CanEditDistrictAdministration(owner) && draft.BeginCitySelection()) RefreshDistrictAdministration(owner); }),
                () => RunUiAction(() => ChooseDistrictLeader(owner)),
                () => RunUiAction(() => BeginDistrictTargetSelection(owner)),
                (field, value) => RunUiAction(() => ChangeDistrictSetting(owner, field, value)));
            map.CloseCityCommandMenu(); SetMapFocus(true); RefreshDistrictAdministration(owner); HideDeploymentHud();
        }
        catch { CloseDistrictAdministration(); throw; }
    }

    private void RefreshDistrictAdministration(DistrictAdministrationWorkspace owner)
    {
        if (!IsDistrictAdministrationCurrent(owner)) return;
        var draft = owner.Draft;
        bool mapStage = draft.Stage is DistrictAdministrationStage.SelectDistrict or DistrictAdministrationStage.SelectCities or DistrictAdministrationStage.SelectTarget;
        owner.MapView.Visible = mapStage; owner.Setup.Visible = !mapStage;
        SetDistrictMapCovered(owner, !mapStage || !CanEditDistrictAdministration(owner));
        if (mapStage)
        {
            if (owner.BoundMapStage != draft.Stage)
            {
                owner.Map.EndDistrictCommandMapPick(owner); owner.MapView.CloseDistrictActions();
                var points = DistrictMapPoints(draft);
                owner.Map.BeginDistrictCommandMapPick(owner, points, hex => draft.Stage switch
                {
                    DistrictAdministrationStage.SelectDistrict => draft.ResolveDistrictAtHex(hex),
                    DistrictAdministrationStage.SelectCities => draft.ResolveCityAtHex(hex),
                    _ => draft.ResolveTargetAtHex(hex)?.Id
                }, () => CanEditDistrictAdministration(owner), id => RunUiAction(() => PickDistrictMapIdentity(owner, id)));
                owner.BoundMapStage = draft.Stage;
            }
            IReadOnlyList<int> selected = draft.Stage switch
            {
                DistrictAdministrationStage.SelectCities => draft.SelectedCityIds,
                DistrictAdministrationStage.SelectDistrict => draft.DistrictId is int district ? [district] : [],
                _ => owner.PendingTarget is { } target ? [target.Id] : []
            };
            owner.Map.ApplyDistrictCommandMapPick(owner, selected);
            bool cities = draft.Stage == DistrictAdministrationStage.SelectCities;
            bool targetStage = draft.Stage == DistrictAdministrationStage.SelectTarget;
            owner.MapView.Apply(new(PersonnelDataText(targetStage ? 0x39E : draft.Reorganize ? 0x3A8 : 0x3A7),
                DistrictAdministrationGuide(draft), cities && draft.SelectedDistrict?.IsPrimary == true && draft.Districts.Count > 1,
                cities || targetStage, cities ? draft.CanConfirmCities : targetStage && owner.PendingTarget is { } pending && draft.Targets.Any(row => row.Target == pending && row.Available == true),
                cities && draft.CanContinueCities, cities, cities,
                draft.Cities.Any(city => city.Available == true), PersonnelDataText(draft.AnyEditableSelected ? 0x3B : 0x3A)));
        }
        else
        {
            owner.Map.EndDistrictCommandMapPick(owner); owner.BoundMapStage = null;
            ApplyDistrictSetup(owner);
        }
        bool busy = !CanEditDistrictAdministration(owner);
        owner.MapView.SetBusy(busy); owner.Setup.SetBusy(busy); HideDeploymentHud();
        if (draft.LastError is { } error) DistrictAdministrationUnavailable(error);
    }
    private static void SetDistrictMapCovered(DistrictAdministrationWorkspace owner, bool covered)
    {
        if (owner.MapCovered == covered) return;
        owner.MapCovered = covered; owner.Map.SetDetailsOpen(covered, keepMapVisible: true);
    }
    private static DistrictCommandMapPoint[] DistrictMapPoints(DistrictAdministrationDraft draft) => draft.Stage switch
    {
        DistrictAdministrationStage.SelectDistrict => draft.Cities.Where(city => city.HexId is not null && city.DistrictId is not null)
            .Select(city => new DistrictCommandMapPoint(city.DistrictId!.Value, city.HexId!.Value,
                draft.Districts.Any(district => district.Id == city.DistrictId))).ToArray(),
        DistrictAdministrationStage.SelectCities => draft.Cities.Where(city => city.HexId is not null)
            .Select(city => new DistrictCommandMapPoint(city.Id, city.HexId!.Value, city.Available == true)).ToArray(),
        _ => draft.Targets.SelectMany(target => target.MapHexIds.Select(hex => new DistrictCommandMapPoint(target.Target.Id, hex, target.Available == true))).ToArray()
    };
    private void PickDistrictMapIdentity(DistrictAdministrationWorkspace owner, int id)
    {
        if (!CanEditDistrictAdministration(owner)) return;
        var draft = owner.Draft;
        if (draft.Stage == DistrictAdministrationStage.SelectDistrict)
        {
            var district = draft.Districts.FirstOrDefault(row => row.Id == id);
            if (district is null) return;
            void Edit() { if (CanEditDistrictAdministration(owner) && draft.SelectDistrict(id)) RefreshDistrictAdministration(owner); }
            if (district.IsPrimary) Edit();
            else owner.MapView.ShowDistrictActions(Edit, () =>
            {
                if (CanEditDistrictAdministration(owner) && draft.SelectDistrict(id)) ConfirmDistrictDissolution(owner);
            });
            return;
        }
        if (draft.Stage == DistrictAdministrationStage.SelectCities) draft.ToggleCity(id);
        else if (draft.Targets.FirstOrDefault(row => row.Target.Id == id && row.Available == true) is { } target) owner.PendingTarget = target.Target;
        RefreshDistrictAdministration(owner);
    }
    private void ConfirmDistrictCitySelection(DistrictAdministrationWorkspace owner, bool keepOpen)
    {
        if (!CanEditDistrictAdministration(owner)) return;
        if (owner.Draft.Stage == DistrictAdministrationStage.SelectTarget)
        {
            if (owner.PendingTarget is { } target && owner.Draft.SelectTarget(target)) RefreshDistrictAdministration(owner);
            return;
        }
        if (owner.Draft.Stage != DistrictAdministrationStage.SelectCities) return;
        if (!owner.Draft.Next()) { if (owner.Draft.LastError is { } missing) DistrictAdministrationUnavailable(missing); return; }
        if (owner.Draft.Stage == DistrictAdministrationStage.Ready) SubmitDistrictAdministration(owner, keepOpen);
        else RefreshDistrictAdministration(owner);
    }
    private void ToggleAllDistrictCities(DistrictAdministrationWorkspace owner)
    {
        if (!CanEditDistrictAdministration(owner)) return;
        var draft = owner.Draft;
        if (draft.AnyEditableSelected) draft.ClearCities();
        else draft.SelectAllCities();
        RefreshDistrictAdministration(owner);
    }

    private void ChooseDistrictLeader(DistrictAdministrationWorkspace owner)
    {
        if (!CanEditDistrictAdministration(owner) || !owner.Draft.BeginLeaderSelection()) return;
        var draft = owner.Draft;
        OpenDistrictSelection(owner, 0x32E, draft.Leaders.Select(person => new CommandSelectionRow(person.OfficerId,
            [new(person.Name, SortKey: person.NameSortKey)], person.Enabled == true, person.DisabledReason)).ToArray(),
            draft.LeaderId is int id ? [id] : [], true, selection =>
            { if (selection is { Count: > 0 }) draft.SelectLeader(selection[0]); else draft.Back(); });
    }
    private void BeginDistrictTargetSelection(DistrictAdministrationWorkspace owner)
    {
        if (!CanEditDistrictAdministration(owner) || !owner.Draft.BeginTargetSelection()) return;
        owner.PendingTarget = owner.Draft.Settings?.Target; RefreshDistrictAdministration(owner);
    }
    private void OpenDistrictMapList(DistrictAdministrationWorkspace owner)
    {
        if (!CanEditDistrictAdministration(owner)) return;
        var draft = owner.Draft;
        if (draft.Stage != DistrictAdministrationStage.SelectCities) return;
        var locked = draft.LockedSelectedCityIds.ToHashSet();
        var rows = draft.Cities.Select(city => new CommandSelectionRow(city.Id, [new(city.Name)], city.Available == true, city.UnresolvedOwner,
            locked.Contains(city.Id))).ToArray();
        OpenDistrictSelection(owner, 0x39A, rows, draft.SelectedCityIds, false, selection =>
            {
                if (selection is null) return;
                draft.SelectCities(selection);
            });
    }
    private void OpenDistrictSelection(DistrictAdministrationWorkspace owner, int title,
        IReadOnlyList<CommandSelectionRow> rows, IReadOnlyList<int> selected, bool single, Action<IReadOnlyList<int>?> apply)
    {
        if (_repository is null || !CanEditDistrictAdministration(owner)) return;
        var list = new CommandSelectionView(); owner.List = list; AddChild(list);
        SetDistrictMapCovered(owner, true);
        owner.Setup.SetBusy(true); owner.MapView.SetBusy(true);
        try
        {
            list.Configure(_repository, _ui, title, [new("name", PersonnelDataText(title == 0x39A ? 0x478 : 0x211), 1416)],
                rows, selected, single ? 1 : rows.Count, () => IsDistrictAdministrationCurrent(owner) && owner.List == list,
                result => RunUiAction(() =>
                {
                    if (!IsDistrictAdministrationCurrent(owner) || owner.List != list) return;
                    owner.List = null; list.Abandon(); list.QueueFree(); apply(result); RefreshDistrictAdministration(owner);
                }), CurrentListScrollSpeed, requireSelection: single, singleSelection: single);
        }
        catch { owner.List = null; list.Abandon(); list.QueueFree(); RefreshDistrictAdministration(owner); throw; }
    }

    private void ConfirmDistrictDissolution(DistrictAdministrationWorkspace owner)
    {
        if (_repository is null || !CanEditDistrictAdministration(owner) || !owner.Draft.CanDissolve) return;
        var prompt = owner.Draft.QueryDissolveConfirmation();
        if (!prompt.Available) { DistrictAdministrationUnavailable(prompt.UnresolvedInput ?? "district.dissolve.confirmation"); return; }
        var question = new DeploymentTargetConfirmation(); owner.Question = question; AddChild(question);
        SetDistrictMapCovered(owner, true);
        owner.MapView.SetBusy(true); owner.Setup.SetBusy(true);
        try
        {
            question.ConfigureWrappedMessage(_repository, _ui, prompt.Value!, accepted => RunPlayAction(() =>
            {
                if (!IsDistrictAdministrationCurrent(owner) || owner.Question != question) return;
                owner.Question = null; question.Abandon(); question.QueueFree();
                if (accepted && owner.Draft.BuildDissolveOrder() is { } order)
                {
                    if (ApplyPlayableOrder(order, () =>
                    {
                        CloseDistrictAdministration();
                        if (owner.Session.World.Districts.Values.Count(district => district.ForceId == order.ForceId) > 1)
                            _districtAdministrationContinuation = new(owner.Session, owner.Map, true, null);
                    })) return;
                }
                //The original returns to district picking when the question is declined.
                if (!accepted) owner.Draft.Back();
                RefreshDistrictAdministration(owner);
            }));
        }
        catch { owner.Question = null; question.Abandon(); question.QueueFree(); RefreshDistrictAdministration(owner); throw; }
    }
    private void SubmitDistrictAdministration(DistrictAdministrationWorkspace owner, bool keepOpen)
    {
        if (!CanEditDistrictAdministration(owner) || (keepOpen ? !owner.Draft.CanContinue : !owner.Draft.CanConfirm) || owner.Draft.BuildOrder() is not { } order) return;
        if (!ApplyPlayableOrder(order, () =>
        {
            CloseDistrictAdministration();
            if (keepOpen) _districtAdministrationContinuation = new(owner.Session, owner.Map, owner.Draft.Reorganize, owner.Draft.DistrictId);
        })) RefreshDistrictAdministration(owner);
    }
    private bool TryResumeDistrictAdministration()
    {
        if (_districtAdministrationContinuation is not { } next) return false;
        if (_failed || _shuttingDown || IsReturningToTitle || _session != next.Session || _residentMap != next.Map || next.Session.World.Phase != TurnPhase.Strategy)
        { _districtAdministrationContinuation = null; return false; }
        if (HasAdministrationPage || HasMilitaryOrderPage || HasEnvironmentSettings || HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace ||
            HasPersonnelConfirmation || _detailsView is not null || _diplomacyNegotiation is not null) return false;
        _districtAdministrationContinuation = null; OpenDistrictAdministration(next.Editing, next.DistrictId); return HasDistrictAdministrationPage;
    }
    private void RequestDistrictAdministrationBack()
    {
        if (_districtAdministration is not { } owner || !IsDistrictAdministrationCurrent(owner)) return;
        if (owner.Question is { } question) { question.RequestCancel(); return; }
        if (owner.List is { } list) { list.RequestCancel(); return; }
        if (owner.Amount is { } amount) { amount.RequestCancel(); return; }
        if (owner.MapView.HasDistrictActions) { owner.MapView.CloseDistrictActions(); return; }
        if (!owner.Draft.Back()) CloseDistrictAdministration(); else RefreshDistrictAdministration(owner);
    }
    private void CloseDistrictAdministration()
    {
        var owner = _districtAdministration; _districtAdministration = null;
        if (owner is null) return;
        owner.Question?.Abandon(); owner.Question?.QueueFree(); owner.List?.Abandon(); owner.List?.QueueFree();
        owner.Amount?.Abandon(); owner.Amount?.QueueFree(); owner.Setup.Abandon(); owner.MapView.Abandon();
        owner.Setup.GetParent()?.RemoveChild(owner.Setup); owner.MapView.GetParent()?.RemoveChild(owner.MapView);
        owner.Setup.QueueFree(); owner.MapView.QueueFree();
        if (GodotObject.IsInstanceValid(owner.Map))
        {
            owner.Map.EndDistrictCommandMapPick(owner);
            if (!_failed && !_shuttingDown && _residentMap == owner.Map) { owner.Map.SetDetailsOpen(false); SetMapFocus(true); }
        }
    }
    private void DistrictAdministrationUnavailable(string missing)
    { _playMessage.Text = _ui.Get("information_partial"); GD.PushWarning(missing); }
    private string DistrictAdministrationGuide(DistrictAdministrationDraft draft) => draft.UnresolvedOwner is not null || draft.LastError is not null
        ? _ui.Get("information_partial") : PersonnelDataText(draft.Stage switch
        {
            DistrictAdministrationStage.SelectDistrict => 0x3A9,
            DistrictAdministrationStage.SelectCities => 0x39A,
            DistrictAdministrationStage.SelectTarget => 0x39F,
            _ => 0x3A6
        });
}
