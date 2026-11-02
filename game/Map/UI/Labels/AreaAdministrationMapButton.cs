using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>One held Area's Domestic map button. The map picker remains the
/// authority for input; this tree owns only the source artwork and hit pane.</summary>
internal partial class AreaAdministrationMapButton : UiSourceButton
{
    internal static readonly string[] DevelopmentSprites =
        ["000_cmn_icon_kane", "000_cmn_icon_st_hyourou", "000_cmn_icon_heishi"];
    private GameResourceRepository repository = null!;
    private AdministrationPageChrome.Resources resources = null!;
    private UiSourceLayout root = null!;
    private AreaAdministrationMapFace large = null!;
    private AreaAdministrationMapCompact compact = null!;
    private readonly AreaAdministrationMapDevelopmentIcon[] development = new AreaAdministrationMapDevelopmentIcon[4];
    private OfficerPortraitRoute? portrait;
    private AreaMarkerColor color;
    private string areaName = "";
    private bool hasFace, far, bandConfigured;
    private int sourceState;
    private Rect2 hit;
    internal int HexId { get; private set; }
    internal int AreaId { get; private set; }
    internal Vector2 ProjectionOffset => new(-large.Size.X * .5f - root.Nodes[73].HeaderValues[0], -20);

    internal void Configure(GameResourceRepository repository, AdministrationPageChrome.Resources resources,
        AreaAdministrationMapRow row, Func<int, bool> admitted, Action<int> picked)
    {
        this.repository = repository; this.resources = resources;
        root = resources.Assets.SourceLayout(0xBED46BA4);
        Name = "DomesticArea" + row.AreaId; Size = root.Rectangle(0).Size;
        MouseFilter = MouseFilterEnum.Stop; ActionMode = ActionModeEnum.Release;
        InputAdmission = () => admitted(HexId);
        for (int i = 0; i < development.Length; i++)
        {
            var icon = new AreaAdministrationMapDevelopmentIcon { Name = "Development" + i };
            AddChild(icon); icon.Configure(resources); development[i] = icon;
        }
        Apply(row);
        ConfigureVisualState(value =>
        {
            sourceState = value >= 3 ? 4 : value;
            large.SetInteractionState(sourceState); compact.SetInteractionState(sourceState);
        });
        Pressed += () => { if (!Disabled && SourceInputAdmitted) picked(HexId); };
        SetMeta("source_layout", root.Source.Source.ResourceUri);
    }

    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);

    internal void Apply(AreaAdministrationMapRow row)
    {
        AreaId = row.AreaId;
        HexId = Known(row.RepresentativeHexId) ?? throw new InvalidDataException("area-map.hex_identity");
        string name = Known(row.AreaName);
        bool validPerson = Known(row.PersonValid) ?? throw new InvalidDataException("area-map.person_validity");
        var nextPortrait = Known(row.Portrait);
        if (validPerson && nextPortrait is null) throw new InvalidDataException("area-map.person_portrait");
        if (!validPerson && nextPortrait is not null) throw new InvalidDataException("area-map.empty_slot_portrait");
        var nextColor = Known(row.ForceColor) ?? throw new InvalidDataException("area-map.force_color");
        if (!hasFace || portrait != nextPortrait || color != nextColor || areaName != name)
        {
            if (hasFace) { Retire(large); Retire(compact); }
            portrait = nextPortrait; color = nextColor; areaName = name;
            large = new AreaAdministrationMapFace { Name = "LargeFace", Position = root.Rectangle(73).Position };
            AddChild(large); large.Configure(repository, resources, portrait, color);
            compact = new AreaAdministrationMapCompact { Name = "Compact", Position = root.Rectangle(55).Position };
            AddChild(compact); compact.Configure(repository, resources, name, portrait, color);
            MoveChild(large, 0); MoveChild(compact, 1);
            hasFace = true;
            bandConfigured = false;
            large.SetInteractionState(sourceState); compact.SetInteractionState(sourceState);
        }
        if (row.Development.Count != 3) throw new InvalidDataException("area-map.development_count");
        for (int i = 0; i < 3; i++) development[i].Apply(i, row.Development[i]);
        byte raw = Known(row.RawFocus) ?? throw new InvalidDataException("area-map.focus");
        var focus = Known(row.Focus);
        if (raw != 0 && focus is { } kind)
        {
            int index = row.Development.ToList().FindIndex(value => value.Focus == kind);
            if (index < 0) throw new InvalidDataException("area-map.focus_kind");
            development[3].Apply(index, row.Development[index]);
            development[3].Modulate = Colors.White;
        }
        else development[3].Modulate = new(1, 1, 1, 0);
        ApplyBand(far ? 4 : 3);
    }

    internal void ApplyBand(int band)
    {
        if (bandConfigured && far == (band > 3)) return;
        bandConfigured = true;
        far = band > 3;
        if (large.Visible != far) large.Visible = far;
        if (compact.Visible == far) compact.Visible = !far;
        for (int i = 0; i < 3; i++)
        {
            development[i].Visible = !far;
            // The source control setter already uses Y-down coordinates.
            development[i].Position = new(28 * (i + 1), -19);
        }
        development[3].Visible = far;
        development[3].Position = root.Rectangle(74).Position;
        hit = far
            ? new Rect2(large.Position + resources.Assets.SourceLayout(0x7104E19F).Rectangle(1).Position,
                resources.Assets.SourceLayout(0x7104E19F).Rectangle(1).Size)
            : new Rect2(compact.Position + compact.Hit.Position, compact.Hit.Size);
    }

    internal static T Known<T>(AreaAdministrationQuery<T> value) => value.Available
        ? value.Value! : throw new InvalidDataException(value.UnresolvedInput ?? "area-map.unknown");

    private static void Retire(Control view)
    { view.Hide(); view.GetParent()?.RemoveChild(view); view.QueueFree(); }
}

