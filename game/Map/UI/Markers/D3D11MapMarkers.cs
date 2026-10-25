using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using NMatrix = System.Numerics.Matrix4x4;
using NVector3 = System.Numerics.Vector3;
using NVector4 = System.Numerics.Vector4;

internal sealed record D3D11MapLocation(int Id, bool IsGate, string Name,
    int HexId, Vector3 Position);

/// <summary>Original UI sprites over the accepted native Main image. World
/// positions and text change with the owned campaign; the same Main matrix
/// that produced the visible terrain projects the labels. No Godot camera or
/// hidden 3D preview participates in this Canvas layer.</summary>
internal partial class D3D11MapMarkers : Control
{
    private enum Kind { City, Gate, Area }
    private sealed class Marker(Kind kind, int id, int hex, Vector3 position)
    {
        public Kind Kind { get; } = kind;
        public int Id { get; } = id;
        public int Hex { get; } = hex;
        public Vector3 Position { get; set; } = position;
        public bool PositionReady { get; set; }
        public NVector3 AreaUnitPosition { get; set; }
        public AreaMapLabelPresentation AreaPresentation { get; set; }
        public NVector3 CityUnitPosition { get; set; }
        public CityGateMapLabelPresentation CityPresentation { get; set; }
        public string Name { get; set; } = "";
        public string Troops { get; set; } = "";
        public string FlagGlyph { get; set; } = "";
        public Color FlagLetterColor { get; set; }
        public int? Force { get; set; }
        public int? DurabilityCurrent { get; set; }
        public int? DurabilityCapacity { get; set; }
        public byte? Morale44 { get; set; }
        public int? MoraleCapacity { get; set; }
        public int? HpFillWidth { get; set; }
        public int? FullMoraleFillWidth { get; set; }
        public int? SimpleMoraleFillWidth { get; set; }
        public Color Tint { get; set; } = Colors.White;
        public int? DistrictForce { get; set; }
        public Color DistrictTint { get; set; }
        public Color? OutlineTint { get; set; }
        public bool HasHexOwner { get; set; }
        public Vector2 Screen { get; set; }
        public bool OnScreen { get; set; }
    }

    private static readonly Vector2 CanonicalSize = new(1920, 1080);
    private readonly List<D3D11MapLocation> _locations = [];
    private readonly Dictionary<(Kind, int), Marker> _markers = [];
    private MapFont? _font;
    private MapFontProfile? _locationFont, _numberFont, _simpleNumberFont, _cityGlyphFont, _areaFont;
    private MapFontProfile[] _locationStates = [], _simpleNumberStates = [], _cityGlyphStates = [];
    private CityGateMapMarkerSources? _citySources;
    private MapSpritePalettes? _spritePalettes;
    private MapTextPresets? _textPresets;
    private D3D11ArmyMapLabels? _armyLabels;
    private AreaMapMarkerSources? _areaSources;
    private AreaMarkerRestGeometry? _areaLayout;
    private D3D11AreaCapitalPublication? _areaPublication;
    private D3D11CityGatePublication? _cityPublication;
    private readonly Dictionary<int, Color> _areaForceColors = [];
    private int? _playerForce;
    private UiAssets? _assets;
    private bool _ownsAssets;
    private MainMapCameraFrame? _frame;
    private Texture2D[] _cityFlag = [], _gateFlag = [], _areaFlag = [];
    private Texture2D? _soldier, _barFill;
    private MapMarkerSlices? _namePanel, _numberPanel, _barPanel;
    private int? _selectedHex;
    private int? _hoveredHex, _pressedHex;
    private bool _baseInformationVisible = true, _armyInformationVisible = true;

    internal void SetInformationVisibility(bool bases, bool armies, int informationMode)
    {
        if (_baseInformationVisible == bases && _armyInformationVisible == armies && foodWarningInformationMode == informationMode) return;
        foodWarningInformationMode = informationMode;
        _baseInformationVisible = bases; _armyInformationVisible = armies;
        if (_armyLabels is not null) _armyLabels.Visible = armies && campaignMarkerHexes is null;
        if (!bases) ClearPointer();
        PositionFoodWarnings();
        QueueRedraw();
    }

