using System.Numerics;

namespace San14.Data;

public abstract record OriginalMapStaticModelFieldEdit;
public sealed record OriginalMapStaticModelFieldPositionEdit(Vector3 Position) : OriginalMapStaticModelFieldEdit;
public sealed record OriginalMapStaticModelFieldScaleEdit(Vector3 Scale) : OriginalMapStaticModelFieldEdit;
public sealed record OriginalMapStaticModelFieldRangeEdit(float? WrapperMinimum, float Maximum, float Minimum) : OriginalMapStaticModelFieldEdit;

/// <summary>One explicitly selected current scene Field. The manager/wrapper
/// branch and subsequent edits are supplied; the source preparation and query
/// prefixes are performed here once, with the retained packed sample copy.</summary>
public sealed record OriginalMapStaticModelGroundHeightSource(LoadedSceneResources Scene,
    LoadedTerrainResources Terrain, OriginalUnitWorldGroundHeightGrid Grid,
    OriginalUnitWorldGroundHeightFieldPreparationBranch PreparationBranch,
    IReadOnlyList<OriginalMapStaticModelFieldEdit> CurrentEdits)
{
    public float? Query(float x, float z) => OriginalUnitWorldGroundHeight.QueryA0B830(Grid, x, z);

    public static OriginalMapStaticModelGroundHeightSource PrepareCurrentField(OriginalGameRepository repository,
        LoadedSceneResources scene, OriginalUnitWorldGroundHeightFieldPreparationBranch? explicitManagerBranch,
        bool? thisFieldIsCurrentSceneTerrain, IReadOnlyList<OriginalMapStaticModelFieldEdit>? explicitCurrentEdits)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(scene);
        if (thisFieldIsCurrentSceneTerrain is not true)
            throw new InvalidDataException("Current scene terrain Field identity is unknown or belongs to another selected terrain.");
        if (explicitCurrentEdits is null)
            throw new InvalidDataException("Current native Field edits are unknown; an explicit empty edit sequence names the fresh preparation prefix.");
        var terrain = OriginalTerrainResources.Load(repository, "KIDSSystemResource", scene.TerrainBundle.Source.FileId);
        if (terrain.Objects.Source.ResourceUri != scene.TerrainBundle.Source.ResourceUri ||
            terrain.Objects.Source.DecodedSha256 != scene.TerrainBundle.Source.DecodedSha256)
            throw new InvalidDataException("Native current Field belongs to another scene terrain revision.");
        var grid = OriginalUnitWorldGroundHeightWriters.PrepareFreshFieldBc49C0(terrain.HeightField, explicitManagerBranch);
        foreach (OriginalMapStaticModelFieldEdit edit in explicitCurrentEdits)
            grid = edit switch
            {
                OriginalMapStaticModelFieldPositionEdit position => OriginalUnitWorldGroundHeightWriters.SetPositionA0D650(grid, position.Position),
                OriginalMapStaticModelFieldScaleEdit scale => OriginalUnitWorldGroundHeightWriters.SetScaleA0D5C0(grid, scale.Scale),
                OriginalMapStaticModelFieldRangeEdit range => OriginalUnitWorldGroundHeightWriters.RequantizeRangeA0D740(grid,
                    range.WrapperMinimum, range.Maximum, range.Minimum),
                _ => throw new InvalidDataException("Current native Field edit has no verified writer.")
            };
        return new(scene, terrain, grid, explicitManagerBranch!.Value, Array.AsReadOnly(explicitCurrentEdits.ToArray()));
    }
}
