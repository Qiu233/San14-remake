using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private MainMapCameraTerrainSource? baseCapturePresentationTerrain;
    private bool baseCapturePresentationTerrainBound;

    /// <summary>Supplies the actual map Landscape for source-time188880.
    /// Explicit null is its absent-Landscape branch; an unbound host is unknown.
    /// The immutable terrain source may be shared with a simulation worker.</summary>
    public void ConfigureBaseCapturePresentationTerrain(MainMapCameraTerrainSource? terrain)
    {
        baseCapturePresentationTerrain = terrain;
        baseCapturePresentationTerrainBound = true;
    }

    //2851B0: before the winner11/HomeArmy18 packets or any Person/Home
    //changes. The two independent consumers receive the same source-time
    //choice and position; neither request claims a created effect or playback.
    internal void AppendBaseCapturePresentationCurrent(GameState state, SourceWorldContext current,
        BaseReference target, DistrictReference targetDistrict, List<CampaignEffectRequest> effects)
    {
        int destination = CapturePresentationNeed(current.ReadDistrictPhysicalSlot(targetDistrict), "target_District_identity");
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw CapturePresentationMissing("player_Force_pointer");
        var primary = ForcePrimaryDistrictSource.Resolve(current, player);
        int playerPrimary = CapturePresentationNeed(current.ReadDistrictPhysicalSlot(primary), "player_primary_District_identity");
        int kind = destination == playerPrimary ? 8 : 7;
        int location = CapturePresentationNeed(current.ReadBaseObjectLink(target), "Base4E");
        int hex = current.ReadBaseCurrentHexIndex(target);
        if ((uint)hex >= 48400) throw CapturePresentationMissing("Base_Hex_position");
        if (!baseCapturePresentationTerrainBound) throw CapturePresentationMissing("source_Landscape_owner");
        var point = CeHexWorldCoordinates.Project(hex);
        float x = (float)point.X, z = (float)point.Z, y = 0;
        //1889A0: no Landscape returns0; an existing Landscape samples the
        //actual point and uses445 as the source's lower ground bound.
        if (baseCapturePresentationTerrain is { } terrain)
        {
            float height = terrain.Query(x, z);
            y = 445f <= height ? height : 445f;
        }
        var snapshot = new BaseCapturePresentationSnapshot(kind, destination, playerPrimary,
            x, y, z, 2f, 0, 0xB3);
        var directory = current.CurrentMapObjectDirectory ?? throw CapturePresentationMissing("Base_receiver_directory");
        var receiver = directory.ReceiverAtPhysical(location);
        string? definition = state.MapObjects?.Find(receiver)?.Subject?.DefinitionKey;
        effects.Add(new(null, null) { MapObjectPresentation = new(
            MapObjectPresentationEvent.BaseOwnershipLostVisual, receiver, hex, definition) { BaseCapture = snapshot } });
        effects.Add(new(null, null) { MapObjectPresentation = new(
            MapObjectPresentationEvent.BaseOwnershipLostSound, receiver, hex, definition) { BaseCapture = snapshot } });
    }

    private static T CapturePresentationNeed<T>(T? value, string field) where T : struct
        => value ?? throw CapturePresentationMissing(field);
    private static InvalidDataException CapturePresentationMissing(string field)
        => new("Base-capture.presentation." + field);
}