    internal void UpdatePointer(Vector2 point, MainMapCameraFrame frame, bool? pressed = null)
    {
        int? hit = TryPickMarker(point, frame, out int hex) ? hex : null;
        if (pressed == true) _pressedHex = hit;
        else if (pressed == false) _pressedHex = null;
        if (_hoveredHex == hit && pressed is null) return;
        _hoveredHex = hit;
        PositionFoodWarnings();
        QueueRedraw();
    }

    internal void ClearPointer()
    {
        _hoveredHex = _pressedHex = null;
        PositionFoodWarnings();
        QueueRedraw();
    }

    internal bool TryGetCityMenuAnchor(int cityId, out Vector2 anchor)
    {
        anchor = default;
        if (!_baseInformationVisible || !_markers.TryGetValue((Kind.City, cityId), out var marker) || !marker.OnScreen) return false;
        anchor = (marker.Screen + new Vector2(19, -44)) * Size / CanonicalSize;
        return true;
    }

    internal IReadOnlyList<D3D11MapLocation> Locations => _locations;
    internal D3D11ArmyLabelPublication? CaptureArmyLabels(GameState world) => _armyLabels?.CaptureWorld(world);

    internal void Configure(GameResourceRepository repository, GameState world,
        MapWorldProjection projection, LoadedTerrainResources terrain,
        IReadOnlyDictionary<int, Color>? markerColors,
        string locale, int? playerForceId, ScenarioCityPreview scenario,
        UiAssets? sharedAssets = null, LoadedLayout? sharedLayout = null,
        IReadOnlyDictionary<int, int>? durabilityCapacities = null)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        _assets = sharedAssets ?? new UiAssets(repository);
        _ownsAssets = sharedAssets is null;
        _citySources = CityGateMapMarkerSources.Load(repository);
        var layout = _citySources.Full;
        _playerForce = playerForceId;
        _areaSources = AreaMapMarkerSources.LoadCurrentTc(repository);
        _areaLayout = _areaSources.NormalRestGeometry();
        var textPresets = MapTextPresets.Create();
        _textPresets = textPresets;
        _spritePalettes = MapSpritePalettes.Create();
        _font = new(repository, locale);
        _locationFont = MapFontProfile.FromNode(_citySources.Simple.Nodes[14], _citySources.Simple.Source.ResourceUri).WithPreset(textPresets[0]);
        _numberFont = MapFontProfile.FromNode(layout.Nodes[78], layout.Source.ResourceUri).WithPreset(textPresets[19]);
        // 1A7220 -> 931F20(3) sets the current Simple numeric line arrangement.
        _simpleNumberFont = (MapFontProfile.FromNode(_citySources.Simple.Nodes[13], _citySources.Simple.Source.ResourceUri) with { RawLineArrange = 1 }).WithPreset(textPresets[0]);
        _cityGlyphFont = MapFontProfile.FromNode(_citySources.CityFlag.Nodes[18], _citySources.CityFlag.Source.ResourceUri);
        _locationStates = [_locationFont, _locationFont.WithPreset(textPresets[4]), _locationFont.WithPreset(textPresets[5])];
        _simpleNumberStates = [_simpleNumberFont, _simpleNumberFont.WithPreset(textPresets[4]), _simpleNumberFont.WithPreset(textPresets[5])];
        _cityGlyphStates = [_cityGlyphFont, _cityGlyphFont.WithPreset(textPresets[4]), _cityGlyphFont.WithPreset(textPresets[5])];
        _areaFont = MapFontProfile.FromNode(_areaSources.Text.Nodes.Single(node => node.Name == "val_fc020"), _areaSources.Text.Source.ResourceUri).WithPreset(textPresets[19]);
        _font.Prepare(["0123456789/ "], _numberFont);
        _font.Prepare(["0123456789/ "], _simpleNumberFont);
        foreach (var force in scenario.Forces)
            if (force.RawReportColor49 is byte index)
                _areaForceColors[force.Slot] = CanvasColor(_areaSources.ForceFillColor(index));
        _cityFlag = Sprites("000_cmn_prt_flag_default_base", "000_cmn_prt_flag_default_outline", "000_cmn_prt_flag_default");
        _gateFlag = Sprites("000_cmn_prt_flag_seki_base", "000_cmn_prt_flag_seki_outline", "000_cmn_prt_flag_seki");
        _areaFlag = Sprites("000_cmn_prt_flag_s_outline", "000_cmn_prt_flag_s");
        _soldier = _assets.Sprite("000_cmn_icon_map_heishi_fuchi").Texture;
        _barFill = _assets.Sprite("000_cmn_bar").Texture;
        _namePanel = MapMarkerSlices.Load(_citySources.Simple, 3, _assets);
        _numberPanel = MapMarkerSlices.Load(layout, 67, _assets);
        _barPanel = MapMarkerSlices.Load(_citySources.Bar, 1, _assets);
        RefreshWorld(world, markerColors, durabilityCapacities);
        SetMeta("source_marker_layout_uri", layout.Source.ResourceUri);
        SetMeta("source_marker_layout_sha256", layout.Source.DecodedSha256);
        SetMeta("map_marker_projection", "accepted-native-main-world-to-projection-100");
        SetMeta("source_area_marker_flag_layout_uri", _areaSources.Flag.Source.ResourceUri);
        SetMeta("source_area_marker_name_layout_uri", _areaSources.Text.Source.ResourceUri);
        SetMeta("source_city_marker_name_layout_uri", _citySources.Simple.Source.ResourceUri);
        SetMeta("source_city_marker_flag_layout_uri", _citySources.CityFlag.Source.ResourceUri);
        SetMeta("source_gate_marker_flag_layout_uri", _citySources.Gate.Source.ResourceUri);
        SetMeta("source_map_font_uri", _font.Source.Resource.ResourceUri);
        SetMeta("source_map_font_locale", _font.Source.Locale);
        SetMeta("source_area_marker_colour_domain", "authored-rgba-for-non-hdr-canvas");
        // Sprite and panel resources are original. Main-map local placement,
        // zoom visibility and bar semantics have not received a pixel audit.
        SetMeta("original_main_map_marker_layout_verified", false);
        SetMeta("map_bar_binding", "same-frame-current-Object14-and-Base-capacity");

