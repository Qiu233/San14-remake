using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

public sealed record ProjectedMapUnit(int Id, int HexId, int ForceId, int OfficerId,
    int Troops, Vector3 Position, Node3D Marker, Sprite3D ColoredSign, Color BaseColor);

/// <summary>Current playable units with source-backed preview signs; runtime branch and models remain unresolved.</summary>
public partial class MapUnitLayer : Node3D
{
    private const float ReferenceSignPixelSize = 0.00102f;
    private const float ReferenceViewportHeight = 994f;
    private readonly List<ProjectedMapUnit> _units = [];
    private readonly List<Sprite3D> _signSprites = [];
    private readonly List<Label3D> _signLabels = [];
    private readonly SystemFont _markerFont = new()
    {
        FontNames = ["KaiTi", "DFKai-SB", "Microsoft YaHei"]
    };
    public IReadOnlyList<ProjectedMapUnit> Units => _units;

    internal void Build(MapWorldProjection projection, TerrainMeshResult terrain,
        GameState world, int playerForceId, UiAssets assets, LoadedLayout layout,
        IReadOnlyDictionary<int, Color>? forceColorCandidates = null, IReadOnlySet<int>? visibleUnitIds = null)
    {
        if (_units.Count != 0) throw new InvalidOperationException("Map unit layer already contains markers.");
        Name = "PlayableMapUnits";
        if (world.MapWidth != projection.Policy.Width || world.MapHeight != projection.Policy.Height ||
            !world.MapSha256.Equals(projection.Policy.MapSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Playable unit state and map projection sources differ.");
        ValidateSourceLayout(layout);
        var outline = assets.Sprite("000_cmn_prt_sign_unit_s_outline");
        var sign = assets.Sprite("000_cmn_prt_sign_unit_s");
        if (outline.Source.AtlasSource.ResourceUri != "san14://rdb/ScreenLayout/FEA5F28C" ||
            sign.Source.AtlasSource.ResourceUri != outline.Source.AtlasSource.ResourceUri ||
            outline.Source.Texture.FileId != 0x301526D7 || sign.Source.Texture.FileId != 0x301526D7 ||
            !SourceSize(outline.Texture, 24, 24) || !SourceSize(sign.Texture, 24, 24))
            throw new InvalidDataException("Original unit sign sprites differ from the verified source atlas.");
        foreach (UnitState unit in world.Units.Values.OrderBy(unit => unit.Id))
        {
            if (visibleUnitIds is not null && !visibleUnitIds.Contains(unit.Id)) continue;
            if (!world.Officers.TryGetValue(unit.OfficerId, out OfficerState? officer))
                throw new InvalidDataException($"Map unit {unit.Id} has no officer.");
            ProjectedMapPoint point = projection.Project(unit.HexId);
            float ground = MapGrounding.SampleHeight(terrain, point.X, point.Z);
            Vector3 position = new((float)point.X, ground, (float)point.Z);
            if (!position.IsFinite())
                throw new InvalidDataException($"Map unit {unit.Id} position exceeds native float range.");
            var marker = new Node3D
            {
                Name = $"Unit_{unit.Id}",
                Position = position + new Vector3(0, 200, 0)
            };
            marker.SetMeta("unit_id", unit.Id);
            marker.SetMeta("hex_id", unit.HexId);
            marker.SetMeta("force_id", unit.ForceId);
            marker.SetMeta("officer_id", unit.OfficerId);
            marker.SetMeta("troops", unit.Troops);
            marker.SetMeta("source_ground_position", position);
            marker.SetMeta("original_world_mapping_verified", false);
            marker.SetMeta("source_sign_layout_uri", layout.Source.ResourceUri);
            marker.SetMeta("source_sign_layout_sha256", layout.Source.DecodedSha256);
            marker.SetMeta("source_sign_atlas_uri", sign.Source.AtlasSource.ResourceUri);
            marker.SetMeta("source_sign_atlas_sha256", sign.Source.AtlasSource.DecodedSha256);
            marker.SetMeta("source_sign_texture_id", sign.Source.Texture.FileId);
            marker.SetMeta("original_unit_marker_runtime_branch_verified", false);
            Color baseColor = unit.Mission?.Kind == UnitMissionKind.Transport
                ? new Color("#55bdb3")
                : PlayableMapVisualPalette.Force(unit.ForceId, forceColorCandidates);
            AddSignLayer(marker, "UnitSignOutline", outline.Texture, 0, Colors.White,
                outline.Source.Definition.Name);
            Sprite3D coloredSign = AddSignLayer(marker, "UnitSignForce", sign.Texture, 1,
                baseColor, sign.Source.Definition.Name);
            AddChild(marker);
            var label = new Label3D
            {
                Name = $"UnitLabel_{unit.Id}",
                Text = $"{officer.Name}  {unit.Troops}",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Font = _markerFont,
                FontSize = 22, PixelSize = ReferenceSignPixelSize,
                FixedSize = true, Offset = new Vector2(57, 0),
                DoubleSided = true, NoDepthTest = true,
                Modulate = Colors.White, OutlineModulate = Colors.Black,
                OutlineSize = 1
            };
            marker.AddChild(label);
            _signLabels.Add(label);
            _units.Add(new(unit.Id, unit.HexId, unit.ForceId, unit.OfficerId,
                unit.Troops, position, marker, coloredSign, baseColor));
        }
        SetMeta("projection_policy_id", projection.Policy.Id);
        SetMeta("source_formula_id", projection.Policy.SourceFormulaId ?? "");
        SetMeta("map_sha256", projection.Policy.MapSha256);
        SetMeta("unit_count", _units.Count);
        SetMeta("original_unit_models_resolved", false);
        SetMeta("source_sign_layout_uri", layout.Source.ResourceUri);
        SetMeta("source_sign_layout_sha256", layout.Source.DecodedSha256);
        SetMeta("source_sign_atlas_uri", sign.Source.AtlasSource.ResourceUri);
        SetMeta("source_sign_texture_id", sign.Source.Texture.FileId);
        SetMeta("source_unit_sign_preview", true);
        SetMeta("original_unit_marker_runtime_branch_verified", false);
        SetMeta("original_world_mapping_verified", false);
    }

    public void SetViewportHeight(float viewportHeight)
    {
        if (!float.IsFinite(viewportHeight) || viewportHeight <= 0) return;
        float scale = ReferenceSignPixelSize * ReferenceViewportHeight / viewportHeight;
        foreach (Sprite3D sprite in _signSprites) sprite.PixelSize = scale;
        foreach (Label3D label in _signLabels) label.PixelSize = scale;
        SetMeta("sign_pixel_size_preview", scale);
        SetMeta("sign_viewport_height", viewportHeight);
    }

    public void SelectAtHex(int hexId, int? playerForceId)
    {
        int? chosen = _units.Where(unit => unit.HexId == hexId &&
                (playerForceId is null || unit.ForceId == playerForceId.Value))
            .Select(unit => (int?)unit.Id).FirstOrDefault();
        SelectUnit(chosen);
    }

    public void SelectUnit(int? chosen)
    {
        if (chosen is int selected && !_units.Any(unit => unit.Id == selected))
            throw new InvalidDataException($"Selected map unit {selected} is absent.");
        foreach (ProjectedMapUnit unit in _units)
            unit.ColoredSign.Modulate = unit.Id == chosen ? new Color("#ffe57a") : unit.BaseColor;
        SetMeta("selected_unit_id", chosen ?? -1);
    }

    private static bool SourceSize(AtlasTexture texture, int width, int height) =>
        Math.Abs(texture.Region.Size.X - width) < 0.02f &&
        Math.Abs(texture.Region.Size.Y - height) < 0.02f;

    private Sprite3D AddSignLayer(Node3D marker, string name, AtlasTexture texture,
        int renderPriority, Color tint, string spriteName)
    {
        var sprite = new Sprite3D
        {
            Name = name, Texture = texture,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = ReferenceSignPixelSize, FixedSize = true,
            DoubleSided = true, NoDepthTest = true,
            RenderPriority = renderPriority, Modulate = tint
        };
        marker.AddChild(sprite);
        _signSprites.Add(sprite);
        sprite.SetMeta("source_sprite_name", spriteName);
        sprite.SetMeta("original_unit_sign_tint_verified", false);
        return sprite;
    }

    private static void ValidateSourceLayout(LoadedLayout layout)
    {
        if (layout.Source.ResourceUri != "san14://rdb/ScreenLayout/3DA1641D")
            throw new InvalidDataException("Unit sign preview received a different source layout.");
        IReadOnlyList<LcskNodeRecord> nodes = LcskNodeReader.Read(layout.Layout);
        LcskNodeRecord parent = nodes.Single(node => node.Name == "00_81_07_0410_btn_unit_offset_00");
        Dictionary<int, LcskNodeRecord> byIndex = nodes.ToDictionary(node => node.Index);
        string[] names = nodes.Where(node => node.Type == 1 && DescendsFrom(node, parent.Index))
            .SelectMany(node => LcskSpriteNodeReader.Read(layout.Layout, node).Groups)
            .SelectMany(group => group.Parts).Select(part => part.SpriteName).ToArray();
        if (!names.SequenceEqual(new[] { "000_cmn_prt_sign_unit_s_outline",
                "000_cmn_prt_sign_unit_s" }))
            throw new InvalidDataException("Original unit sign layout no longer references the ordered small sign layers.");

        bool DescendsFrom(LcskNodeRecord node, int ancestor)
        {
            int? parentIndex = node.ParentIndex;
            for (int depth = 0; depth < nodes.Count; depth++)
            {
                if (parentIndex == ancestor) return true;
                if (parentIndex is not int index || !byIndex.TryGetValue(index, out LcskNodeRecord? next))
                    return false;
                parentIndex = next.ParentIndex;
            }
            throw new InvalidDataException("Original unit sign layout contains a parent cycle.");
        }
    }

    public override void _ExitTree() => _markerFont.Dispose();
}
