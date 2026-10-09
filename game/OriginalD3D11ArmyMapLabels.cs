using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using NVector3 = System.Numerics.Vector3;

internal readonly record struct OriginalArmyMapLabelHit(int UnitId, int HexId, NVector3 Position,
    OriginalArmyMapHitGeometry Geometry);

/// <summary>Army Canvas population from one captured World and the native
/// Unit positions belonging to its prepared Main image. Child resources and
/// materials survive camera requests; logical fields refresh on World changes.</summary>
internal partial class OriginalD3D11ArmyMapLabels : Control
{
    private static readonly Vector2 CanonicalSize = new(1920, 1080);
    private readonly Node2D _root = new() { Name = "CanonicalArmyLabels" };
    private readonly Dictionary<int, OriginalArmyMapCard> _cards = [];
    private readonly Dictionary<(int Appearance, bool Group), ShaderMaterial> _portraits = [];
    private readonly Dictionary<(int Appearance, bool Group), string> _portraitUnavailable = [];
    private OriginalGameRepository _repository = null!;
    private OriginalUiAssets _assets = null!;
    private OriginalMapFont _font = null!;
    private OriginalMapFontProfile _numberFont = null!;
    private OriginalArmyMapMarkerSources _source = null!;
    private OriginalArmyMapUiSources _ui = null!;
    private OriginalArmyMapUiCurrentSource _currentSource = null!;
    private OriginalArmyNormalMarkerGeometry _geometry = null!;
    private OriginalArmyGroupMarkerGeometry _groupGeometry = null!;
    private OriginalAreaMapMarkerSources _colors = null!;
    private OriginalMapSpritePalettes _palettes = null!;
    private OriginalCityGateMapMarkerSources _bars = null!;
    private Texture2D _faceBase = null!, _faceFill = null!, _barFill = null!;
    private Texture2D _groupFaceBase = null!, _groupFaceFill = null!;
    private Texture2D _singleFlagBase = null!, _singleFlagOutline = null!, _singleFlagFill = null!;
    private Texture2D _groupFlagBase = null!, _groupFlagOutline = null!, _groupFlagFill = null!;
    private OriginalMapMarkerSlices _numberPanel = null!, _barPanel = null!;
    private Shader _portraitShader = null!;
    private IReadOnlyList<LcskSpriteNodePart> _portraitParts = null!;
    private IReadOnlyList<LcskSpriteNodePart> _groupPortraitParts = null!;
    private Func<GameState, OriginalEventInitializationContext?>? _queries;
    private float _nearDistance, _distanceRange;
    private GameState? _capturedWorld;
    private OriginalArmyMapUiCurrentFrame? _fields;
    private readonly Dictionary<int, OriginalArmyMapUiCurrentRow> _rows = [];
    private readonly Dictionary<int, OriginalArmySimpleOutlineResult> _simpleStyles = [];
    private OriginalMainMapCameraFrame? _preparedHitFrame, _acceptedHitFrame;
    private OriginalArmyMapLabelHit[] _preparedHits = [], _acceptedHits = [];
    private OriginalArmyMapHitGeometry _normalHit, _groupHit, _singleFlagHit, _groupFlagHit;

    internal OriginalArmyMapUiCurrentFrame? CurrentFields => _fields;
    internal IReadOnlyDictionary<int, OriginalArmyMapCard> Cards => _cards;

