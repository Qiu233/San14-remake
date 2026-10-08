using Godot;

public partial class Main
{
    private TransportOrdersPanel? _transportOrders;

    private void BuildTransportOrders(VBoxContainer military)
    {
        _transportOrders = new TransportOrdersPanel();
        military.AddChild(_transportOrders);
    }
    private void ConfigureTransportOrders() => _transportOrders?.Configure(_ui.Get,
        command => RunPlayAction(() => ApplyPlayableOrder(command)));
    private void RefreshTransportOrders() => _transportOrders?.Refresh(_world,
        _playUnit.Selected >= 0 ? _playUnit.GetSelectedId() : null, _playerForceId);
}
