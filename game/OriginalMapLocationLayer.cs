using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

public sealed record ProjectedMapLocation(int Id, bool IsGate, string Name, int HexId,
    int? ForceId, Vector3 Position, Node3D Marker);
public sealed record CityModelDisplay(FacilityModelDisplay Assets, int Category, int Variant,
    int DamageTier, string ImplementationVersion, string SceneUri, string SceneSha256,
    string CatalogUri, string CatalogSha256, string EvidenceUri,
    string EvidenceSha256)
{
    public string SelectionBasis { get; init; } = "observed-opening-capacity";
    public string? RuntimeCaptureSha256 { get; init; }
    public bool CurrentCategoryObserved { get; init; }
}
public sealed record GateModelDisplay(FacilityModelDisplay Assets, int SourceSlot,
    int SourceAreaSlot, int SourceAngleDegrees, int Category,
    Quaternion PreviewRotation, bool InitialRotationVerified, string RuleVersion,
    string SceneUri, string SceneSha256, string CatalogUri, string CatalogSha256);

/// <summary>Client markers using an explicit fitted projection, separate from simulation rules.</summary>
public partial class OriginalMapLocationLayer : Node3D
{
    // OpenGL capture at a 994px viewport height measured a 36x45 body at
    // 0.00094. The source BMPs show roughly 38x50 across all four zoom levels.
    private const float ReferenceFlagPixelSize = 0.00102f;
    // Neutral white A-BMP marker bodies are 21x23-24 px. This preview scale
    // was calibrated against that source silhouette, not the runtime zoom rule.
    private const float AreaFlagScale = 0.85f;
    private const float ReferenceViewportHeight = 994f;
    private readonly List<ProjectedMapLocation> _locations = [];
    private readonly List<Sprite3D[]> _sourceFlags = [];
    private readonly List<Sprite3D> _fixedMarkerSprites = [];
    private readonly List<Sprite3D> _areaMarkerSprites = [];
    private readonly List<Label3D> _fixedMarkerLabels = [];
    private OriginalMapMarkerPanel? _cityNamePanel;
    private OriginalMapMarkerPanel? _cityNumberPanel;
    private IReadOnlyDictionary<int, Color>? _forceColorCandidates;
    private readonly SystemFont _markerFont = new()
    {
        FontNames = ["KaiTi", "DFKai-SB", "Microsoft YaHei"]
    };
    private sealed record OriginalMarkerSprite(string Name, Texture2D Texture,
        string AtlasUri, string AtlasSha256);
    public IReadOnlyList<ProjectedMapLocation> Locations => _locations;
    public int UnplacedCount { get; private set; }
    public int AreaMarkerCount { get; private set; }

