using Godot;
using San14.Core;
using San14.Data;

internal sealed record CurrentWorldItemPageValues(string Name, ItemEditorValues Values,
    string HolderName, bool HasHolder, bool CanEditCustody, bool TransferProhibited,
    bool CanConfirm, string Notice, OfficerPortraitRoute? HolderPortrait = null,
    DetailForceAppearance? ForceFlag = null);

/// <summary>The original live-world Item page. Its parent owns the detached
/// values, holder selection, child popups and final game order.</summary>
internal partial class CurrentWorldItemEditView : Control
{
    private AdministrationPageChrome chrome = null!;
    private GameResourceRepository repository = null!;
    private ItemEditorCatalog catalog = null!;
    private Func<CurrentWorldItemPageValues>? read;
    private Func<bool>? admitted;
    private Action? returned, accepted;
    private Action<int>? edit, worthChanged;
    private Action<bool>? flagChanged;
    private readonly List<UiSourceCommandButton> commands = [];
    private UiSourceCommandButton holder = null!, clear = null!, kind = null!, ability = null!, effect = null!;
    private UiSourceRadioButton flagOff = null!, flagOn = null!;
    private UiSourceText name = null!, effectAmount = null!;
    private UiSourceEditSlider worth = null!;
    private UiSourceLayout source = null!;
    private Control holderHost = null!;
    private OfficerPortraitRoute? shownPortrait;
    private DetailForceAppearance? shownFlag;
    private bool holderShown, abandoned, busy;
    private int worthMaximum;
    private static readonly Vector2 Mount = new(0, 170);

