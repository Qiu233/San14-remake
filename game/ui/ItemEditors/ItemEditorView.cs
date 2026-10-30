using Godot;
using San14.Core;
using San14.Data;

internal sealed record ItemEditorPageValues(string Name, string Description, ItemEditorValues Values,
    bool CanConfirm, string Notice);

/// <summary>The original item page and its mounted worth body. The caller owns
/// the working record; this page emits edits without publishing an archive.</summary>
internal partial class ItemEditorView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly Dictionary<int, UiSourceText> fields = [];
    private readonly List<UiSourceCommandButton> buttons = [];
    private Func<ItemEditorPageValues>? read;
    private Func<bool>? admitted;
    private Action? returned, accepted;
    private Action<int>? edit;
    private Action<int>? worthChanged;
    private GameResourceRepository repository = null!;
    private ItemEditorCatalog catalog = null!;
    private Control imageHost = null!;
    private UiSourceLayout root = null!;
    private UiSourceSlider slider = null!;
    private Control description = null!;
    private MapFontProfile descriptionProfile = null!;
    private string? descriptionText;
    private IReadOnlyList<ItemEditorExternalImage> external = [];
    private UiSourceBottomButton restore = null!;
    private int image = -1;
    private bool abandoned, busy;
    internal void Configure(GameResourceRepository repository, PreviewText text, ItemEditorCatalog catalog,
        bool created, bool creating, IReadOnlyList<ItemEditorExternalImage> external, Func<ItemEditorPageValues> read, Func<bool> admitted,
        Action returned, Action accepted, Action<int> edit, Action<int> worthChanged)
    {
        this.repository = repository; this.catalog = catalog; this.external = external; this.read = read; this.admitted = admitted;
        this.returned = returned; this.accepted = accepted; this.edit = edit; this.worthChanged = worthChanged;
        Name = created ? "NewItemEditor" : "HistoricalItemEditor"; ZIndex = 6; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, created ? creating ? 0x7D1 : 0x7CD : 0x72C,
            RequestBack, () => { if (CanEdit() && this.read?.Invoke().CanConfirm == true) this.accepted?.Invoke(); });
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        restore = new UiSourceBottomButton { Name = "RestoreItemEditorOriginal",
            Position = footer.Rectangle(17,new(0,904)).Position, InputAdmission = CanEdit };
        chrome.Canvas.AddChild(restore);
        restore.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,catalog.Data(0x97),
            () => { if(CanEdit())this.edit?.Invoke(7); });
        root = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_11_05_edit_item"));
        foreach (var node in root.Nodes.Where(node => node.Type is 1 or 3))
            if (node.Index != 8) root.Art(chrome.Canvas, node.Index, root.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        imageHost = new Control { Name = "ItemEditorArtwork", MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(imageHost);
        var body = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_11_05_edit_offset_03_cn"));
        Vector2 mount = root.Rectangle(6).Position;
        var pane = new Control { Name = "ItemEditorWorthBody", Position = mount, Size = body.Rectangle(0).Size,
            MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(pane);
        int[] hidden = [4, 5, 7, 14, 15, 16, 20, 21, 26, 27, 45, 57];
        foreach (var node in body.Nodes.Where(node => node.Type is 1 or 3))
            // 5EE010 mounts CEditSlider at node25. Its authored placeholder
            // and descendants are replaced by UiSourceSlider, which resizes
            // the real common slider before drawing its nine-part pieces.
            if (!hidden.Contains(node.Index) && !InSlider(node.Index))
                body.Art(pane, node.Index, body.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        foreach (int node in new[] { 3, 10, 13, 19, 22 }) fields[node] = body.Text(pane, node, default, "", chrome.Font);
        foreach (var (node, caption) in new[] { (9,0x401),(12,0x452),(18,0x411),(24,0x423),(55,0x400) })
            AddButton(body.Rectangle(node), node == 9 ? 2 : node == 12 ? 3 : node == 18 ? 4 : node == 24 ? 5 : 6, catalog.Data(caption), pane);
        if (created) AddButton(body.Rectangle(2), 0, catalog.Data(0x5A8), pane);
        // Historical names and descriptions are read-only. Its description
        // caption is authored text, and its name-edit control is hidden.
        if (!created)
        {
            Label(2, catalog.Data(0x5A8)); Label(55, catalog.Data(0x400));
        }
        var imageHit = new Button { Name = "ItemEditorImageHit", Position = root.Rectangle(8).Position,
            Size = root.Rectangle(8).Size, Flat = true, Text = "" };
        chrome.Canvas.AddChild(imageHit);
        imageHit.Pressed += () => { if (CanEdit()) this.edit?.Invoke(1); };
        var sliderBox = body.Rectangle(25);
        slider = new UiSourceSlider { Name = "ItemWorthSlider", Position = sliderBox.Position,
            Size = sliderBox.Size };
        pane.AddChild(slider);
        slider.Configure(repository, chrome.Assets, chrome.SpriteShader, 0, 50, 1, read().Values.Worth, true,
            value => { if (CanEdit()) { this.worthChanged?.Invoke(value); Refresh(); } });
        var paragraphBox = body.Rectangle(56);
        // 5EE010 widens the authored literal description by one twentieth
        // and shifts it left by half that amount before assigning its text.
        int extra=(int)paragraphBox.Size.X/20;
        paragraphBox.Position-=new Vector2(extra/2,0);paragraphBox.Size+=new Vector2(extra,0);
        description = new Control { Name = "ItemEditorDescription", Position = paragraphBox.Position,
            Size = paragraphBox.Size,MouseFilter=MouseFilterEnum.Ignore };
        pane.AddChild(description);
        // This is a literal item-description control (20E560 ->30BEB0),
        // rather than the expanded ordinary speech/message paragraph.
        // Keep its original character mode; the shared message guard stays strict.
        descriptionProfile=MapFontProfile.FromNode(body.Nodes[56],body.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[0]) with {RawAlign=0,RawLineArrange=1,WidthFitMode=0};
        SetMeta("source_layout", root.Source.Source.ResourceUri); Refresh();

        bool InSlider(int index)
        {
            for(int? current=index;current is int node && node>0;current=body.Nodes[node].ParentIndex)
                if(node==25)return true;
            return false;
        }

        void AddButton(Rect2 box, int section, string caption, Control parent)
        {
            if (!created && section is 0 or 6) return;
            var button = new UiSourceCommandButton { Name = "ItemEditorSection" + section,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            parent.AddChild(button); buttons.Add(button); button.SetMeta("item_section", section);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption,
                () => { if (CanEdit()) this.edit?.Invoke(section); });
        }
        void Label(int mountNode, string caption)
        {
            var label = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_05_00_offset_01"));
            var mountPosition = body.Rectangle(mountNode).Position;
            foreach (var node in label.Nodes.Where(node => node.Type is 1 or 3))
                label.Art(pane, node.Index, label.Rectangle(node.Index, mountPosition), chrome.Assets, chrome.SpriteShader);
            label.Text(pane, 2, mountPosition, caption, chrome.Font, MapTextPresets.Create()[13]);
        }
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void Refresh()
    {
        if (abandoned || read is null) return;
        var value = read(); fields[3].Text = value.Name; fields[10].Text = catalog.Kind(value.Values);
        fields[13].Text = catalog.Ability(value.Values); fields[19].Text = catalog.Effect(value.Values);
        fields[22].Text = value.Values.EffectKind == 2 && value.Values.EffectValue > 0 ? value.Values.EffectValue.ToString() : "";
        ApplyDescription(value.Description); slider.SetValue(value.Values.Worth, 50, !busy);
        if (image != value.Values.ImageIndex)
        {
            foreach (var child in imageHost.GetChildren()) { imageHost.RemoveChild(child); child.QueueFree(); }
            image = value.Values.ImageIndex;
            if(image>=201 && external.FirstOrDefault(entry=>entry.ImageIndex==image) is {Texture:{ } rgba})
            {
                using var bitmap=Image.CreateFromData(rgba.Width,rgba.Height,false,Image.Format.Rgba8,rgba.Pixels);
                var box=root.Rectangle(8);
                imageHost.AddChild(new TextureRect {Position=box.Position,Size=box.Size,
                    Texture=ImageTexture.CreateFromImage(bitmap),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore});
            }
            else
            {
                var route=CollectionImageRoute.ForItem(image>=201?999:image);
                root.Art(imageHost, 8, root.Rectangle(8), chrome.Assets, chrome.SpriteShader,
                    commonTexturePack: route.TexturePack, commonSpriteOverride: route.SpriteName);
            }
        }
        chrome.ApplyDecision(!busy && value.CanConfirm, true); chrome.ApplyDefaultHint(value.Notice);
    }
    private void ApplyDescription(string value)
    {
        if(descriptionText==value)return;
        foreach(var child in description.GetChildren()){description.RemoveChild(child);child.QueueFree();}
        float advance=chrome.Font.Source.Record('\u3042',descriptionProfile.Face).Height*descriptionProfile.ScaleY+
            descriptionProfile.VerticalSpace;
        int allowedLf=Math.Max(0,(int)(description.Size.Y/advance)-1);
        string wrapped=DeploymentNarrationPlainParagraph.WrapKnownColourParagraph(value,
            line=>(int)chrome.Font.Measure(line,descriptionProfile).Width,(int)description.Size.X,Math.Min(64,allowedLf));
        var lines=DeploymentNarrationParagraphPlan.Runs(wrapped);
        for(int index=0;index<lines.Count;index++)
        {
            var line=new UiSourceText {Name="ItemDescriptionLine"+index,Position=new(0,index*advance),
                Size=new(description.Size.X,advance)};
            description.AddChild(line);line.Configure(chrome.Font,descriptionProfile);
            line.SetKnownColourLine(lines[index].VisibleText);
        }
        descriptionText=value;
    }
    internal void SetBusy(bool value)
    { if (abandoned) return; busy = value; restore.Disabled=value; foreach (var button in buttons) button.Disabled = value; chrome.ApplyReturn(!value); Refresh(); }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    { abandoned = true; read = null; admitted = null; returned = null; accepted = null; edit = null; worthChanged = null;
      foreach (var button in buttons) button.Abandon();
      if (restore is not null) { restore.Disabled = true; restore.InputAdmission = null; }
      chrome?.Abandon(); Hide(); }
}
