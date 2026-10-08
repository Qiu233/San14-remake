using Godot;
using San14.Core;
using System.Globalization;

/// <summary>Convoy objectives use city identities and the same command gate as
/// every other player order. Binding reads persisted facts without moving cargo.</summary>
public partial class TransportOrdersPanel : VBoxContainer
{
    private readonly Label _title = new();
    private readonly Label _status = new() { Name = "TransportUnitStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _hint = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly OptionButton _destination = new() { Name = "TransportRetargetCity" };
    private readonly Button _redirect = new() { Name = "TransportRetarget" };
    private readonly Button _source = new() { Name = "TransportRetargetSource" };
    private Func<string, string>? _text;
    private Action<GameOrder>? _issue;
    private GameState? _world;
    private UnitState? _unit;
    private int? _force;
    private bool _strategy;

    public TransportOrdersPanel()
    {
        Name = "TransportOrdersPanel";
        AddChild(_title); AddChild(_status); AddChild(_hint);
        var row = new HFlowContainer(); row.AddChild(_destination); row.AddChild(_redirect); row.AddChild(_source);
        AddChild(row);
        _destination.ItemSelected += _ => UpdateEnabled();
        _redirect.Pressed += () =>
        {
            if (!_redirect.Disabled && _force is int force && _unit is { } unit)
                _issue?.Invoke(new RetargetTransportOrder(force, unit.Id, _destination.GetSelectedId()));
        };
        _source.Pressed += () =>
        {
            if (!_source.Disabled && _force is int force && _unit?.SourceLocationId is int source)
                _issue?.Invoke(new RetargetTransportOrder(force, _unit.Id, source));
        };
        UpdateEnabled();
    }

    public void Configure(Func<string, string> text, Action<GameOrder> issue)
    {
        _text = text; _issue = issue;
        _title.Text = text("play_transport_orders_title"); _hint.Text = text("play_transport_orders_hint");
        _redirect.Text = text("play_retarget"); _source.Text = text("play_transport_redirect_source");
        RenderStatus();
    }

    public void Refresh(GameState? world, int? selectedUnitId, int? forceId)
    {
        int previous = _destination.Selected >= 0 ? _destination.GetSelectedId() : -1;
        int? previousUnit = _unit?.Id;
        int? previousOfficer = _unit?.OfficerId;
        _world = world; _force = forceId;
        _unit = world is not null && selectedUnitId is int id && world.Units.TryGetValue(id, out UnitState? unit) &&
            unit.ForceId == forceId && unit.Mission?.Kind == UnitMissionKind.Transport ? unit : null;
        _strategy = world is { Phase: TurnPhase.Strategy } && forceId is int force &&
            world.Forces.TryGetValue(force, out ForceState? owner) && owner.IsActive &&
            PlayableOutcome.Evaluate(world, force) == PlayableResult.Ongoing;
        _destination.Clear();
        if (_unit is not null && world is not null)
        {
            foreach (CityState city in world.Cities.Values.Where(city => city.ForceId == forceId && city.HexId is not null).OrderBy(city => city.Id))
                _destination.AddItem(city.Name, city.Id);
            int wanted = previousUnit == _unit.Id && previousOfficer == _unit.OfficerId ? previous : _unit.Mission!.DestinationCityId!.Value;
            int index = _destination.GetItemIndex(wanted);
            if (index < 0 && world.Cities.Values.FirstOrDefault(city => city.ForceId == forceId && city.HexId == _unit.HexId) is { } current)
                index = _destination.GetItemIndex(current.Id);
            if (index >= 0) _destination.Select(index);
        }
        Visible = _unit is not null;
        RenderStatus(); UpdateEnabled();
    }

    private void RenderStatus()
    {
        SetMeta("transport_command_unit_id", _unit?.Id ?? -1); SetMeta("transport_original_source", _unit?.SourceLocationId ?? -1);
        SetMeta("transport_returning", _unit?.Mission?.Returning ?? false);
        if (_text is null || _unit is null) { _status.Text = ""; return; }
        UnitMission mission = _unit.Mission!;
        _status.Text = string.Format(CultureInfo.InvariantCulture, _text("play_transport_orders_status"),
            _world?.Officers.GetValueOrDefault(_unit.OfficerId)?.Name ?? _unit.OfficerId.ToString(),
            _unit.Troops, _unit.Supplies, mission.CargoGold,
            _unit.SourceLocationId is int source ? _world?.Cities.GetValueOrDefault(source)?.Name ?? _text("detail_unknown") : _text("detail_unknown"),
            _world?.Cities.GetValueOrDefault(mission.DestinationCityId!.Value)?.Name ?? _text("detail_unknown"),
            _text(mission.Returning ? "play_transport_stage_returning" : "play_transport_stage_outbound"),
            _text(mission.Arrival == TransportArrivalDisposition.EnterCity ? "play_transport_enter_city" : "play_transport_return_city"));
    }

    private void UpdateEnabled()
    {
        _destination.Disabled = !_strategy || _unit is null || _destination.ItemCount == 0;
        _redirect.Disabled = _destination.Disabled || _destination.Selected < 0;
        _source.Disabled = !_strategy || _unit?.SourceLocationId is not int source ||
            _world?.Cities.GetValueOrDefault(source)?.ForceId != _force;
    }
}
