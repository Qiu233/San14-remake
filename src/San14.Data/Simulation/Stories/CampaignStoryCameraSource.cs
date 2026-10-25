using San14.Core;

namespace San14.Data;

public sealed record StoryCameraTarget(float FocusX, float FocusY, float FocusZ,
    double Distance, double Yaw, double Pitch);

public sealed partial class ArmyCampaignRuleAdapter
{
    private MainMapCameraTerrainSource? storyCameraTerrain;
    private bool storyCameraTerrainKnown;

    /// <summary>The map owner supplies its existing prepared Landscape. An
    /// explicit null is the observed absent-Landscape1889A0 branch; omission
    /// means that this query has not received the current terrain owner.</summary>
    public void ConfigureStoryCameraTerrain(MainMapCameraTerrainSource? terrain)
    {
        storyCameraTerrain = terrain;
        storyCameraTerrainKnown = true;
    }

    //Frozen TC1A41990, indexed by the current physical Hex's object link.
    //These are source floats; conversion to radians precedes widening to double.
    private static readonly float[] StoryCameraYawDegrees =
    [
        150, 150, 150, 150, 150, 150, 150, 150,
        150, 150, 150, 150, 150, 150, 150, 150,
        150,  72, 150, 150, 150, 150, 150, 150,
        226, 150, 150, 150, 150, 150, 150, 150,
        150, 150, 150, 150, 150, 150, 150, 150,
        150,  75, 150, 150, 195, 150,  61, 150,
        150, 150, 150, 150
    ];

    /// <summary>3E11C0's source camera packet for an already resolved physical
    /// Hex receiver. It does not resolve that identity through the lookup table
    /// again, mutate the camera, or acknowledge a story operation.</summary>
    public AreaAdministrationQuery<StoryCameraTarget> QueryStoryCameraTarget(GameState state, int hex)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            if ((uint)hex >= 48400) throw StoryCameraMissing("physical_hex");
            if (state.MapWidth != CeHexWorldCoordinates.Width || state.MapHeight != CeHexWorldCoordinates.Height ||
                state.MapSha256 != _map.Source.Sha256) throw StoryCameraMissing("map_identity");
            var point = CeHexWorldCoordinates.Project(hex);
            float x = (float)point.X, z = (float)point.Z;
            if (!storyCameraTerrainKnown) throw StoryCameraMissing("current_Landscape_height_owner");
            float y = 0;
            if (storyCameraTerrain is { } terrain)
            {
                float ground = terrain.Query(x, z);
                //1889A0 clamps the real BA9890 sample to445. Ordered
                //comparison also preserves the native unordered-result branch.
                y = 445f <= ground ? ground : 445f;
            }

            //Only current Hex1A is consumed. This narrow capture borrows the
            //owned object/Hex-binding projection without importing any Army or
            //personnel manager, temporary list pool, or scenario opening links.
            var current = new SourceWorldContext();
            OwnedMapObjectLifecycle.BindCurrent(state, current);
            ushort link = current.ReadHexObjectLink(new(hex)) ?? throw StoryCameraMissing("current_Hex1A");
            double yaw = link is >= 1 and <= 51
                ? (double)(StoryCameraYawDegrees[link] * 0.017453292f)
                : 2.6179938316345215;
            return AreaAdministrationQuery<StoryCameraTarget>.Known(new(x, y, z, 10000.0, yaw, 0.38));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<StoryCameraTarget>.Unknown(error.Message); }
    }

    private static InvalidDataException StoryCameraMissing(string field) => new("story.camera." + field);
}
