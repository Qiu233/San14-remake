using Godot;
using San14.Client.Gameplay;
using San14.Core;
using San14.Data;

public partial class Main
{
    private sealed class DistrictAdministrationWorkspace(GameplaySession session, GameState world,
        MapSceneView map, DistrictAdministrationDraft draft, ArmyCampaignRuleAdapter rules)
    {
        internal GameplaySession Session { get; } = session;
        internal GameState World { get; set; } = world;
        internal MapSceneView Map { get; } = map;
        internal DistrictAdministrationDraft Draft { get; } = draft;
        internal ArmyCampaignRuleAdapter Rules { get; } = rules;
        internal Dictionary<int, string> Guides { get; } = [];
        internal DistrictCityHints? CityHints { get; set; }
        internal int? CityHintDistrict { get; set; }
        internal bool CityHintsPrepared { get; set; }
        internal string? CityHintsMissing { get; set; }
        internal DistrictSetupView Setup { get; set; } = null!;
        internal DistrictMapSelectionView MapView { get; set; } = null!;
        internal DistrictAdministrationStage? BoundMapStage { get; set; }
        internal int? HoveredMapIdentity { get; set; }
        internal DistrictAdministrationListView? List { get; set; }
        internal DeploymentTargetConfirmation? Question { get; set; }
        internal MerchantAmountMenuView? Amount { get; set; }
        internal DistrictTarget? PendingTarget { get; set; }
        internal bool? MapCovered { get; set; }
    }
    private sealed record DistrictAdministrationListView(Control View, Action RequestCancel, Action Abandon);
    private sealed record DistrictAdministrationContinuation(GameplaySession Session, MapSceneView Map, bool Editing, int? DistrictId);
    private DistrictAdministrationWorkspace? _districtAdministration;
    private DistrictAdministrationContinuation? _districtAdministrationContinuation;
    private bool HasDistrictAdministrationPage => _districtAdministration is not null;
    private void AdoptDistrictAdministrationSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        // The caller admits only a committed speech transition. Retain the
        // same draft and child callbacks with their captured business baseline.
        if (_districtAdministration is not { } owner || owner.Session != session ||
            owner.Map != _residentMap || !ReferenceEquals(owner.World, previous)) return;
        owner.World = replacement;
    }

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
        var rules = composition.PrepareArmyQueries(world);
        var draft = rules.OpenDistrictAdministrationDraft(world, player, editing);
        if (draft.UnresolvedOwner is { } missing) { DistrictAdministrationUnavailable(missing); return; }
        if (editing && retainedDistrict is int retained) draft.SelectDistrict(retained);
        var owner = new DistrictAdministrationWorkspace(session, world, map, draft, rules);
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
                (field, value) => RunUiAction(() => ChangeDistrictSetting(owner, field, value)),
                root => DistrictAdministrationGuide(owner, root));
            map.CloseCityCommandMenu(); SetMapFocus(true); RefreshDistrictAdministration(owner); HideDeploymentHud();
        }
        catch { CloseDistrictAdministration(); throw; }
    }

    private void RefreshDistrictAdministration(DistrictAdministrationWorkspace owner)
    {
        if (!IsDistrictAdministrationCurrent(owner)) return;
        var draft = owner.Draft;
        owner.Map.SetMapSummaryMode(draft.Stage switch
        {
            DistrictAdministrationStage.SelectCities => MapSummarySelectionMode.NewDistrictCities,
            DistrictAdministrationStage.SelectTarget => draft.Settings?.PolicyId switch
            {
                3 => MapSummarySelectionMode.Foothold,
                2 => MapSummarySelectionMode.Force,
                5 => MapSummarySelectionMode.District,
                _ => MapSummarySelectionMode.Normal
            },
            _ => MapSummarySelectionMode.Normal
        });
        bool mapStage = draft.Stage is DistrictAdministrationStage.SelectDistrict or DistrictAdministrationStage.SelectCities or DistrictAdministrationStage.SelectTarget;
        owner.MapView.Visible = owner.List is null && mapStage;
        owner.Setup.Visible = owner.List is null && !mapStage;
        SetDistrictMapCovered(owner, !mapStage || !CanEditDistrictAdministration(owner));
        if (mapStage)
        {
            if (owner.BoundMapStage != draft.Stage)
            {
                owner.Map.EndDistrictCommandMapPick(owner); owner.MapView.CloseDistrictActions();
                owner.HoveredMapIdentity=null;
                var points = DistrictMapPoints(draft);
                owner.Map.BeginDistrictCommandMapPick(owner, points, hex => draft.Stage switch
                {
                    DistrictAdministrationStage.SelectDistrict => draft.ResolveDistrictAtHex(hex),
                    DistrictAdministrationStage.SelectCities => draft.ResolveCityAtHex(hex),
                    _ => draft.ResolveTargetAtHex(hex)?.Id
                }, () => CanEditDistrictAdministration(owner), id => RunUiAction(() => PickDistrictMapIdentity(owner, id)),
                    id =>
                    {
                        if(!IsDistrictAdministrationCurrent(owner))return;
                        owner.HoveredMapIdentity=id;ApplyDistrictMapHover(owner);
                    });
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
                DistrictAdministrationGuide(owner), cities && draft.SelectedDistrict?.IsPrimary == true && draft.Districts.Count > 1,
                cities || targetStage, cities ? draft.CanConfirmCities : targetStage && owner.PendingTarget is { } pending && draft.Targets.Any(row => row.Target == pending && row.Available == true),
                cities && draft.CanContinueCities, cities, cities,
                draft.Cities.Any(city => city.Available == true), PersonnelDataText(draft.AnyEditableSelected ? 0x3B : 0x3A)) { Hints=DistrictMapHints(owner) });
            ApplyDistrictMapHover(owner);
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
        if (!CanEditDistrictAdministration(owner)) return;
        var selection = owner.Rules.PrepareDistrictLeaderSelection(owner.Draft);
        if (!selection.Available)
        { DistrictAdministrationUnavailable(selection.UnresolvedInput ?? "district.leader.selection"); return; }
        if (!owner.Draft.BeginLeaderSelection()) return;
        var view = new DistrictLeaderSelectionView();
        MountDistrictAdministrationList(owner, view, () => view.Configure(_repository!, _ui, selection.Value!,
            () => IsDistrictAdministrationCurrent(owner) && owner.List?.View == view,
            person => RunUiAction(() =>
            {
                if (!IsDistrictAdministrationCurrent(owner) || owner.List?.View != view) return;
                CloseDistrictAdministrationList(owner);
                if (person is not int id || !owner.Draft.SelectLeaderFromList(id)) owner.Draft.Back();
                RefreshDistrictAdministration(owner);
            }), CurrentListScrollSpeed), view.Abandon, view.RequestCancel);
    }
    private void BeginDistrictTargetSelection(DistrictAdministrationWorkspace owner)
    {
        if (!CanEditDistrictAdministration(owner) || !owner.Draft.BeginTargetSelection()) return;
        owner.PendingTarget = owner.Draft.Settings?.Target; RefreshDistrictAdministration(owner);
    }
    private void OpenDistrictMapList(DistrictAdministrationWorkspace owner)
    {
        if (!CanEditDistrictAdministration(owner) || owner.Draft.Stage != DistrictAdministrationStage.SelectCities) return;
        var selection = owner.Rules.PrepareDistrictCitySelection(owner.Draft);
        if (!selection.Available)
        { DistrictAdministrationUnavailable(selection.UnresolvedInput ?? "district.city.selection"); return; }
        var hints = DistrictCityHintPublication(owner);
        if (hints is null)
        { DistrictAdministrationUnavailable(owner.CityHintsMissing ?? "district.city.hints"); return; }
        var view = new DistrictCitySelectionView();
        MountDistrictAdministrationList(owner, view, () => view.Configure(_repository!, _ui, selection.Value!, hints,
            () => IsDistrictAdministrationCurrent(owner) && owner.List?.View == view,
            result =>
            {
                void ApplyResult()
                {
                    if (!IsDistrictAdministrationCurrent(owner) || owner.List?.View != view) return;
                    CloseDistrictAdministrationList(owner);
                    if (result.Action == DistrictCitySelectionAction.CancelStage)
                    {
                        if (!owner.Draft.Back()) CloseDistrictAdministration(); else RefreshDistrictAdministration(owner);
                        return;
                    }
                    if (!owner.Draft.SelectCities(result.CityIds)) { RefreshDistrictAdministration(owner); return; }
                    if (result.Action == DistrictCitySelectionAction.Confirm)
                        ConfirmDistrictCitySelection(owner, false);
                    else RefreshDistrictAdministration(owner);
                }
                if (result.Action == DistrictCitySelectionAction.Confirm) RunPlayAction(ApplyResult);
                else RunUiAction(ApplyResult);
            }, CurrentListScrollSpeed, id =>
            {
                if (GodotObject.IsInstanceValid(owner.Map) && (id is null || IsDistrictAdministrationCurrent(owner)))
                    owner.Map.SetMapSummaryHover(owner, id is int city ? new(MapSummaryHoverKind.Foothold, city) : null);
            }), view.Abandon, view.RequestCancel);
    }
    private void MountDistrictAdministrationList(DistrictAdministrationWorkspace owner, Control view,
        Action configure, Action abandon, Action cancel)
    {
        if (_repository is null || !CanEditDistrictAdministration(owner)) return;
        owner.List = new(view, cancel, abandon); AddChild(view);
        SetDistrictMapCovered(owner, true);
        owner.Setup.SetBusy(true); owner.MapView.SetBusy(true);
        owner.Setup.Hide(); owner.MapView.Hide();
        try { configure(); }
        catch { CloseDistrictAdministrationList(owner); RefreshDistrictAdministration(owner); throw; }
    }
    private static void CloseDistrictAdministrationList(DistrictAdministrationWorkspace owner)
    {
        var list = owner.List; owner.List = null;
        if (list is null) return;
        list.Abandon(); list.View.GetParent()?.RemoveChild(list.View); list.View.QueueFree();
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
        owner.Question?.Abandon(); owner.Question?.QueueFree(); CloseDistrictAdministrationList(owner);
        owner.Amount?.Abandon(); owner.Amount?.QueueFree(); owner.Setup.Abandon(); owner.MapView.Abandon();
        owner.Setup.GetParent()?.RemoveChild(owner.Setup); owner.MapView.GetParent()?.RemoveChild(owner.MapView);
        owner.Setup.QueueFree(); owner.MapView.QueueFree();
        if (GodotObject.IsInstanceValid(owner.Map))
        {
            owner.Map.EndDistrictCommandMapPick(owner);
            owner.Map.SetMapSummaryMode(MapSummarySelectionMode.Normal);
            owner.Map.SetMapSummaryHover(owner,null);
            if (!_failed && !_shuttingDown && _residentMap == owner.Map) { owner.Map.SetDetailsOpen(false); SetMapFocus(true); }
        }
    }
    private void DistrictAdministrationUnavailable(string missing)
    { _playMessage.Text = _ui.Get("information_partial"); GD.PushWarning(missing); }
    private void ApplyDistrictMapHover(DistrictAdministrationWorkspace owner)
    {
        if(!IsDistrictAdministrationCurrent(owner))return;
        var draft=owner.Draft;string? value=null;
        if(owner.HoveredMapIdentity is int id)
        {
            if(draft.Stage==DistrictAdministrationStage.SelectCities)
            {
                var query=DistrictCityHintPublication(owner)?.QueryHint(DistrictCityHintKind.City,id,draft.SelectedCityIds);
                value=query is {Available:true,Value:{} message}?message:_ui.Get("information_partial");
            }
            else if(draft.Stage==DistrictAdministrationStage.SelectTarget&&draft.Targets.Any(row=>row.Target.Id==id&&row.Available==true))
            {
                int root=draft.Settings?.PolicyId switch
                {
                    3=>draft.Reorganize?0x5960:0x5921,
                    2=>draft.Reorganize?0x5961:0x5922,
                    5=>draft.Reorganize?0x5962:0x5923,
                    _=>0x595F
                };
                value=DistrictAdministrationGuide(owner,root);
            }
        }
        owner.MapView.ApplyHoverHint(value);
    }
    private DistrictCityHints? DistrictCityHintPublication(DistrictAdministrationWorkspace owner)
    {
        if(!owner.CityHintsPrepared||owner.CityHintDistrict!=owner.Draft.DistrictId)
        {
            var query=owner.Rules.PrepareDistrictCityHints(owner.Draft);
            owner.CityHintsPrepared=true;owner.CityHintDistrict=owner.Draft.DistrictId;
            owner.CityHints=query.Available?query.Value:null;owner.CityHintsMissing=query.UnresolvedInput;
        }
        return owner.CityHints;
    }
    private IReadOnlyDictionary<DistrictMapHint,string> DistrictMapHints(DistrictAdministrationWorkspace owner)
    {
        var draft=owner.Draft;
        var result=new Dictionary<DistrictMapHint,string>();
        string Guide(int root)=>DistrictAdministrationGuide(owner,root);
        if(draft.Stage==DistrictAdministrationStage.SelectCities)
        {
            var cityHints=DistrictCityHintPublication(owner);
            string CityHint(DistrictCityHintKind kind)
            {
                var query=cityHints?.QueryHint(kind,null,draft.SelectedCityIds);
                return query is {Available:true,Value:{} value}?value:_ui.Get("information_partial");
            }
            result[DistrictMapHint.Return]=CityHint(DistrictCityHintKind.CancelStage);
            result[DistrictMapHint.Decide]=CityHint(DistrictCityHintKind.Decide);
            result[DistrictMapHint.ShowList]=Guide(0x594E);
            result[DistrictMapHint.SelectAll]=CityHint(DistrictCityHintKind.SelectAll);
            if(draft.SelectedDistrict?.IsPrimary==true&&draft.Districts.Count>1)
                result[DistrictMapHint.Continue]=Guide(0x5948);
        }
        else if(draft.Stage==DistrictAdministrationStage.SelectTarget)
        {
            result[DistrictMapHint.Return]=Guide(draft.Reorganize?0x2733:0x591D);
            result[DistrictMapHint.Decide]=Guide(0x5924);
        }
        else
        {
            result[DistrictMapHint.Return]=Guide(0x2733);
            result[DistrictMapHint.Edit]=Guide(0x593D);
            result[DistrictMapHint.Dissolve]=Guide(0x593E);
        }
        return result;
    }
    private string DistrictAdministrationGuide(DistrictAdministrationWorkspace owner)
    {
        var draft=owner.Draft;
        if(draft.UnresolvedOwner is not null||draft.LastError is not null)return _ui.Get("information_partial");
        return DistrictAdministrationGuide(owner,draft.Stage switch
        {
            DistrictAdministrationStage.SelectDistrict=>0x5939,
            DistrictAdministrationStage.SelectCities=>draft.Reorganize?0x593F:0x590E,
            DistrictAdministrationStage.SelectTarget=>0x595F,
            _=>draft.Reorganize?0x594A:0x5916
        });
    }
    private string DistrictAdministrationGuide(DistrictAdministrationWorkspace owner,int root)
    {
        if(owner.Guides.TryGetValue(root,out var saved))return saved;
        var query=owner.Rules.ResolveDistrictAdministrationGuide(root);
        if(!query.Available||query.Value is not {} value)
            throw new InvalidDataException(query.UnresolvedInput??"district.guide.owner");
        return owner.Guides[root]=value;
    }
}
