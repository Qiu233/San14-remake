using Godot;
using San14.Client.Gameplay;
using San14.Data;

/// <summary>Compact resident map controls. All figures come from the current simulation state.</summary>
public partial class PlayableForceHud : Control
{
    private UiAssets? _assets;
    private Label _date = null!;
    private Label _ruler = null!;
    private Label _orders = null!;
    private Label _doctrine = null!;
    private Label _gold = null!;
    private Label _supplies = null!;
    private Label _troops = null!;
    private Label _cities = null!;
    private Label _officers = null!;
    private Label _districts = null!;
    private TextureButton _advance = null!;

    internal void Configure(GameResourceRepository repository, PreviewText text, ForceHudView view,
        Action advance, Action openForceDetails)
    {
        Name = "PlayableForceHud";
        Size = new Vector2(600, 340);
        MouseFilter = MouseFilterEnum.Pass;
        _assets = new UiAssets(repository);
        Theme = new Theme
        {
            DefaultFont = new SystemFont { FontNames = ["KaiTi", "DFKai-SB", "Microsoft YaHei"] }
        };

        AddPanel(new Rect2(0, 0, 590, 195), new Color(0.06f, 0.055f, 0.05f, 0.84f));
        var portraitClip = new Control
        {
            Name = "RulerPortraitClip", Position = new Vector2(0, 0),
            Size = new Vector2(176, 195), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(portraitClip);
        int? portraitCode = view.PortraitAppearanceCode;
        if (portraitCode is int code)
        {
            var portrait = new PortraitView { Name = "RulerPortrait" };
            portraitClip.AddChild(portrait);
            portrait.Configure(repository, _assets, code, new Rect2(-12, -75, 206, 292));
        }
        SetMeta("portrait_appearance_code", portraitCode ?? -1);

        _date = AddLabel("Date", new Rect2(183, 8, 335, 37), 26, new Color("#eee2b1"));
        _ruler = AddLabel("Ruler", new Rect2(183, 48, 280, 35), 27, new Color("#fff0bd"));
        AddPicture("CommandBookIcon", "000_cmn_icon_meirei", new Rect2(183, 91, 28, 28));
        _orders = AddLabel("Orders", new Rect2(218, 90, 295, 31), 22, new Color("#ead6a3"));
        _doctrine = AddLabel("Doctrine", new Rect2(455, 50, 130, 70), 20, new Color("#e6d79c"));
        _doctrine.HorizontalAlignment = HorizontalAlignment.Center;
        Color figureColor = new("#ead6a3");
        _gold = AddLabel("ForceGold", new Rect2(183, 127, 135, 28), 18, figureColor);
        _supplies = AddLabel("ForceSupplies", new Rect2(318, 127, 140, 28), 18, figureColor);
        _troops = AddLabel("ForceTroops", new Rect2(458, 127, 130, 28), 18, figureColor);
        _cities = AddLabel("ForceCities", new Rect2(183, 158, 135, 28), 18, figureColor);
        _officers = AddLabel("ForceOfficers", new Rect2(318, 158, 140, 28), 18, figureColor);
        _districts = AddLabel("ForceDistricts", new Rect2(458, 158, 130, 28), 18, figureColor);

        _advance = new TextureButton
        {
            Name = "MapAdvanceTenDays",
            Position = new Vector2(35, 209), Size = new Vector2(116, 116),
            MouseFilter = MouseFilterEnum.Stop,
            TextureNormal = _assets.Sprite("000_cmn_btn_adv_0").Texture,
            StretchMode = TextureButton.StretchModeEnum.Scale
        };
        _advance.Pressed += advance;
        AddChild(_advance);
        var advanceText = AddLabel("AdvanceText", new Rect2(35, 245, 116, 46), 28,
            new Color("#302515"));
        advanceText.Text = text.Get("play_map_advance");
        advanceText.HorizontalAlignment = HorizontalAlignment.Center;
        var forceDetails = new Button
        {
            Name = "MapForceInformation", Flat = true, Text = "",
            TooltipText = text.Get("play_force_information"),
            Position = Vector2.Zero, Size = new Vector2(176, 195),
            MouseFilter = MouseFilterEnum.Stop
        };
        forceDetails.Pressed += openForceDetails;
        AddChild(forceDetails);
        Refresh(view);
        SetMeta("original_map_hud_layout_verified", false);
    }

    internal void Refresh(ForceHudView view)
    {
        _date.Text = view.Date;
        _ruler.Text = view.ForceName;
        _orders.Text = view.Orders;
        _doctrine.Text = view.DoctrineName;
        _gold.Text = view.Gold;
        _supplies.Text = view.Supplies;
        _troops.Text = view.Troops;
        _cities.Text = view.Cities;
        _officers.Text = view.Officers;
        _districts.Text = view.Districts;
        _advance.Disabled = !view.CanAdvance;
        SetMeta("force_id", view.ForceId);
        SetMeta("world_period_number", view.PeriodNumber);
        SetMeta("gold", view.Figures.Gold);
        SetMeta("supplies", view.Figures.Supplies);
        SetMeta("troops", view.Figures.Troops);
        SetMeta("cities", view.Figures.Cities);
        SetMeta("officers", view.Figures.Officers);
        SetMeta("districts", view.Figures.Districts);
    }

    internal void FitToMap(Vector2 mapSize)
    {
        float scale = MathF.Min(mapSize.X / 1880f, mapSize.Y / 994f);
        Scale = new Vector2(scale, scale);
        Position = Vector2.Zero;
    }

    private Label AddLabel(string name, Rect2 rectangle, int fontSize, Color color)
    {
        var label = new Label
        {
            Name = name, Position = rectangle.Position, Size = rectangle.Size,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        AddChild(label);
        return label;
    }

    private void AddPicture(string name, string spriteName, Rect2 rectangle)
    {
        var picture = new TextureRect
        {
            Name = name, Position = rectangle.Position, Size = rectangle.Size,
            Texture = _assets!.Sprite(spriteName).Texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(picture);
    }

    private void AddPanel(Rect2 rectangle, Color color)
    {
        var panel = new ColorRect
        {
            Name = "ForceHudBacking", Position = rectangle.Position, Size = rectangle.Size,
            Color = color, MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(panel);
    }

    public override void _ExitTree()
    {
        _assets?.Dispose();
        _assets = null;
    }
}