    internal void Configure(GameResourceRepository repository, PreviewText text, ItemEditorCatalog catalog,
        int worthMaximum, IReadOnlyList<string> functionIcons, Func<CurrentWorldItemPageValues> read,
        Func<bool> admitted, Action returned, Action accepted, Action<int> edit,
        Action<int> worthChanged, Action<bool> flagChanged)
    {
        this.repository = repository; this.catalog = catalog; this.worthMaximum = worthMaximum;
        this.read = read; this.admitted = admitted; this.returned = returned; this.accepted = accepted;
        this.edit = edit; this.worthChanged = worthChanged; this.flagChanged = flagChanged;
        Name = "CurrentWorldItemEditor"; ZIndex = 6; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x736, RequestBack, () =>
            { if (CanEdit() && this.read?.Invoke().CanConfirm == true) this.accepted?.Invoke(); });
        var authored = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "11_02_04_edit_item"));
        float shift = authored.Rectangle(39).Position.Y - authored.Rectangle(45).Position.Y;
        var geometry = new Dictionary<int, UiSourceNodeGeometry>();
        foreach (int node in new[] { 43, 49, 55 })
        {
            var values = authored.Nodes[node].HeaderValues;
            geometry[node] = new(Offset: new(values[0], -values[1] + shift));
        }
        var extra = authored.Nodes[61].HeaderValues;
        geometry[61] = new(Offset: new(extra[0] + authored.Rectangle(55).Position.X - authored.Rectangle(61).Position.X, -extra[1]));
        source = new(authored.Source, geometry);
        int[] replaced = [7, 14, 23, 26, 32, 35, 39, 41, 47, 53, 59, 69, 97, 99, 101];
        foreach (var node in source.Nodes)
        {
            bool hidden = false;
            for (int? cursor = node.Index; cursor is int at; cursor = source.Nodes[at].ParentIndex)
                if (replaced.Contains(at)) { hidden = true; break; }
            if (hidden) continue;
            if (node.Type is 1 or 3) source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index, Mount), chrome.Assets, chrome.SpriteShader);
            else if (node.Type == 6) source.Blur(chrome.Canvas, node.Index, source.Rectangle(node.Index, Mount));
        }
        name = source.Text(chrome.Canvas, 17, Mount, "", chrome.Font, MapTextPresets.Create()[3]);
        foreach (var (node, message) in new[] { (21,0x425),(30,0x730),(45,0x401),(51,0x452),(57,0x411),(64,0x792),(68,0x423) })
            source.Text(chrome.Canvas, node, Mount, catalog.Data(message), chrome.Font, MapTextPresets.Create()[13]);
        effectAmount = source.Text(chrome.Canvas, 65, Mount, "", chrome.Font);
        holder = Command(23, "CurrentItemHolder", "", 0);
        clear = Command(26, "CurrentItemClearHolder", catalog.Data(0x5A4), 1);
        kind = Command(47, "CurrentItemKind", "", 2);
        ability = Command(53, "CurrentItemAbility", "", 3);
        effect = Command(59, "CurrentItemEffect", "", 4);
        flagOff = Radio(32, false, 0x27F, UiSourceRadioSegment.Left);
        flagOn = Radio(35, true, 0x280, UiSourceRadioSegment.Right);
        worth = new() { Name = "CurrentItemWorth", Position = source.Rectangle(69, Mount).Position };
        chrome.Canvas.AddChild(worth);
        worth.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "", 0, worthMaximum, 1,
            read().Values.Worth, true, CanEdit, value => { if (CanEdit()) this.worthChanged?.Invoke(value); },
            () => { if (CanEdit()) this.edit?.Invoke(5); }, functionIcons);
        holderHost = new() { Name = "CurrentItemHolderPreview", MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(holderHost);
        DrawImage(read().Values.ImageIndex);
        SetMeta("source_layout", source.Source.Source.ResourceUri); Refresh();

        UiSourceCommandButton Command(int node, string buttonName, string caption, int section)
        {
            var box = source.Rectangle(node, Mount);
            var button = new UiSourceCommandButton { Name = buttonName, Position = box.Position,
                Size = box.Size, InputAdmission = CanEdit };
            chrome.Canvas.AddChild(button); commands.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption,
                () => { if (CanEdit() && !button.Disabled) this.edit?.Invoke(section); });
            return button;
        }
        UiSourceRadioButton Radio(int node, bool flag, int caption, UiSourceRadioSegment segment)
        {
            var box = source.Rectangle(node, Mount);
            var button = new UiSourceRadioButton { Name = flag ? "CurrentItemTransferProhibited" : "CurrentItemTransferAllowed",
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            chrome.Canvas.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, segment, catalog.Data(caption),
                () => { if (CanEdit() && !button.Disabled) this.flagChanged?.Invoke(flag); });
            return button;
        }
    }

    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void Refresh()
    {
        if (abandoned || read is null) return;
        var page = read(); name.Text = page.Name;
        holder.Caption = page.HolderName; holder.Disabled = clear.Disabled = busy || !page.CanEditCustody;
        kind.Caption = catalog.Kind(page.Values); ability.Caption = catalog.Ability(page.Values); effect.Caption = catalog.Effect(page.Values);
        kind.Disabled = ability.Disabled = effect.Disabled = busy;
        effectAmount.Text = page.Values.EffectKind == 2 ? page.Values.EffectValue.ToString() : "";
        flagOff.Selected = !page.TransferProhibited; flagOn.Selected = page.TransferProhibited;
        flagOff.Disabled = flagOn.Disabled = busy || !page.CanEditCustody;
        worth.SetValue(page.Values.Worth, worthMaximum, !busy);
        chrome.ApplyReturn(!busy); chrome.ApplyDecision(!busy && page.CanConfirm, true); chrome.ApplyDefaultHint(page.Notice);
        if (!holderShown || shownPortrait != page.HolderPortrait || shownFlag != page.ForceFlag)
        {
            foreach (var child in holderHost.GetChildren()) { holderHost.RemoveChild(child); child.QueueFree(); }
            shownPortrait = page.HolderPortrait; shownFlag = page.ForceFlag; holderShown = true;
            if (page.HolderPortrait is { Kind: OfficerPortraitRouteKind.StockNamed, ResourceName: { } pack, SpriteName: { } sprite })
            {
                var box = source.Rectangle(97, Mount);
                var portrait = new UiSourcePortrait { Name = "CurrentItemHolderPortrait", Position = box.Position, Size = box.Size };
                holderHost.AddChild(portrait);
                portrait.ConfigureFace2(repository, chrome.Assets, chrome.SpriteShader,
                    new LocalizedUiSpriteSource(repository).LoadTexturePackSprite(pack, sprite));
            }
            var frameBox = source.Rectangle(99, Mount);
            var frame = new UiSourceFaceFrame { Name = "CurrentItemHolderFrame", Position = frameBox.Position, Size = frameBox.Size };
            holderHost.AddChild(frame); frame.Configure(repository, chrome.Assets, chrome.SpriteShader);
            if (page.ForceFlag is { } appearance)
            {
                var flag = new UiSourceForceFlag { Name = "CurrentItemHolderForce", Position = source.Rectangle(101, Mount).Position };
                holderHost.AddChild(flag); flag.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, appearance);
            }
        }
    }
    private void DrawImage(int image)
    {
        if (image == 0) return;
        var box = source.Rectangle(7, Mount);
        if (image >= 201 && ItemEditorExternalImages.ReadImage(image)?.Texture is { } pixels)
        {
            using var bitmap = Image.CreateFromData(pixels.Width, pixels.Height, false, Image.Format.Rgba8, pixels.Pixels);
            chrome.Canvas.AddChild(new TextureRect { Name = "CurrentItemImage", Position = box.Position, Size = box.Size,
                Texture = ImageTexture.CreateFromImage(bitmap), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore });
        }
        else
        {
            var route = CollectionImageRoute.ForItem(image >= 201 ? 999 : image);
            source.Art(chrome.Canvas, 7, box, chrome.Assets, chrome.SpriteShader,
                commonTexturePack: route.TexturePack, commonSpriteOverride: route.SpriteName);
        }
    }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; Refresh(); } }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; read = null; admitted = null; returned = null; accepted = null; edit = null; worthChanged = null; flagChanged = null;
        foreach (var command in commands) command.Abandon();
        if (worth is not null) worth.Abandon();
        chrome?.Abandon(); Hide();
    }
}
