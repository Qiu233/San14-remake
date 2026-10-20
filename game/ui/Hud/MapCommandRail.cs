using Godot;
using San14.Data;

/// <summary>Original left command rail. Folding belongs to the view; commands
/// are supplied by the existing gameplay editors.</summary>
internal partial class MapCommandRail : Control
{
    private Shader? _shader;
    private UiAssets? _assets;
    private LocalizedUiSpriteSource _declaredSprites = null!;
    private Control _body = null!;
    private MapCommandRailButton _fold = null!;
    private Control _menu = null!;
    private MapFont? _font;
    private MapCommandRailSources _source = null!;
    private IReadOnlyList<MapMenuEntry>[] _menus = [];
    private IReadOnlyDictionary<MapMenuCommand, Action> _commands = null!;
    internal MapCommandCategory? OpenCategory { get; private set; }
    internal bool Expanded { get; private set; } = true;

    internal void Configure(GameResourceRepository repository, string locale,
        IReadOnlyDictionary<MapMenuCommand, Action> commands)
    {
        Name = "MapCommandRail";
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new(270, 961);
        _assets = new(repository);
        _shader = UiSourceSprite.CreateShader();
        var source = new MapCommandRailSources(repository);
        _source = source;
        _menus = MapCommandRailSources.ReadMenus(repository, locale);
        _commands = commands;
        _font = new MapFont(repository, locale);
        _body = new Control { Name = "RailBody", Size = Size, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_body);
        Picture("MainBacking", source.Main, 3, new(-46, 161, 312, 800), UiSourceSprite.Blend.Ordinary);
        Picture("InnerBacking", source.Main, 5, new(-133, 161, 256, 800), UiSourceSprite.Blend.Ordinary);
        Texture2D clip = _assets.SrgbSprite(MapCommandRailSources.Sprite(source.Main, 19));

        // Stable main_7btn_in endpoints, in content coordinates. Hit widths are
        // caller overrides; the 160-pixel artwork is not compressed to fit them.
        Vector2[] positions = [new(60,319), new(84,398), new(96,478), new(98,558),
            new(88,638), new(68,718), new(35,798)];
        int[] widths = [142,141,140,140,141,142,142];
        string[] captions = ["01shussei", "02seifu", "03jinzai", "04keiryaku", "05gaikou", "07gunjo", "06kessai"];
        _declaredSprites = new LocalizedUiSpriteSource(repository);
        Texture2D[] first = Images(source.Button, [3,6,10,13,16]);
        Texture2D[] second = Images(source.Button, [4,7,11,14,17]);
        for (int i = 0; i < positions.Length; i++)
        {
            var category = (MapCommandCategory)i;
            var button = new MapCommandRailButton { Name = category.ToString(), Position = positions[i] };
            LoadedSprite caption = _declaredSprites.LoadCommandCaption(locale, "000_cmn_btn_main_txt_" + captions[i]);
            button.Configure(_shader, first, second,
                _assets.SrgbSprite(caption.Definition.Name, caption),
                new(160,64), new(widths[i],64), new(0,0,160,64),
                clip, new(new Vector2(-46,161) - positions[i], new Vector2(312,800)));
            button.Pressed += () => ShowCategory(category);
            _body.AddChild(button);
        }
        _fold = new MapCommandRailButton { Name = "FoldCommands", Position = new(234,503) };
        _fold.Configure(_shader, Images(source.Fold, [3,5,8,10,12]), null,
            _assets.SrgbSprite("000_cmn_com_icon_close"), new(36,116), new(36,116), new(10,48,20,20));
        _fold.Pressed += () => SetExpanded(!Expanded);
        _body.AddChild(_fold);
        _menu = new Control { Name = "CommandSubmenu", Size = new(410,1080),
            MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        AddChild(_menu);
        VisibilityChanged += () => { if (!IsVisibleInTree()) CloseMenu(); };

        Texture2D[] Images(MapMarkerLayout layout, int[] nodes) =>
            nodes.Select(index => (Texture2D)_assets.SrgbSprite(MapCommandRailSources.Sprite(layout, index))).ToArray();
        void Picture(string name, MapMarkerLayout layout, int index, Rect2 rectangle, UiSourceSprite.Blend blend)
        {
            var picture = new UiSourceSprite { Name = name, Position = rectangle.Position, Size = rectangle.Size };
            picture.Configure(_shader, _assets.SrgbSprite(MapCommandRailSources.Sprite(layout, index)), blend);
            _body.AddChild(picture);
        }
    }

    internal void SetExpanded(bool expanded)
    {
        CloseMenu();
        Expanded = expanded;
        _body.Position = new(expanded ? 0 : -238, 0);
        _fold.SetCaption(_assets!.SrgbSprite(expanded ? "000_cmn_com_icon_close" : "000_cmn_com_icon_open"));
    }

    internal void CloseMenu()
    {
        OpenCategory = null;
        if (_menu is not null) _menu.Hide();
        if (_fold is not null) _fold.Show();
        if (_body is not null)
            foreach (var button in _body.GetChildren().OfType<MapCommandRailButton>()) button.Selected = false;
    }

    internal void ShowCategory(MapCommandCategory category)
    {
        if (OpenCategory == category) { CloseMenu(); return; }
        OpenCategory = category;
        foreach (var button in _body.GetChildren().OfType<MapCommandRailButton>())
            button.Selected = button.Name.ToString() == category.ToString();
        foreach (Node child in _menu.GetChildren()) { _menu.RemoveChild(child); child.QueueFree(); }
        _fold.Hide();
        int kind = (int)category;
        var background = _source.Submenu.Nodes[14 + kind];
        var panel = new UiSourceSprite { Name = "SubmenuBacking",
            Position = new(background.HeaderValues[0], -background.HeaderValues[1]),
            Size = new(background.Value44, background.Value46) };
        panel.Configure(_shader!, LocalSprite(14 + kind),
            UiSourceSprite.Blend.Ordinary);
        _menu.AddChild(panel);
        int[] starts = [0,2,4,6,8,10,11];
        int[] slots = [29,34,40,45,50,55,60,65,70,75,80,85,90,95,100];
        int[] hitWidths = [168,163,159,156,153,151,153,156,159,163,168,173,179,187,187];
        var maskNode = _source.Submenu.Nodes[27];
        Vector2 maskCenter = new(maskNode.HeaderValues[0], -maskNode.HeaderValues[1]);
        Rect2 maskRectangle = new(maskCenter - new Vector2(146,300), new Vector2(292,600));
        // sub_in's stable endpoint moves node28 from local X=-79 to +94.
        Rect2 secondMaskRectangle = new(maskCenter + new Vector2(94,0) - new Vector2(224,300), new Vector2(448,600));
        Texture2D mask = LocalSprite(27);
        Texture2D secondMask = LocalSprite(28);
        Texture2D[] first = Images([3,6,10,13,16]), second = Images([4,7,11,14,17]);
        MapFontProfile profile = MapFontProfile.FromNode(_source.SubmenuButton.Nodes[20], _source.SubmenuButton.Source.ResourceUri);
        int row = starts[kind];
        foreach (MapMenuEntry entry in _menus[kind])
        {
            var node = _source.Submenu.Nodes[slots[row]];
            Vector2 position = maskCenter + new Vector2(node.HeaderValues[0], -node.HeaderValues[1]);
            var button = new MapCommandRailButton { Name = entry.Command.ToString(), Position = position };
            button.Configure(_shader!,first,second,null,new(188,36),new(hitWidths[row],36),default,
                mask,new(maskRectangle.Position-position,maskRectangle.Size),
                secondMask,new(secondMaskRectangle.Position-position,secondMaskRectangle.Size));
            button.ConfigureText(_font!,profile,entry.Caption);
            button.Disabled = !_commands.TryGetValue(entry.Command, out Action? action);
            if (action is not null) button.Pressed += () => { CloseMenu(); action(); };
            _menu.AddChild(button);
            row++;
        }
        _menu.Show();

        Texture2D[] Images(int[] nodes) => nodes.Select(index => (Texture2D)_assets!.SrgbSprite(
            MapCommandRailSources.Sprite(_source.SubmenuButton,index))).ToArray();
    }

    private Texture2D LocalSprite(int index)
    {
        string name = MapCommandRailSources.Sprite(_source.Submenu, index);
        return _assets!.SrgbSprite(name, _declaredSprites.LoadLayoutLocalSprite(_source.Submenu, name));
    }

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || OpenCategory is null) return;
        bool cancel = input is InputEventKey { Pressed: true, Keycode: Key.Escape } ||
            input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right };
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse)
            cancel = !_menu.GetChildren().OfType<MapCommandRailButton>().Any(Contains) &&
                !_body.GetChildren().OfType<MapCommandRailButton>().Any(Contains);
        bool Contains(MapCommandRailButton button) => button.IsVisibleInTree() &&
            button._HasPoint(button.GetGlobalTransformWithCanvas().AffineInverse() * ((InputEventMouseButton)input).Position);
        if (cancel) { CloseMenu(); GetViewport().SetInputAsHandled(); }
    }

    public override void _ExitTree()
    {
        _font?.Dispose();
        _font = null;
        _shader?.Dispose();
        _shader = null;
        _assets?.Dispose();
        _assets = null;
    }
}
