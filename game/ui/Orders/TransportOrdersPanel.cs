using Godot;
using San14.Core;
using San14.Client.Gameplay;
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
    private TransportCommandView _view = TransportCommandView.Empty;

    public TransportOrdersPanel()
    {
        Name = "TransportOrdersPanel";
        AddChild(_title); AddChild(_status); AddChild(_hint);
        var row = new HFlowContainer(); row.AddChild(_destination); row.AddChild(_redirect); row.AddChild(_source);
        AddChild(row);
        _destination.ItemSelected += _ => UpdateEnabled();
        _redirect.Pressed += () =>
        {
            if (!_redirect.Disabled && _view.ForceId is int force && _view.Unit is { } unit)
                _issue?.Invoke(new RetargetTransportOrder(force, unit.Id, _destination.GetSelectedId()));
        };
        _source.Pressed += () =>
        {
            if (!_source.Disabled && _view.ForceId is int force && _view.Unit?.SourceLocationId is int source)
                _issue?.Invoke(new RetargetTransportOrder(force, _view.Unit.Id, source));
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
        int? previousUnit = _view.Unit?.Id;
        int? previousOfficer = _view.Unit?.OfficerId;
        _view = TransportCommandView.Create(world, selectedUnitId, forceId);
        _destination.Clear();
        if (_view.Unit is { } unit)
        {
            foreach (NamedCommandChoice city in _view.Destinations)
                _destination.AddItem(city.Name, city.Id);
            int wanted = previousUnit == unit.Id && previousOfficer == unit.OfficerId ? previous : unit.DestinationCityId;
            int index = _destination.GetItemIndex(wanted);
            if (index < 0 && unit.CurrentCityId is int current)
                index = _destination.GetItemIndex(current);
            if (index >= 0) _destination.Select(index);
        }
        Visible = _view.Unit is not null;
        RenderStatus(); UpdateEnabled();
    }

    private void RenderStatus()
    {
        TransportUnitView? unit = _view.Unit;
        SetMeta("transport_command_unit_id", unit?.Id ?? -1); SetMeta("transport_original_source", unit?.SourceLocationId ?? -1);
        SetMeta("transport_returning", unit?.Returning ?? false);
        if (_text is null || unit is null) { _status.Text = ""; return; }
        _status.Text = string.Format(CultureInfo.InvariantCulture, _text("play_transport_orders_status"),
            unit.OfficerName, unit.Troops, unit.Supplies, unit.CargoGold,
            unit.SourceCityName ?? _text("detail_unknown"), unit.DestinationCityName ?? _text("detail_unknown"),
            _text(unit.Returning ? "play_transport_stage_returning" : "play_transport_stage_outbound"),
            _text(unit.Arrival == TransportArrivalDisposition.EnterCity ? "play_transport_enter_city" : "play_transport_return_city"));
    }

    private void UpdateEnabled()
    {
        _destination.Disabled = !_view.CanIssue || _view.Unit is null || _destination.ItemCount == 0;
        _redirect.Disabled = _destination.Disabled || _destination.Selected < 0;
        _source.Disabled = !_view.CanIssue || _view.Unit?.SourceCityOwned != true;
    }
}
