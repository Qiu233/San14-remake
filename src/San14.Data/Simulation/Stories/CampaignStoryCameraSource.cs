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

    /// <summary>3E11C0 projects the original word index for position, then
    /// independently resolves the Hex-table receiver for orbit settings.
    /// It neither mutates the camera nor acknowledges a story operation.</summary>
    public AreaAdministrationQuery<StoryCameraTarget> QueryStoryCameraTarget(GameState state, int hex)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            if ((uint)hex > ushort.MaxValue) throw StoryCameraMissing("raw_hex_word");
            if (state.MapWidth != CeHexWorldCoordinates.Width || state.MapHeight != CeHexWorldCoordinates.Height ||
                state.MapSha256 != _map.Source.Sha256) throw StoryCameraMissing("map_identity");
            var point = CeHexWorldCoordinates.ProjectEventCamera((ushort)hex);
            float x = (float)point.X, z = (float)point.Z;
            float y = 0;
            //1889A0 returns 0 beyond the physical table without consuming a
            //Landscape owner. This is distinct from the context skip gate.
            if (hex < 48400)
            {
                if (!storyCameraTerrainKnown) throw StoryCameraMissing("current_Landscape_height_owner");
                if (storyCameraTerrain is { } terrain)
                {
                    float ground = terrain.Query(x, z);
                    //1889A0 clamps the real BA9890 sample to445. Ordered
                    //comparison preserves the native unordered-result branch.
                    y = 445f <= ground ? ground : 445f;
                }
            }

            //Only current Hex1A is consumed. This narrow capture borrows the
            //owned object/Hex-binding projection without importing any Army or
            //personnel manager, temporary list pool, or scenario opening links.
            var current = new SourceWorldContext();
            _definitions.BindMapIdentityCurrent(current);
            OwnedMapObjectLifecycle.BindCurrent(state, current);
            var receiver = current.ReadHexTableReference(hex < 48400 ? hex : 0)
                ?? throw StoryCameraMissing("current_Hex_lookup");
            //CHexData's native-validity virtual returns 1 for every nonnull
            //receiver, including slot 0. Only a known null keeps these packet
            //sentinels; an unknown pointer never authorizes that branch.
            if (receiver.PhysicalSlot is null)
                return AreaAdministrationQuery<StoryCameraTarget>.Known(new(x, y, z, -1.0, 62.83185308, -1.0));
            ushort link = current.ReadHexObjectLink(receiver) ?? throw StoryCameraMissing("current_Hex1A");
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
