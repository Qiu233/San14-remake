using Godot;
using San14.Core;
using San14.Data;

/// <summary>Compact resident map controls. All figures come from the current simulation state.</summary>
public partial class PlayableForceHud : Control
{
    private UiAssets? _assets;
    private PreviewText? _text;
    private int _forceId;
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

    internal void Configure(GameResourceRepository repository, PreviewText text, GameState world,
        int forceId, ScenarioCityPreview? snapshot, Action advance, Action openForceDetails)
    {
        Name = "PlayableForceHud";
        Size = new Vector2(600, 340);
        MouseFilter = MouseFilterEnum.Pass;
        _text = text;
        _forceId = forceId;
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
        int? portraitCode = snapshot?.Characters.FirstOrDefault(person =>
            person.CharacterId != 0 && person.CharacterId == world.Forces[forceId].RulerId)?.AppearanceCode;
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
        Refresh(world);
        SetMeta("original_map_hud_layout_verified", false);
    }

    internal void Refresh(GameState world)
    {
        if (_text is null) return;
        ForceState force = world.Forces[_forceId];
        CityState[] cities = world.Cities.Values.Where(city => city.ForceId == _forceId).ToArray();
        _date.Text = _text.Format("play_map_date", world.Date.Year, world.Date.Month,
            world.Date.Day);
        _ruler.Text = force.Name;
        int orders = world.Districts.Values.Where(district => district.ForceId == _forceId &&
            district.Number == 1).Sum(district => district.OrdersRemaining ?? 0);
        _orders.Text = _text.Format("play_map_orders", orders);
        _doctrine.Text = force.DoctrineId is int doctrineId && world.Doctrines.TryGetValue(doctrineId,
            out DoctrineDefinition? doctrine) ? doctrine.Name : "";
        long gold = cities.Sum(city => (long)city.Gold);
        long supplies = cities.Sum(city => (long)city.Supplies);
        long troops = cities.Sum(city => (long)city.Troops) +
            world.Gates.Values.Where(gate => gate.ForceId == _forceId).Sum(gate => (long)gate.Troops);
        int officers = world.Officers.Values.Count(officer => officer.ForceId == _forceId &&
            (officer.Presence is null or OfficerPresence.Present) &&
            officer.Role is OfficerRole.Officer or OfficerRole.Ruler or OfficerRole.DistrictLeader or OfficerRole.Governor);
        int districts = world.Districts.Values.Count(district => district.ForceId == _forceId);
        _gold.Text = _text.Format("play_map_force_gold", gold);
        _supplies.Text = _text.Format("play_map_force_supplies", supplies);
        _troops.Text = _text.Format("play_map_force_troops", troops);
        _cities.Text = _text.Format("play_map_force_cities", cities.Length);
        _officers.Text = _text.Format("play_map_force_officers", officers);
        _districts.Text = _text.Format("play_map_force_districts", districts);
        _advance.Disabled = world.Phase != TurnPhase.Strategy ||
            PlayableOutcome.Evaluate(world, _forceId) != PlayableResult.Ongoing;
        SetMeta("force_id", _forceId);
        SetMeta("world_period_number", world.PeriodNumber);
        SetMeta("gold", gold);
        SetMeta("supplies", supplies);
        SetMeta("troops", troops);
        SetMeta("cities", cities.Length);
        SetMeta("officers", officers);
        SetMeta("districts", districts);
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
