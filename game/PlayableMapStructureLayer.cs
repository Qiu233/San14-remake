using Godot;
using San14.Core;
using San14.Data;

public sealed record FacilityModelDisplay(SceneModelReference Reference,
    OriginalModelMeshResult Mesh, OriginalPreviewMaterialSet Materials);
public sealed record FacilityModelDisplays(FacilityModelDisplay Unfinished,
    FacilityModelDisplay Completed, string SceneUri, string SceneSha256,
    string CatalogUri, string CatalogSha256);

/// <summary>Playable buildings placed over the source field scene.</summary>
public partial class PlayableMapStructureLayer : Node3D
{
    private const float ReferencePixelSize = 0.00102f;
    private readonly SystemFont _font = new()
    {
        FontNames = ["KaiTi", "DFKai-SB", "Microsoft YaHei"]
    };
    private readonly List<Label3D> _labels = [];
    private int _sourceModelCount;
    public int Count => _labels.Count;

    internal void Build(MapWorldProjection projection, OriginalTerrainMeshResult terrain,
        GameState world,
        IReadOnlyDictionary<int, Color>? forceColorCandidates = null,
        IReadOnlyDictionary<string, string>? sourceNames = null,
        IReadOnlyDictionary<string, FacilityModelDisplays>? facilityModels = null)
    {
        Name = "PlayableMapStructures";
        if (world.MapWidth != projection.Policy.Width ||
            world.MapHeight != projection.Policy.Height ||
            !world.MapSha256.Equals(projection.Policy.MapSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Structure projection and playable map differ.");
        foreach (StructureState structure in (world.Structures ?? []).Values.OrderBy(item => item.Id))
        {
            ProjectedMapPoint point = projection.Project(structure.HexId);
            float ground = OriginalMapGrounding.SampleHeight(terrain,
                (float)point.X, (float)point.Z);
            bool completed = structure.Completed;
            string name = sourceNames is not null &&
                sourceNames.TryGetValue(structure.KindId, out string? originalName)
                    ? originalName : structure.KindId;
            var marker = new Label3D
            {
                Name = $"Structure_{structure.Id}",
                Position = new Vector3((float)point.X, ground + 240, (float)point.Z),
                Text = completed ? $"[{name}]" : $"[{name} {structure.Durability}/{structure.MaxDurability}]",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Font = _font, FontSize = 24, PixelSize = ReferencePixelSize,
                FixedSize = true, DoubleSided = true, NoDepthTest = true,
                Modulate = structure.ForceId is int forceId
                    ? PlayableMapVisualPalette.Force(forceId, forceColorCandidates) : Colors.White,
                OutlineModulate = Colors.Black, OutlineSize = 2
            };
            marker.SetMeta("structure_id", structure.Id);
            marker.SetMeta("hex_id", structure.HexId);
            marker.SetMeta("source_object_definition_slot", structure.KindId);
            marker.SetMeta("original_structure_model_verified", false);
            AddChild(marker);
            _labels.Add(marker);
            if (facilityModels is not null &&
                facilityModels.TryGetValue(structure.KindId, out FacilityModelDisplays? kindModels))
            {
                FacilityModelDisplay display = completed
                    ? kindModels.Completed : kindModels.Unfinished;
                var model = new MeshInstance3D
                {
                    Name = $"SourceStructureModel_{structure.Id}",
                    Mesh = display.Mesh.Mesh,
                    Position = new Vector3((float)point.X, ground, (float)point.Z)
                };
                for (int surface = 0; surface < display.Materials.Materials.Count; surface++)
                    model.SetSurfaceOverrideMaterial(surface, display.Materials.Materials[surface]);
                model.SetMeta("structure_id", structure.Id);
                model.SetMeta("hex_id", structure.HexId);
                model.SetMeta("model_resource_uri", display.Mesh.Source.ResourceUri);
                model.SetMeta("model_decoded_sha256", display.Mesh.Source.DecodedSha256);
                model.SetMeta("dictionary_resource_uri", display.Reference.Dictionary.Source.ResourceUri);
                model.SetMeta("dictionary_decoded_sha256", display.Reference.Dictionary.Source.DecodedSha256);
                model.SetMeta("source_model_object_id", display.Reference.ModelObject.ObjectId);
                model.SetMeta("scene_resource_uri", kindModels.SceneUri);
                model.SetMeta("scene_decoded_sha256", kindModels.SceneSha256);
                model.SetMeta("catalog_resource_uri", kindModels.CatalogUri);
                model.SetMeta("catalog_decoded_sha256", kindModels.CatalogSha256);
                model.SetMeta("source_materials", display.Materials.Sources);
                model.SetMeta("source_facility_model_reference_verified", true);
                model.SetMeta("original_structure_transform_and_material_verified", false);
                AddChild(model);
                _sourceModelCount++;
            }
        }
        SetMeta("provisional_structure_markers", true);
        SetMeta("structure_count", Count);
        SetMeta("source_facility_model_count", _sourceModelCount);
    }

    public void SetViewportHeight(float viewportHeight)
    {
        if (!float.IsFinite(viewportHeight) || viewportHeight <= 0) return;
        float scale = ReferencePixelSize * 994f / viewportHeight;
        foreach (Label3D label in _labels) label.PixelSize = scale;
    }

    public override void _ExitTree() => _font.Dispose();
}
