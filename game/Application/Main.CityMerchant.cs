using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private CityMerchantView? _cityMerchantView;
    private CityMerchantDraft? _cityMerchantDraft;
    private CityMerchantPersonSelectionView? _cityMerchantPersons;
    private MerchantAmountMenuView? _cityMerchantAmount;
    private DeploymentNarrationView? _cityMerchantNarration;
    private MapSceneView? _cityMerchantMap;
    private GameState? _cityMerchantWorld;
    private GameplaySession? _cityMerchantSession;
    private AreaAdministrationOfficerContent? _cityMerchantOfficers;
    private bool HasCityMerchantPage => _cityMerchantView is not null || _cityMerchantNarration is not null;
    private bool IsCityMerchantCurrent => !_failed && !_shuttingDown && _cityMerchantView is not null &&
        _cityMerchantDraft is not null && ReferenceEquals(_session, _cityMerchantSession) &&
        ReferenceEquals(_world, _cityMerchantWorld) && ReferenceEquals(_session?.World, _cityMerchantWorld) &&
        _residentMap == _cityMerchantMap && _cityMerchantMap is not null;
    private bool CanEditCityMerchant => IsCityMerchantCurrent && _cityMerchantPersons is null &&
        _cityMerchantAmount is null && _cityMerchantNarration is null;

    private void OpenCityMerchant(int city)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasActionReport || HasPersonnelConfirmation || HasAdministrationPage || HasMilitaryOrderPage ||
            HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || _detailsView is not null ||
            _repository is null || _session is not { PlayerForceId: int force } session || session.HasPendingOperation ||
            _world is not { Phase: TurnPhase.Strategy } world || !ReferenceEquals(session.World, world) ||
            _campaignComposition is not { } composition || _residentMap is not { IsPlayableMapReady: true } map) return;
        var voiceRequests = new NarrationVoiceRequests(_preferences.Voice.Capture(), _presentationRandom.Next);
        var opened = composition.PrepareArmyQueries(world).OpenCityMerchantDraft(world, city, voiceRequests);
        if (!opened.Available) { CityMerchantUnavailable(opened.UnresolvedInput!); return; }
        _cityMerchantDraft = opened.Value!; _cityMerchantWorld = world;
        _cityMerchantSession = session; _cityMerchantMap = map;
        _cityMerchantOfficers = new(_repository);
        var view = new CityMerchantView(); _cityMerchantView = view; AddChild(view);
        try
        {
            SetMapFocus(true); map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
            view.Configure(_repository, _ui,
                () => RunUiAction(() => { if (!CanEditCityMerchant) return; CloseCityMerchant(); ReturnToCityMenu(session, map, city); }),
                () => RunPlayAction(() => ConfirmCityMerchant(force)),
                () => RunUiAction(OpenCityMerchantPersons),
                mode => RunUiAction(() => OpenCityMerchantAmount(mode)),
                () => RunUiAction(() =>
                {
                    if (CanEditCityMerchant && AcceptCityMerchant(_cityMerchantDraft!.SetMode(CityMerchantMode.Buy)))
                        RefreshCityMerchant();
                }));
            RefreshCityMerchant();
        }
        catch { CloseCityMerchant(); throw; }
    }

    private void RefreshCityMerchant()
    {
        if (!IsCityMerchantCurrent) return;
        var page = _cityMerchantDraft!.QueryPage();
        if (!page.Available) { CityMerchantUnavailable(page.UnresolvedInput!); return; }
        var card = _cityMerchantDraft.QueryOfficerCard();
        var hint = _cityMerchantDraft.QueryDefaultHint();
        _cityMerchantView!.Apply(page.Value!, _cityMerchantOfficers!.Create(card.Available ? card.Value : null),
            hint.Available ? hint.Value : null);
        _cityMerchantView.SetBusy(!CanEditCityMerchant);
    }

    private void OpenCityMerchantPersons()
    {
        if (!CanEditCityMerchant || _repository is null) return;
        var draft = _cityMerchantDraft!; var query = draft.QueryPage();
        if (!query.Available) { CityMerchantUnavailable(query.UnresolvedInput!); return; }
        var page = query.Value!;
        var selector = new CityMerchantPersonSelectionView(); _cityMerchantPersons = selector; AddChild(selector);
        _cityMerchantView!.SetBusy(true); _cityMerchantView.Hide();
        try
        {
            selector.Configure(_repository, _ui, page.Candidates, page.SelectedOfficerIds, page.MaximumOfficers,
                () => IsCityMerchantCurrent && _cityMerchantPersons == selector,
                result => RunUiAction(() =>
                {
                    if (!IsCityMerchantCurrent || _cityMerchantPersons != selector || _cityMerchantDraft != draft) return;
                    _cityMerchantPersons = null; selector.Abandon(); selector.QueueFree(); _cityMerchantView!.Show();
                    if (result is not null) AcceptCityMerchant(draft.SelectOfficers(result));
                    RefreshCityMerchant();
                }), CurrentListScrollSpeed);
        }
        catch
        {
            _cityMerchantPersons = null; selector.Abandon(); selector.QueueFree();
            _cityMerchantView.Show(); RefreshCityMerchant(); throw;
        }
    }

    private void OpenCityMerchantAmount(CityMerchantMode mode)
    {
        if (!CanEditCityMerchant || _repository is null) return;
        var draft = _cityMerchantDraft!; var query = draft.QueryAmountOptions(mode);
        if (!query.Available) { CityMerchantUnavailable(query.UnresolvedInput!); return; }
        var request = query.Value!;
        var menu = new MerchantAmountMenuView(); _cityMerchantAmount = menu; AddChild(menu);
        _cityMerchantView!.SetBusy(true); _cityMerchantView.Hide();
        try
        {
            menu.Configure(_repository, _ui, request.PresetAmounts, request.Current, request.Minimum, request.Maximum,
                () => IsCityMerchantCurrent && _cityMerchantAmount == menu,
                result => RunUiAction(() =>
                {
                    if (!IsCityMerchantCurrent || _cityMerchantAmount != menu || _cityMerchantDraft != draft) return;
                    _cityMerchantAmount = null; menu.Abandon(); menu.QueueFree(); _cityMerchantView!.Show();
                    if (result is int amount && AcceptCityMerchant(draft.SetMode(mode)))
                        AcceptCityMerchant(draft.SetAmount(amount));
                    RefreshCityMerchant();
                }));
        }
        catch
        {
            _cityMerchantAmount = null; menu.Abandon(); menu.QueueFree();
            _cityMerchantView.Show(); RefreshCityMerchant(); throw;
        }
    }

    private void ConfirmCityMerchant(int force)
    {
        if (!CanEditCityMerchant || _repository is null) return;
        var draft = _cityMerchantDraft!; var view = _cityMerchantView!;
        var order = draft.BuildOrder(force);
        if (!order.Available) { CityMerchantUnavailable(order.UnresolvedInput!); return; }
        var prepared = draft.PrepareNarration(_ui.SourceLocale, _presentationRandom.Next);
        if (!prepared.Available) { CityMerchantUnavailable(prepared.UnresolvedInput!); return; }
        var speech = prepared.Value!; var route = speech.PortraitRoute;
        var sprites = new LocalizedUiSpriteSource(_repository);
        var layers = new[] { route.SpriteB, route.SpriteA, route.SpriteC }
            .Select(name => sprites.LoadTexturePackSprite(route.ResourceName, name)).ToArray();
        var narration = new DeploymentNarrationView { KeyboardInput = HandleGameplayNarrationKeyboard }; _cityMerchantNarration = narration; AddChild(narration);
        view.SetBusy(true);
        try
        {
            narration.Configure(_repository, _ui, speech.SpeakerName, speech.Text, layers, () => RunPlayAction(() =>
            {
                if (_cityMerchantNarration != narration) return;
                if (!IsCityMerchantCurrent || _cityMerchantDraft != draft) { CloseCityMerchant(); return; }
                _sceneAudioBinding?.StopNarrationVoice();
                _cityMerchantNarration = null; narration.Abandon(); narration.QueueFree();
                try
                {
                    if (!ApplyPlayableOrder(order.Value!, CloseCityMerchant))
                    { RefreshCityMerchant(); view.ApplyNotice(_playMessage.Text); }
                }
                catch { if (IsCityMerchantCurrent) RefreshCityMerchant(); throw; }
            }), () => _preferences.AutomaticMessageAdvance,
                () => CanAdvanceNarration&&_cityMerchantNarration==narration&&IsCityMerchantCurrent&&_cityMerchantDraft==draft);
            narration.Name = "CityMerchantNarration";
            foreach (var voice in speech.Voices) _sceneAudioBinding?.PlayNarrationVoice(voice);
        }
        catch
        {
            _sceneAudioBinding?.StopNarrationVoice();
            _cityMerchantNarration = null; narration.Abandon(); narration.QueueFree(); view.SetBusy(false); throw;
        }
    }

    private bool AcceptCityMerchant(AreaAdministrationDraftResult result)
    {
        if (result.Available) return true;
        CityMerchantUnavailable(result.UnresolvedInput ?? "city-merchant.unavailable"); return false;
    }
    private void CityMerchantUnavailable(string reason)
    {
        string message = _ui.Get("detail_unknown"); _playMessage.Text = message;
        _cityMerchantView?.ApplyNotice(message); GD.PushWarning(reason);
    }
    private void RequestCityMerchantBack()
    {
        if (!IsCityMerchantCurrent || _cityMerchantNarration is not null) return;
        if (_cityMerchantPersons is { } persons) persons.RequestCancel();
        else if (_cityMerchantAmount is { } amount) amount.RequestCancel();
        else _cityMerchantView!.RequestBack();
    }
    private void CloseCityMerchant()
    {
        if (!HasCityMerchantPage && _cityMerchantMap is null && _cityMerchantDraft is null) return;
        _sceneAudioBinding?.StopNarrationVoice();
        var map = _cityMerchantMap; _cityMerchantMap = null; _cityMerchantWorld = null; _cityMerchantSession = null;
        var persons = _cityMerchantPersons; _cityMerchantPersons = null;
        var amount = _cityMerchantAmount; _cityMerchantAmount = null;
        var narration = _cityMerchantNarration; _cityMerchantNarration = null;
        var view = _cityMerchantView; _cityMerchantView = null;
        var draft = _cityMerchantDraft; _cityMerchantDraft = null; _cityMerchantOfficers = null;
        if (persons is not null) { persons.Abandon(); persons.QueueFree(); }
        if (amount is not null) { amount.Abandon(); amount.QueueFree(); }
        if (narration is not null) { narration.Abandon(); narration.QueueFree(); }
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        draft?.Dispose();
        if (map is not null && GodotObject.IsInstanceValid(map) && _residentMap == map) map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown && _residentMap is not null) SetMapFocus(_mapFocused);
    }
}
