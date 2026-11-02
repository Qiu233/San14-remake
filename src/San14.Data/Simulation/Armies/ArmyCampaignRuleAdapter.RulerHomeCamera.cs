using San14.Core;

namespace San14.Data;

/// <summary>A known null Hex is the native command's legitimate no-target
/// result. Unknown query inputs instead belong to UnresolvedInput.</summary>
public sealed record RulerHomeCameraTarget(int? PhysicalHexId);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Reads an owned World or a borrowed playback checkpoint. The
    /// caller must not supply a concurrently mutating worker candidate. This
    /// command follows the ruler's first affiliation Base, never a moving Army.</summary>
    public AreaAdministrationQuery<RulerHomeCameraTarget> QueryCurrentRulerHomeCameraTarget(
        GameState ownedOrCheckpoint)
    {
        ArgumentNullException.ThrowIfNull(ownedOrCheckpoint);
        try
        {
            var current = CaptureCurrentDefinitions(ownedOrCheckpoint)
                ?? throw RulerHomeCameraMissing("current_definitions");
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(ownedOrCheckpoint, current)
                ?? throw RulerHomeCameraMissing("current_player_Force_reference");
            if (!Need(current.ReadForceNativeValidity(player), "current_player_Force_validity")) return None();
            int forcePhysical = Need(current.ReadForcePhysicalSlot(player), "current_player_Force_physical");
            ushort rulerId = (current.ReadForceRecord(forcePhysical)
                ?? throw RulerHomeCameraMissing("current_player_Force_record")).RawRuler10;
            //The native person getter routes an out-of-range ID to its
            //dedicated invalid receiver, not an arbitrary logical Person ID.
            var ruler = (rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId)
                : current.ReadTransferPersonTableReference(0))
                ?? throw RulerHomeCameraMissing("ruler_Person_reference");
            if (ruler.PhysicalSlot is not int personPhysical) return None();
            if (!Need(current.ReadPersonNativeValidity(personPhysical), "ruler_Person_validity")) return None();
            ushort firstLocation = Need(current.ReadPersonFirstLocation(personPhysical), "ruler_first_affiliation");
            var home = BaseLookup.Find(firstLocation, current);
            if (!Need(current.ReadBaseNativeValidity(home), "ruler_home_Base_validity")) return None();
            //This exact map query sends an invalid Object/Hex link to Hex0.
            //ReadBaseCurrentHexIndex's48400 fallback is a different consumer.
            var hex = CurrentGateMapQueries.FindHex(home, current);
            //CHex's native validity is true for every nonnull physical
            //receiver, including Hex0; an observed null pointer is invalid.
            if (hex.PhysicalSlot is not int physicalHex) return None();
            return AreaAdministrationQuery<RulerHomeCameraTarget>.Known(new(physicalHex));
        }
        catch (InvalidDataException error)
        {
            return AreaAdministrationQuery<RulerHomeCameraTarget>.Unknown(error.Message);
        }

        static AreaAdministrationQuery<RulerHomeCameraTarget> None() =>
            AreaAdministrationQuery<RulerHomeCameraTarget>.Known(new(null));
        static T Need<T>(T? value, string name) where T : struct =>
            value ?? throw RulerHomeCameraMissing(name);
    }

    private static InvalidDataException RulerHomeCameraMissing(string name) =>
        new("hud.ruler_home_camera." + name);
}