    internal void Configure(OriginalGameRepository repository, ScenarioCityPreview scenario,
        OriginalUiAssets assets, OriginalMapFont font, OriginalMapTextPresets presets,
        OriginalAreaMapMarkerSources colors, OriginalMapSpritePalettes palettes,
        OriginalCityGateMapMarkerSources bars, OriginalMapMarkerSlices barPanel,
        Func<GameState, OriginalEventInitializationContext?>? queries,
        OriginalMainMapCameraControllerParameters cameraParameters)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        AddChild(_root);
        _repository = repository; _assets = assets; _font = font; _queries = queries;
        _colors = colors; _palettes = palettes; _bars = bars; _barPanel = barPanel;
        _source = OriginalArmyMapMarkerSources.Load(repository);
        _ui = OriginalArmyMapUiSources.Create();
        _currentSource = new(scenario, repository.LoadTerrainTypes(scenario.FixedDataPath));
        _geometry = _source.NormalRestGeometry();
        _groupGeometry = _source.GroupRestGeometry();
        _normalHit = _source.SettledHitGeometry(false, false);
        _groupHit = _source.SettledHitGeometry(true, false);
        _singleFlagHit = _source.SettledHitGeometry(false, true);
        _groupFlagHit = _source.SettledHitGeometry(true, true);
        _nearDistance = cameraParameters.ZoomAnchor(0);
        _distanceRange = cameraParameters.ZoomAnchor(4);
        _numberFont = OriginalMapFontProfile.FromNode(_source.Normal.Nodes[0x32],
            _source.Normal.Source.ResourceUri).WithPreset(presets[0]);
        _font.Prepare(["0123456789"], _numberFont);
        _numberPanel = OriginalMapMarkerSlices.Load(_source.Normal, 0x29, assets);
        _faceBase = Sprite(_source.Face, 4);
        _faceFill = Sprite(_source.Face, 5);
        _groupFaceBase = Sprite(_source.Grouping, 4);
        _groupFaceFill = Sprite(_source.Grouping, 5);
        _singleFlagBase = assets.Sprite("000_cmn_prt_sign_unit_s_base").Texture;
        _singleFlagOutline = assets.Sprite("000_cmn_prt_sign_unit_s_outline").Texture;
        _singleFlagFill = assets.Sprite("000_cmn_prt_sign_unit_s").Texture;
        _groupFlagBase = assets.Sprite("000_cmn_prt_sign_gunzei_s_base").Texture;
        _groupFlagOutline = assets.Sprite("000_cmn_prt_sign_gunzei_s_outline").Texture;
        _groupFlagFill = assets.Sprite("000_cmn_prt_sign_gunzei_s").Texture;
        _barFill = assets.Sprite("000_cmn_bar").Texture;
        _portraitParts = LcskSpriteNodeReader.Read(_source.Face.Layout, _source.Face.Nodes[7]).Groups.Single().Parts;
        _groupPortraitParts = LcskSpriteNodeReader.Read(_source.Grouping.Layout, _source.Grouping.Nodes[0x14]).Groups.Single().Parts;
        ValidatePortrait(_portraitParts);
        ValidatePortrait(_groupPortraitParts);
        _portraitShader = OriginalMapPortraitQuad.CreateShader();
        Resized += UpdateCanonicalScale;
        UpdateCanonicalScale();
        SetMeta("source_army_label_layout_uri", _source.Normal.Source.ResourceUri);
        SetMeta("source_army_portrait_program", "CLayoutShader variant13/PS19, ordinary startup");
        SetMeta("army_label_scope", "normal settled cards and source Army groups; full interaction/action curves deferred");

        Texture2D Sprite(OriginalMapMarkerLayout layout, int index)
        {
            var part = LcskSpriteNodeReader.Read(layout.Layout, layout.Nodes[index]).Groups.Single().Parts.Single();
            return assets.SpritePart(part.SpriteName, part.Uv);
        }

