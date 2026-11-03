using Godot;
using San14.Data;

/// <summary>The actual main-command resource hierarchy and command-entry owner.
/// Stable animation endpoints are prepared once with its normal HUD resources.</summary>
internal partial class MapCommandRail : Control
{
    private Shader? _shader;
    private Shader? _cursorShader, _focusShader, _submenuFocusShader;
    private UiAssets? _assets;
    private MapFont? _font;
    private IReadOnlyDictionary<MapCommandCategory, LoadedSprite> _captions = null!;
    private MainCommandLayoutSources _layouts = null!;
    private MapCommandRailSources _source = null!;
    private MainCommandLayoutState _outer = null!;
    private MainCommandLayoutState _cursor = null!;
    private MainCommandLayoutState? _main, _submenuInput;
    private int _submenuInputBackground;
    private Control _mainRoot = null!, _submenuRoot = null!, _linkRoot = null!;
    private MapCommandRailButton? _fold;
    private readonly Dictionary<MapCommandCategory, MapCommandRailButton> _buttons = [];
    private readonly Dictionary<MapCommandCategory, MapCommandRailCursor> _cursors = [];
    private readonly List<MapCommandRailButton> _submenuButtons = [];
    private IReadOnlyList<MapMenuEntry>[] _menus = [];
    private IReadOnlyDictionary<MapMenuCommand, Action> _commands = null!;
    private Color _submenuFocusTint, _submenuDisabledFocusTint;
    private string _locale = null!;
    private int? _style;
    private long _generation;
    private long _menuGeneration;
    private bool _retired;
    private readonly Dictionary<MapCommandCategory, Control> _attention = [];
    private readonly Dictionary<MapCommandCategory, Control> _badges = [];
    private MapMainCommandPresentation? _presentation;
    internal bool AutomaticallyHidden { get; private set; }
    internal event Action? PresentationChanged;
    internal MapCommandCategory? OpenCategory { get; private set; }
    internal bool Expanded { get; private set; } = true;
    internal bool SourceControlReady => !_retired && !AutomaticallyHidden && _style is not null && _buttons.Count > 0 &&
        IsInsideTree() && !IsQueuedForDeletion() && IsVisibleInTree();

