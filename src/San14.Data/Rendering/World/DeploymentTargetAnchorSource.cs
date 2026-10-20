using System.Numerics;

namespace San14.Data;

public sealed record DeploymentTargetAnchor(int HexId, Vector3? GroundWorld,
    Vector3? MenuWorld, string? UnresolvedInput);

/// <summary>A derived scene-lifetime owner of ordinary Hex anchors. Construct
/// it from the active prepared terrain; replace it when that terrain owner
/// or its resources/Field edits change. Camera/world changes retain this cache.</summary>
public sealed class DeploymentTargetAnchorSource
{
    private readonly MainMapCameraTerrainSource terrain;
    private readonly Dictionary<int, float> heights = [];
    public MainMapCameraTerrainSource Terrain => terrain;
    public DeploymentTargetAnchorSource(MainMapCameraTerrainSource currentTerrain) =>
        terrain = currentTerrain ?? throw new ArgumentNullException(nameof(currentTerrain));

    public DeploymentTargetAnchor Query(int hexId)
    {
        try
        {
            // The caller supplies a resolved current Hex identity. Nonnull
            // CHex physical0 is valid; an out-of-range API ID is not Hex0.
            if (hexId is < 0 or >= 48400) throw new InvalidDataException("deployment.target_anchor.hex_identity");
            var planar = CeHexWorldCoordinates.Project(hexId);
            float x = (float)planar.X, z = (float)planar.Z;
            if (!heights.TryGetValue(hexId, out float y) || !(y > 0f))
            {
                // Current Scene Landscape and its exact prepared BA9890
                // wrapper/grid are held by the existing camera terrain owner.
                float ground = terrain.Query(x, z);
                y = 445f > ground ? 445f : ground;
                heights[hexId] = y;
            }
            return new(hexId, new(x, y, z), new(x, UnitWorldGroundHeightFloat.Add(y, 200f), z), null);
        }
        catch (InvalidDataException error) { return new(hexId, null, null, error.Message); }
    }
}



