using Godot;
using San14.Core;
using San14.Data;

/// <summary>Each original radio/amount popup owns a detached seven-value
/// record. None clears its amount only on the popup's actual decision.</summary>
internal partial class ItemEditorFieldView : Control
{
    private AdministrationPageChrome chrome = null!;
    private ItemEditorValues working = null!;
    private Func<bool>? admitted;
    private Action<ItemEditorValues?>? closed;
    private readonly List<(int Id, UiSourceRadioButton Button)> choices = [];
    private UiSourceSlider? amount;
    private bool abandoned;
    private int section, selected, magnitude;
    internal void Configure(GameResourceRepository repository, PreviewText text, ItemEditorCatalog catalog,
        ItemEditorValues original, int section, Func<bool> admitted, Action<ItemEditorValues?> closed)
    {
        this.admitted = admitted; this.closed = closed; this.section = section; working = original;
        Name = "ItemEditorField" + section; ZIndex = 8; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x72C, RequestBack, Accept);
        string layout = section switch { 2 => "02_11_05_edit_offset_00", 3 => "02_11_05_edit_offset_01_2",
            4 => "02_11_05_edit_offset_01_1", _ => throw new ArgumentOutOfRangeException(nameof(section)) };
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, layout));
        var panel = new Control { Name = "OriginalItemFieldPopup", Position = new(460, 398), Size = source.Rectangle(0).Size,
            MouseFilter = MouseFilterEnum.Ignore }; chrome.Canvas.AddChild(panel);
        foreach (var node in source.Nodes.Where(node => node.Type is 1 or 3))
            source.Art(panel, node.Index, source.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        selected = section == 2 ? original.Kind : section == 3 ? original.AbilityKind : original.EffectKind;
        magnitude = Math.Max(1, section == 3 ? original.AbilityValue : original.EffectValue);
        var options = section == 2 ? catalog.Kinds : section == 3 ? catalog.AbilityKinds : catalog.EffectKinds;
        for (int index = 0; index < options.Count; index++)
        {
            var option = options[index]; Rect2 box = source.Rectangle(6 + index * 3);
            var button = new UiSourceRadioButton { Name = "ItemFieldChoice" + option.Id,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            panel.AddChild(button); choices.Add((option.Id, button));
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, UiSourceRadioSegment.Centre,
                option.Caption, () => { if (CanEdit()) { selected = option.Id; Refresh(); } });
        }
        if (section is 3 or 4)
        {
            int node = section == 3 ? 24 : 15;
            var box = source.Rectangle(node);
            amount = new UiSourceSlider { Name = "ItemEffectAmount", Position = box.Position, Size = box.Size };
            panel.AddChild(amount);
            amount.Configure(repository, chrome.Assets, chrome.SpriteShader, 1, 10, 1, magnitude, true,
                value => { if (CanEdit()) magnitude = value; });
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri); Refresh();
    }
    private bool CanEdit() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    private void Refresh()
    {
        foreach (var (id, button) in choices) button.Selected = id == selected;
        amount?.SetValue(magnitude, 10, section == 3 ? selected != 0 : selected == 2);
        chrome.ApplyDecision(true, true);
    }
    private void Accept()
    {
        if (!CanEdit()) return;
        working = section switch
        {
            2 => working with { Kind = selected },
            3 => working with { AbilityKind = selected, AbilityValue = selected == 0 ? 0 : magnitude },
            _ => working with { EffectKind = selected, EffectValue = selected == 2 ? magnitude : 0 }
        };
        working.Validate(); Finish(working);
    }
    private void Finish(ItemEditorValues? result) { var callback = closed; Abandon(); callback?.Invoke(result); }
    internal void RequestBack() { if (CanEdit()) Finish(null); }
    internal void Abandon()
    { abandoned = true; admitted = null; closed = null; foreach (var item in choices) item.Button.Abandon(); chrome?.Abandon(); Hide(); }
}
