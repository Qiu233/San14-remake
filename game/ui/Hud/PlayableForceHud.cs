using Godot;
using San14.Client.Gameplay;
using San14.Data;
using San14.Data.Formats;

/// <summary>Resident map header using the original header and portrait artwork.
/// Current date, orders and force identity come from the simulation projection.</summary>
public partial class PlayableForceHud : Control
{
    private UiAssets? _assets;
    private MapFont? _font;
    private UiSourceText _year = null!;
    private UiSourceText _month = null!;
    private UiSourceText _period = null!;
    private UiSourceText _ruler = null!;
    private UiSourceText _orders = null!;
    private UiSourceText _doctrine = null!;
    private UiSourceText _doctrineLevel = null!;
    private MapDoctrineGauge _doctrineGauge = null!;
    private UiSourceText _flagText = null!;
    private Control _commandBookIcon = null!;
    private UiSourceText _commandBookCaption = null!;
    private bool? _commandBooksVisible;
    internal MapHeaderDisplayState? DisplayState { get; private set; }
    private Control _flag = null!;
    private Control _flagFill = null!;
    private UiSourcePortrait? _portrait;
    private Rect2 _portraitRectangle;
    private GameResourceRepository _repository = null!;
    private LocalizedUiSpriteSource _portraitSources = null!;
    private readonly Dictionary<(string Pack, string Sprite), LoadedSprite> _portraits = [];
    private (string Pack, string Sprite)? _shownPortrait;
    private MapAdvanceButton _advance = null!;
    private Shader? _spriteShader;
    private MapCommandRail? _commandRail;
    private Control? _ordinaryHeader;
    internal void ApplyMainCommandAutomaticHide(bool? hidden) => _commandRail?.ApplyAutomaticHide(hidden);
    internal bool? StrategyControlReady => _commandRail?.SourceControlReady;

