using System.Numerics;
using San14.Core;

namespace San14.Data;

/// <summary>The owned inputs selected by CTitleState's initial Main-camera
/// focus path. RequestedWorldFocus is before the native controller's clamps.</summary>
public sealed record MainMapCameraFocus(int PlayerForcePhysicalSlot,
    int RulerPersonSourceId, int AffiliationLocationId, int PhysicalHexId,
    Vector4 RequestedWorldFocus, string OwnedWorldProvenance);

/// <summary>SC4FA710 / TC4FA790 selects the current player Force's ruler and
/// first affiliation place, resolves its physical Hex, and forces focus Y=0.
/// The Core source initializer seeds Officer.LocationId from that first place;
/// the separate second native location is not read by this source path.</summary>
public static class MainMapCameraFocusSource
{
    public const string ContractId = "ce-20261002-main-map-ruler-affiliation-focus";

    public static MainMapCameraFocus ResolveCurrentRuler(GameState currentOwnedWorld,
        int currentNativePlayerForceSlot, string ownedWorldProvenance)
    {
        ArgumentNullException.ThrowIfNull(currentOwnedWorld);
        if (string.IsNullOrWhiteSpace(ownedWorldProvenance) ||
            currentOwnedWorld.MapWidth != CeHexWorldCoordinates.Width ||
            currentOwnedWorld.MapHeight != CeHexWorldCoordinates.Height ||
            currentNativePlayerForceSlot is < 1 or > 51 ||
            !currentOwnedWorld.Forces.TryGetValue(currentNativePlayerForceSlot, out ForceState? force) ||
            force.RulerId is not (>= 1 and <= 6000) ||
            !currentOwnedWorld.Officers.TryGetValue(force.RulerId.Value, out OfficerState? ruler) ||
            ruler.LocationId is not (>= 1 and <= 61))
            throw new InvalidDataException("Main camera focus requires an owned current player Force and ruler affiliation place.");

        int place = ruler.LocationId.Value;
        int? hex = place <= 51
            ? currentOwnedWorld.Cities.GetValueOrDefault(place)?.HexId
            : currentOwnedWorld.Gates.GetValueOrDefault(place)?.HexId;
        if (hex is not (>= 0 and < CeHexWorldCoordinates.Width * CeHexWorldCoordinates.Height))
            throw new InvalidDataException("The ruler affiliation Base has no owned physical Hex for Main camera focus.");

        // 188880 yields binary32 world coordinates. The caller widens those
        // exact values for the controller and overwrites Y with literal zero.
        ProjectedMapPoint world = CeHexWorldCoordinates.Project(hex.Value);
        return new(currentNativePlayerForceSlot, force.RulerId.Value, place, hex.Value,
            new((float)world.X, 0f, (float)world.Z, 1f), ownedWorldProvenance);
    }
}
