using Godot;
using San14.Core;
using System.Globalization;

/// <summary>Known item holders and office definitions; changes use the shared personnel command policy.</summary>
public partial class PersonnelOrdersPanel : VBoxContainer
{
    private readonly Label _title = new();
    private readonly Label _officerLabel = new();
    private readonly OptionButton _officer = new() { Name = "PersonnelOfficer" };
    private readonly Label _itemLabel = new();
    private readonly OptionButton _item = new() { Name = "PersonnelItem" };
    private readonly Label _officeLabel = new();
    private readonly OptionButton _office = new() { Name = "PersonnelOffice" };
    private readonly Label _status = new() { Name = "PersonnelStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _inventory = new() { Name = "PersonnelInventoryStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button _grant = new() { Name = "PersonnelGrantItem" };
    private readonly Button _revoke = new() { Name = "PersonnelRevokeItem" };
    private readonly Button _appoint = new() { Name = "PersonnelSetOffice" };
    private Func<string, string>? _text;
    private Action<GameOrder>? _submit;
    private GameState? _world;
    private int? _player;
    private bool _officeCommandsAvailable;

    public PersonnelOrdersPanel()
    {
        Name = "PersonnelOrdersPanel";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(_title);
        Row(_officerLabel, _officer); Row(_itemLabel, _item); Row(_officeLabel, _office);
        var actions = new HFlowContainer();
        actions.AddChild(_grant); actions.AddChild(_revoke); actions.AddChild(_appoint);
        AddChild(actions); AddChild(_status); AddChild(_inventory);
        _officer.ItemSelected += _ => { SelectCurrentOffice(); RefreshStatus(); };
        _item.ItemSelected += _ => RefreshStatus();
        _grant.Pressed += () =>
        {
            if (CanSubmit() && _item.Selected >= 0 && _officer.Selected >= 0)
                _submit?.Invoke(new GrantItemOrder(_player!.Value, _item.GetSelectedId(), _officer.GetSelectedId()));
        };
        _revoke.Pressed += () =>
        {
            if (CanSubmit() && SelectedItem()?.Holder?.OfficerId is int holder)
                _submit?.Invoke(new RevokeItemOrder(_player!.Value, _item.GetSelectedId(), holder));
        };
        _appoint.Pressed += () =>
        {
            if (CanSubmit() && _officer.Selected >= 0 && _office.Selected >= 0)
                _submit?.Invoke(new SetOfficerOfficeOrder(_player!.Value, _officer.GetSelectedId(), _office.GetSelectedId()));
        };
        RefreshStatus();
    }

    public void Configure(Func<string, string> text, Action<GameOrder> submit, bool officeCommandsAvailable = false)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _submit = submit ?? throw new ArgumentNullException(nameof(submit));
        _officeCommandsAvailable = officeCommandsAvailable;
        _title.Text = text("play_personnel_title"); _officerLabel.Text = text("play_personnel_officer");
        _itemLabel.Text = text("play_personnel_item"); _officeLabel.Text = text("play_personnel_office");
        _grant.Text = text("play_personnel_grant"); _revoke.Text = text("play_personnel_revoke");
        _appoint.Text = text("play_personnel_appoint");
        RefreshStatus();
    }

    public void Refresh(GameState? world, int? playerForceId)
    {
        _world = world; _player = playerForceId;
        int officer = Selected(_officer), item = Selected(_item), office = Selected(_office);
        _officer.Clear(); _item.Clear(); _office.Clear();
        if (world is not null && playerForceId is int player)
        {
            foreach (OfficerState target in world.Officers.Values.Where(target => target.ForceId == player).OrderBy(target => target.Id))
                _officer.AddItem(target.Name, target.Id);
            foreach (ItemState target in (world.Items?.Values ?? Enumerable.Empty<ItemState>()).Where(target =>
                         target.Holder?.OfficerId is int owner && world.Officers.TryGetValue(owner, out OfficerState? holder) && holder.ForceId == player)
                         .OrderBy(target => target.Id))
                _item.AddItem(world.ItemDefinitions[target.DefinitionId].Name, target.Id);
            foreach (OfficerOfficeDefinition definition in world.OfficerOffices.Values.OrderBy(definition => definition.Id))
                _office.AddItem(definition.Name, definition.Id);
        }
        Restore(_officer, officer); Restore(_item, item);
        if (_office.GetItemIndex(office) >= 0) Restore(_office, office);
        else SelectCurrentOffice();
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        bool strategy = CanSubmit();
        _officer.Disabled = !strategy || _officer.ItemCount == 0;
        _item.Disabled = !strategy || _item.ItemCount == 0;
        _office.Disabled = !strategy || _office.ItemCount == 0;
        _grant.Disabled = !strategy || _officer.Selected < 0 || _item.Selected < 0;
        _revoke.Disabled = !strategy || SelectedItem()?.Holder?.OfficerId is null;
        _appoint.Disabled = !_officeCommandsAvailable || !strategy || _officer.Selected < 0 || _office.Selected < 0;
        if (_text is null) return;
        _appoint.TooltipText = _officeCommandsAvailable ? "" : _text("play_personnel_office_unverified");
        string currentOffice = _text("detail_unknown"), loyalty = _text("detail_unknown"), holderName = _text("detail_unknown");
        if (_world is not null && _officer.Selected >= 0 && _world.Officers.TryGetValue(_officer.GetSelectedId(), out OfficerState? officer))
        {
            if (officer.OfficeId is int office && _world.OfficerOffices.TryGetValue(office, out OfficerOfficeDefinition? definition))
                currentOffice = definition.Name;
            if (officer.Loyalty is int known) loyalty = known.ToString(CultureInfo.InvariantCulture);
        }
        if (SelectedItem()?.Holder?.OfficerId is int holder && _world!.Officers.TryGetValue(holder, out OfficerState? owner)) holderName = owner.Name;
        _status.Text = Format("play_personnel_status", currentOffice, loyalty, holderName);
        _inventory.Text = _world?.Items is null ? _text("play_personnel_inventory_unknown") :
            Format("play_personnel_inventory_known", _item.ItemCount,
                _world.Items.Values.Count(value => value.Holder?.OfficerId is null || value.IsPresent is null));
        SetMeta("personnel_force_items", _item.ItemCount);
        SetMeta("personnel_office_definitions", _office.ItemCount);
    }

    private ItemState? SelectedItem() => _item.Selected >= 0 && _world?.Items?.TryGetValue(_item.GetSelectedId(), out ItemState? item) == true ? item : null;
    private void SelectCurrentOffice()
    {
        _office.Select(-1);
        if (_officer.Selected >= 0 && _world?.Officers.TryGetValue(_officer.GetSelectedId(), out OfficerState? officer) == true &&
            officer.OfficeId is int current && _office.GetItemIndex(current) is int index && index >= 0)
            _office.Select(index);
    }
    private bool CanSubmit() => _world?.Phase == TurnPhase.Strategy && _player is int player && _world.Forces.TryGetValue(player, out ForceState? owner) && owner.IsActive;
    private string Format(string key, params object[] values) => string.Format(CultureInfo.InvariantCulture, _text!(key), values);
    private static int Selected(OptionButton value) => value.Selected < 0 ? -1 : value.GetSelectedId();
    private static void Restore(OptionButton value, int id)
    {
        if (value.ItemCount > 0) { int index = value.GetItemIndex(id); value.Select(index < 0 ? 0 : index); }
    }
    private void Row(Control label, Control field)
    {
        var row = new HFlowContainer(); row.AddChild(label); row.AddChild(field); AddChild(row);
    }
}
