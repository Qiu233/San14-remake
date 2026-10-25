using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private MapSceneView? _cityAttentionMap;
    private GameplaySession? _cityAttentionSession;
    private GameState? _cityAttentionWorld;

    private bool IsMerchantAttentionEnabled(GameplaySession session) =>
        ReferenceEquals(_session, session) && session.World.Phase == TurnPhase.Strategy &&
        !session.HasPendingOperation && !_advancingPeriod && !IsReturningToTitle && !_shuttingDown;

    private bool TryRefreshProvinceAttention(GameState owner, MapSceneView map, out string? missing)
    {
        var query = _campaignComposition!.PrepareArmyQueries(owner).QueryCityProvinceAdvantageIndicators(owner);
        missing = query.UnresolvedInput;
        return query.Value is { } rows && map.TryApplyProvinceAttention(rows, out missing);
    }

    private void RefreshCityMapAttention(GameState owner, MapSceneView map)
    {
        if (_session is not { } session || _campaignComposition is null) return;
        if (ReferenceEquals(_cityAttentionMap, map) && ReferenceEquals(_cityAttentionSession, session) &&
            ReferenceEquals(_cityAttentionWorld, owner)) return;
        // Invoked on actual world publication, not by ordinary display ticks
        // or removal of presentation outbox entries.
        _cityAttentionMap = map; _cityAttentionSession = session; _cityAttentionWorld = owner;
        if (!TryRefreshProvinceAttention(owner, map, out var missing)) GD.PushWarning(missing);
        var query = _campaignComposition.PrepareArmyQueries(owner)
            .QueryCityMerchantAttention(owner, IsMerchantAttentionEnabled(session));
        if (query.Value is not { } rows) GD.PushWarning(query.UnresolvedInput);
        else if (!map.TryApplyMerchantAttention(rows, () => IsMerchantAttentionEnabled(session), out missing))
            GD.PushWarning(missing);
    }

    private void RestoreCityMapAttention(GameState owner, MapSceneView map)
    {
        if (!ReferenceEquals(_cityAttentionMap, map) || !ReferenceEquals(_cityAttentionSession, _session))
            RefreshCityMapAttention(owner, map);
    }
}
