using Godot;
using San14.Data;
using San14.Client.Gameplay;

/// <summary>Selections are local to this publication. Only the owner can quote
/// or confirm a transfer through the shared personnel command rules.</summary>
internal partial class PersonnelItemPage : Control
{
    private AdministrationPageChrome? chrome;
    private PersonnelCommandView source = null!;
    private PreviewText ui = null!;
    private OptionButton? mode;
    private ItemList? inventory, recipients;
    private UiSourceText holder = null!, cost = null!, loyalty = null!, status = null!;
    private Action<PersonnelItemSelection?>? changed;
    private PersonnelItemEvaluationView? evaluation;
    private bool busy, abandoned;

    internal PersonnelItemSelection? Selection
    {
        get
        {
            int item = inventory?.GetSelectedItems().FirstOrDefault(-1) ?? -1;
            if ((uint)item >= (uint)source.Items.Count) return null;
            bool revoke = mode?.GetSelectedId() == 1;
            int officer = recipients?.GetSelectedItems().FirstOrDefault(-1) ?? -1;
            int? recipient = revoke ? source.Items[item].HolderOfficerId :
                officer >= 0 ? recipients!.GetItemMetadata(officer).AsInt32() : null;
            return new(source.Items[item].Id, recipient, revoke);
        }
    }

    internal void Configure(GameResourceRepository repository, PreviewText text, Theme theme,
        PersonnelCommandView model, Action returned, Action accepted, Action<PersonnelItemSelection?> changed)
    {
        source = model; ui = text; this.changed = changed;
        Name = "PersonnelItemPage"; MouseFilter = MouseFilterEnum.Stop; ZIndex = 3;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new AdministrationPageChrome(); AddChild(chrome);
        chrome.Configure(repository, text, 0x223, returned, accepted);
        chrome.ApplyTitle(text.Get("play_personnel_items_title"));
        chrome.ApplyDefaultHint(text.Get("play_personnel_items_hint"));
        var layout = new UiSourceLayout(repository, 0xAB6ADAA7);
        var profile = MapFontProfile.FromNode(layout.Nodes.Single(node => node.Type == 2),
            layout.Source.Source.ResourceUri).WithPreset(MapTextPresets.Create()[13]);
        Line("PersonnelItemsCaption", text.Get("play_personnel_items_inventory"), new(320, 170));
        mode = new OptionButton { Name = "PersonnelItemMode", Position = new(1120, 160), Size = new(400, 40), Theme = theme };
        chrome.Canvas.AddChild(mode);
        mode.AddItem(text.Get("play_personnel_grant"), 0); mode.AddItem(text.Get("play_personnel_revoke"), 1);
        mode.ItemSelected += _ => { if (!busy && !abandoned) { RenderRecipients(); NotifyChanged(); } };
        inventory = List("PersonnelItemInventory", new(320, 230), new(680, 420));
        foreach (var item in model.Items)
            inventory.AddItem(text.Format("play_personnel_items_row", item.Name, item.HolderName ?? text.Get("detail_unknown")));
        inventory.ItemSelected += _ => NotifyChanged();
        if (inventory.ItemCount > 0) inventory.Select(0);
        Line("PersonnelItemRecipientCaption", text.Get("play_personnel_officer"), new(1120, 220));
        recipients = List("PersonnelItemRecipients", new(1120, 270), new(600, 250));
        recipients.ItemSelected += _ => NotifyChanged();
        holder = Line("PersonnelItemHolder", "", new(320, 690));
        cost = Line("PersonnelItemCost", "", new(1120, 560));
        loyalty = Line("PersonnelItemLoyalty", "", new(1120, 610));
        status = Line("PersonnelItemEvaluation", "", new(1120, 680));
        Line("PersonnelItemCount", model.InventoryKnown ? text.Format("play_personnel_items_count", model.Items.Count) :
            text.Get("play_personnel_inventory_unknown"), new(320, 740));
        RenderRecipients(); NotifyChanged();

        ItemList List(string name, Vector2 position, Vector2 size)
        {
            var list = new ItemList { Name = name, Position = position, Size = size, Theme = theme };
            chrome.Canvas.AddChild(list); return list;
        }
        UiSourceText Line(string name, string value, Vector2 position)
        {
            var line = new UiSourceText { Name = name, Position = position, Size = new(680, 36) };
            chrome.Canvas.AddChild(line); line.Configure(chrome.Font, profile); line.Text = value; return line;
        }
    }

    private void RenderRecipients()
    {
        if (recipients is null) return;
        int old = recipients.GetSelectedItems().FirstOrDefault(-1);
        int? oldId = old >= 0 ? recipients.GetItemMetadata(old).AsInt32() : null;
        recipients.Clear();
        foreach (var officer in source.Officers.Where(person => person.Id != source.RulerId))
        {
            recipients.AddItem(ui.Format("play_personnel_items_officer", officer.Name,
                officer.Loyalty?.ToString() ?? ui.Get("detail_unknown")));
            int row = recipients.ItemCount - 1; recipients.SetItemMetadata(row, officer.Id);
            if (officer.Id == oldId) recipients.Select(row);
        }
        if (recipients.ItemCount > 0 && recipients.GetSelectedItems().Length == 0) recipients.Select(0);
    }
    private void NotifyChanged()
    {
        if (busy || abandoned) return;
        var selected = Selection;
        var item = selected is null ? null : source.FindItem(selected.ItemId);
        holder.Text = ui.Format("play_personnel_items_holder", item?.HolderName ?? ui.Get("detail_unknown"));
        changed?.Invoke(selected); RefreshEditors();
    }
    internal void ApplyEvaluation(PersonnelItemEvaluationView value)
    {
        evaluation = value;
        cost.Text = ui.Format("play_personnel_confirm_cost", value.CommandBooks?.ToString() ?? ui.Get("detail_unknown"),
            value.Gold?.ToString() ?? ui.Get("detail_unknown"));
        loyalty.Text = ui.Format("play_personnel_confirm_loyalty", value.LoyaltyBefore?.ToString() ?? ui.Get("detail_unknown"),
            value.LoyaltyAfter?.ToString() ?? ui.Get("detail_unknown"));
        status.Text = value.Status; RefreshEditors();
    }
    internal void SetBusy(bool value) { busy = value; RefreshEditors(); }
    internal void ApplyHint(string value) => chrome?.ApplyDefaultHint(value);
    private void RefreshEditors()
    {
        bool blocked = busy || abandoned || !source.CanIssue;
        chrome?.ApplyReturn(!busy); chrome?.ApplyDecision(!blocked && evaluation is { Allowed: true } && Selection is not null, true);
        if (mode is not null) mode.Disabled = blocked;
        if (inventory is not null)
        {
            inventory.MouseFilter = blocked ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
            inventory.FocusMode = blocked ? FocusModeEnum.None : FocusModeEnum.All;
        }
        if (recipients is not null)
        {
            recipients.Visible = mode?.GetSelectedId() != 1;
            recipients.MouseFilter = blocked ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
            recipients.FocusMode = blocked ? FocusModeEnum.None : FocusModeEnum.All;
        }
    }
    internal void Abandon() { abandoned = true; changed = null; SetBusy(true); chrome?.Abandon(); Hide(); }
}