    internal void Configure(GameResourceRepository repository, PreviewText text, ForceHudView view,
        Action advance, Action openForceDetails, IReadOnlyDictionary<MapMenuCommand, Action>? mapCommands = null,
        Action? openSystemMenu = null, Action? openInformationMenu = null,
        Action<int>? changeSpeed = null,int speed=1)
    {
        Name = "PlayableForceHud";
        ZIndex = 2;
        Size = new Vector2(620, 288);
        MouseFilter = MouseFilterEnum.Ignore;
        //Attach owned controls to their final header before configuring them.
        //Reparenting a live source sprite retires its material in _ExitTree.
        _ordinaryHeader = new Control { Name = "OrdinaryMapHeader", MouseFilter = MouseFilterEnum.Ignore, Size = Size };
        AddChild(_ordinaryHeader);
        _assets = new UiAssets(repository);
        _font = new MapFont(repository, text.SourceLocale);
        _repository = repository; _portraitSources = new(repository);
        executionText=text;
        _spriteShader = UiSourceSprite.CreateShader();
        // CMainTopLayoutOffset selects the Chinese layout by its Common name.
        var header = repository.LoadLayout("ScreenLayout", 0xBB442620);
        var nodes = LcskNodeReader.Read(header.Layout);
        var source = new UiSourceLayout(header);
        MapTextPresets presets = MapTextPresets.Create();

        var commandRail = new MapCommandRail();
        commandRail.Configure(repository, text.SourceLocale, mapCommands ?? new Dictionary<MapMenuCommand,Action>(),
            view.MainCommandStyle, view.MainCommandPresentation, view.UnknownText);
        _commandRail=commandRail;
        AddChild(commandRail);
        if (openSystemMenu is not null || openInformationMenu is not null)
        {
            // log_slide_in's settled node83 X is960. Its authored -200 is
            // replaced by the source animation, never added to this endpoint.
            var main = new UiSourceLayout(repository, 0x036BB544,
                new Dictionary<int, UiSourceNodeGeometry> { [83] = new(Origin: new Vector2(960, 0)) });
            var top = new UiSourceLayout(repository, 0xF28BE325);
            if (openInformationMenu is not null) Function("MapInformationButton", 30, "000_cmn_icon_info", openInformationMenu);
            if (openSystemMenu is not null) Function("MapSystemButton", 24, "000_cmn_icon_opt", openSystemMenu);

            void Function(string name, int node, string icon, Action action)
            {
                var button = new UiSourceFunctionButton { Name = name,
                    Position = main.Rectangle(84).Position + top.Rectangle(node).Position };
                AddChild(button);
                button.Configure(repository, _assets, _spriteShader, icon, action);
                button.SetMeta("source_mount_layout", main.Source.Source.ResourceUri);
                button.SetMeta("source_group_layout", top.Source.Source.ResourceUri);
                button.SetMeta("source_group_node", node);
            }
        }

        // The original mounts a real Common09 control here. Its three-part
        // composition replaces this layout's two-part dummy.
        _portraitRectangle = source.Rectangle(1);
        AddPicture("ForceHudBacking", "000_cmn_base_main_header", new Rect2(0, 0, 620, 220));
        _year = AddText("Year", "val_2_fc001_0000", 0);
        _month = AddText("Month", "val_3_fc001_0000", 0);
        _period = AddText("Period", "val_4_fc001_0000", 0);
        _ruler = AddText("Ruler", "name_fc001", 0);
        _commandBookIcon = AddPicture("CommandBookIcon", "000_cmn_icon_meirei", new Rect2(299, 95, 32, 32));
        _commandBookIcon.Modulate = CanvasColor(MapSpritePalettes.Create()[10].LT);
        _commandBookCaption = AddText("CommandBookCaption", "cap_10_fc014_0000", 13);
        _commandBookCaption.Text = text.Get("map_hud_command_books");
        _orders = AddText("Orders", "val_10_fc001_0000", 0);
        _doctrine = AddText("Doctrine", "syugi_fc042", 41);
        _doctrineLevel = AddText("DoctrineLevel", "val_lv_fc001", 0);
        _doctrineGauge = new MapDoctrineGauge();
        _doctrineGauge.Configure(source, _assets, _spriteShader);
        _ordinaryHeader.AddChild(_doctrineGauge);
        ConfigureExperienceHeader(source, nodes, text.SourceLocale, AddText);

        LcskNodeRecord flagNode = nodes.Single(n => n.Name == "flag");
        _flag = new Control
        {
            Name = "ForceFlag", Position = new(flagNode.HeaderValues[0], -flagNode.HeaderValues[1]),
            Size = new(flagNode.Value44, flagNode.Value46), MouseFilter = MouseFilterEnum.Ignore
        };
        _ordinaryHeader.AddChild(_flag);
        foreach (string suffix in new[] { "a", "b", "c" })
        {
            string sprite = "000_cmn_main_haeder_flag_" + suffix;
            LcskNodeRecord node = nodes.Single(n => n.Name == sprite);
            var layer = source.Art(_flag, node.Index, source.Rectangle(node.Index, -_flag.Position), _assets, _spriteShader);
            layer.Name = "ForceFlag_" + suffix;
            if (suffix == "b") _flagFill = layer;
        }
        _flagText = AddTextTo(_flag, "ForceFlagText", "val_10_fc001_0001", null);

        var forceDetails = new Button
        {
            Name = "MapForceInformation", Flat = true, Text = "",
            TooltipText = text.Get("play_force_information"),
            Position = _portraitRectangle.Position,
            Size = new Vector2((int)_portraitRectangle.Size.X, (int)_portraitRectangle.Size.Y),
            MouseFilter = MouseFilterEnum.Stop
        };
        forceDetails.Pressed += openForceDetails;
        _ordinaryHeader.AddChild(forceDetails);

        var executionLayout=new UiSourceLayout(repository,0x454A1BCC);
        Vector2 advancePosition=executionLayout.Rectangle(26).Position;
        progressRing=new MapProgressRing {Position=advancePosition};
        _ordinaryHeader.AddChild(progressRing);progressRing.Configure(repository,_assets,_spriteShader);
        _advance = new MapAdvanceButton
        {
            Name = "MapAdvanceTenDays",
            Position = advancePosition
        };
        _advance.Configure(_assets, repository);
        _advance.Pressed += advance;
        _ordinaryHeader.AddChild(_advance);
        executionControls=new MapExecutionControls {Position=executionLayout.Rectangle(18).Position};
        _ordinaryHeader.AddChild(executionControls);
        executionControls.Configure(repository,_assets,_spriteShader,_advance,changeSpeed??(_=>{}),speed);
        //Sibling ordering changes without removing any initialized subtree.
        MoveChild(_ordinaryHeader, -1);
        // The command submenu does not own the advance controls in this header.
        Refresh(view);
        SetMeta("original_map_hud_layout_verified", false);

        UiSourceText AddText(string name, string sourceName, int? preset)
            => AddTextTo(_ordinaryHeader!, name, sourceName, preset);

        UiSourceText AddTextTo(Control parent, string name, string sourceName, int? preset)
        {
            LcskNodeRecord node = nodes.Single(n => n.Name == sourceName);
            Rect2 rectangle = Rectangle(node);
            var label = new UiSourceText
            {
                Name = name,
                Position = rectangle.Position, Size = rectangle.Size
            };
            MapFontProfile profile = MapFontProfile.FromNode(node, header.Source.ResourceUri);
            label.Configure(_font, preset is int style ? profile.WithPreset(presets[style]) : profile);
            parent.AddChild(label);
            return label;
        }

        static Rect2 Rectangle(LcskNodeRecord node)
        {
            Vector2 size = new(node.Value44, node.Value46);
            Vector2 position = new(node.HeaderValues[0], -node.HeaderValues[1]);
            if (node.Value48 == 0x1111) position -= size / 2;
            else if (node.Value48 != 0x1100)
                throw new InvalidDataException($"Unsupported header anchor: {node.Name}.");
            return new(position, size);
        }
    }