        Texture2D[] Sprites(params string[] names) =>
            names.Select(name => (Texture2D)_assets.Sprite(name).Texture).ToArray();
    }

    internal void ConfigureArmyLabels(GameResourceRepository repository, ScenarioCityPreview scenario,
        Func<GameState, SourceWorldContext?>? currentQueries,
        MainMapCameraControllerParameters cameraParameters)
    {
        _armyLabels = new() { Name = "NativeArmyMapLabels", Visible = _armyInformationVisible };
        AddChild(_armyLabels);
        _armyLabels.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _armyLabels.Configure(repository, scenario, _assets!, _font!, _textPresets!, _areaSources!,
            _spritePalettes!, _citySources!, _barPanel!, currentQueries, cameraParameters);
    }

    internal void PresentArmy(D3D11MapInput input, D3D11MapReceipt receipt)
        => _armyLabels?.Present(input, receipt);

    internal void AcceptArmyHits() => _armyLabels?.AcceptPreparedHits();
    internal bool ArmySpeechManagerVisible => _armyLabels?.IsVisibleInTree() == true;
    internal bool HasArmySpeechLayout(int unitId) => _armyLabels?.HasSpeechLayout(unitId) == true;

    internal bool TryPickArmy(Vector2 localPoint, MainMapCameraFrame displayedFrame,
        out ArmyMapLabelHit hit)
    {
        hit = default;
        return campaignMarkerHexes is null && _armyLabels?.TryPick(localPoint, displayedFrame, out hit) == true;
    }

    internal void RefreshWorld(GameState world, IReadOnlyDictionary<int, Color>? markerColors,
        IReadOnlyDictionary<int, int>? durabilityCapacities = null)
    {
        var live = new HashSet<(Kind, int)>();
        _locations.Clear();
        foreach (CityState city in world.Cities.Values.OrderBy(city => city.Id))
        {
            Update(Kind.City, city.Id, city.HexId, city.Name, city.ForceId, city.Troops);
            SetDistrict(Kind.City, city.Id, city.DistrictId);
        }
        foreach (GateState gate in world.Gates.Values.OrderBy(gate => gate.Id))
        {
            Update(Kind.Gate, gate.Id, gate.HexId, gate.Name, gate.ForceId, gate.Troops);
            SetDistrict(Kind.Gate, gate.Id, gate.DistrictId);
        }
        HashSet<int> occupied = _locations.Select(location => location.HexId).ToHashSet();
        foreach (AreaState area in world.Areas.Values.OrderBy(area => area.Id))
            if (area.HexId is int hex && !occupied.Contains(hex))
            {
                Update(Kind.Area, area.Id, hex, area.Name, area.ForceId, 0);
                var marker = _markers[(Kind.Area, area.Id)];
                DistrictState? district = area.CityId is int cityId && world.Cities.TryGetValue(cityId, out var city) &&
                    city.DistrictId is int id && world.Districts.TryGetValue(id, out var current) ? current : null;
                marker.DistrictForce = district?.ForceId;
                marker.DistrictTint = district is not null ? CanvasColor(_areaSources!.DistrictColor(district.Number)) : Colors.White;
                RefreshAreaColour(marker);
            }
        foreach (var key in _markers.Keys.Where(key => !live.Contains(key)).ToArray())
            _markers.Remove(key);
        _font!.Prepare(_markers.Values.Where(marker => marker.Kind != Kind.Area).Select(marker => marker.Name), _locationFont!);
        _font.Prepare(_markers.Values.Where(marker => marker.Kind == Kind.Area).Select(marker => marker.Name), _areaFont!);
        if (_frame is { } frame) ProjectMarkers(frame);
        QueueRedraw();

        void SetDistrict(Kind kind, int id, int? districtId)
        {
            if (!_markers.TryGetValue((kind, id), out var marker)) return;
            DistrictState? district = districtId is int currentId ? world.Districts.GetValueOrDefault(currentId) : null;
            marker.DistrictForce = district?.ForceId;
            marker.DistrictTint = district is not null ? CanvasColor(_areaSources!.DistrictColor(district.Number)) : Colors.White;
            marker.OutlineTint = AreaMapMarkerSources.ShowPlayerDistrictOutline(marker.DistrictForce, _playerForce)
                ? marker.DistrictTint : null;
        }

        void Update(Kind kind, int id, int? hex, string name, int? force, int troops)
        {
            if (hex is not int hexId) return;
            var key = (kind, id);
            live.Add(key);
            if (!_markers.TryGetValue(key, out Marker? marker) || marker.Hex != hexId)
            {
                // Every marker takes its actual Unit position in Present.
                marker = new(kind, id, hexId, Vector3.Zero);
                _markers[key] = marker;
            }
            marker.Name = name;
            marker.Troops = troops.ToString(CultureInfo.InvariantCulture);
            marker.Force = force;
            // An unavailable force palette stays neutral; no generated HSV
            // palette or guessed ruler-first-character is published as source.
            marker.Tint = force is int owner && markerColors is not null && markerColors.TryGetValue(owner, out Color color)
                ? color : Colors.White;
            if (kind != Kind.Area) _locations.Add(new(id, kind == Kind.Gate, name, hexId,
                new(marker.CityUnitPosition.X, marker.CityUnitPosition.Y, marker.CityUnitPosition.Z)));
        }
    }

    internal void Present(MainMapCameraFrame frame, D3D11AreaCapitalPublication? areas,
        D3D11CityGatePublication? cities)
    {
        _frame = frame;
        if (!ReferenceEquals(_cityPublication, cities))
        {
            _cityPublication = cities;
            foreach (Marker marker in _markers.Values.Where(marker => marker.Kind != Kind.Area)) marker.PositionReady = false;
            if (cities is not null)
            {
                var damage = cities.Source.Damage.ToDictionary(row => row.Identity);
                var glyphs = cities.Source.Accessories.ToDictionary(row => row.Index);
                foreach (var owner in cities.Source.Primary.Owners)
                {
                    Kind kind = owner.CitySelection is not null ? Kind.City : Kind.Gate;
                    if (!_markers.TryGetValue((kind, owner.PhysicalSlot), out var marker)) continue;
                    var p = owner.Instance.SourceWorldState!.Position18;
                    marker.CityUnitPosition = p;
                    marker.Position = new(p.X, p.Y + CityGateMapMarkerSources.WorldHeightOffset, p.Z);
                    marker.PositionReady = true;
                    string key = (kind == Kind.City ? "city:" : "gate:") + owner.PhysicalSlot;
                    var current = damage[key];
                    marker.DurabilityCurrent = current.Hp;
                    marker.DurabilityCapacity = current.Capacity;
                    marker.Morale44 = current.Morale44;
                    marker.MoraleCapacity = current.MoraleCapacity;
                    marker.Force = current.Force;
                    var material = glyphs[owner.Index].Material;
                    marker.FlagGlyph = kind == Kind.City && material is { } flag ? ((char)flag.Glyph).ToString() : "";
                    marker.Tint = material is { } color ? CanvasColor(_areaSources!.ForceFillColor(color.Color49)) : Colors.White;
                    marker.FlagLetterColor = CanvasColor(_spritePalettes!.ForceLetter(material?.Color49 ?? 61));
                }
                _font!.Prepare(_markers.Values.Select(marker => marker.FlagGlyph), _cityGlyphFont!);
                for (int i = 0; i < _locations.Count; i++)
                {
                    var location = _locations[i];
                    var marker = _markers[(location.IsGate ? Kind.Gate : Kind.City, location.Id)];
                    var p = marker.CityUnitPosition;
                    _locations[i] = location with { Position = new(p.X, p.Y, p.Z) };
                }
            }
        }
        if (!ReferenceEquals(_areaPublication, areas))
        {
            _areaPublication = areas;
            foreach (Marker marker in _markers.Values.Where(marker => marker.Kind == Kind.Area)) marker.PositionReady = false;
            if (areas is not null)
                foreach (var owner in areas.Source.Source.Owners)
                    if (_markers.TryGetValue((Kind.Area, owner.NativeAreaSlot), out var marker))
                    {
                        var p = owner.Primary.SourceWorldState!.Position18;
                        marker.AreaUnitPosition = new(p.X, p.Y, p.Z);
                        marker.Position = new(p.X, p.Y + AreaMapMarkerSources.WorldHeightOffset, p.Z);
                        marker.PositionReady = true;
                        // 22CD40 tests Hex14 to select Base->District. The UI
                        // fill itself reads that District's Force.Color49.
                        marker.HasHexOwner = owner.CurrentForce != 0;
                        RefreshAreaColour(marker);
                    }
        }
        ProjectMarkers(frame);
        QueueRedraw();
    }

    private void RefreshAreaColour(Marker marker)
    {
        int force = marker.HasHexOwner ? marker.DistrictForce ?? 0 : 0;
        marker.Tint = _areaForceColors.GetValueOrDefault(force, Colors.White);
        marker.OutlineTint = marker.HasHexOwner && AreaMapMarkerSources.ShowPlayerDistrictOutline(marker.DistrictForce, _playerForce)
            ? marker.DistrictTint : null;
    }

    private static Color CanvasColor(AreaMarkerColor source) => new(source.AuthoredRed / 255f,
        source.AuthoredGreen / 255f, source.AuthoredBlue / 255f, source.AuthoredAlpha / 255f);

    internal void Select(int index)
    {
        _selectedHex = _locations[index].HexId;
        QueueRedraw();
    }

    internal void ClearSelection()
    {
        _selectedHex = null;
        QueueRedraw();
    }

    internal bool TryPickMarker(Vector2 localPoint, MainMapCameraFrame displayedFrame, out int hexId)
    {
        hexId = -1;
        if (!_baseInformationVisible && campaignMarkerHexes is null || !IsVisibleInTree() || Size.X <= 0 || Size.Y <= 0) return false;
        Vector2 cursor = localPoint / Size * CanonicalSize;
        // Hit the image currently on screen, even when a newer GPU request is
        // waiting for its Canvas submission. Reverse matches the draw order.
        foreach (var marker in _markers.Values.Reverse())
        {
            if (!marker.PositionReady || !CampaignMarkerVisible(marker)) continue;
            AreaMapLabelPresentation areaPresentation = default;
            if (marker.Kind == Kind.Area)
            {
                areaPresentation = MapLabelPresentation.Area(displayedFrame, marker.AreaUnitPosition);
                if (!areaPresentation.Visible) continue;
            }
            else if (!MapLabelPresentation.CityGate(displayedFrame, marker.CityUnitPosition).Visible) continue;
            if (!TryProject(marker.Position, displayedFrame.Camera.Matrices.WorldToProjection100, out var p)) continue;
            var rect = marker.Kind == Kind.Area ?
                AreaHitRectangle(marker, p, areaPresentation.ShowName) :
                AreaRectangle(CityGateMapMarkerSources.NormalHitRectangle(
                    MapLabelPresentation.CityGate(displayedFrame, marker.CityUnitPosition).Display,
                    marker.Troops, marker.Kind == Kind.Gate), p);
            // Original86FFE0 includes both edges. Godot Rect2.HasPoint
            // excludes right/bottom, so keep the actual source tester.
            bool hit = cursor.X >= rect.Position.X && cursor.Y >= rect.Position.Y &&
                cursor.X <= rect.End.X && cursor.Y <= rect.End.Y;
            if (hit) { hexId = marker.Hex; return true; }
        }
        return false;
    }

    private void ProjectMarkers(MainMapCameraFrame frame)
    {
        var matrix = frame.Camera.Matrices.WorldToProjection100;
        foreach (Marker marker in _markers.Values)
        {
            bool visible = TryProject(marker.Position, matrix, out Vector2 screen) && marker.PositionReady;
            if (marker.Kind == Kind.Area)
            {
                marker.AreaPresentation = MapLabelPresentation.Area(frame, marker.AreaUnitPosition);
                visible &= marker.AreaPresentation.Visible;
            }
            else
            {
                marker.CityPresentation = MapLabelPresentation.CityGate(frame, marker.CityUnitPosition);
                visible &= marker.CityPresentation.Visible;
            }
            marker.Screen = screen;
            marker.OnScreen = visible && screen.X > -150 && screen.X < 2070 &&
                screen.Y > -100 && screen.Y < 1180;
        }
        PositionFoodWarnings();
    }

    internal static bool TryProject(Vector3 world, NMatrix worldToProjection, out Vector2 canonicalPixel)
    {
        // The native Camera stores row-vector matrices. D3D viewport mapping
        // inverts NDC Y and clips depth to 0..W, including the near plane.
        NVector4 clip = NVector4.Transform(new NVector4(world.X, world.Y, world.Z, 1), worldToProjection);
        canonicalPixel = default;
        if (clip.W <= 0 || clip.Z < 0 || clip.Z > clip.W) return false;
        canonicalPixel = new((clip.X / clip.W + 1) * 960, (1 - clip.Y / clip.W) * 540);
        return true;
    }

    public override void _Draw()
    {
        if (_frame is null || _namePanel is null || _numberPanel is null || _barPanel is null || _font is null) return;
        var textDraw = _font.BeginDraw(this, Size / CanonicalSize);
        var spritePalettes = _spritePalettes!;
        DrawSetTransform(Vector2.Zero, 0, Size / CanonicalSize);
        if (!_baseInformationVisible && campaignMarkerHexes is null) { textDraw.End(); return; }
        foreach (Marker marker in _markers.Values)
        {
            if (!marker.OnScreen || !CampaignMarkerVisible(marker)) continue;
            Vector2 p = marker.Screen;
            if (marker.Kind == Kind.Area)
            {
                Rect2 icon = AreaRectangle(_areaLayout!.FlagRectangle, p);
                var presentation = marker.AreaPresentation;
                if (marker.OutlineTint is Color border) DrawTextureRect(_areaFlag[0], icon, false, Alpha(border, presentation.FlagAlpha));
                DrawTextureRect(_areaFlag[1], icon, false, Alpha(marker.Tint, presentation.FlagAlpha));
                if (presentation.ShowName)
                {
                    Rect2 cell = AreaRectangle(_areaLayout.NameCellRectangle, p);
                    _font.Draw(this, marker.Name, cell.Position + new Vector2(0, _font.CellBaselineY(_areaFont!, cell.Size.Y)), _areaFont!,
                        modulate: Alpha(Colors.White, presentation.TextAlpha));
                }
                continue;
            }
            var city = marker.CityPresentation;
            int interaction = campaignSelectedHexes?.Contains(marker.Hex)==true ? 2 :
                _hoveredHex == marker.Hex ? (_pressedHex == marker.Hex ? 2 : 1) : 0;
            bool numbers = city.Display == 1;
            string simpleText = numbers ? marker.Troops : marker.Name;
            var geometry = CityGateMapMarkerSources.SimpleGeometry(simpleText, numbers, marker.Kind == Kind.Gate);
            Rect2 cityFlag = AreaRectangle(geometry.Flag, p);
            Color panelTint = Alpha(Colors.White, city.PanelAlpha);
            // The name plate extends beneath the force flag. Paint its
            // background first so it cannot cover the flag or its glyph.
            if (city.Display != 2)
                _namePanel.Draw(this, AreaRectangle(geometry.Plate, p), spritePalettes[4 + interaction], city.PanelAlpha);
            if (marker.Kind == Kind.City)
            {
                // 1A64B0 replaces the same outline slot with24D base or24E
                // player outline. They are not two simultaneous white layers.
                Texture2D outlineTexture = _cityFlag[marker.OutlineTint is null ? 0 : 1];
                if (interaction == 0) DrawTextureRect(outlineTexture, cityFlag, false,
                    Alpha(marker.OutlineTint ?? Colors.White, city.FlagAlpha));
                else PaletteSprite(outlineTexture, cityFlag, spritePalettes[4 + interaction], city.FlagAlpha);
                if (interaction == 0) DrawTextureRect(_cityFlag[2], cityFlag, false, Alpha(marker.Tint, city.FlagAlpha));
                else PaletteSprite(_cityFlag[2], cityFlag, spritePalettes[4 + interaction], city.FlagAlpha);
                if (marker.FlagGlyph.Length != 0)
                    Text(marker.FlagGlyph, new(-12, -30, 24, 24), _cityGlyphStates[interaction], HorizontalAlignment.Center, city.FlagAlpha,
                        interaction == 0 ? marker.FlagLetterColor : Colors.White);
            }
            else
            {
                DrawTextureRect(_gateFlag[0], cityFlag, false, Alpha(Colors.White, city.FlagAlpha));
                if (marker.OutlineTint is Color outline)
                    DrawTextureRect(_gateFlag[1], cityFlag, false, Alpha(outline, city.FlagAlpha));
                DrawTextureRect(_gateFlag[2], cityFlag, false, Alpha(marker.Tint, city.FlagAlpha));
            }
            if (city.Display == 2) continue;
            Text(simpleText, geometry.TextCell, numbers ? _simpleNumberStates[interaction] : _locationStates[interaction],
                numbers ? HorizontalAlignment.Left : HorizontalAlignment.Center, city.PanelAlpha);
            if (city.Display == 0)
            {
                _numberPanel.Draw(this, new(p + new Vector2(-6, -1), new Vector2(98, 24)), panelTint);
                DrawTextureRect(_soldier!, new(p + new Vector2(-7, -2), new Vector2(24, 24)), false,
                    Alpha(CanvasColor(spritePalettes[10].LT), city.PanelAlpha));
                Text(marker.Troops, new(19, -2, 156, 24), _numberFont!, HorizontalAlignment.Left, city.PanelAlpha);
            }
            if (marker.DurabilityCurrent is int hp && marker.DurabilityCapacity is int capacity && hp < capacity)
            {
                int style = hp > spritePalettes.DurabilityStylePercent * capacity / 100 ? 1 : 2;
                marker.HpFillWidth = Bar(hp, capacity == 0 ? 6000 : capacity,
                    new(-15, -48, 108, 12), style, marker.HpFillWidth);
            }
            if (marker.Morale44 is byte morale && marker.MoraleCapacity is int moraleCap)
            {
                int style = morale >= spritePalettes.MoraleStyleThreshold ? 3 : 2;
                if (city.Display == 0)
                    marker.FullMoraleFillWidth = Bar(morale, moraleCap, new(-6, 21, 98, 8), style, marker.FullMoraleFillWidth);
                else
                    marker.SimpleMoraleFillWidth = Bar(morale, moraleCap, new(18, -10, 72, 8), style, marker.SimpleMoraleFillWidth);
            }

            void Text(string value, AreaMarkerRectangle source, MapFontProfile profile,
                HorizontalAlignment alignment, byte alpha, Color? color = null)
            {
                Rect2 cell = AreaRectangle(source, p);
                _font.Draw(this, value, cell.Position + new Vector2(0, _font.CellBaselineY(profile, cell.Size.Y)),
                    profile, alignment, cell.Size.X, Alpha(color ?? Colors.White, alpha));
            }

            int Bar(int current, int capacity, AreaMarkerRectangle control, int style, int? retainedWidth)
            {
                var bar = _citySources!.BarGeometry(current, capacity, control, retainedWidth);
                _barPanel.Draw(this, AreaRectangle(bar.Background, p), panelTint, bar.ScaleX, bar.ScaleY);
                if (bar.SourceFillWidth > 0)
                    PaletteSprite(_barFill!, AreaRectangle(bar.Fill, p), _spritePalettes![style], city.PanelAlpha);
                return bar.SourceFillWidth;
            }
        }
        textDraw.End();
    }

    private static Rect2 AreaRectangle(AreaMarkerRectangle source, Vector2 p) =>
        new(p + new Vector2(source.Left, source.Top), new Vector2(source.Width, source.Height));

    private static Color Alpha(Color color, byte alpha) => new(color.R, color.G, color.B, color.A * (alpha / 255f));

    private void PaletteSprite(Texture2D texture, Rect2 rectangle, MapCornerColors colors, byte alpha)
    {
        Rect2 uv = new(Vector2.Zero, Vector2.One);
        if (texture is AtlasTexture atlas)
        {
            texture = atlas.Atlas;
            uv = new(atlas.Region.Position / texture.GetSize(), atlas.Region.Size / texture.GetSize());
        }
        Vector2[] points = [rectangle.Position, new(rectangle.End.X, rectangle.Position.Y), rectangle.End,
            new(rectangle.Position.X, rectangle.End.Y)];
        Vector2[] uvs = [uv.Position, new(uv.End.X, uv.Position.Y), uv.End, new(uv.Position.X, uv.End.Y)];
        Color[] corners = [Alpha(CanvasColor(colors.LT), alpha), Alpha(CanvasColor(colors.RT), alpha),
            Alpha(CanvasColor(colors.RB), alpha), Alpha(CanvasColor(colors.LB), alpha)];
        DrawPolygon(points, corners, uvs, texture);
    }

    private Rect2 AreaHitRectangle(Marker marker, Vector2 p, bool showName)
        => AreaRectangle(AreaMapMarkerSources.NormalHitRectangle(marker.Name.Length, showName), p);

    public override void _Ready() => Resized += QueueRedraw;

    public override void _ExitTree()
    {
        foodWarningShader?.Dispose();
        if (_ownsAssets) _assets?.Dispose();
        _font?.Dispose();
    }
}
