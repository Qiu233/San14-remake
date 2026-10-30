using Godot;
using San14.Data;
using San14.Data.Formats;

internal partial class ItemEditorImageView : Control
{
    private sealed record Entry(int ImageIndex, string Name, RgbaTexture? External = null, string? Error = null);
    private AdministrationPageChrome chrome = null!;
    private GameResourceRepository repository = null!;
    private Func<bool>? admitted;
    private Action<int?>? closed;
    private readonly List<Control> cells = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private IReadOnlyList<Entry>[] pages = [[], []];
    private UiSourceLayout pane = null!, cell = null!;
    private Control host = null!;
    private UiSourceScrollbar scroll = null!;
    private int tab, selected;
    private bool abandoned;
    internal void Configure(GameResourceRepository repository, PreviewText text, ItemEditorCatalog catalog,
        IReadOnlyList<ItemEditorExternalImage> external, int original, Func<bool> admitted, Action<int?> closed)
    {
        this.repository = repository; this.admitted = admitted; this.closed = closed; selected = original;
        Name = "ItemImageSelector"; ZIndex = 8; MouseFilter = MouseFilterEnum.Stop; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, 0x72C, RequestBack, Accept);
        var root = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_11_05_edit_item"));
        pane = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_11_05_edit_offset_02"));
        cell = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_11_05_edit_offset_02_item"));
        host = new Control { Position = root.Rectangle(5).Position, Size = pane.Rectangle(0).Size,
            MouseFilter = MouseFilterEnum.Ignore }; chrome.Canvas.AddChild(host);
        foreach (var node in pane.Nodes.Where(node => node.Type is 1 or 3))
            pane.Art(host, node.Index, pane.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        pages = [catalog.NormalImages().Select(item => new Entry(item.Values.ImageIndex, item.Name)).ToArray(),
            external.Select(item => new Entry(item.ImageIndex, "", item.Texture, item.Error)).ToArray()];
        tab = pages[1].Any(item => item.ImageIndex == original) ? 1 : 0;
        // Original 604810 mounts fifteen cells at 16,21,...,86. The list's
        // backing publication retains physical item occurrences, including duplicate images.
        for (int index = 0; index < 2; index++)
        {
            int page = index; Rect2 box = pane.Rectangle(index == 0 ? 6 : 8);
            var button = new UiSourceTabButton { Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            host.AddChild(button); tabs.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                catalog.Data(index == 0 ? 0x3CD : 0x6D0), index == 0 ? 0x172EDC24u : 0xD45873AAu);
            button.Pressed += () => { if (CanEdit()) { tab = page; RebuildScroll(); } };
        }
        scroll = new UiSourceScrollbar { Position = pane.Rectangle(91).Position, Size = pane.Rectangle(91).Size };
        host.AddChild(scroll); RebuildScroll(); SetMeta("source_layout", pane.Source.Source.ResourceUri);
    }
    private bool CanEdit() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    private void RebuildScroll()
    {
        // UiSourceScrollbar's Configure is called once per new tab publication;
        // the previous control is retired rather than stacking callbacks.
        var old = scroll; var box = pane.Rectangle(91);
        scroll = new UiSourceScrollbar { Position = box.Position, Size = box.Size };
        host.AddChild(scroll); if (old is not null) { host.RemoveChild(old); old.QueueFree(); }
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0, (pages[tab].Count + 2) / 3 - 5),
            1, _ => Refresh(), stepPixels:1);
        Refresh();
    }
    private void Refresh()
    {
        foreach (var entry in cells) { host.RemoveChild(entry); entry.QueueFree(); } cells.Clear();
        for (int index = 0; index < 15; index++)
        {
            int row = (int)scroll.Value * 3 + index; if (row >= pages[tab].Count) break;
            var entry = pages[tab][row]; var box = pane.Rectangle(16 + index * 5);
            var control = new Control { Position = box.Position, Size = box.Size, MouseFilter = MouseFilterEnum.Pass };
            host.AddChild(control); cells.Add(control);
            foreach (var node in cell.Nodes.Where(node => node.Type is 1 or 3))
                if (node.Index != 3) cell.Art(control, node.Index, cell.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
            if (entry.External is { } rgba)
            {
                using var image = Image.CreateFromData(rgba.Width, rgba.Height, false, Image.Format.Rgba8, rgba.Pixels);
                var imageBox = cell.Rectangle(3);
                control.AddChild(new TextureRect { Position = imageBox.Position, Size = imageBox.Size,
                    Texture = ImageTexture.CreateFromImage(image), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore });
            }
            else if (entry.ImageIndex < 201)
            {
                var route = CollectionImageRoute.ForItem(entry.ImageIndex);
                cell.Art(control, 3, cell.Rectangle(3), chrome.Assets, chrome.SpriteShader,
                    commonTexturePack: route.TexturePack, commonSpriteOverride: route.SpriteName);
            }
            cell.Text(control, 6, default, entry.Name, chrome.Font);
            var hit = new Button { Name = "ItemImage" + row, Size = box.Size, Flat = true, Text = "", Disabled = entry.Error is not null };
            control.AddChild(hit);
            hit.Pressed += () => { if (CanEdit()) { selected = entry.ImageIndex; Refresh(); } };
            hit.MouseEntered += () => { if (CanEdit()) chrome.ApplyDefaultHint(entry.Error ?? ""); };
        }
        for (int index = 0; index < tabs.Count; index++) tabs[index].Selected = tab == index;
        chrome.ApplyDecision(pages[tab].Any(item => item.ImageIndex == selected && item.Error is null), true);
    }
    private void Accept() { if (CanEdit() && pages[tab].Any(item => item.ImageIndex == selected && item.Error is null)) Finish(selected); }
    private void Finish(int? result) { var callback = closed; Abandon(); callback?.Invoke(result); }
    internal void RequestBack() { if (CanEdit()) Finish(null); }
    internal void Abandon() { abandoned = true; admitted = null; closed = null; chrome?.Abandon(); Hide(); }
}