        static void ValidatePortrait(IReadOnlyList<LcskSpriteNodePart> parts)
        {
            if (parts.Count != 3 || !parts[0].RawBytes[6..9].SequenceEqual(new byte[] { 0, 0, 0 }) ||
                !parts[1].RawBytes[6..9].SequenceEqual(new byte[] { 3, 1, 0 }) ||
                !parts[2].RawBytes[6..9].SequenceEqual(new byte[] { 3, 0, 0 }))
                throw new NotSupportedException("Army portrait requires its source multiply-RGB/min-then-max-alpha parts.");
        }
    }

    internal void Present(OriginalD3D11MapInput input, OriginalD3D11MapReceipt receipt)
    {
        var world = input.WorldState!;
        var frame = input.SourceFrame!;
        if (!ReferenceEquals(_capturedWorld, world))
        {
            _capturedWorld = world;
            _rows.Clear();
            _simpleStyles.Clear();
            var current = world.Units.Count > 0 ? _queries?.Invoke(world) : null;
            _fields = current is not null ? _currentSource.Capture(world, current) :
                new([], world.Units.Keys.Select(id => new OriginalArmyMapUiCurrentUnavailable(id, "current.World_query_owner")).ToArray());
            foreach (var row in _fields.Rows)
            {
                _rows.Add(row.UnitId, row);
                byte? districtRaw = row.PersonValid == false ? (byte)0 :
                    row.PersonValid == true ? row.PersonDistrictE0 : null;
                if (current is not null && districtRaw is byte raw)
                    _simpleStyles.Add(row.UnitId, OriginalArmySimpleOutlineSource.ReadOrdinaryMap(world,
                        current, current.ReadDistrictTableReference(raw <= 51 ? raw : 0)));
            }
            foreach (int id in _cards.Keys.Where(id => !_rows.ContainsKey(id)).ToArray())
            {
                _cards[id].Visible = false;
                _cards[id].QueueFree();
                _cards.Remove(id);
            }
        }
        foreach (var card in _cards.Values) card.Visible = false;
        var missing = new List<string>();
        int level = OriginalMapLabelPresentation.SettledCameraLevel(frame.ZoomSelection);
        if (level < 6)
            foreach (var native in receipt.ArmyUnitPositions)
            {
                if (!_rows.TryGetValue(native.UnitId, out var row) || row.ArmyValid != true) continue;
                bool group = false;
                if (level >= 3)
                {
                    var groups = input.ArmyGroupingWorld;
                    if (groups is null || row.ArmyPhysicalSlot is not int physical ||
                        groups.RootPhysicalSlot(physical, level - 3) is not int root ||
                        groups.IsValid(root) is not bool valid)
                    { missing.Add(row.UnitId + ":current.Army_group_root"); continue; }
                    if (valid)
                    {
                        if (groups.IsExcluded(root) is not bool excluded)
                        { missing.Add(row.UnitId + ":current.Army_group_root233560"); continue; }
                        if (!excluded)
                        {
                            //1ACFD0's settled other-root result hides the whole
                            //parent. Self roots retain their own CUnit position.
                            if (root != physical) continue;
                            group = true;
                            if (groups.ReadMembers(physical, level - 3) is null)
                                missing.Add(row.UnitId + ":current.Army_group_members");
                        }
                    }
                }
                NVector3 p = new(native.Position.X, native.Position.Y, native.Position.Z);
                var presentation = OriginalMapLabelPresentation.Army(frame, p);
                if (!presentation.Visible || !OriginalD3D11MapMarkers.TryProject(new(p.X, p.Y, p.Z),
                    frame.Camera.Matrices.WorldToProjection100, out var screen)) continue;
                //012780 casts the actual1920x1080 native viewport point to
                //Position20 integers before19E620 adds its screen offsets.
                screen = new((int)screen.X, (int)screen.Y);
                OriginalArmySimpleOutlineSelection? simpleStyle = null;
                OriginalMapCornerColors? simpleColors = null;
                if (presentation.Display == 2)
                {
                    if (!_simpleStyles.TryGetValue(row.UnitId, out var style) || style.Selection is not { } selectedStyle)
                    { missing.Add(row.UnitId + ":" + (style?.Missing ?? "current.Army_simple_District_pointer")); continue; }
                    simpleStyle = selectedStyle;
                    simpleColors = SimpleColors(selectedStyle.Primary);
                }
                if (!_cards.TryGetValue(row.UnitId, out var card))
                {
                    card = new(this) { Name = "Army_" + row.UnitId.ToString(CultureInfo.InvariantCulture) };
                    _root.AddChild(card);
                    _cards.Add(row.UnitId, card);
                }
                Color tint = row.SourceForceColor49 is byte color
                    ? CanvasColor(_colors.ForceFillColor(color)) : Colors.White;
                ShaderMaterial? portrait = presentation.Display != 2 && row.PersonValid == true && row.AppearanceA6 is ushort appearance
                    ? Portrait(appearance, missing, group) : null;
                Texture2D? icon = !group && presentation.Display != 2 && row.Icon is { } selected
                    ? _assets.Sprite(_ui.IconName(selected)).Texture : null;
                card.Present(row, p, presentation, portrait, icon, tint, group, simpleStyle, simpleColors);
                card.Position = presentation.Display == 2 ? screen : screen +
                    ToGodot(OriginalArmyMapMarkerSources.NormalScreenOffset(
                        frame.ControllerState.Distance90, _nearDistance, _distanceRange));
                card.Visible = true;
            }
        if (_fields is not null)
            missing.AddRange(_fields.Unavailable.Select(row => row.UnitId + ":" + row.Field));
        SetMeta("army_label_unavailable", string.Join(";", missing));
        SetMeta("army_label_card_count", _cards.Values.Count(card => card.Visible));
        PrepareHits(input);
    }

    private void PrepareHits(OriginalD3D11MapInput input)
    {
        var hits = new List<OriginalArmyMapLabelHit>();
        var missing = new List<int>();
        //Keep the actual child paint order, including retirement/reinsertion;
        //receipt roster and Dictionary enumeration need not match that order.
        foreach (OriginalArmyMapCard card in _root.GetChildren())
        {
            if (!card.Visible) continue;
            var unit = input.WorldState!.Units[card.Fields.UnitId];
            ushort? rawHex = unit.NativeMarch?.CurrentHex2A ?? unit.NativeCurrentHex2A;
            if (rawHex is not ushort hex) { missing.Add(unit.Id); continue; }
            int x = (int)card.Position.X, y = (int)card.Position.Y;
            if (card.IsSimple) { x -= 16; y -= 16; }
            var geometry = (card.IsSimple ? card.IsGroup ? _groupFlagHit : _singleFlagHit :
                card.IsGroup ? _groupHit : _normalHit).At(x, y);
            hits.Add(new(unit.Id, hex < 48400 ? hex : 0, card.UnitPosition, geometry));
        }
        _preparedHitFrame = input.SourceFrame;
        _preparedHits = hits.ToArray();
        SetMeta("army_label_hit_unavailable", string.Join(",", missing));
    }

    internal void AcceptPreparedHits()
    {
        //The surface publishes only after this prepared Canvas submission.
        //No card, later logical World or mutable group owner escapes it.
        _acceptedHitFrame = _preparedHitFrame;
        _acceptedHits = _preparedHits;
    }

    internal bool TryPick(Vector2 localPoint, OriginalMainMapCameraFrame displayedFrame,
        out OriginalArmyMapLabelHit hit)
    {
        hit = default;
        if (!IsVisibleInTree() || Size.X <= 0 || Size.Y <= 0 ||
            !ReferenceEquals(displayedFrame, _acceptedHitFrame)) return false;
        //GuiInput is already local to the same FullRect/aspect map display.
        //Mouse mapping truncates once, independently of projection's cast.
        Vector2 canonical = localPoint / Size * CanonicalSize;
        int x = (int)canonical.X, y = (int)canonical.Y;
        for (int i = _acceptedHits.Length - 1; i >= 0; i--)
            if (_acceptedHits[i].Geometry.Contains(x, y)) { hit = _acceptedHits[i]; return true; }
        return false;
    }

    private ShaderMaterial? Portrait(ushort appearance, List<string> missing, bool group = false)
    {
        var key = ((int)appearance, group);
        if (_portraits.TryGetValue(key, out var material)) return material;
        if (_portraitUnavailable.TryGetValue(key, out string? old)) { missing.Add(old); return null; }
        try
        {
            var names = _ui.PortraitNames(appearance);
            material = OriginalMapPortraitQuad.CreateMaterial(_repository, _portraitShader, _assets,
                names.SpriteName, group ? _groupPortraitParts : _portraitParts);
            _portraits.Add(key, material);
            return material;
        }
        catch (Exception error) when (error is InvalidDataException or KeyNotFoundException or NotSupportedException)
        {
            string reason = appearance + ":" + error.Message;
            _portraitUnavailable.Add(key, reason);
            missing.Add(reason);
            return null;
        }
    }

    private void UpdateCanonicalScale() => _root.Scale = Size / CanonicalSize;
    private OriginalMapCornerColors? SimpleColors(OriginalArmySimpleOutlineColorChoice choice)
    {
        return choice.Kind switch
        {
            OriginalArmySimpleOutlineColorKind.Preserve => null,
            OriginalArmySimpleOutlineColorKind.DistrictNumber => Uniform(_colors.DistrictColor((int)choice.Value)),
            OriginalArmySimpleOutlineColorKind.ForceFill => Uniform(_colors.ForceFillColor((int)choice.Value)),
            OriginalArmySimpleOutlineColorKind.SpritePalette => _palettes[(int)choice.Value],
            OriginalArmySimpleOutlineColorKind.PackedLiteral => Uniform(new(choice.Value, choice.Value)),
            _ => throw new NotSupportedException("Unknown original Army Simple color selector.")
        };
        static OriginalMapCornerColors Uniform(OriginalAreaMarkerColor color) => new(color, color, color, color);
    }
    private static Vector2 ToGodot(System.Numerics.Vector2 v) => new(v.X, v.Y);
    private static Rect2 Rectangle(OriginalAreaMarkerRectangle r) => new(r.Left, r.Top, r.Width, r.Height);
    private static Color Alpha(Color c, byte alpha) => new(c.R, c.G, c.B, c.A * (alpha / 255f));
    private static Color CanvasColor(OriginalAreaMarkerColor c) => new(c.AuthoredRed / 255f,
        c.AuthoredGreen / 255f, c.AuthoredBlue / 255f, c.AuthoredAlpha / 255f);
    private static void DrawColoredSprite(CanvasItem canvas, Texture2D texture, Rect2 rect,
        OriginalMapCornerColors colors, byte alpha, float rgbMultiplier = 1)
    {
        var atlas = (AtlasTexture)texture;
        Rect2 uv = new(atlas.Region.Position / atlas.Atlas.GetSize(), atlas.Region.Size / atlas.Atlas.GetSize());
        canvas.DrawPolygon([rect.Position, new(rect.End.X, rect.Position.Y), rect.End, new(rect.Position.X, rect.End.Y)],
            [Tint(colors.LT), Tint(colors.RT), Tint(colors.RB), Tint(colors.LB)],
            [uv.Position, new(uv.End.X, uv.Position.Y), uv.End, new(uv.Position.X, uv.End.Y)], atlas.Atlas);
        Color Tint(OriginalAreaMarkerColor color)
        {
            Color c = Alpha(CanvasColor(color), alpha);
            return new(c.R * rgbMultiplier, c.G * rgbMultiplier, c.B * rgbMultiplier, c.A);
        }
    }

    internal partial class OriginalArmyMapCard : Node2D
    {
        private readonly OriginalD3D11ArmyMapLabels _owner;
        private readonly OriginalMapPortraitQuad _portrait = new() { Name = "Portrait" };
        private readonly ArmyDetails _details;
        internal OriginalArmyMapUiCurrentRow Fields { get; private set; } = null!;
        internal NVector3 UnitPosition { get; private set; }
        internal OriginalArmyMapLabelPresentation Presentation { get; private set; }
        internal Texture2D? Icon { get; private set; }
        internal Color ForceTint { get; private set; }
        internal int? MoraleFillWidth { get; private set; }
        internal bool PortraitReady => _portrait.Visible;
        internal bool IsGroup { get; private set; }
        internal bool IsSimple => Presentation.Display == 2;
        internal bool SimpleUsesOutline { get; private set; }
        internal OriginalMapCornerColors SimpleTints { get; private set; } = new(
            new(uint.MaxValue, uint.MaxValue), new(uint.MaxValue, uint.MaxValue),
            new(uint.MaxValue, uint.MaxValue), new(uint.MaxValue, uint.MaxValue));
        private float _simpleRgbMultiplier = 1;

        internal OriginalArmyMapCard(OriginalD3D11ArmyMapLabels owner)
        {
            _owner = owner;
            _details = new(this) { Name = "Details" };
            AddChild(_portrait);
            AddChild(_details);
        }
        internal void Present(OriginalArmyMapUiCurrentRow row, NVector3 position,
            OriginalArmyMapLabelPresentation presentation, ShaderMaterial? material, Texture2D? icon, Color tint,
            bool group = false, OriginalArmySimpleOutlineSelection? simpleStyle = null, OriginalMapCornerColors? simpleColors = null)
        {
            Fields = row; UnitPosition = position; Presentation = presentation; Icon = icon; ForceTint = tint;
            IsGroup = group;
            SimpleUsesOutline = simpleStyle?.UsesOutline == true;
            if (simpleColors is { } colors)
            { SimpleTints = colors; _simpleRgbMultiplier = simpleStyle!.Primary.RgbMultiplier; }
            _details.Visible = !group && !IsSimple;
            _portrait.Visible = !IsSimple && material is not null;
            if (material is not null)
            {
                _portrait.Configure(Rectangle(group ? _owner._groupGeometry.Portrait : _owner._geometry.Portrait), material);
                _portrait.Modulate = Alpha(Colors.White, group ? presentation.GroupAlpha : presentation.PortraitAlpha);
                _portrait.QueueRedraw();
            }
            QueueRedraw(); _details.QueueRedraw();
        }
        public override void _Draw()
        {
            if (IsSimple)
            {
                Rect2 flag = Rectangle(OriginalArmyMapMarkerSources.SimpleGeometry(IsGroup).Flag);
                Texture2D basis = SimpleUsesOutline
                    ? IsGroup ? _owner._groupFlagOutline : _owner._singleFlagOutline
                    : IsGroup ? _owner._groupFlagBase : _owner._singleFlagBase;
                DrawColoredSprite(this, basis, flag, SimpleTints, Presentation.SimpleAlpha, _simpleRgbMultiplier);
                DrawTextureRect(IsGroup ? _owner._groupFlagFill : _owner._singleFlagFill,
                    flag, false, Alpha(ForceTint, Presentation.SimpleAlpha));
                return;
            }
            byte alpha = IsGroup ? Presentation.GroupAlpha : Presentation.PortraitAlpha;
            DrawTextureRect(IsGroup ? _owner._groupFaceBase : _owner._faceBase,
                Rectangle(IsGroup ? _owner._groupGeometry.FaceBase : _owner._geometry.FaceBase), false, Alpha(Colors.White, alpha));
            DrawTextureRect(IsGroup ? _owner._groupFaceFill : _owner._faceFill,
                Rectangle(IsGroup ? _owner._groupGeometry.FaceFill : _owner._geometry.FaceFill), false, Alpha(ForceTint, alpha));
        }

        private partial class ArmyDetails(OriginalArmyMapCard card) : Node2D
        {
            public override void _Draw()
            {
                var owner = card._owner;
                var textDraw = owner._font.BeginDraw(this);
                byte alpha = card.Presentation.DetailsAlpha;
                if (card.Icon is { } icon && card.Fields.Icon is { } choice)
                    DrawTextureRect(icon, Rectangle(owner._geometry.Icon), false,
                        Alpha(CanvasColor(owner._palettes[choice.Palette].LT), alpha));
                if (card.Presentation.Display == 0)
                {
                    owner._numberPanel.Draw(this, Rectangle(owner._geometry.NumberPanel),
                        Alpha(CanvasColor(owner._palettes[4].LT), alpha));
                    if (card.Fields.Ready16 is ushort ready)
                    {
                        Rect2 cell = Rectangle(owner._geometry.NumberCell);
                        owner._font.Draw(this, ready.ToString(CultureInfo.InvariantCulture), cell.Position +
                            new Vector2(0, owner._font.CellBaselineY(owner._numberFont, cell.Size.Y)),
                            owner._numberFont, HorizontalAlignment.Right, cell.Size.X, Alpha(Colors.White, alpha));
                    }
                }
                if (card.Fields.EffectiveMorale is int morale && card.Fields.MoraleCapacity is int cap)
                {
                    int style = morale >= owner._palettes.MoraleStyleThreshold ? 3 : 2;
                    var bar = owner._bars.BarGeometry(morale, cap, owner._geometry.Morale, card.MoraleFillWidth);
                    card.MoraleFillWidth = bar.SourceFillWidth;
                    owner._barPanel.Draw(this, Rectangle(bar.Background), Alpha(Colors.White, alpha), bar.ScaleX, bar.ScaleY);
                    if (bar.SourceFillWidth > 0)
                        DrawColoredSprite(this, owner._barFill, Rectangle(bar.Fill), owner._palettes[style], alpha);
                }
                textDraw.End();
            }
        }
    }

    public override void _ExitTree()
    {
        foreach (var material in _portraits.Values) material.Dispose();
        _portraits.Clear();
        _portraitShader?.Dispose();
    }
}
