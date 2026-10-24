using Godot;
using San14.Core;
using San14.Client.Gameplay;
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
    private readonly Button _inspect = new() { Name = "PersonnelOfficerDetails" };
    private readonly Button _openItems = new() { Name = "OpenPersonnelItems" };
    private readonly Control _itemRow;
    private Action<int>? _inspectOfficer;
    private Action? _showItems;
    private Func<string, string>? _text;
    private Action<GameOrder>? _submit;
    private PersonnelCommandView _view = PersonnelCommandView.Empty;
    private bool _officeCommandsAvailable;

    public PersonnelOrdersPanel()
    {
        Name = "PersonnelOrdersPanel";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(_title);
        AddChild(_openItems); _openItems.Pressed += () => _showItems?.Invoke();
        Row(_officerLabel, _officer, _inspect); _itemRow = Row(_itemLabel, _item); Row(_officeLabel, _office);
        _inspect.Pressed += () => { if (_officer.Selected >= 0) _inspectOfficer?.Invoke(_officer.GetSelectedId()); };
        var actions = new HFlowContainer();
        actions.AddChild(_grant); actions.AddChild(_revoke); actions.AddChild(_appoint);
        AddChild(actions); AddChild(_status); AddChild(_inventory);
        _officer.ItemSelected += _ => { SelectCurrentOffice(); RefreshStatus(); };
        _item.ItemSelected += _ => RefreshStatus();
        _grant.Pressed += () =>
        {
            if (CanSubmit() && _item.Selected >= 0 && _officer.Selected >= 0)
                _submit?.Invoke(new GrantItemOrder(_view.PlayerForceId!.Value, _item.GetSelectedId(), _officer.GetSelectedId()));
        };
        _revoke.Pressed += () =>
        {
            if (CanSubmit() && SelectedItem()?.HolderOfficerId is int holder)
                _submit?.Invoke(new RevokeItemOrder(_view.PlayerForceId!.Value, _item.GetSelectedId(), holder));
        };
        _appoint.Pressed += () =>
        {
            if (CanSubmit() && _officer.Selected >= 0 && _office.Selected >= 0)
                _submit?.Invoke(new SetOfficerOfficeOrder(_view.PlayerForceId!.Value, _officer.GetSelectedId(), _office.GetSelectedId()));
        };
        RefreshStatus();
    }

    public void Configure(Func<string, string> text, Action<GameOrder> submit, bool officeCommandsAvailable = false,
        Action<int>? inspectOfficer = null, Action? openItems = null)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _submit = submit ?? throw new ArgumentNullException(nameof(submit));
        _officeCommandsAvailable = officeCommandsAvailable;
        _inspectOfficer = inspectOfficer; _inspect.Text = text("detail_officer_open");
        _showItems = openItems; _openItems.Text = text("play_personnel_items_open");
        _title.Text = text("play_personnel_title"); _officerLabel.Text = text("play_personnel_officer");
        _itemLabel.Text = text("play_personnel_item"); _officeLabel.Text = text("play_personnel_office");
        _grant.Text = text("play_personnel_grant"); _revoke.Text = text("play_personnel_revoke");
        _appoint.Text = text("play_personnel_appoint");
        RefreshStatus();
    }

    public void Refresh(GameState? world, int? playerForceId)
    {
        bool native = world?.NativeArmySimulation is not null;
        _openItems.Visible = native; _itemRow.Visible = !native;
        _grant.Visible = !native; _revoke.Visible = !native; _inventory.Visible = !native;
        _view = PersonnelCommandView.Create(world, playerForceId);
        int officer = Selected(_officer), item = Selected(_item), office = Selected(_office);
        _officer.Clear(); _item.Clear(); _office.Clear();
        foreach (PersonnelOfficerChoice target in _view.Officers) _officer.AddItem(target.Name, target.Id);
        foreach (PersonnelItemChoice target in _view.Items) _item.AddItem(target.Name, target.Id);
        foreach (NamedCommandChoice definition in _view.Offices) _office.AddItem(definition.Name, definition.Id);
        Restore(_officer, officer); Restore(_item, item);
        if (_office.GetItemIndex(office) >= 0) Restore(_office, office);
        else SelectCurrentOffice();
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        bool strategy = CanSubmit();
        _openItems.Disabled = !strategy || _showItems is null;
        _inspect.Disabled = _inspectOfficer is null || _officer.Selected < 0;
        _officer.Disabled = !strategy || _officer.ItemCount == 0;
        _item.Disabled = !strategy || _item.ItemCount == 0;
        _office.Disabled = !strategy || _office.ItemCount == 0;
        var selected = SelectedItem();
        _grant.Disabled = !strategy || _officer.Selected < 0 || _view.RulerId is not int ruler ||
            _officer.GetSelectedId() == ruler || selected?.HolderOfficerId != ruler;
        _revoke.Disabled = !strategy || _view.RulerId is null || selected?.HolderOfficerId is not int holder || holder == _view.RulerId;
        _appoint.Disabled = !_officeCommandsAvailable || !strategy || _officer.Selected < 0 || _office.Selected < 0;
        if (_text is null) return;
        _appoint.TooltipText = _officeCommandsAvailable ? "" : _text("play_personnel_office_unverified");
        string currentOffice = _text("detail_unknown"), loyalty = _text("detail_unknown"), holderName = _text("detail_unknown");
        if (_officer.Selected >= 0 && _view.FindOfficer(_officer.GetSelectedId()) is { } officer)
        {
            if (officer.OfficeName is string officeName) currentOffice = officeName;
            if (officer.Loyalty is int known) loyalty = known.ToString(CultureInfo.InvariantCulture);
        }
        if (SelectedItem()?.HolderName is string knownHolder) holderName = knownHolder;
        _status.Text = Format("play_personnel_status", currentOffice, loyalty, holderName);
        _inventory.Text = !_view.InventoryKnown ? _text("play_personnel_inventory_unknown") :
            Format("play_personnel_inventory_known", _item.ItemCount, _view.UnresolvedItemCount);
        SetMeta("personnel_force_items", _item.ItemCount);
        SetMeta("personnel_office_definitions", _office.ItemCount);
    }

    private PersonnelItemChoice? SelectedItem() => _item.Selected >= 0 ? _view.FindItem(_item.GetSelectedId()) : null;
    private void SelectCurrentOffice()
    {
        _office.Select(-1);
        if (_officer.Selected >= 0 && _view.FindOfficer(_officer.GetSelectedId()) is { } officer &&
            officer.OfficeId is int current && _office.GetItemIndex(current) is int index && index >= 0)
            _office.Select(index);
    }
    private bool CanSubmit() => _view.CanIssue;
    private string Format(string key, params object[] values) => string.Format(CultureInfo.InvariantCulture, _text!(key), values);
    private static int Selected(OptionButton value) => value.Selected < 0 ? -1 : value.GetSelectedId();
    private static void Restore(OptionButton value, int id)
    {
        if (value.ItemCount > 0) { int index = value.GetItemIndex(id); value.Select(index < 0 ? 0 : index); }
    }
    private Control Row(params Control[] controls)
    {
        var row = new HFlowContainer(); foreach (var control in controls) row.AddChild(control); AddChild(row);
        return row;
    }
}
