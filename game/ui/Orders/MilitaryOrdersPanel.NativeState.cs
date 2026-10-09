using San14.Core;
using San14.Client.Gameplay;

public partial class MilitaryOrdersPanel
{
    /// <summary>Reads only persisted campaign facts. No source query, inferred
    /// supply path, readiness deadline or simulation writer is called here.</summary>
    private void RenderNativeState()
    {
        if (_text is null) return;
        MilitaryUnitView? unit = _view.FocusedUnit;
        bool selected = unit is not null;
        _nativeWaiting.Visible = selected;
        _departureSource.Visible = selected;
        _nativeLogistics.Visible = selected;
        SetMeta("military_native_owned", unit?.SourceCreated == true);
        SetMeta("military_native_morale1A", unit?.NativeMorale ?? -1);
        SetMeta("military_native_waiting_known", unit?.Waiting is not null);
        SetMeta("military_native_waiting20", unit?.Waiting?.Primary ?? -1);
        SetMeta("military_native_waiting21", unit?.Waiting?.Second ?? -1);
        SetMeta("military_native_waiting22", unit?.Waiting?.Third ?? -1);
        SetMeta("military_native_ready_ordinal", unit?.Waiting?.ReadyOrdinal ?? -1);
        SetMeta("military_native_current_ordinal", _view.CurrentOrdinal ?? -1);
        SetMeta("military_departure_location_id", unit?.SourceLocationId ?? -1);
        SetMeta("military_departure_supplies", unit?.Departure?.Supplies ?? -1);
        SetMeta("military_native_logistics_mask", unit?.Waiting is { } cached ? (cached.LogisticsSet ? 2 : 0) : -1);
        if (unit is null)
        { _nativeWaiting.Text = ""; _departureSource.Text = ""; _nativeLogistics.Text = ""; return; }

        if (unit.Waiting is not { } runtime)
            _nativeWaiting.Text = _text("play_military_native_waiting_unknown");
        else
        {
            _nativeWaiting.Text = Format("play_military_native_waiting", runtime.Primary, runtime.Second, runtime.Third);
            _nativeWaiting.Text += "\n" + (runtime.Primary > 0 ? _text("play_military_native_waiting_pending") :
                runtime.ReadyOrdinal is long ready && _view.CurrentOrdinal is long ordinal
                    ? Format("play_military_native_readiness_record", ready, ordinal)
                    : _text("play_military_native_readiness_unknown"));
        }
        _nativeWaiting.TooltipText = _text("play_military_native_waiting_hint");
        _departureSource.Text = unit.Departure is { IsCity: true } city
            ? Format("play_military_departure_city", city.Name, city.Supplies)
            : unit.Departure is { } gate
                ? Format("play_military_departure_gate", gate.Name, _text("detail_unknown"))
                : _text("play_military_departure_unknown");
        _nativeLogistics.Text = unit.Waiting is { } stored
            ? Format("play_military_native_logistics_record", _text(stored.LogisticsSet
                ? "play_military_native_logistics_set" : "play_military_native_logistics_clear"))
            : _text("play_military_native_logistics_unknown");
        _nativeLogistics.TooltipText = _text("play_military_native_logistics_hint");
    }
}