    internal void Refresh(ForceHudView view)
    {
        _commandRail?.Refresh(view.MainCommandStyle);
        _commandRail?.RefreshPresentation(view.MainCommandPresentation);
        _year.Text = view.Year;
        _month.Text = view.Month;
        _period.Text = view.Period;
        if (view.Identity.Update == MapHeaderUpdate.Updated)
            DisplayState = new(view.Identity.DistrictId!.Value, DisplayState?.OrderCount);
        SetMeta("command_books_update", view.CommandBooks.Update.ToString());
        SetMeta("command_books_issue", view.CommandBooks.UnresolvedInput ?? "");
        if (view.CommandBooks.Update != MapHeaderUpdate.Unchanged || view.CommandBooks.Count is not null)
        {
            _orders.Text = view.OrderCountText;
            if (view.CommandBooks.Update == MapHeaderUpdate.Updated)
                DisplayState = DisplayState! with { OrderCount = view.CommandBooks.Count };
        }
        SetMeta("header_identity_update", view.Identity.Update.ToString());
        SetMeta("header_identity_issue", view.Identity.UnresolvedInput ?? "");
        if (view.Identity.Update != MapHeaderUpdate.Unchanged)
        {
            _commandBooksVisible = view.Identity.CommandBooksVisible;
            SetMeta("command_books_visibility_issue", view.Identity.CommandBooksVisibilityIssue ?? "");
            _ruler.Text = view.RulerName;
            _doctrine.Text = view.DoctrineName;
            RefreshExperienceHeader(view);
            var flag = view.Identity.Flag;
            _flag.Visible = flag is { ForceValidity: true, CurrentForceColor49: not null };
            if (_flag.Visible)
            {
                _flagText.Text = flag!.NativeShortName;
                AreaMarkerColor fill = MapSpritePalettes.ForceFill(flag.CurrentForceColor49!.Value);
                foreach (var part in _flagFill.GetChildren().OfType<UiSourceSprite>()) part.SetColors(new(fill, fill, fill, fill));
            }
            else _flagText.Text = "";
            ApplyPortrait(view.Identity.Portrait);
        }
        _commandBookIcon.Visible = _commandBooksVisible == true;
        _commandBookCaption.Visible = _orders.Visible = _commandBooksVisible != false;
        if (_commandBooksVisible is null) _orders.Text = view.UnknownText;
        SetMeta("command_books_visibility", _commandBooksVisible is bool shown ? shown ? "Visible" : "Hidden" : "Unresolved");
        lastCanAdvance=view.CanAdvance;
        _advance.Disabled = executingPeriod?false:!view.CanAdvance;
        SetMeta("force_id", view.ForceId);
        SetMeta("world_period_number", view.PeriodNumber);
        SetMeta("gold", view.Figures.Gold);
        SetMeta("supplies", view.Figures.Supplies);
        SetMeta("troops", view.Figures.Troops);
        SetMeta("cities", view.Figures.Cities);
        SetMeta("officers", view.Figures.Officers);
        SetMeta("districts", view.Figures.Districts);
    }

