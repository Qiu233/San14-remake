using Godot;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

internal sealed record PersonnelItemPageValues(PersonnelItemSelection? Selection, string ItemName,
    int? ItemImageIndex, LoadedSprite? HolderPortrait, AreaAdministrationOfficerValues Target,
    IReadOnlyDictionary<int, string> Text, IReadOnlyDictionary<int, bool> Visible,
    bool CanChooseItem, bool CanChooseTarget, bool CanSubmit, string Guide)
{
    internal int? TargetLoyaltyAfter { get; init; }
    internal int? TargetOfficerId { get; init; }
}

/// <summary>The ordinary item-transfer command body. Its owner supplies the
/// retained draft and real quote; this view never calculates a transfer.</summary>
internal partial class PersonnelItemPage : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceLayout source = null!;
    private UiSourceCommandButton item = null!;
    private AreaAdministrationOfficerView target = null!;
    private Control itemImage = null!, holderFace = null!;
    private readonly Dictionary<int, UiSourceText> fields = [];
    private readonly List<(int Node, Control View)> body = [];
    private GameResourceRepository repository = null!;
    private PreviewText ui = null!;
    private PersonnelItemPageValues? publication;
    private Func<bool>? current;
    private Action? returned, accepted;
    private Texture2D? externalItemTexture;
    private LoadedSprite? currentPortrait;
    private int? currentImageIndex;
    private bool busy, abandoned;
    private static readonly Vector2 Mount = new(464, 235);

    internal PersonnelItemSelection? Selection => publication?.Selection;
    internal AdministrationPageChrome.Resources SharedResources => new(chrome.Assets, chrome.Font, chrome.SpriteShader);

    internal void Configure(GameResourceRepository repository, PreviewText text, Func<bool> current,
        Action returned, Action accepted, Action chooseItem, Action chooseTarget)
    {
        this.repository = repository; ui = text; this.current = current;
        this.returned = returned; this.accepted = accepted;
        Name = "PersonnelItemPage"; MouseFilter = MouseFilterEnum.Stop; ZIndex = 3;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new UiSourceCommonBlur(); AddChild(backdrop); backdrop.Configure(repository);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x246, RequestBack, RequestApply);
        chrome.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        source = new(repository, 0x49D2E4C6);
        foreach (var node in source.Nodes)
        {
            if (node.Index == 7)
            {
                Rect2 box = source.Rectangle(7, Mount);
                item = new() { Name = "ChoosePersonnelItem", Position = box.Position, Size = box.Size,
                    InputAdmission = Admitted };
                chrome.Canvas.AddChild(item);
                item.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "",
                    () => { if (Admitted() && publication?.CanChooseItem == true) chooseItem(); });
                continue;
            }
            if (node.Index == 14)
            {
                itemImage = new() { Name = "PersonnelItemImage", Position = source.Rectangle(14, Mount).Position,
                    Size = source.Rectangle(14, Mount).Size, MouseFilter = MouseFilterEnum.Ignore };
                chrome.Canvas.AddChild(itemImage); continue;
            }
            if (node.Index == 26)
            {
                holderFace = new() { Name = "PersonnelItemHolderPortrait", Position = source.Rectangle(26, Mount).Position,
                    Size = source.Rectangle(26, Mount).Size, MouseFilter = MouseFilterEnum.Ignore };
                chrome.Canvas.AddChild(holderFace); continue;
            }
            if (node.Index == 47)
            {
                target = new() { Name = "PersonnelItemTargetCard", Position = source.Rectangle(47, Mount).Position };
                chrome.Canvas.AddChild(target);
                target.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text,
                    () => { if (Admitted() && publication?.CanChooseTarget == true) chooseTarget(); }, chrome.Canvas);
                continue;
            }
            if (Replaced(node.Index)) continue;
            if (node.Type is 1 or 3)
                body.Add((node.Index, source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index, Mount),
                    chrome.Assets, chrome.SpriteShader)));
            else if (node.Type == 6)
                body.Add((node.Index, source.Blur(chrome.Canvas, node.Index, source.Rectangle(node.Index, Mount))));
        }
        var presets = MapTextPresets.Create();
        foreach (var (node, caption) in new[] { (4, 0x246), (16, 0x452), (18, 0x411), (23, 0x425),
            (36, 0x21D), (45, 0x542), (112, 0x21D), (122, 0x292) })
            Field(node, Message(caption), presets[13]);
        foreach (int node in new[] { 17, 19, 20, 24, 29, 30, 32, 33, 34, 37, 39, 40, 41,
            113, 115, 116, 117, 124, 126, 127 })
            Field(node, "", presets[node == 29 ? 13 : 0]);
        foreach (int node in new[] { 31, 38, 112, 114 })
            foreach (var entry in body.Where(entry => Within(entry.Node, node))) entry.View.Hide();
        ApplyImage(999);
        target.Apply(new(null, null, null, null, null), false);
        chrome.ApplyDefaultHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x5869));
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("source_mount_node", 0); RefreshAdmission();

        void Field(int node, string value, MapTextPreset preset)
        {
            var label = source.Text(chrome.Canvas, node, Mount, value, chrome.Font, preset);
            label.Name = "PersonnelItemField" + node;
            fields.Add(node, label); body.Add((node, label));
        }
    }

    internal void Apply(PersonnelItemPageValues value)
    {
        if (abandoned) return;
        publication = value;
        item.Caption = value.ItemName;
        foreach (var (node, label) in fields)
            if (value.Text.TryGetValue(node, out var text)) label.SetKnownColourLine(text);
        fields[115].Configure(chrome.Font, MapFontProfile.FromNode(source.Nodes[115], source.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[value.TargetLoyaltyAfter is >= 100 ? 17 : 0]));
        foreach (var entry in body)
        {
            bool visible = true;
            for (int? index = entry.Node; index is int at; index = source.Nodes[at].ParentIndex)
                if (value.Visible.TryGetValue(at, out bool shown) && !shown) { visible = false; break; }
            entry.View.Visible = visible;
        }
        ApplyImage(value.ItemImageIndex ?? 999);
        if (!ReferenceEquals(currentPortrait, value.HolderPortrait))
        {
            Clear(holderFace); currentPortrait = value.HolderPortrait;
            if (currentPortrait is { } portrait)
            {
                var face = new UiSourcePortrait { Name = "ItemHolderFacePane", Size = holderFace.Size };
                holderFace.AddChild(face);
                face.ConfigureLayoutPane(repository, chrome.Assets, chrome.SpriteShader, portrait, source, 26);
            }
        }
        // 85A230(NULL) hides the actual face pane; frame27 remains authored art.
        holderFace.Visible = value.HolderPortrait is not null;
        target.Apply(value.Target, !busy && value.CanChooseTarget);
        chrome.ApplyDefaultHint(value.Guide); RefreshAdmission();
        if (value.Selection is { } selected) SetMeta("item_id", selected.ItemId);
        else RemoveMeta("item_id");
        if (value.TargetOfficerId is int person) SetMeta("target_officer_id", person);
        else RemoveMeta("target_officer_id");
    }

    private void ApplyImage(int index)
    {
        if (currentImageIndex == index) return;
        Clear(itemImage); externalItemTexture?.Dispose(); externalItemTexture = null;
        currentImageIndex = index;
        if (itemImage.HasMeta("unresolved_owner")) itemImage.RemoveMeta("unresolved_owner");
        if (index == 0) return; // The authored item image has a known empty slot.
        Rect2 box = new(Vector2.Zero, itemImage.Size);
        if (index is >= 201 and <= 401)
        {
            var custom = ItemEditorExternalImages.ReadImage(index);
            if (custom.Texture is { } pixels)
            {
                using var image = Image.CreateFromData(pixels.Width, pixels.Height, false, Image.Format.Rgba8, pixels.Pixels);
                externalItemTexture = ImageTexture.CreateFromImage(image);
                itemImage.AddChild(new TextureRect { Name = "ItemArtwork", Size = box.Size, Texture = externalItemTexture,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                    MouseFilter = MouseFilterEnum.Ignore });
                return;
            }
            itemImage.SetMeta("unresolved_owner", custom.Error ?? "information.Item.external_image_slot");
            index = 999;
        }
        var route = CollectionImageRoute.ForItem(index);
        source.Art(itemImage, 14, box, chrome.Assets, chrome.SpriteShader,
            commonTexturePack: route.TexturePack, commonSpriteOverride: route.SpriteName);
    }

    private static void Clear(Control parent)
    {
        foreach (Node child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); }
    }
    private bool Within(int node, int root)
    {
        for (int? index = node; index is int at; index = source.Nodes[at].ParentIndex)
            if (at == root) return true;
        return false;
    }
    private bool Replaced(int node) => Within(node, 7) || Within(node, 14) || Within(node, 26) || Within(node, 47);
    private string Message(int id) => repository.LoadMessages(ui.SourceLocale, San14.Data.Formats.MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException($"Missing Item command caption {id:X}.");
    private bool Admitted() => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (current?.Invoke() ?? false);
    private void RefreshAdmission()
    {
        item.Disabled = busy || publication?.CanChooseItem != true;
        chrome.ApplyReturn(!busy); chrome.ApplyDecision(!busy && publication?.CanSubmit == true, true);
    }
    internal void SetBusy(bool value)
    { busy = value; if (publication is { } page) target.Apply(page.Target, !busy && page.CanChooseTarget); RefreshAdmission(); }
    internal void ApplyHint(string value) => chrome.ApplyDefaultHint(value);
    internal void RequestBack() { if (Admitted()) returned?.Invoke(); }
    private void RequestApply() { if (Admitted() && publication?.CanSubmit == true) accepted?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; current = null; returned = null; accepted = null; publication = null;
        item?.Abandon(); target?.Abandon(); chrome?.Abandon(); Hide();
    }
    public override void _ExitTree() { externalItemTexture?.Dispose(); externalItemTexture = null; }
}