    internal void Build(MapWorldProjection projection, OriginalTerrainMeshResult terrain,
        GameState world, OriginalUiAssets assets, LoadedLayout layout,
        IReadOnlyDictionary<int, Color>? forceColorCandidates = null,
        IReadOnlyDictionary<int, CityModelDisplay>? cityModels = null,
        IReadOnlyDictionary<int, GateModelDisplay>? gateModels = null)
    {
        if (_locations.Count != 0) throw new InvalidOperationException("Map location layer already contains markers.");
        Name = "LogicalMapLocations";
        _forceColorCandidates = forceColorCandidates;
        if (world.MapWidth != projection.Policy.Width || world.MapHeight != projection.Policy.Height ||
            !world.MapSha256.Equals(projection.Policy.MapSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Logical location state and map projection sources differ.");
        ValidateSourceLayout(layout);
        OriginalMarkerSprite[] cityFlags =
        [
            Resolve(assets, "000_cmn_prt_flag_default_outline", 40, 52),
            Resolve(assets, "000_cmn_prt_flag_default", 40, 52)
        ];
        OriginalMarkerSprite[] gateFlags =
        [
            Resolve(assets, "000_cmn_prt_flag_seki_base", 40, 40),
            Resolve(assets, "000_cmn_prt_flag_seki_outline", 40, 40),
            Resolve(assets, "000_cmn_prt_flag_seki", 40, 40)
        ];
        OriginalMarkerSprite[] areaFlags =
        [
            Resolve(assets, "000_cmn_prt_flag_s_outline", 32, 32),
            Resolve(assets, "000_cmn_prt_flag_s", 32, 32)
        ];
        _cityNamePanel = OriginalMapMarkerPanel.Resolve(layout, assets,
            "000_cmn_prt_map_sign_base_pc005_0000", "000_cmn_prt_map_sign_base",
            new Vector2(32, 32), new Vector2I(100, 32));
        _cityNumberPanel = OriginalMapMarkerPanel.Resolve(layout, assets,
            "000_cmn_prt_map_num_base", "000_cmn_prt_map_num_base",
            new Vector2(24, 24), new Vector2I(98, 24));
        OriginalMarkerSprite soldierIcon = Resolve(assets,
            "000_cmn_icon_map_heishi_fuchi", 24, 24);
        foreach (CityState city in world.Cities.Values.OrderBy(city => city.Id))
        {
            CityModelDisplay? cityModel = cityModels is not null &&
                cityModels.TryGetValue(city.Id, out CityModelDisplay? found) ? found : null;
            AddLocation(city.Id, false, city.Name, city.HexId, city.ForceId, city.Troops,
                projection, terrain, cityFlags, _cityNamePanel, _cityNumberPanel, soldierIcon,
                cityModel);
        }
        foreach (GateState gate in world.Gates.Values.OrderBy(gate => gate.Id))
        {
            GateModelDisplay? gateModel = gateModels is not null &&
                gateModels.TryGetValue(gate.Id, out GateModelDisplay? found) ? found : null;
            AddLocation(gate.Id, true, gate.Name, gate.HexId, gate.ForceId, gate.Troops,
                projection, terrain, gateFlags, null, null, null, null, gateModel);
        }
        HashSet<int> occupiedBaseHexes = world.Cities.Values.Select(city => city.HexId)
            .Concat(world.Gates.Values.Select(gate => gate.HexId))
            .Where(hex => hex is not null).Select(hex => hex!.Value).ToHashSet();
        foreach (AreaState area in world.Areas.Values.OrderBy(area => area.Id))
            if (area.HexId is int hexId && !occupiedBaseHexes.Contains(hexId))
                AddAreaMarker(area, hexId, projection, terrain, areaFlags);
        SetMeta("projection_policy_id", projection.Policy.Id);
        SetMeta("source_formula_id", projection.Policy.SourceFormulaId ?? "");
        SetMeta("map_sha256", projection.Policy.MapSha256);
        SetMeta("field_resource_uri", projection.Policy.FieldResourceUri);
        SetMeta("field_decoded_sha256", projection.Policy.FieldDecodedSha256);
        SetMeta("original_world_mapping_verified", false);
        SetMeta("location_count", _locations.Count);
        SetMeta("unplaced_location_count", UnplacedCount);
        SetMeta("area_marker_count", AreaMarkerCount);
        SetMeta("area_marker_source_art_preview", true);
        SetMeta("original_area_marker_binding_verified", false);
        SetMeta("preview_grounding", "adjacent source MHK cells and low-byte diagonal; native scene offset, float order and object Y path unverified");
        SetMeta("source_marker_layout_uri", layout.Source.ResourceUri);
        SetMeta("source_marker_layout_sha256", layout.Source.DecodedSha256);
        SetMeta("source_marker_layout_is_override", layout.Source.OverrideSource is not null);
        SetMeta("source_marker_art_preview", true);
        SetMeta("original_main_map_marker_binding_verified", false);
    }

    private static void ValidateSourceLayout(LoadedLayout layout)
    {
        if (layout.Source.ResourceUri != "san14://rdb/ScreenLayout/EDC35713")
            throw new InvalidDataException("Map marker preview received the wrong source layout.");
        IReadOnlyList<LcskNodeRecord> nodes = LcskNodeReader.Read(layout.Layout);
        Dictionary<int, LcskNodeRecord> byIndex = nodes.ToDictionary(node => node.Index);
        RequireParts("00_81_07_0110_btn_city_offset_00",
            ["000_cmn_prt_flag_default_outline", "000_cmn_prt_flag_default"]);
        RequireParts("00_81_07_btn_gate_offset_01",
            ["000_cmn_prt_flag_seki_base", "000_cmn_prt_flag_seki_outline",
             "000_cmn_prt_flag_seki"]);

        void RequireParts(string parentName, string[] expected)
        {
            LcskNodeRecord parent = nodes.Single(node => node.Name == parentName);
            string[] actual = nodes.Where(node => node.Type == 1 && DescendsFrom(node, parent.Index))
                .SelectMany(node => LcskSpriteNodeReader.Read(layout.Layout, node).Groups)
                .SelectMany(group => group.Parts).Select(part => part.SpriteName).ToArray();
            bool found = Enumerable.Range(0, Math.Max(0, actual.Length - expected.Length + 1))
                .Any(start => expected.SequenceEqual(actual.Skip(start).Take(expected.Length)));
            if (!found)
                throw new InvalidDataException($"Source map marker layout branch {parentName} has no expected ordered sprite variant.");
        }

        bool DescendsFrom(LcskNodeRecord child, int ancestorIndex)
        {
            int? parentIndex = child.ParentIndex;
            for (int depth = 0; depth < nodes.Count; depth++)
            {
                if (parentIndex == ancestorIndex) return true;
                if (parentIndex is not int index ||
                    !byIndex.TryGetValue(index, out LcskNodeRecord? parent)) return false;
                parentIndex = parent.ParentIndex;
            }
            throw new InvalidDataException("Source map marker layout contains a parent cycle.");
        }
    }

    private static OriginalMarkerSprite Resolve(OriginalUiAssets assets, string name,
        int width, int height)
    {
        var loaded = assets.Sprite(name);
        AtlasTexture texture = loaded.Texture;
        // Source UVs are float32; AtlasTexture's integer GetHeight can truncate
        // a 51.999..-pixel region even when its intended prefab size is 52.
        Vector2 region = texture.Region.Size;
        if (Math.Abs(region.X - width) > 0.02f || Math.Abs(region.Y - height) > 0.02f)
            throw new InvalidDataException($"Source map marker sprite has unexpected region: {region}; expected {width}x{height}.");
        return new(name, texture, loaded.Source.AtlasSource.ResourceUri,
            loaded.Source.AtlasSource.DecodedSha256);
    }

    private void AddLocation(int id, bool gate, string name, int? hex, int? force, int troops,
        MapWorldProjection projection, OriginalTerrainMeshResult terrain,
        IReadOnlyList<OriginalMarkerSprite> flagLayers,
        OriginalMapMarkerPanel? cityNamePanel, OriginalMapMarkerPanel? cityNumberPanel,
        OriginalMarkerSprite? soldierIcon, CityModelDisplay? cityModel = null,
        GateModelDisplay? gateModel = null)
    {
        if (hex is not int hexId) { UnplacedCount++; return; }
        ProjectedMapPoint point = projection.Project(hexId);
        float nativeX = (float)point.X, nativeZ = (float)point.Z;
        float ground = OriginalMapGrounding.SampleHeight(terrain, nativeX, nativeZ);
        Vector3 position = new(nativeX, ground, nativeZ);
        if (!position.IsFinite()) throw new InvalidDataException($"Location {id}: preview world position exceeds native float range.");
        var node = new Node3D
        {
            Name = $"{(gate ? "Gate" : "City")}_{id}",
            Position = position + new Vector3(0, 180, 0)
        };
        AddChild(node);
        if (cityModel is not null)
        {
            FacilityModelDisplay asset = cityModel.Assets;
            var model = new MeshInstance3D
            {
                Name = $"SourceCityModel_{id}", Mesh = asset.Mesh.Mesh,
                Position = position
            };
            for (int surface = 0; surface < asset.Materials.Materials.Count; surface++)
                model.SetSurfaceOverrideMaterial(surface, asset.Materials.Materials[surface]);
            model.SetMeta("city_id", id);
            model.SetMeta("source_city_category", cityModel.Category);
            model.SetMeta("source_city_variant", cityModel.Variant);
            model.SetMeta("source_city_damage_tier", cityModel.DamageTier);
            model.SetMeta("city_variant_rule_version", cityModel.ImplementationVersion);
            model.SetMeta("scene_resource_uri", cityModel.SceneUri);
            model.SetMeta("scene_decoded_sha256", cityModel.SceneSha256);
            model.SetMeta("catalog_resource_uri", cityModel.CatalogUri);
            model.SetMeta("catalog_decoded_sha256", cityModel.CatalogSha256);
            model.SetMeta("opening_evidence_uri", cityModel.EvidenceUri);
            model.SetMeta("opening_evidence_sha256", cityModel.EvidenceSha256);
            model.SetMeta("city_category_selection_basis", cityModel.SelectionBasis);
            model.SetMeta("original_city_current_category_observed", cityModel.CurrentCategoryObserved);
            if (cityModel.RuntimeCaptureSha256 is string captureHash)
                model.SetMeta("opening_runtime_capture_sha256", captureHash);
            model.SetMeta("model_resource_uri", asset.Mesh.Source.ResourceUri);
            model.SetMeta("model_decoded_sha256", asset.Mesh.Source.DecodedSha256);
            model.SetMeta("dictionary_resource_uri", asset.Reference.Dictionary.Source.ResourceUri);
            model.SetMeta("dictionary_decoded_sha256", asset.Reference.Dictionary.Source.DecodedSha256);
            model.SetMeta("source_model_object_id", asset.Reference.ModelObject.ObjectId);
            model.SetMeta("source_materials", asset.Materials.Sources);
            model.SetMeta("source_city_model_reference_verified", true);
            model.SetMeta("original_city_horizontal_position_verified", true);
            model.SetMeta("original_city_initial_rotation_verified", true);
            model.SetMeta("original_city_ground_height_verified", false);
            model.SetMeta("original_city_transform_and_material_verified", false);
            AddChild(model);
        }
        if (gateModel is not null)
        {
            FacilityModelDisplay asset = gateModel.Assets;
            var model = new MeshInstance3D
            {
                Name = $"SourceGateModel_{id}", Mesh = asset.Mesh.Mesh,
                Position = position, Quaternion = gateModel.PreviewRotation
            };
            for (int surface = 0; surface < asset.Materials.Materials.Count; surface++)
                model.SetSurfaceOverrideMaterial(surface, asset.Materials.Materials[surface]);
            model.SetMeta("gate_id", id);
            model.SetMeta("source_gate_slot", gateModel.SourceSlot);
            model.SetMeta("source_gate_area_slot", gateModel.SourceAreaSlot);
            model.SetMeta("source_gate_angle_degrees", gateModel.SourceAngleDegrees);
            model.SetMeta("source_gate_category", gateModel.Category);
            model.SetMeta("source_gate_rule_version", gateModel.RuleVersion);
            model.SetMeta("scene_resource_uri", gateModel.SceneUri);
            model.SetMeta("scene_decoded_sha256", gateModel.SceneSha256);
            model.SetMeta("catalog_resource_uri", gateModel.CatalogUri);
            model.SetMeta("catalog_decoded_sha256", gateModel.CatalogSha256);
            model.SetMeta("model_resource_uri", asset.Mesh.Source.ResourceUri);
            model.SetMeta("model_decoded_sha256", asset.Mesh.Source.DecodedSha256);
            model.SetMeta("dictionary_resource_uri", asset.Reference.Dictionary.Source.ResourceUri);
            model.SetMeta("dictionary_decoded_sha256", asset.Reference.Dictionary.Source.DecodedSha256);
            model.SetMeta("source_model_object_id", asset.Reference.ModelObject.ObjectId);
            model.SetMeta("source_materials", asset.Materials.Sources);
            model.SetMeta("source_gate_model_reference_verified", true);
            model.SetMeta("original_gate_horizontal_position_verified", true);
            model.SetMeta("original_gate_initial_rotation_verified", gateModel.InitialRotationVerified);
            model.SetMeta("original_gate_runtime_rotation_verified", false);
            model.SetMeta("original_gate_ground_height_verified", false);
            model.SetMeta("original_gate_final_material_verified", false);
            AddChild(model);
        }
        var sprites = new Sprite3D[flagLayers.Count];
        for (int layer = 0; layer < flagLayers.Count; layer++)
        {
            var sprite = new Sprite3D
            {
                Name = $"SourceFlagLayer_{layer}", Texture = flagLayers[layer].Texture,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = ReferenceFlagPixelSize, FixedSize = true,
                DoubleSided = true, NoDepthTest = true, RenderPriority = layer,
                Position = new Vector3(0, layer * 0.5f, 0),
                Modulate = layer == flagLayers.Count - 1 && force is int forceId
                    ? PlayableMapVisualPalette.Force(forceId, _forceColorCandidates) : Colors.White
            };
            node.AddChild(sprite);
            sprite.SetMeta("source_sprite_name", flagLayers[layer].Name);
            sprite.SetMeta("source_atlas_uri", flagLayers[layer].AtlasUri);
            sprite.SetMeta("source_atlas_decoded_sha256", flagLayers[layer].AtlasSha256);
            sprites[layer] = sprite;
            _fixedMarkerSprites.Add(sprite);
        }
        _sourceFlags.Add(sprites);
        if (!gate && cityNamePanel is not null && cityNumberPanel is not null && soldierIcon is not null)
        {
            _fixedMarkerSprites.Add(cityNamePanel.AddTo(node,
                $"CityNamePanel_{id}", new Vector2(70, 6),
                ReferenceFlagPixelSize, 2,
                new Color(0.28f, 0.20f, 0.12f, 1f)));
            _fixedMarkerSprites.Add(cityNumberPanel.AddTo(node,
                $"CityNumberPanel_{id}", new Vector2(70, -29),
                ReferenceFlagPixelSize, 3, Colors.White));
            var icon = new Sprite3D
            {
                Name = $"CitySoldierIcon_{id}", Texture = soldierIcon.Texture,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = ReferenceFlagPixelSize, FixedSize = true,
                DoubleSided = true, NoDepthTest = true, RenderPriority = 4,
                Offset = new Vector2(34, -29)
            };
            node.AddChild(icon);
            icon.SetMeta("source_sprite_name", soldierIcon.Name);
            icon.SetMeta("source_atlas_uri", soldierIcon.AtlasUri);
            icon.SetMeta("source_atlas_decoded_sha256", soldierIcon.AtlasSha256);
            _fixedMarkerSprites.Add(icon);
        }
        var label = new Label3D
        {
            Name = $"{(gate ? "Gate" : "City")}_Label_{id}",
            Text = name,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Font = _markerFont, FontSize = 24, PixelSize = ReferenceFlagPixelSize,
            FixedSize = true, Width = 95, Offset = new Vector2(70, 6),
            DoubleSided = true, NoDepthTest = true,
            Modulate = new Color("#fff5d4"), OutlineModulate = Colors.Black,
            OutlineSize = 1
        };
        node.AddChild(label);
        _fixedMarkerLabels.Add(label);
        var troopLabel = new Label3D
        {
            Name = $"{(gate ? "Gate" : "City")}_Troops_{id}",
            Text = troops.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Font = _markerFont, FontSize = 22, PixelSize = ReferenceFlagPixelSize,
            FixedSize = true, Width = 72, Offset = new Vector2(83, -29),
            DoubleSided = true, NoDepthTest = true,
            Modulate = Colors.White, OutlineModulate = Colors.Black,
            OutlineSize = 1
        };
        node.AddChild(troopLabel);
        _fixedMarkerLabels.Add(troopLabel);
        node.SetMeta("location_id", id); node.SetMeta("is_gate", gate);
        node.SetMeta("hex_id", hexId); node.SetMeta("location_name", name);
        node.SetMeta("force_id", force ?? 0); node.SetMeta("source_ground_position", position);
        node.SetMeta("troops", troops);
        node.SetMeta("original_flag_sprite_resolved", true);
        node.SetMeta("original_main_map_marker_binding_verified", false);
        node.SetMeta("original_force_tint_verified", false);
        node.SetMeta("source_city_caption_parts_used", !gate);
        node.SetMeta("original_city_caption_placement_verified", false);
        node.SetMeta("flag_pixel_size_preview", ReferenceFlagPixelSize);
        node.SetMeta("flag_fixed_screen_size_preview", true);
        node.SetMeta("original_world_mapping_verified", false);
        _locations.Add(new(id, gate, name, hexId, force, position, node));
    }

    private void AddAreaMarker(AreaState area, int hexId,
        MapWorldProjection projection, OriginalTerrainMeshResult terrain,
        IReadOnlyList<OriginalMarkerSprite> layers)
    {
        ProjectedMapPoint point = projection.Project(hexId);
        float nativeX = (float)point.X, nativeZ = (float)point.Z;
        float ground = OriginalMapGrounding.SampleHeight(terrain, nativeX, nativeZ);
        var position = new Vector3(nativeX, ground + 160, nativeZ);
        if (!position.IsFinite())
            throw new InvalidDataException($"Area {area.Id}: preview world position exceeds native float range.");
        var marker = new Node3D { Name = $"Area_{area.Id}", Position = position };
        AddChild(marker);
        for (int layer = 0; layer < layers.Count; layer++)
        {
            OriginalMarkerSprite source = layers[layer];
            var sprite = new Sprite3D
            {
                Name = $"AreaFlagLayer_{layer}", Texture = source.Texture,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = ReferenceFlagPixelSize * AreaFlagScale, FixedSize = true,
                DoubleSided = true, NoDepthTest = true, RenderPriority = layer,
                Modulate = layer == 1
                    ? area.ForceId is int forceId
                        ? PlayableMapVisualPalette.Force(forceId, _forceColorCandidates)
                        : Colors.Transparent
                    : Colors.White
            };
            marker.AddChild(sprite);
            sprite.SetMeta("source_sprite_name", source.Name);
            sprite.SetMeta("source_atlas_uri", source.AtlasUri);
            sprite.SetMeta("source_atlas_decoded_sha256", source.AtlasSha256);
            _areaMarkerSprites.Add(sprite);
        }
        marker.SetMeta("area_id", area.Id);
        marker.SetMeta("area_name", area.Name);
        marker.SetMeta("hex_id", hexId);
        marker.SetMeta("force_id", area.ForceId ?? 0);
        marker.SetMeta("original_area_marker_binding_verified", false);
        marker.SetMeta("original_force_tint_verified", false);
        AreaMarkerCount++;
    }

    public ProjectedMapLocation Select(int index)
    {
        if ((uint)index >= _locations.Count) throw new ArgumentOutOfRangeException(nameof(index));
        SetSelected(index);
        return _locations[index];
    }

    public void ClearSelection() => SetSelected(-1);

    public void SetViewportHeight(float viewportHeight)
    {
        if (!float.IsFinite(viewportHeight) || viewportHeight <= 0) return;
        float scale = ReferenceFlagPixelSize * ReferenceViewportHeight / viewportHeight;
        foreach (Sprite3D sprite in _fixedMarkerSprites) sprite.PixelSize = scale;
        foreach (Sprite3D sprite in _areaMarkerSprites) sprite.PixelSize = scale * AreaFlagScale;
        foreach (Label3D label in _fixedMarkerLabels) label.PixelSize = scale;
        foreach (ProjectedMapLocation location in _locations)
            location.Marker.SetMeta("flag_pixel_size_preview", scale);
        SetMeta("city_flag_target_screen_pixels_preview", new Vector2(40, 52));
        SetMeta("gate_flag_target_screen_pixels_preview", new Vector2(40, 40));
        SetMeta("flag_viewport_height", viewportHeight);
    }

    private void SetSelected(int index)
    {
        for (int i = 0; i < _locations.Count; i++)
        {
            ProjectedMapLocation location = _locations[i];
            Sprite3D[] layers = _sourceFlags[i];
            for (int layer = 0; layer < layers.Length; layer++)
                layers[layer].Modulate = i == index ? new Color("#ffeab0") :
                    layer == layers.Length - 1 && location.ForceId is int forceId
                        ? PlayableMapVisualPalette.Force(forceId, _forceColorCandidates) : Colors.White;
        }
        SetMeta("selected_location_index", index);
    }

    public override void _ExitTree()
    {
        _sourceFlags.Clear();
        _fixedMarkerSprites.Clear();
        _areaMarkerSprites.Clear();
        _fixedMarkerLabels.Clear();
        _cityNamePanel?.Dispose();
        _cityNumberPanel?.Dispose();
        _markerFont.Dispose();
    }
}