    internal void Configure(GameResourceRepository repository, string locale, MapHudResources resources,
        IReadOnlyDictionary<MapMenuCommand, Action> commands, MapMainCommandStyle? style = null, MapMainCommandPresentation? presentation = null, string unknownText = "")
    {
        Name = "MapCommandRail";
        MouseFilter = MouseFilterEnum.Stop;
        _assets = resources.Shared.Assets; _shader = resources.Shared.SpriteShader;
        _font = resources.Shared.Font;
        _cursorShader = resources.CursorShader; _focusShader = resources.FocusShader;
        _submenuFocusShader = resources.SubmenuFocusShader;
        _source = resources.Sources.Rail; _layouts = resources.Sources.Layouts;
        _captions = resources.Sources.Captions;
        _submenuFocusTint = resources.Sources.SubmenuFocusTint;
        _submenuDisabledFocusTint = resources.Sources.SubmenuDisabledFocusTint;
        _menus = resources.Sources.Menus;
        _commands = commands; _locale = locale; _presentation = presentation;
        ConfigureEntryHints(repository, unknownText);
        _outer = new(_layouts.Outer, _layouts.Outer.Stable("cmd_slide_in"));
        _cursor = new(_layouts.Cursor, _layouts.Cursor.Stable("1_focus"));
        Size = _outer.Layout.Rectangle(0).Size;
        //4520A0 mounts the owner on actual CGameState template node1. The
        //normal layout mount replaces the child's absolute designer root.
        var game = new UiSourceLayout(repository, 0x036BB544);
        Position = game.Rectangle(1).Position;
        _mainRoot = Layer("MainCommandBase", MainCommandLayoutSources.OuterMainMount);
        _submenuRoot = Layer("SubCommands", MainCommandLayoutSources.OuterSubmenuMount);
        _linkRoot = Layer("CommandLink", MainCommandLayoutSources.OuterLinkMount);
        _submenuRoot.Hide(); _linkRoot.Hide();
        SetMeta("source_outer_layout", _layouts.Outer.Source.Source.ResourceUri);
        SetMeta("source_mount_layout", game.Source.Source.ResourceUri);
        SetMeta("source_mount_node", 1);
        SetMeta("source_category_count", 0);
        SetMeta("source_main_clip", "");
        VisibilityChanged += VisibilityChangedHandler;
        Refresh(style ?? new(null, "map.main_command.style_not_prepared"));

        Control Layer(string name, int mount)
        {
            var rectangle = _outer.Layout.Rectangle(mount);
            var layer = new Control { Name = name, Position = rectangle.Position,
                Size = rectangle.Size, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(layer);
            layer.SetMeta("source_mount_node", mount);
            layer.SetMeta("source_mount_layout", _layouts.Outer.Source.Source.ResourceUri);
            return layer;
        }
    }

    internal void Refresh(MapMainCommandStyle projection)
    {
        SetMeta("source_main_style_issue", projection.UnresolvedInput ?? "");
        SetMeta("source_main_style", projection.Style ?? -1);
        if (_retired || _style == projection.Style) return;
        if (projection.Style is not (null or 0 or 2 or 3))
            throw new InvalidDataException("map.main_command.style_value");
        CloseMenu();
        _style = projection.Style;
        RebuildMain();
    }

    private void RebuildMain()
    {
        try { BuildMain(); }
        catch
        {
            _style = null; _buttons.Clear(); _cursors.Clear(); _fold = null; _main = null;
            Clear(_mainRoot); SetMeta("source_category_count", 0);
            throw;
        }
    }

    private void BuildMain()
    {
        long generation = ++_generation;
        _buttons.Clear(); _cursors.Clear(); _attention.Clear(); _badges.Clear(); _fold = null;
        _categoryHintButtons.Clear();
        Clear(_mainRoot);
        if (_style is not int style)
        { _main = null; SetMeta("source_category_count", 0); SetMeta("source_main_clip", ""); return; }
        _main = new(_layouts.Main, _layouts.MainStable(style, Expanded));
        _main.Art(_mainRoot, 3, _assets!, _shader!);
        _main.Art(_mainRoot, 5, _assets!, _shader!);
        var mask = _main.Layout.Rectangle(19);
        var maskTexture = Texture(_layouts.Main, 19);
        Texture2D[] first = Images(_source.Button, [3, 6, 10, 13, 16]);
        Texture2D[] second = Images(_source.Button, [4, 7, 11, 14, 17]);
        foreach (var mount in _layouts.ReadButtonMounts(style))
        {
            var category = mount.Category;
            var rectangle = _main.Layout.Rectangle(mount.ButtonNode);
            var button = new MapCommandRailButton { Name = category.ToString(), Position = rectangle.Position };
            var caption = _captions[category];
            button.Configure(_shader!, first, second, _assets!.SrgbSprite(caption.Definition.Name, caption),
                rectangle.Size, new(mount.HitWidth, mount.HitHeight), new(Vector2.Zero, rectangle.Size),
                maskTexture, new(mask.Position - rectangle.Position, mask.Size));
            button.SetMeta("source_main_semantic", (int)category);
            button.SetMeta("source_button_mount", mount.ButtonNode);
            button.SetMeta("source_caption_message", mount.CaptionMessageId);
            button.ConfigureFocus(_focusShader!, Texture(_layouts.Button, 8), Texture(_layouts.Button, 18),
                SourceEffectTint(_layouts.Button, "1_focus", 8), SourceEffectTint(_layouts.Button, "4_disable_forcus", 18));
            button.Pressed += () => { if (Current(generation)) ShowCategory(category); };
            var cursorMount = _main.Layout.Rectangle(mount.CursorNode);
            var cursor = new MapCommandRailCursor { Name = "Cursor_" + category, Position = cursorMount.Position,
                Size = cursorMount.Size, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            _mainRoot.AddChild(cursor);
            var cursorArt = _cursor.Art(cursor, 2, _assets!, _cursorShader!);
            cursor.Configure(cursorArt, _layouts.Cursor.Clips.Single(clip => clip.Name == "1_focus"));
            cursor.Modulate = new(1, 1, 1, _main.Opacity(mount.CursorNode));
            cursor.SetMeta("source_cursor_mount", mount.CursorNode);
            _cursors.Add(category, cursor);
            button.HoverChanged += hovered =>
            {
                if (!Current(generation)) return;
                if (hovered && Expanded && !AutomaticallyHidden && IsVisibleInTree()) cursor.Begin();
                else cursor.Hide();
            };
            _buttons.Add(category, button); _mainRoot.AddChild(button);
            _categoryHintButtons.Add(button,category);
            _attention.Add(category, Notification("Attention_" + category, mount.AttentionNode,
                _layouts.Attention, new("static", 0, [])));
            _badges.Add(category, Notification("Badge_" + category, mount.BadgeNode,
                _layouts.Badge, _layouts.Badge.Stable("flicker_loop")));
        }
        var foldBox = _main.Layout.Rectangle(MainCommandLayoutSources.MainFoldMount);
        _fold = new MapCommandRailButton { Name = "FoldCommands", Position = foldBox.Position };
        _fold.Configure(_shader!, Images(_source.Fold, [3, 5, 8, 10, 12]), null,
            _assets!.SrgbSprite(Expanded ? "000_cmn_com_icon_close" : "000_cmn_com_icon_open"),
            foldBox.Size, foldBox.Size, new(10, 48, 20, 20));
        _fold.ConfigureFocus(_shader!, Texture(_layouts.Fold, 6), Texture(_layouts.Fold, 13),
            SourceEffectTint(_layouts.Fold, "1_focus", 6), SourceEffectTint(_layouts.Fold, "4_disable_forcus", 13));
        _fold.Pressed += () => { if (Current(generation)) SetExpanded(!Expanded); };
        _fold.SetMeta("source_fold_mount", MainCommandLayoutSources.MainFoldMount);
        _mainRoot.AddChild(_fold);
        SetMeta("source_main_clip", _layouts.MainStable(style, Expanded).Name);
        SetMeta("source_category_count", _buttons.Count);
        PublishPresentation();
        PublishAutomaticVisibility();

        Control Notification(string name, int mount, MainCommandLayoutAsset resource,
            MainCommandLayoutStableFrame frame)
        {
            var box = _main.Layout.Rectangle(mount);
            var group = new Control { Name = name, Position = box.Position, Size = box.Size,
                MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            _mainRoot.AddChild(group);
            var layout = new MainCommandLayoutState(resource, frame);
            foreach (var node in resource.Nodes.Where(node => node.Type is 1 or 3))
                layout.Art(group, node.Index, _assets!, _shader!);
            group.Modulate = new(1, 1, 1, _main.Opacity(mount));
            group.SetMeta("source_mount_node", mount);
            group.SetMeta("source_layout", resource.Source.Source.ResourceUri);
            return group;
        }
    }

    private bool Current(long generation) => !_retired && generation == _generation && SourceControlReady;

    internal void SetExpanded(bool expanded)
    {
        if (_retired || Expanded == expanded) return;
        CloseMenu(); Expanded = expanded;
        RebuildMain();
    }

    internal void CloseMenu()
    {
        ++_menuGeneration;
        OpenCategory = null; _submenuInput = null;
        if (_submenuRoot is not null) { _submenuRoot.Hide(); Clear(_submenuRoot); }
        if (_linkRoot is not null) { _linkRoot.Hide(); Clear(_linkRoot); }
        _submenuButtons.Clear();
        ClearEntryHints();
        foreach (var button in _buttons.Values) button.Selected = false;
        foreach (var cursor in _cursors.Values) cursor.Hide();
        if (_fold is not null) _fold.Show();
        if (_layouts is not null) SetMeta("source_fold_clip", _layouts.FoldStable(true).Name);
        SetMeta("source_open_category", -1);
        PresentationChanged?.Invoke();
    }

    internal void ShowCategory(MapCommandCategory category)
    {
        if (!SourceControlReady || !_buttons.TryGetValue(category, out var categoryButton) || categoryButton.Disabled) return;
        if (OpenCategory == category) { CloseMenu(); return; }
        if (!Expanded) SetExpanded(true);
        CloseMenu();
        int style = _style!.Value;
        long generation = _generation;
        long menuGeneration = _menuGeneration;
        try
        {
            OpenCategory = category;
            _buttons[category].Selected = true;
            _fold!.Hide();
            SetMeta("source_fold_clip", _layouts.FoldStable(false).Name);
            SetMeta("source_open_category", (int)category);
            var submenu = new MainCommandLayoutState(_layouts.Submenu, _layouts.SubmenuStable(true));
            int kind = (int)category;
            int background = style == 3 ? 14 + kind : 2 + kind;
            _submenuInput = submenu; _submenuInputBackground = background;
            submenu.Art(_submenuRoot, background, _assets!, _shader!);
            int[] starts = style == 3 ? [0, 2, 4, 6, 8, 10, 11] : [-1, 0, 2, 4, 6, 8, 10];
            int[] slots = [29, 34, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100];
            int[] hitWidths = [168, 163, 159, 156, 153, 151, 153, 156, 159, 163, 168, 173, 179, 187, 187];
            var mask = submenu.Layout.Rectangle(27);
            var secondMask = submenu.Layout.Rectangle(28);
            Texture2D maskTexture = Texture(_layouts.Submenu, 27), secondMaskTexture = Texture(_layouts.Submenu, 28);
            Texture2D[] first = Images(_source.SubmenuButton, [3, 6, 10, 13, 16]);
            Texture2D[] second = Images(_source.SubmenuButton, [4, 7, 11, 14, 17]);
            var profile = MapFontProfile.FromNode(_source.SubmenuButton.Nodes[20], _source.SubmenuButton.Source.ResourceUri);
            int row = starts[kind];
            foreach (var entry in _menus[kind])
            {
                var rectangle = submenu.Layout.Rectangle(slots[row]);
                var button = new MapCommandRailButton { Name = entry.Command.ToString(), Position = rectangle.Position };
                button.Configure(_shader!, first, second, null, rectangle.Size, new(hitWidths[row], rectangle.Size.Y), default,
                    maskTexture, new(mask.Position - rectangle.Position, mask.Size),
                    secondMaskTexture, new(secondMask.Position - rectangle.Position, secondMask.Size));
                var effects = Images(_source.SubmenuButton, [8, 18]);
                button.ConfigureFocus(_submenuFocusShader!, effects[0], effects[1], _submenuFocusTint, _submenuDisabledFocusTint);
                button.ConfigureText(_font!, profile, entry.Caption);
                bool bound = _commands.TryGetValue(entry.Command, out var action);
                BindEntryPresentation(button, entry.Command, bound);
                if (action is not null) button.Pressed += () =>
                {
                    if (!Current(generation) || menuGeneration != _menuGeneration || OpenCategory != category || button.Disabled) return;
                    CloseMenu(); action();
                };
                button.SetMeta("source_submenu_mount", slots[row]);
                _submenuButtons.Add(button); _submenuRoot.AddChild(button); row++;
            }
            var link = new MainCommandLayoutState(_layouts.Link, _layouts.Link.Stable("sub_in"),
                MainCommandLayoutSources.LinkRotationNode, _layouts.LinkAngleDegrees(style, category));
            // Actual 55AA9B8A node2 uses 00114400 (SRC_ALPHA/ONE), not source-over.
            link.Art(_linkRoot, 2, _assets!, _shader!, UiSourceSprite.Blend.Additive);
            _linkRoot.SetMeta("source_euler_z_degrees", _layouts.LinkAngleDegrees(style, category));
            _submenuRoot.Show(); _linkRoot.Show();
            PresentationChanged?.Invoke();
        }
        catch { CloseMenu(); throw; }
    }

    private static Color SourceEffectTint(MainCommandLayoutAsset source, string name, int node)
        => SourceEffectTint(source.Stable(name).Values, node);

    internal static Color SourceEffectTint(IEnumerable<LcskAnimationValue> values, int node)
    {
        Color tint = Colors.White;
        foreach (var value in values)
        {
            if (value.NodeId != node) continue;
            float v = (byte)(int)Math.Clamp(value.Value * 255d, 0d, 255d) / 255f;
            switch (value.Channel)
            {
                case 10: tint.A *= v; break;
                case 16: tint.R = v; break;
                case 17: tint.G = v; break;
                case 18: tint.B = v; break;
                case 19: tint.A *= v; break;
            }
        }
        return tint;
    }

    private Texture2D[] Images(MapMarkerLayout source, int[] nodes) => nodes
        .Select(index => (Texture2D)_assets!.SrgbSprite(MapCommandRailSources.Sprite(source, index))).ToArray();

    public override bool _HasPoint(Vector2 point)
    {
        if (_retired || AutomaticallyHidden || !IsVisibleInTree()) return false;
        // Child buttons keep normal GUI priority. Only the actual settled
        // background panes make their gaps belong to this rail.
        Vector2 global = GetGlobalTransformWithCanvas() * point;
        return _main is { } main &&
            (ContainsBackground(_mainRoot, main, 3, global) || ContainsBackground(_mainRoot, main, 5, global)) ||
            _submenuInput is { } submenu && ContainsBackground(_submenuRoot, submenu, _submenuInputBackground, global);
    }

    private static bool ContainsBackground(Control parent, MainCommandLayoutState source, int node, Vector2 global)
        => parent.IsVisibleInTree() && source.Opacity(node) > 0 &&
            source.Layout.Rectangle(node).HasPoint(parent.GetGlobalTransformWithCanvas().AffineInverse() * global);

    private Texture2D Texture(MainCommandLayoutAsset source, int node)
    {
        var sprite = San14.Data.Formats.LcskSpriteNodeReader.Read(source.Source.Layout, source.Nodes[node]);
        var part = sprite.Groups.SelectMany(group => group.Parts).Single();
        return _assets!.SrgbLayoutSpritePart(source.Source, part.SpriteName, part.Uv);
    }

    private static void Clear(Control parent)
    {
        foreach (var child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); }
    }

    private void VisibilityChangedHandler() { if (!IsVisibleInTree()) CloseMenu(); }

    public override void _Input(InputEvent input)
    {
        if (!SourceControlReady || OpenCategory is null) return;
        bool cancel = input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } ||
            input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right };
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse)
            cancel = !_submenuButtons.Any(Contains) && !_buttons.Values.Any(Contains);
        bool Contains(MapCommandRailButton button) => button.IsVisibleInTree() &&
            button._HasPoint(button.GetGlobalTransformWithCanvas().AffineInverse() * ((InputEventMouseButton)input).Position);
        if (cancel) { CloseMenu(); GetViewport().SetInputAsHandled(); }
    }

    public override void _ExitTree()
    {
        _retired = true; ++_generation;
        VisibilityChanged -= VisibilityChangedHandler;
        CloseMenu(); _buttons.Clear(); _cursors.Clear(); _attention.Clear(); _badges.Clear();
        _categoryHintButtons.Clear(); _fixedHintTexts.Clear();
        PresentationChanged = null;
        // These immutable resources belong to Main's startup generation.
        _font = null; _cursorShader = null; _focusShader = null;
        _submenuFocusShader = null; _shader = null; _assets = null;
    }
}