internal partial class AreaAdministrationMapCompact : UiSourceAnimationView
{
    private static readonly string[] Names = ["0_normal", "1_focus ", "2_push", "3_disable"];
    private IReadOnlyDictionary<string, LcskAnimationClip> curves = null!;
    private AreaAdministrationMapFace face = null!;
    private UiSourceText caption = null!;
    private MapFont font = null!;
    private MapFontProfile profile = null!;
    private int state = -1;
    private double elapsed, duration;
    private bool sampling;
    internal Rect2 Hit { get; private set; }

    internal void Configure(GameResourceRepository repository, AdministrationPageChrome.Resources resources,
        string name, OfficerPortraitRoute? portrait, AreaMarkerColor color)
    {
        font = resources.Font;
        var original = resources.Assets.SourceLayout(0xC822D27E);
        profile = MapFontProfile.FromNode(original.Nodes[11], original.Source.Source.ResourceUri);
        var unit = profile with { ScaleX = 1, ScaleY = 1, HorizontalSpace = 0, VerticalSpace = 0 };
        int extra = Math.Max(0, (int)((float)font.Measure(name, unit).Width * profile.ScaleX) - original.Nodes[11].Value44 + 24);
        var changed = new Dictionary<int, UiSourceNodeGeometry>();
        foreach (int index in new[] { 0, 3, 5, 6, 8, 9, 11 })
            changed[index] = new(Size: new(original.Nodes[index].Value44 + extra, original.Nodes[index].Value46));
        var source = new UiSourceLayout(original.Source, changed);
        AutomaticAnimationClock = false;
        curves = resources.Assets.SourceAnimationClips(original, Names.ToHashSet(StringComparer.Ordinal));
        ConfigureAnimation(source, curves, source.Nodes.Select(node => node.Index));
        Show(); // Subsequent retained samples must preserve the button's band visibility.
        var palettes = MapSpritePalettes.Create();
        foreach (int index in new[] { 5, 6, 8, 9 })
        {
            var node = source.Nodes[index];
            source.Art(AnimationPanes[index], index, new(Vector2.Zero, new(node.Value44 + extra, node.Value46)),
                resources.Assets, resources.SpriteShader, colors: palettes[index <= 6 ? 5 : 6], localCoordinates: true);
        }
        // Node12 is the replaced preset. Its old placeholder is absent
        //from the compact clips; the replacement owns its own state clips.
        int parent = source.Nodes[12].ParentIndex ?? throw new InvalidDataException("area-map.compact_face_parent");
        var faceMount = new Node2D { Name = "CompactFaceMount",
            Transform = source.NodeTransform(parent).AffineInverse() * source.NodeTransform(12) };
        AnimationPanes[parent].AddChild(faceMount);
        face = new AreaAdministrationMapFace { Name = "CompactFace" };
        faceMount.AddChild(face); face.Configure(repository, resources, portrait, color);
        caption = new UiSourceText { Name = "AreaName", Size = new(original.Nodes[11].Value44 + extra, original.Nodes[11].Value46) };
        AnimationPanes[11].AddChild(caption); caption.Configure(font, profile.WithPreset(MapTextPresets.Create()[19])); caption.Text = name;
        Hit = source.Rectangle(3);
        SetInteractionState(0);
    }

