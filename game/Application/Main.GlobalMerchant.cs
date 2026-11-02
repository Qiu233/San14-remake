using Godot;
using San14.Core;

public partial class Main
{
    private bool IsGlobalMerchantCurrent(GlobalMerchantCitySelectionView view) =>
        !_failed && !_shuttingDown && _globalMerchantCities == view &&
        _cityMerchantMap is not null && _cityMerchantMap == _residentMap &&
        ReferenceEquals(_session, _cityMerchantSession) && ReferenceEquals(_world, _cityMerchantWorld) &&
        ReferenceEquals(_session?.World, _cityMerchantWorld);

    /// <summary>Called by the sixth original top-bar button. A city is always
    /// chosen explicitly from the current command-admitted publication.</summary>
    private void OpenGlobalMerchant()
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasActionReport ||
            HasPersonnelConfirmation || HasAdministrationPage || HasMilitaryOrderPage || HasCampaignSavePage ||
            HasMapSystemMenu || HasCommandWorkspace || _detailsView is not null || _repository is null ||
            _session is not { PlayerForceId: int force } session || session.HasPendingOperation ||
            _world is not { Phase: TurnPhase.Strategy } world || !ReferenceEquals(session.World, world) ||
            _campaignComposition is not { } composition || _residentMap is not { IsPlayableMapReady: true } map) return;
        var prepared = composition.PrepareArmyQueries(world).PrepareGlobalMerchantCitySelection(world, force);
        if (!prepared.Available) { CityMerchantUnavailable(prepared.UnresolvedInput!); return; }
        var publication = prepared.Value!;
        var selector = new GlobalMerchantCitySelectionView(); _globalMerchantCities = selector;
        _cityMerchantSession = session; _cityMerchantWorld = world; _cityMerchantMap = map;
        AddChild(selector);
        try
        {
            SetMapFocus(true); map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
            selector.Configure(_repository, _ui, publication, () => IsGlobalMerchantCurrent(selector),
                city => RunUiAction(() =>
                {
                    if (!IsGlobalMerchantCurrent(selector)) return;
                    // The selector's retained publication is the only way to
                    // enter a child; command submission rechecks its live world.
                    if (city is int selected && !publication.CurrentCityIds.Contains(selected)) return;
                    CloseCityMerchant();
                    if (city is int id) OpenCityMerchant(id, fromGlobal: true);
                }), CurrentListScrollSpeed, TryBorrowApplicationUiResources(selector));
        }
        catch { CloseCityMerchant(); throw; }
    }
}
