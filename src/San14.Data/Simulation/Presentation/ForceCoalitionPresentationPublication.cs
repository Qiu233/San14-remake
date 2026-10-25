using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The actual441480/453790 EventView still. A known null is
    ///441790/836A00's retirement, not a missing replacement illustration.</summary>
    public AreaAdministrationQuery<FinalOrderIllustrationRoute?> QueryForceCoalitionIllustration(
        GameState state, ForceCoalitionVisualRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (request.TargetHexId is not null)
            return AreaAdministrationQuery<FinalOrderIllustrationRoute?>.Unknown("coalition.scene.site");
        if (request.Operation == ForceCoalitionVisualOperation.NarrationEnter &&
            request.SceneKind is 24 or 40 or 52 or 53 or 54)
            return AreaAdministrationQuery<FinalOrderIllustrationRoute?>.Known(FinalOrderIllustration(request.SceneKind.Value));
        if (request.Operation == ForceCoalitionVisualOperation.NarrationExit && request.SceneKind is null)
            return AreaAdministrationQuery<FinalOrderIllustrationRoute?>.Known(null);
        return AreaAdministrationQuery<FinalOrderIllustrationRoute?>.Unknown("coalition.scene.source_kind");
    }

    public AreaAdministrationQuery<NormalTurnFinalOrderScenePublication> QueryEmperorScene(
        GameState state, MonthlyMaintenanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Operation == MonthlyMaintenanceOperation.EmperorSceneEnter && request.EventKind is 6 or 45 or 55)
            return AreaAdministrationQuery<NormalTurnFinalOrderScenePublication>.Known(new(request.Operation,
                FinalOrderIllustration(request.EventKind.Value), "stl_event_in"));
        if (request.Operation == MonthlyMaintenanceOperation.EmperorSceneExit && request.EventKind is null)
            return AreaAdministrationQuery<NormalTurnFinalOrderScenePublication>.Known(new(request.Operation, null, "stl_event_out"));
        return AreaAdministrationQuery<NormalTurnFinalOrderScenePublication>.Unknown("emperor.scene.source_kind");
    }

    /// <summary>2DA860 saves the current camera and requests452B10's actual
    /// Hex focus for1000ms. Its negative-distance branch calls00E940(1),
    /// whose seven source distances are the normal controller anchors.
    /// This is anchor1 (10000), independently of the story camera defaults.</summary>
    public AreaAdministrationQuery<StoryCameraTarget> QueryForceCoalitionCameraTarget(GameState state, int hex)
    {
        ArgumentNullException.ThrowIfNull(state);
        if ((uint)hex > 48400)
            return AreaAdministrationQuery<StoryCameraTarget>.Unknown("coalition.camera.physical_hex");
        if (state.MapWidth != CeHexWorldCoordinates.Width || state.MapHeight != CeHexWorldCoordinates.Height ||
            state.MapSha256 != _map.Source.Sha256)
            return AreaAdministrationQuery<StoryCameraTarget>.Unknown("coalition.camera.map_identity");
        if (!storyCameraTerrainKnown)
            return AreaAdministrationQuery<StoryCameraTarget>.Unknown("coalition.camera.current_Landscape_height_owner");
        //The shared188880 geometry preserves48400's X/Z and its Y0 branch.
        //452B10 clears Y only for the actual absent-Landscape branch.
        var point = ArmyMapSpeechVoiceGeometry.QueryPacketPosition(hex, storyCameraTerrain);
        return AreaAdministrationQuery<StoryCameraTarget>.Known(new(point.X, point.Y, point.Z,
            10000.0, BitConverter.Int64BitsToDouble(0x404F6A7A2966D6A4),
            BitConverter.Int64BitsToDouble(0x3FE921FB60000000)));
    }
}