    internal void SetInteractionState(int sourceState)
    {
        int next = sourceState switch { 0 => 0, 1 => 1, 2 => 2, 4 => 3, _ => throw new ArgumentOutOfRangeException(nameof(sourceState)) };
        if (next == state) return;
        state = next; elapsed = 0; duration = curves[Names[state]].DurationSeconds; sampling = duration > 0;
        caption.Configure(font, profile.WithPreset(MapTextPresets.Create()[new[] { 19, 4, 5, 23 }[state]]));
        face.SetInteractionState(sourceState); ApplyAnimationAt(Names[state], 0, reveal: false);
    }
    public override void _Process(double delta)
    {
        if (!sampling) return;
        elapsed = Math.Min(elapsed + delta, duration); ApplyAnimationAt(Names[state], elapsed, reveal: false); sampling = elapsed < duration;
    }
}

internal partial class AreaAdministrationMapDevelopmentIcon : Control
{
    private readonly Control[] plates = new Control[3];
    private readonly Dictionary<(int Kind, int Palette), Control> images = [];
    private int previousKind = -1, previousPalette = -1;

    internal void Configure(AdministrationPageChrome.Resources resources)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var source = resources.Assets.SourceLayout(0x1F40C35D); Size = source.Rectangle(0).Size;
        for (int i = 0; i < 3; i++)
        {
            int parent = new[] { 1, 3, 7 }[i];
            var plate = new Control { Name = source.Nodes[parent].Name, Size = Size,
                MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            AddChild(plate); plates[i] = plate;
            foreach (int index in i switch { 0 => new[] { 2 }, 1 => new[] { 4, 5 }, _ => new[] { 8 } })
                source.Art(plate, index, source.Rectangle(index), resources.Assets, resources.SpriteShader,
                    blend: index == 5 ? UiSourceSprite.Blend.AlphaMask : UiSourceSprite.Blend.Ordinary);
            if (i == 1)
            {
                // The on-plate's final sweep consumes the alpha-only pane5.
                // It does not start the declared tantou_on animation by itself.
                Texture2D Texture(int index)
                {
                    var part = LcskSpriteNodeReader.Read(source.Source.Layout, source.Nodes[index]).Groups.Single().Parts.Single();
                    return resources.Assets.SrgbLayoutSpritePart(source.Source, part.SpriteName, part.Uv);
                }
                var box = source.Rectangle(6); var mask = source.Rectangle(5);
                var sweep = new UiSourceSprite { Name = "FocusedDevelopmentSweep", Position = box.Position, Size = box.Size };
                plate.AddChild(sweep);
                var texture = Texture(6);
                sweep.Configure(resources.SpriteShader, texture, UiSourceSprite.Blend.DestinationAlphaAdditive);
                sweep.SetClipping(texture, Texture(5), new(mask.Position - box.Position, mask.Size));
            }
        }
        var iconMount = new Control { Name = "DevelopmentIcon", Size = Size, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(iconMount);
        var palettes = MapSpritePalettes.Create();
        for (int kind = 0; kind < 3; kind++) foreach (int palette in new[] { 50, 51, 13 })
        {
            var art = source.Art(iconMount, 10, source.Rectangle(10), resources.Assets, resources.SpriteShader,
                colors: palettes[palette], commonSpriteOverride: AreaAdministrationMapButton.DevelopmentSprites[kind]);
            art.Visible = false; images.Add((kind, palette), art);
        }
    }
    internal void Apply(int kind, AreaAdministrationMapDevelopment values)
    {
        bool complete = AreaAdministrationMapButton.Known(values.IsComplete) ?? throw new InvalidDataException("area-map.development_complete");
        bool focused = AreaAdministrationMapButton.Known(values.IsFocused) ?? throw new InvalidDataException("area-map.development_focus");
        int palette = complete ? 13 : focused ? 51 : 50;
        if (kind == previousKind && palette == previousPalette) return;
        for (int i = 0; i < 3; i++) plates[i].Visible = i == (complete ? 2 : focused ? 1 : 0);
        foreach (var (key, art) in images) art.Visible = key == (kind, palette);
        previousKind = kind; previousPalette = palette;
    }
}
