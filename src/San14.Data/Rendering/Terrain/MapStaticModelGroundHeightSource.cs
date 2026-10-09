using System.Numerics;

namespace San14.Data;

public abstract record MapStaticModelFieldEdit;
public sealed record MapStaticModelFieldPositionEdit(Vector3 Position) : MapStaticModelFieldEdit;
public sealed record MapStaticModelFieldScaleEdit(Vector3 Scale) : MapStaticModelFieldEdit;
public sealed record MapStaticModelFieldRangeEdit(float? WrapperMinimum, float Maximum, float Minimum) : MapStaticModelFieldEdit;

/// <summary>One explicitly selected current scene Field. The manager/wrapper
/// branch and subsequent edits are supplied; the source preparation and query
/// prefixes are performed here once, with the retained packed sample copy.</summary>
public sealed record MapStaticModelGroundHeightSource(LoadedSceneResources Scene,
    LoadedTerrainResources Terrain, UnitWorldGroundHeightGrid Grid,
    UnitWorldGroundHeightFieldPreparationBranch PreparationBranch,
    IReadOnlyList<MapStaticModelFieldEdit> CurrentEdits)
{
    public float? Query(float x, float z) => UnitWorldGroundHeight.QueryA0B830(Grid, x, z);

    public static MapStaticModelGroundHeightSource PrepareCurrentField(GameResourceRepository repository,
        LoadedSceneResources scene, UnitWorldGroundHeightFieldPreparationBranch? explicitManagerBranch,
        bool? thisFieldIsCurrentSceneTerrain, IReadOnlyList<MapStaticModelFieldEdit>? explicitCurrentEdits)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(scene);
        if (thisFieldIsCurrentSceneTerrain is not true)
            throw new InvalidDataException("Current scene terrain Field identity is unknown or belongs to another selected terrain.");
        if (explicitCurrentEdits is null)
            throw new InvalidDataException("Current native Field edits are unknown; an explicit empty edit sequence names the fresh preparation prefix.");
        var terrain = TerrainResources.Load(repository, "KIDSSystemResource", scene.TerrainBundle.Source.FileId);
        if (terrain.Objects.Source.ResourceUri != scene.TerrainBundle.Source.ResourceUri ||
            terrain.Objects.Source.DecodedSha256 != scene.TerrainBundle.Source.DecodedSha256)
            throw new InvalidDataException("Native current Field belongs to another scene terrain revision.");
        var grid = UnitWorldGroundHeightWriters.PrepareFreshFieldBc49C0(terrain.HeightField, explicitManagerBranch);
        foreach (MapStaticModelFieldEdit edit in explicitCurrentEdits)
            grid = edit switch
            {
                MapStaticModelFieldPositionEdit position => UnitWorldGroundHeightWriters.SetPositionA0D650(grid, position.Position),
                MapStaticModelFieldScaleEdit scale => UnitWorldGroundHeightWriters.SetScaleA0D5C0(grid, scale.Scale),
                MapStaticModelFieldRangeEdit range => UnitWorldGroundHeightWriters.RequantizeRangeA0D740(grid,
                    range.WrapperMinimum, range.Maximum, range.Minimum),
                _ => throw new InvalidDataException("Current native Field edit has no verified writer.")
            };
        return new(scene, terrain, grid, explicitManagerBranch!.Value, Array.AsReadOnly(explicitCurrentEdits.ToArray()));
    }
}
