using San14.Core;

public partial class MilitaryOrdersPanel
{
    /// <summary>Reads only persisted campaign facts. No source query, inferred
    /// supply path, readiness deadline or simulation writer is called here.</summary>
    private void RenderNativeState()
    {
        if (_text is null) return;
        UnitState? unit = _boundUnit;
        bool selected = unit is not null;
        _nativeWaiting.Visible = selected;
        _departureSource.Visible = selected;
        _nativeLogistics.Visible = selected;
        SetMeta("military_native_owned", unit?.NativeArmyOrigin is not null);
        SetMeta("military_native_morale1A", unit?.NativeMorale1A is byte morale ? morale : -1);
        SetMeta("military_native_waiting_known", unit?.NativeTacticRuntime is not null);
        SetMeta("military_native_waiting20", unit?.NativeTacticRuntime?.Waiting20 ?? -1);
        SetMeta("military_native_waiting21", unit?.NativeTacticRuntime?.Waiting21 ?? -1);
        SetMeta("military_native_waiting22", unit?.NativeTacticRuntime?.Waiting22 ?? -1);
        SetMeta("military_native_ready_ordinal", unit?.NativeTacticRuntime?.PrimaryWaitingReadyAtUpdateOrdinal ?? -1);
        SetMeta("military_native_current_ordinal", _world?.NativeClock?.UpdateOrdinal ?? -1);
        SetMeta("military_departure_location_id", unit?.SourceLocationId ?? -1);
        SetMeta("military_departure_supplies", unit?.SourceLocationId is int sourceId &&
            _world?.Cities.TryGetValue(sourceId, out CityState? source) == true ? source.Supplies : -1);
        SetMeta("military_native_logistics_mask", unit?.NativeTacticRuntime is { } cached ? (cached.Flags26 & 2) : -1);
        if (unit is null)
        { _nativeWaiting.Text = ""; _departureSource.Text = ""; _nativeLogistics.Text = ""; return; }

        if (unit.NativeTacticRuntime is not { } runtime)
            _nativeWaiting.Text = _text("play_military_native_waiting_unknown");
        else
        {
            _nativeWaiting.Text = Format("play_military_native_waiting", runtime.Waiting20, runtime.Waiting21, runtime.Waiting22);
            _nativeWaiting.Text += "\n" + (runtime.Waiting20 > 0 ? _text("play_military_native_waiting_pending") :
                runtime.PrimaryWaitingReadyAtUpdateOrdinal is long ready && _world?.NativeClock is { } clock
                    ? Format("play_military_native_readiness_record", ready, clock.UpdateOrdinal)
                    : _text("play_military_native_readiness_unknown"));
        }
        _nativeWaiting.TooltipText = _text("play_military_native_waiting_hint");
        _departureSource.Text = unit.SourceLocationId is int id && _world?.Cities.TryGetValue(id, out CityState? city) == true
            ? Format("play_military_departure_city", city.Name, city.Supplies)
            : unit.SourceLocationId is int gateId && _world?.Gates.TryGetValue(gateId, out GateState? gate) == true
                ? Format("play_military_departure_gate", gate.Name, _text("detail_unknown"))
                : _text("play_military_departure_unknown");
        _nativeLogistics.Text = unit.NativeTacticRuntime is { } stored
            ? Format("play_military_native_logistics_record", _text((stored.Flags26 & 2) != 0
                ? "play_military_native_logistics_set" : "play_military_native_logistics_clear"))
            : _text("play_military_native_logistics_unknown");
        _nativeLogistics.TooltipText = _text("play_military_native_logistics_hint");
    }
}