    private void ApplyPortrait(OfficerPortraitRoute? route)
    {
        SetMeta("portrait_appearance_code", route?.CurrentAppearanceIndex is ushort index ? index : -1);
        SetMeta("portrait_kind", route?.Kind.ToString() ?? "Unresolved");
        SetMeta("portrait_issue", route?.UnresolvedInput ?? "");
        (string Pack, string Sprite)? key = route is { Kind: OfficerPortraitRouteKind.StockNamed }
            ? (route.ResourceName!, route.SpriteName!) : null;
        if (_shownPortrait == key) return;
        UiSourcePortrait? next = null;
        if (key is { } selected)
        {
            if (!_portraits.TryGetValue(selected, out var image))
            { image = _portraitSources.LoadTexturePackSprite(selected.Pack, selected.Sprite); _portraits.Add(selected, image); }
            next = new UiSourcePortrait { Name = "RulerPortrait", Position = _portraitRectangle.Position };
            try { next.ConfigureMapHeader(_repository, _assets!, _spriteShader!, image); }
            catch { next.Free(); throw; }
        }
        // Publish the successful resource/control pair together. A load or
        // format failure must leave the old portrait and retry key intact.
        if (_portrait is not null)
        { _portrait.GetParent().RemoveChild(_portrait); _portrait.QueueFree(); _portrait = null; }
        _shownPortrait = key;
        _portrait = next;
        if (next is not null) { (_ordinaryHeader ?? this).AddChild(next); next.GetParent().MoveChild(next, 0); }
    }

    internal void FitToMap(Vector2 mapSize)
    {
        float scale = MathF.Min(mapSize.X / 1920f, mapSize.Y / 1080f);
        Scale = new Vector2(scale, scale);
        Position = Vector2.Zero;
    }

    private TextureRect AddPicture(string name, string? spriteName, Rect2 rectangle)
    {
        var picture = new TextureRect
        {
            Name = name, Position = rectangle.Position, Size = rectangle.Size,
            Texture = spriteName is null ? null : _assets!.Sprite(spriteName).Texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _ordinaryHeader!.AddChild(picture);
        return picture;
    }

    private static Color CanvasColor(AreaMarkerColor color) => new(color.AuthoredRed / 255f,
        color.AuthoredGreen / 255f, color.AuthoredBlue / 255f, color.AuthoredAlpha / 255f);

    public override void _ExitTree()
    {
        _font?.Dispose();
        _font = null;
        _assets?.Dispose();
        _assets = null;
        _spriteShader?.Dispose(); _spriteShader = null;
    }
}
